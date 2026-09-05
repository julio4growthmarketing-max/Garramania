import os
import sys
import subprocess
import shutil
from pathlib import Path

# Configura codificação do console para evitar crash no Windows cp1252
if sys.platform == "win32":
    try:
        sys.stdout.reconfigure(encoding="utf-8", errors="replace")
        sys.stderr.reconfigure(encoding="utf-8", errors="replace")
    except Exception:
        pass

# Adiciona diretório ao path
BASE_DIR = Path(__file__).resolve().parent
sys.path.insert(0, str(BASE_DIR))

from config import (
    BLENDER_EXE,
    UNITY_RESOURCES_PRIZES,
    DEFAULT_TARGET_HEIGHT,
    DEFAULT_MAX_POLYGONS,
    PROJECT_ROOT
)
from unity_bridge import UnityBridge
from trellis_service import (
    generate_3d_from_image,
    validate_and_save_hf_token,
    get_saved_hf_token,
    get_token_status_display
)

def run_trellis_generation(image_file, hf_token):
    logs = []
    def log(msg):
        logs.append(msg)
        print(f"[PlushieStudio-Trellis] {msg}")

    if not image_file:
        return None, None, "⚠️ Por favor envie uma imagem (foto ou arte 2D) primeiro.", "\n".join(logs)

    img_path = image_file.name if hasattr(image_file, 'name') else str(image_file)
    log(f"Iniciando Trellis AI para imagem: {img_path}")

    try:
        glb_path = generate_3d_from_image(img_path, hf_token=hf_token, progress_callback=log)
        log("🎉 Modelo 3D gerado com sucesso pelo Trellis!")
        return glb_path, glb_path, "✅ Trellis gerou o modelo 3D com sucesso! Você pode inspecioná-lo no visualizador 360° ao lado.", "\n".join(logs)
    except Exception as e:
        err_msg = str(e)
        log(f"❌ Erro ao conectar com Trellis: {err_msg}")
        if "ZeroGPU quota" in err_msg or "quota" in err_msg.lower():
            err_msg += "\n\n💡 Dica: Crie um token gratuito no Hugging Face (https://huggingface.co/settings/tokens) e cole no campo 'Token do Hugging Face' acima para liberar sua cota ZeroGPU!"
        return None, None, f"Falha no Trellis: {err_msg}", "\n".join(logs)

