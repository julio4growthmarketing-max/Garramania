import os
import shutil
from pathlib import Path
from gradio_client import Client, handle_file

# Espaços disponíveis do Trellis no Hugging Face
TRELLIS_SPACES = [
    "trellis-community/TRELLIS",
    "microsoft/TRELLIS.2"
]

def generate_3d_from_image(image_path: str, hf_token: str = None, progress_callback=None) -> str:
    """
    Envia uma imagem para a API do Trellis no Hugging Face e retorna o caminho do arquivo .glb gerado.
    """
    def log(msg):
        if progress_callback:
            progress_callback(msg)
        print(f"[TrellisService] {msg}")

    log("Conectando ao serviço TRELLIS AI no Hugging Face...")

    last_err = None
    for space_id in TRELLIS_SPACES:
        try:
            tok = hf_token.strip() if (hf_token and hf_token.strip()) else None
            client = Client(space_id, token=tok)

            log("1/3 Pré-processando imagem (remoção de fundo e enquadramento)...")
            try:
                preprocessed = client.predict(
                    image=handle_file(image_path),
                    api_name="/preprocess_image"
                )
            except Exception:
                # Alguns endpoints chamam de 'input'
                preprocessed = client.predict(
                    input=handle_file(image_path),
                    api_name="/preprocess_image"
                )

            log("2/3 Gerando malha e volumetria 3D no Trellis (ZeroGPU)...")
            
            # Se for trellis-community/TRELLIS
            if "community" in space_id:
                res = client.predict(
                    image=handle_file(preprocessed),
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
                # res é tupla: (video_preview, extracted_glb, download_glb)
                glb_file = res[2] or res[1]
            else:
                # microsoft/TRELLIS.2
                client.predict(
                    image=handle_file(preprocessed),
                    seed=0,
                    resolution=1024,
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
                log("3/3 Extraindo arquivo .GLB com texturas...")
                res_extract = client.predict(
                    decimation_target=30000,
                    texture_size=1024,
                    api_name="/extract_glb"
                )
                glb_file = res_extract[1] or res_extract[0]

            if glb_file and os.path.exists(glb_file):
                log(f"✅ Modelo 3D (.glb) gerado com sucesso pelo Trellis!")
                return glb_file

        except Exception as e:
            last_err = e
            log(f"Aviso no endpoint {space_id}: {e}")
            continue

    raise RuntimeError(f"Não foi possível gerar no Trellis: {last_err}")
