import os
import sys
import shutil
import tempfile
from pathlib import Path
from PIL import Image
from gradio_client import Client, handle_file

if hasattr(sys.stdout, "reconfigure"):
    try:
        sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    except Exception:
        pass
if hasattr(sys.stderr, "reconfigure"):
    try:
        sys.stderr.reconfigure(encoding="utf-8", errors="replace")
    except Exception:
        pass

def safe_print(msg: str):
    try:
        print(msg)
    except Exception:
        try:
            print(str(msg).encode("ascii", errors="replace").decode("ascii"))
        except Exception:
            pass

# Espaços disponíveis do Trellis no Hugging Face (TRELLIS.2 é o oficial mais recente e com melhor qualidade)
TRELLIS_SPACES = [
    "microsoft/TRELLIS.2",
    "trellis-community/TRELLIS"
]

TOKEN_FILE = Path(__file__).resolve().parent / "hf_token.txt"

def get_saved_hf_token() -> str:
    """Recupera o token do Hugging Face salvo em arquivo, ambiente ou cache local."""
    if TOKEN_FILE.exists():
        try:
            tok = TOKEN_FILE.read_text(encoding="utf-8").strip()
            if tok:
                return tok
        except Exception:
            pass

    env_tok = os.environ.get("HF_TOKEN") or os.environ.get("HUGGING_FACE_HUB_TOKEN")
    if env_tok and env_tok.strip():
        return env_tok.strip()

    try:
        from huggingface_hub import get_token
        cached = get_token()
        if cached and cached.strip():
            return cached.strip()
    except Exception:
        pass

    return ""

def validate_and_save_hf_token(token_str: str) -> tuple[bool, str]:
    """
    Testa a autenticidade do token no Hugging Face.
    Se for válido, salva no arquivo hf_token.txt e configura o login local.
    """
    token_clean = token_str.strip() if token_str else ""
    if not token_clean:
        return False, "Nenhum token fornecido."

    try:
        import huggingface_hub
        api = huggingface_hub.HfApi()
        user_info = api.whoami(token=token_clean)
        username = user_info.get("name") or user_info.get("fullname") or "Usuário Hugging Face"

        # Salva no arquivo local para persistência permanente
        TOKEN_FILE.write_text(token_clean, encoding="utf-8")

        # Configura no ambiente e cache
        try:
            huggingface_hub.login(token=token_clean, add_to_git_credential=False)
        except Exception:
            pass

        os.environ["HF_TOKEN"] = token_clean
        return True, username
    except Exception as e:
        err_msg = str(e)
        if "401" in err_msg or "Invalid" in err_msg or "Unauthorized" in err_msg:
            return False, "Token inválido. Verifique se copiou o token completo iniciando com 'hf_'."
        return False, f"Erro ao verificar token: {err_msg}"

def get_token_status_display() -> tuple[str, str]:
    """Retorna (token_atual, mensagem_status_markdown) para a interface."""
    saved_tok = get_saved_hf_token()
    if not saved_tok:
        return "", "⚪ **Nenhum token salvo.** (Cota ZeroGPU anônima é 0s. Gere um token gratuito no Hugging Face abaixo para liberar sua cota pessoal)."

    is_valid, user_or_err = validate_and_save_hf_token(saved_tok)
    if is_valid:
        return saved_tok, f"🟢 **Hugging Face Conectado!** Usuário: **@{user_or_err}** (Sua cota pessoal está ativa)."
    else:
        return saved_tok, f"🔴 **Token expirado ou inválido:** {user_or_err}"

def prepare_image_as_png(image_path: str) -> str:
    """Converte qualquer formato (webp, jfif, etc.) para PNG limpo com transparência preservada"""
    try:
        im = Image.open(image_path)
        if im.mode != "RGBA":
            im = im.convert("RGBA")

        tmp_dir = Path(tempfile.gettempdir()) / "garramania_trellis"
        tmp_dir.mkdir(parents=True, exist_ok=True)
        target_png = tmp_dir / "input_clean.png"
        im.save(target_png, format="PNG")
        return str(target_png)
    except Exception as e:
        print(f"[TrellisService] Aviso na conversão de imagem: {e}")
        return image_path

def generate_3d_with_triposr(clean_image_path: str, active_token: str = None, log=safe_print) -> str:
    """
    Gera modelo 3D utilizando o motor TripoSR (Stability AI).
    Vantagens: Geração ultra-rápida (10-15s), altíssima disponibilidade e livre de limites do ZeroGPU.
    """
    log("🚀 Conectando ao motor TripoSR (Stability AI)...")
    client = Client("stabilityai/TripoSR", token=active_token)

    log("1/2 Pré-processando imagem no TripoSR (remoção de fundo e enquadramento)...")
    preprocessed_img = client.predict(
        handle_file(clean_image_path),
        True,   # remove_background
        0.85,   # foreground_ratio
        api_name="/preprocess"
    )

    log("2/2 Gerando malha e volumetria 3D no TripoSR (Marching Cubes)...")
    res = client.predict(
        handle_file(preprocessed_img),
        256,    # marching_cubes_resolution
        api_name="/generate"
    )

    glb_file = res[1] if (isinstance(res, (list, tuple)) and len(res) > 1) else res
    if isinstance(glb_file, (list, tuple)):
        glb_file = glb_file[0]

    if glb_file and os.path.exists(str(glb_file)):
        log("✅ Modelo 3D (.glb) gerado com sucesso pelo TripoSR!")
        return str(glb_file)

    raise RuntimeError("TripoSR finalizou sem retornar arquivo .glb válido.")

