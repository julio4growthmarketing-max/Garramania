import os
import shutil
import tempfile
from pathlib import Path
from PIL import Image
from gradio_client import Client, handle_file

# Espaços disponíveis do Trellis no Hugging Face (TRELLIS.2 é o mais recente e estável)
TRELLIS_SPACES = [
    "microsoft/TRELLIS.2",
    "trellis-community/TRELLIS"
]

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

def generate_3d_from_image(image_path: str, hf_token: str = None, progress_callback=None) -> str:
    """
    Envia uma imagem para a API do Trellis no Hugging Face e retorna o caminho do arquivo .glb gerado.
    """
    def log(msg):
        if progress_callback:
            progress_callback(msg)
        print(f"[TrellisService] {msg}")

    # Converte para PNG padrão para garantir compatibilidade total com rembg/ZeroGPU
    clean_image_path = prepare_image_as_png(image_path)
    log(f"Imagem preparada para Trellis: {Path(clean_image_path).name}")

    tok = hf_token.strip() if (hf_token and hf_token.strip()) else None
    if tok:
        log("🔑 Utilizando Hugging Face Access Token fornecido!")
    else:
        log("ℹ️ Conectando com cota pública padrão...")

    last_err = None
    for space_id in TRELLIS_SPACES:
        try:
            log(f"Conectando ao endpoint {space_id}...")
            client = Client(space_id, token=tok)

            log("1/3 Pré-processando imagem (remoção de fundo e enquadramento)...")
            preprocessed_file = None
            try:
                # Tenta chamar preprocess_image
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

            # Garante que o arquivo pré-processado existe fisicamente
            if not preprocessed_file or not os.path.exists(str(preprocessed_file)):
                preprocessed_file = clean_image_path

            log("2/3 Gerando malha e volumetria 3D no Trellis (ZeroGPU)...")
            
            if "TRELLIS.2" in space_id:
                # microsoft/TRELLIS.2
                client.predict(
                    image=handle_file(str(preprocessed_file)),
                    seed=0,
                    resolution='1024', # Deve ser string '1024', não int!
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
                # trellis-community/TRELLIS
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
                log(f"✅ Modelo 3D (.glb) gerado com sucesso pelo Trellis!")
                return str(glb_file)

        except Exception as e:
            last_err = e
            log(f"Aviso no endpoint {space_id}: {e}")
            continue

    raise RuntimeError(f"Não foi possível gerar no Trellis: {last_err}")