def process_and_inject_plushie(
    model_file,
    prize_id: str,
    display_name: str,
    rarity: str,
    lore: str,
    target_theme: str,
    target_height: float,
    max_polygons: int,
    stock_capacity: int,
    initial_visible: int,
    capture_chance_pct: float,
    grip_required: float
):
    logs = []
    def log(msg):
        logs.append(msg)
        print(f"[PlushieStudio] {msg}")

    if not model_file:
        return "⚠️ Nenhum modelo 3D disponível. Envie um arquivo .glb ou gere um a partir de uma foto com o Trellis.", None, "\n".join(logs)

    prize_id = prize_id.strip().replace(" ", "_")
    if not prize_id:
        prize_id = "NovoBichinho"

    log(f"Iniciando pipeline de ripagem/otimização para: {display_name} (ID: {prize_id})")

    # Arquivos temporários e definitivos
    input_path = Path(model_file.name if hasattr(model_file, 'name') else str(model_file))
    output_dir = UNITY_RESOURCES_PRIZES
    output_dir.mkdir(parents=True, exist_ok=True)

    fbx_out = output_dir / f"{prize_id}.fbx"
    tex_out = output_dir / f"{prize_id}_Texture.png"
    blend_out = PROJECT_ROOT / "Assets" / "DroneClaw" / "Models" / f"{prize_id}.blend"
    preview_out = BASE_DIR / "previews" / f"{prize_id}_preview.png"
    preview_out.parent.mkdir(parents=True, exist_ok=True)

    # 1. Execução do Blender Headless (Ripagem e Otimização)
    blender_script = BASE_DIR / "blender_scripts" / "process_model.py"
    if not Path(BLENDER_EXE).exists():
        log(f"❌ Blender não encontrado no caminho: {BLENDER_EXE}")
        return "Erro: Blender não encontrado", None, "\n".join(logs)

    log(f"Chamando Blender 5.2 para calibração, escala ({target_height}m), pivô nos pés (Z=0) e decimate...")
    cmd = [
        str(BLENDER_EXE),
        "-b",
        "-P", str(blender_script),
        "--",
        "--input", str(input_path),
        "--output_fbx", str(fbx_out),
        "--output_blend", str(blend_out),
        "--output_preview", str(preview_out),
        "--output_tex", str(tex_out),
        "--target_height", str(target_height),
        "--max_poly", str(max_polygons),
        "--model_name", prize_id
    ]

    try:
        res = subprocess.run(cmd, capture_output=True, text=True, timeout=120)
        if res.returncode != 0:
            log(f"❌ Erro na execução do Blender:\n{res.stderr}")
            return "Falha no processamento do Blender", None, "\n".join(logs)
        else:
            log("✅ Modelo 3D ripado e processado com sucesso pelo Blender!")
            log(f"   -> FBX salvo em: {fbx_out.relative_to(PROJECT_ROOT)}")
    except Exception as e:
        log(f"❌ Exceção ao rodar Blender: {e}")
        return f"Erro: {e}", None, "\n".join(logs)

    # 2. Criação / Configuração do Prefab na Unity
    prefab_out = output_dir / f"{prize_id}.prefab"
    teddy_prefab = output_dir / "Teddy.prefab"
    if not prefab_out.exists() and teddy_prefab.exists():
        shutil.copyfile(teddy_prefab, prefab_out)
        log(f"✅ Template de Prefab criado para {prize_id}.prefab")

    # 3. Registro no Sistema C# da Unity via UnityBridge
    bridge = UnityBridge()
    log("Injetando definições nos scripts C# da Unity...")

    # A) Coleção
    theme_map = {"ALL": "ALL", "Cyber Neon": "CyberNeon", "Kawaii Pastel": "KawaiiPastel", "Gold Casino": "GoldCasino"}
    theme_key = theme_map.get(target_theme, "ALL")

    ok_col, msg_col = bridge.register_in_collection(prize_id, display_name, rarity, lore)
    log(f"   [CollectionManager] {msg_col}")

    # B) Estoque
    chance_float = float(capture_chance_pct) / 100.0
    spawn_weight = 40.0 if rarity == "Rare" else 30.0 if rarity == "Uncommon" else 100.0 if rarity == "Common" else 10.0
    ok_stk, msg_stk = bridge.register_in_stock(
        prize_id=prize_id,
        rarity=rarity,
        capacity=stock_capacity,
        visible=initial_visible,
        spawn_weight=spawn_weight,
        capture_chance=chance_float
    )
    log(f"   [PrizeStockManager] {msg_stk}")

    # C) Gabinetes
    ok_cab, msg_cab = bridge.register_in_cabinet_themes(prize_id, theme_key)
    log(f"   [CabinetThemeData] {msg_cab}")

    # D) Física e Spawner
    ok_phy, msg_phy = bridge.register_in_spawner_and_physics(
        prize_id=prize_id,
        rarity=rarity,
        grip_required=grip_required,
        mass=0.85,
        capture_chance=chance_float
    )
    log(f"   [PrizePhysics & Spawner] {msg_phy}")

    final_msg = f"🎉 Sucesso! {display_name} ({rarity}) foi integrado à máquina com sucesso!"
    preview_img = str(preview_out) if preview_out.exists() else None

    return final_msg, preview_img, "\n".join(logs)

def full_auto_pipeline_from_image(
    image_file,
    hf_token: str,
    prize_id: str,
    display_name: str,
    rarity: str,
    lore: str,
    target_theme: str,
    target_height: float,
    max_polygons: int,
    stock_capacity: int,
    initial_visible: int,
    capture_chance_pct: float,
    grip_required: float
):
    """Executa o pipeline completo: Imagem ➔ Trellis 3D ➔ Blender Ripagem ➔ Unity Prefab em 1 clique"""
    if not image_file:
        return None, "⚠️ Envie uma imagem primeiro para rodar o pipeline completo.", None, "Nenhuma imagem selecionada."

    # 1. Gera o 3D com Trellis
    glb_path, _, status_trellis, logs_trellis = run_trellis_generation(image_file, hf_token)
    if not glb_path:
        return None, status_trellis, None, logs_trellis

    # 2. Ripa no Blender e Injeta na Unity
    status_unity, preview_img, logs_unity = process_and_inject_plushie(
        model_file=glb_path,
        prize_id=prize_id,
        display_name=display_name,
        rarity=rarity,
        lore=lore,
        target_theme=target_theme,
        target_height=target_height,
        max_polygons=max_polygons,
        stock_capacity=stock_capacity,
        initial_visible=initial_visible,
        capture_chance_pct=capture_chance_pct,
        grip_required=grip_required
    )

    full_logs = f"{logs_trellis}\n\n{logs_unity}"
    return glb_path, status_unity, preview_img, full_logs

