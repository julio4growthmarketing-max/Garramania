# 🧸 GarraMania 3D Plushie Studio (Gradio + Blender + Unity)

Plataforma automatizada para transformar modelos 3D gerados por Inteligência Artificial (Trellis, Meshy, Tripo, etc.) em bichinhos de pelúcia prontos para jogar na máquina de garra arcade!

---

## ⚡ Como Funciona o Pipeline

```
[ Modelo 3D (Trellis/GLB/OBJ) ]
               │
               ▼
[ 1. Interface Gradio (app.py) ]
   - Upload do arquivo 3D
   - Inspeção visual 360° no navegador
   - Configuração de Nome, Raridade, Lore, Física e Estoque
               │
               ▼
[ 2. Blender Headless (process_model.py) ]
   - Calibração automática da altura (~0.55m de pelúcia)
   - Ajuste do pivô na base dos pés (Z = 0, X = 0, Y = 0)
   - Redução de malha inteligente (Decimate para <25k polígonos - 60fps no WebGL)
   - Extração da textura PNG
   - Renderização de thumbnail 512x512 de alta definição
   - Exportação para FBX nos eixos padrão da Unity (-Z forward, Y up)
               │
               ▼
[ 3. Ponte Unity (unity_bridge.py) ]
   - Salva FBX e Textura em `Assets/Resources/Prizes/`
   - Cria o Prefab de jogo com `BoxCollider` e `Prize`
   - Registra no `CollectionManager.cs` (álbum de figurinhas)
   - Registra no `PrizeStockManager.cs` (estoque e taxa de spawn)
   - Registra no `CabinetThemeData.cs` (máquinas Cyber Neon, Kawaii Pastel ou Gold Casino)
   - Registra calibração especial no `Prize.cs` e `PrizePileSpawner.cs`
```

---

## 🚀 Como Executar

1. **Pelo Prompt / Terminal:**
   ```bash
   cd tools/plushie_studio
   pip install -r requirements.txt
   python app.py
   ```

2. **Pelo Executável Batch:**
   - Dê dois cliques em `run_studio.bat` dentro da pasta `tools/plushie_studio/`.

3. Abra seu navegador em: **`http://127.0.0.1:7860`**