def generate_3d_from_image(image_path: str, hf_token: str = None, engine: str = "auto", progress_callback=None) -> str:
    """
    Envia uma imagem para geração 3D no Hugging Face e retorna o caminho do arquivo .glb gerado.
    Suporta engine="auto" (Trellis com fallback inteligente para TripoSR), "trellis" ou "triposr".
    """
    def log(msg):
        if progress_callback:
            try:
                progress_callback(msg)
            except Exception:
                pass
        safe_print(f"[AIService] {msg}")

    # Converte para PNG padrão para garantir compatibilidade total
    clean_image_path = prepare_image_as_png(image_path)
    log(f"Imagem preparada: {Path(clean_image_path).name}")

    # Determina o token ativo
    tok = (hf_token.strip() if (hf_token and hf_token.strip()) else "") or get_saved_hf_token()
    if tok:
        ok, user_res = validate_and_save_hf_token(tok)
        if ok:
            log(f"🔑 Hugging Face autenticado como @{user_res}!")
        else:
            log("🔑 Utilizando token do Hugging Face fornecido.")
        os.environ["HF_TOKEN"] = tok
    else:
        log("⚠️ Conectando sem token (Cota pública anônima).")

    active_token = tok if tok else None

    # Modo direto: TripoSR
    if engine == "triposr":
        return generate_3d_with_triposr(clean_image_path, active_token=active_token, log=log)

    # Tentativa com Trellis (modo 'trellis' ou primeira tentativa do 'auto')
    last_err = None
    zero_gpu_exceeded = False

    for space_id in TRELLIS_SPACES:
        try:
            log(f"Conectando ao endpoint Trellis: {space_id}...")
            client = Client(space_id, token=active_token)

            try:
                client.predict(api_name="/start_session")
            except Exception:
                pass

            log("1/3 Pré-processando imagem (remoção de fundo e enquadramento)...")
            preprocessed_file = None
            try:
                if "TRELLIS.2" in space_id:
                    preprocessed_file = client.predict(
                        input=handle_file(clean_image_path),
                        api_name="/preprocess_image"
                    )
                else:
                    preprocessed_file = client.predict(
                        image=handle_file(clean_image_path),
                        api_name="/preprocess_image"
                    )
            except Exception as e_prep:
                log(f"Aviso no preprocessamento ({e_prep}). Usando imagem direta...")
                preprocessed_file = clean_image_path

            if not preprocessed_file or not os.path.exists(str(preprocessed_file)):
                preprocessed_file = clean_image_path

            log("2/3 Gerando malha e volumetria 3D no Trellis (ZeroGPU)...")

            if "TRELLIS.2" in space_id:
                client.predict(
                    image=handle_file(str(preprocessed_file)),
                    seed=0,
                    resolution='1024',
                    ss_guidance_strength=7.5,
                    ss_guidance_rescale=0.7,
                    ss_sampling_steps=12,
                    ss_rescale_t=5.0,
                    shape_slat_guidance_strength=7.5,
                    shape_slat_guidance_rescale=0.5,
                    shape_slat_sampling_steps=12,
                    shape_slat_rescale_t=3.0,
                    tex_slat_guidance_strength=1.0,
                    tex_slat_guidance_rescale=0.0,
                    tex_slat_sampling_steps=12,
                    tex_slat_rescale_t=3.0,
                    api_name="/image_to_3d"
                )
                log("3/3 Extraindo arquivo .GLB com texturas e materiais...")
                res_extract = client.predict(
                    decimation_target=30000,
                    texture_size=1024,
                    api_name="/extract_glb"
                )
                glb_file = res_extract[1] if (isinstance(res_extract, (list, tuple)) and len(res_extract) > 1) else res_extract
                if isinstance(glb_file, (list, tuple)):
                    glb_file = glb_file[0]
            else:
                res = client.predict(
                    image=handle_file(str(preprocessed_file)),
                    multiimages=[],
                    seed=0,
                    ss_guidance_strength=7.5,
                    ss_sampling_steps=12,
                    slat_guidance_strength=3.0,
                    slat_sampling_steps=12,
                    multiimage_algo="stochastic",
                    mesh_simplify=0.95,
                    texture_size=1024,
                    api_name="/generate_and_extract_glb"
                )
                glb_file = res[2] or res[1] if isinstance(res, (list, tuple)) else res

            if glb_file and os.path.exists(str(glb_file)):
                log("✅ Modelo 3D (.glb) gerado com sucesso pelo Trellis!")
                return str(glb_file)

        except Exception as e:
            err_str = str(e)
            last_err = e
            if "ZeroGPU quota" in err_str or "quota" in err_str.lower() or "0s left" in err_str:
                zero_gpu_exceeded = True
            log(f"Aviso no endpoint {space_id}: {err_str}")
            continue

    # Se estiver no modo auto e o Trellis falhar (cota ZeroGPU ou indisponibilidade), fallback para TripoSR
    if engine == "auto":
        log("🔄 Trellis indisponível ou cota ZeroGPU atingida. Ativando fallback automático para TripoSR (Stability AI)...")
        try:
            return generate_3d_with_triposr(clean_image_path, active_token=active_token, log=log)
        except Exception as e_tripo:
            raise RuntimeError(f"Falha no Trellis ({last_err}) e no Fallback TripoSR ({e_tripo})")

    raise RuntimeError(f"Não foi possível gerar no Trellis: {last_err}")
