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
        return "⚠️ Por favor envie um arquivo de modelo 3D (.glb, .gltf ou .obj).", None, "\n".join(logs)

    prize_id = prize_id.strip().replace(" ", "_")
    if not prize_id:
        prize_id = "NovoBichinho"

    log(f"Iniciando pipeline para: {display_name} (ID: {prize_id})")

    # Arquivos temporários e definitivos
    input_path = Path(model_file.name if hasattr(model_file, 'name') else str(model_file))
    output_dir = UNITY_RESOURCES_PRIZES
    output_dir.mkdir(parents=True, exist_ok=True)

    fbx_out = output_dir / f"{prize_id}.fbx"
    tex_out = output_dir / f"{prize_id}_Texture.png"
    blend_out = PROJECT_ROOT / "Assets" / "DroneClaw" / "Models" / f"{prize_id}.blend"
    preview_out = BASE_DIR / "previews" / f"{prize_id}_preview.png"
    preview_out.parent.mkdir(parents=True, exist_ok=True)

    # 1. Execução do Blender Headless
    blender_script = BASE_DIR / "blender_scripts" / "process_model.py"
    if not Path(BLENDER_EXE).exists():
        log(f"❌ Blender não encontrado no caminho: {BLENDER_EXE}")
        return "Erro: Blender não encontrado", None, "\n".join(logs)

    log(f"Chamando Blender 5.2 para calibração, escala ({target_height}m), pivô nos pés e decimate...")
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
            log("✅ Modelo 3D processado com sucesso pelo Blender!")
            log(f"   -> FBX salvo em: {fbx_out.relative_to(PROJECT_ROOT)}")
    except Exception as e:
        log(f"❌ Exceção ao rodar Blender: {e}")
        return f"Erro: {e}", None, "\n".join(logs)

    # 2. Criação / Configuração do Prefab na Unity
    prefab_out = output_dir / f"{prize_id}.prefab"
    teddy_prefab = output_dir / "Teddy.prefab"
    if not prefab_out.exists() and teddy_prefab.exists():
        # Clona o template do Teddy que já possui a estrutura compatível
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

def build_gradio_ui():
    import gradio as gr

    with gr.Blocks(title="GarraMania 3D Plushie Studio") as demo:
        gr.Markdown(
            """
            # 🧸 GarraMania 3D Plushie Studio & Pipeline
            ### Transforme modelos 3D de IA (Trellis, Meshy, Tripo) em bichinhos jogáveis na máquina de garra arcade em 1 clique!
            """
        )

        with gr.Row():
            with gr.Column(scale=1):
                gr.Markdown("### 1. Entrada do Modelo 3D")
                model_input = gr.File(
                    label="Envie o arquivo 3D (.glb, .gltf ou .obj)",
                    file_types=[".glb", ".gltf", ".obj", ".fbx"]
                )
                model_3d_preview = gr.Model3D(label="Visualizador 3D Interativo 360°")
                model_input.change(lambda f: f.name if f else None, inputs=[model_input], outputs=[model_3d_preview])

            with gr.Column(scale=1):
                gr.Markdown("### 2. Atributos do Bichinho na Unity")
                with gr.Row():
                    prize_id_input = gr.Textbox(label="ID do Asset (sem espaços)", value="Capivara_Chic", placeholder="Ex: Capivara_Chic")
                    display_name_input = gr.Textbox(label="Nome no Jogo", value="Capivara de Óculos Escuros", placeholder="Ex: Capivara Elegante")

                with gr.Row():
                    rarity_dropdown = gr.Dropdown(label="Raridade", choices=["Common", "Uncommon", "Rare", "Legendary"], value="Rare")
                    theme_dropdown = gr.Dropdown(label="Máquinas que exibem o prêmio", choices=["ALL", "Cyber Neon", "Kawaii Pastel", "Gold Casino"], value="ALL")

                lore_input = gr.Textbox(label="Descrição de Lore (Exibida no Álbum de Coleção)", value="Uma capivara relaxada e cheia de estilo que ama o fliperama!", lines=2)

                with gr.Accordion("⚙️ Calibrações Físicas e Spawn", open=False):
                    target_height_slider = gr.Slider(label="Altura no Mundo Real (metros)", minimum=0.30, maximum=0.90, value=0.55, step=0.05)
                    max_poly_slider = gr.Slider(label="Limite Máximo de Polígonos (Decimate)", minimum=5000, maximum=50000, value=25000, step=2500)
                    stock_cap_slider = gr.Slider(label="Capacidade Total no Estoque", minimum=5, maximum=100, value=30, step=5)
                    visible_slider = gr.Slider(label="Quantidade Visível no Tabuleiro", minimum=1, maximum=10, value=4, step=1)
                    capture_chance_slider = gr.Slider(label="Chance Base de Captura (%)", minimum=10, maximum=95, value=75, step=5)
                    grip_slider = gr.Slider(label="Força Mínima de Garra (Menor = Mais fácil pegar)", minimum=0.10, maximum=0.60, value=0.18, step=0.02)

                btn_process = gr.Button("🚀 Otimizar no Blender & Injetar na Unity", variant="primary", size="lg")

        gr.Markdown("---")
        with gr.Row():
            with gr.Column(scale=1):
                status_output = gr.Textbox(label="Status da Integração", interactive=False)
                preview_image_output = gr.Image(label="Thumbnail Renderizado pelo Blender", type="filepath")
            with gr.Column(scale=1):
                logs_output = gr.TextArea(label="Terminal de Operações", lines=10, interactive=False)

        btn_process.click(
            fn=process_and_inject_plushie,
            inputs=[
                model_input,
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

    return demo

if __name__ == "__main__":
    try:
        import gradio as gr
        demo = build_gradio_ui()
        print("\n=======================================================")
        print("  [GarraMania] 3D Plushie Studio Iniciado!")
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