def build_gradio_ui():
    import gradio as gr

    saved_token, initial_status = get_token_status_display()

    with gr.Blocks(title="GarraMania 3D Plushie Studio") as demo:
        gr.Markdown(
            """
            # 🧸 GarraMania 3D Plushie Studio & Trellis AI Pipeline
            ### Suba uma imagem 2D ou modelo 3D para gerar, ripar e colocar novos bichinhos jogáveis dentro da máquina de garra arcade!
            """
        )

        current_3d_file = gr.State(None)

        with gr.Row():
            # Coluna Esquerda: Entrada (Imagem com Trellis OU Arquivo 3D)
            with gr.Column(scale=1):
                with gr.Tabs():
                    with gr.TabItem("📸 1. Criar via Trellis AI (Foto ➔ 3D)"):
                        gr.Markdown("Envie uma foto ou ilustração de pelúcia para gerar o modelo 3D automaticamente via IA:")
                        img_input = gr.Image(label="Foto / Imagem do Bichinho", type="filepath")

                        with gr.Accordion("🔑 Autenticação Hugging Face (Cota ZeroGPU Ilimitada)", open=True):
                            token_status_md = gr.Markdown(initial_status)
                            with gr.Row():
                                hf_token_input = gr.Textbox(
                                    label="Hugging Face Access Token",
                                    placeholder="Cole aqui seu token hf_... (huggingface.co/settings/tokens)",
                                    value=saved_token,
                                    type="password",
                                    scale=3
                                )
                                btn_save_token = gr.Button("💾 Salvar Token", variant="primary", scale=1)

                            gr.Markdown(
                                """
                                ℹ️ **Como gerar seu token gratuito em 1 minuto:**
                                1. Acesse [huggingface.co/settings/tokens](https://huggingface.co/settings/tokens)
                                2. Clique em **"Create new token"**
                                3. Tipo: **"Read"** | Nome: `garramania`
                                4. Copie o token (`hf_...`), cole no campo acima e clique em **Salvar Token**!
                                """
                            )

                        btn_trellis_only = gr.Button("🧠 Gerar Modelo 3D com Trellis AI", variant="secondary")

                    with gr.TabItem("📁 2. Ou Enviar Modelo 3D Pronto (.glb / .obj)"):
                        gr.Markdown("Se já baixou o arquivo 3D de pelúcia (.glb, .gltf ou .obj), envie aqui:")
                        manual_3d_input = gr.File(
                            label="Arquivo 3D",
                            file_types=[".glb", ".gltf", ".obj", ".fbx"]
                        )

                gr.Markdown("---")
                gr.Markdown("### 🔍 Visualizador 3D Interativo (360°)")
                model_3d_viewer = gr.Model3D(label="Inspecione o modelo gerado/enviado antes de injetar na máquina")

            # Coluna Direita: Atributos, Calibração e Botões de Ação
            with gr.Column(scale=1):
                gr.Markdown("### ⚙️ Atributos do Bichinho na Unity")
                with gr.Row():
                    prize_id_input = gr.Textbox(label="ID do Asset (sem espaços)", value="Capivara_Chic", placeholder="Ex: Capivara_Chic")
                    display_name_input = gr.Textbox(label="Nome no Jogo", value="Capivara de Óculos Escuros", placeholder="Ex: Capivara Elegante")

                with gr.Row():
                    rarity_dropdown = gr.Dropdown(label="Raridade", choices=["Common", "Uncommon", "Rare", "Legendary"], value="Rare")
                    theme_dropdown = gr.Dropdown(label="Máquinas que exibem o prêmio", choices=["ALL", "Cyber Neon", "Kawaii Pastel", "Gold Casino"], value="ALL")

                lore_input = gr.Textbox(label="Descrição de Lore (Exibida no Álbum de Coleção)", value="Uma capivara relaxada e cheia de estilo que ama o fliperama!", lines=2)

                with gr.Accordion("🕹️ Calibrações Físicas da Garra e Spawner", open=False):
                    target_height_slider = gr.Slider(label="Altura no Mundo Real (metros)", minimum=0.30, maximum=0.90, value=0.55, step=0.05)
                    max_poly_slider = gr.Slider(label="Limite Máximo de Polígonos (Decimate)", minimum=5000, maximum=50000, value=25000, step=2500)
                    stock_cap_slider = gr.Slider(label="Capacidade Total no Estoque", minimum=5, maximum=100, value=30, step=5)
                    visible_slider = gr.Slider(label="Quantidade Visível no Tabuleiro", minimum=1, maximum=10, value=4, step=1)
                    capture_chance_slider = gr.Slider(label="Chance Base de Captura (%)", minimum=10, maximum=95, value=75, step=5)
                    grip_slider = gr.Slider(label="Força Mínima de Garra (Menor = Mais fácil pegar)", minimum=0.10, maximum=0.60, value=0.18, step=0.02)

                gr.Markdown("### 🚀 Ações de Injeção")
                btn_process_manual = gr.Button("🛠️ Ripar no Blender & Injetar na Unity", variant="secondary", size="lg")
                btn_full_auto = gr.Button("⚡ Pipeline Automático em 1 Clique (Foto ➔ Trellis ➔ Blender ➔ Unity)", variant="primary", size="lg")

        gr.Markdown("---")
        with gr.Row():
            with gr.Column(scale=1):
                status_output = gr.Textbox(label="Status da Operação", interactive=False)
                preview_image_output = gr.Image(label="Thumbnail Renderizado pelo Blender", type="filepath")
            with gr.Column(scale=1):
                logs_output = gr.TextArea(label="Terminal de Logs em Tempo Real", lines=10, interactive=False)

        # Eventos do Token
        def on_save_token(tok):
            if not tok or not tok.strip():
                return "⚪ Nenhum token informado."
            ok, user_or_err = validate_and_save_hf_token(tok.strip())
            if ok:
                return f"🟢 **Hugging Face Conectado!** Usuário: **@{user_or_err}** (Sua cota ZeroGPU pessoal está ativa e pronta)."
            else:
                return f"🔴 **Erro na validação:** {user_or_err}"

        btn_save_token.click(
            fn=on_save_token,
            inputs=[hf_token_input],
            outputs=[token_status_md]
        )

        manual_3d_input.change(
            fn=lambda f: (f.name if f else None, f.name if f else None),
            inputs=[manual_3d_input],
            outputs=[model_3d_viewer, current_3d_file]
        )

        btn_trellis_only.click(
            fn=run_trellis_generation,
            inputs=[img_input, hf_token_input],
            outputs=[model_3d_viewer, current_3d_file, status_output, logs_output]
        )

        btn_process_manual.click(
            fn=process_and_inject_plushie,
            inputs=[
                current_3d_file,
                prize_id_input,
                display_name_input,
                rarity_dropdown,
                lore_input,
                theme_dropdown,
                target_height_slider,
                max_poly_slider,
                stock_cap_slider,
                visible_slider,
                capture_chance_slider,
                grip_slider
            ],
            outputs=[status_output, preview_image_output, logs_output]
        )

        btn_full_auto.click(
            fn=full_auto_pipeline_from_image,
            inputs=[
                img_input,
                hf_token_input,
                prize_id_input,
                display_name_input,
                rarity_dropdown,
                lore_input,
                theme_dropdown,
                target_height_slider,
                max_poly_slider,
                stock_cap_slider,
                visible_slider,
                capture_chance_slider,
                grip_slider
            ],
            outputs=[model_3d_viewer, status_output, preview_image_output, logs_output]
        )

    return demo

if __name__ == "__main__":
    try:
        import gradio as gr
        demo = build_gradio_ui()
        print("\n=======================================================")
        print("  [GarraMania] 3D Plushie Studio & Trellis Iniciado!")
        print("  Acesse: http://127.0.0.1:7860")
        print("=======================================================\n")
        demo.launch(
            server_name="127.0.0.1",
            server_port=7860,
            share=False,
            inbrowser=True,
            theme=gr.themes.Soft(primary_hue="purple", secondary_hue="cyan")
        )
    except ImportError:
        print("Gradio não está instalado no ambiente.")
