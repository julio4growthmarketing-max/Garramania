import os
from pathlib import Path

# Raiz do projeto Unity
PROJECT_ROOT = Path(__file__).resolve().parent.parent.parent

# Caminho do executável do Blender
BLENDER_EXE = os.environ.get(
    "BLENDER_EXE",
    r"C:\Program Files\Blender Foundation\Blender 5.2\blender.exe"
)

# Diretórios de destino na Unity
UNITY_RESOURCES_PRIZES = PROJECT_ROOT / "Assets" / "Resources" / "Prizes"
UNITY_MODELS_DIR = PROJECT_ROOT / "Assets" / "DroneClaw" / "Models"
UNITY_PREFABS_DIR = PROJECT_ROOT / "Assets" / "Resources" / "Prizes"

# Arquivos de código da Unity para injeção automática de dados
SCRIPTS_DIR = PROJECT_ROOT / "Assets" / "Scripts"
COLLECTION_MANAGER_CS = SCRIPTS_DIR / "CollectionManager.cs"
PRIZE_STOCK_MANAGER_CS = SCRIPTS_DIR / "PrizeStockManager.cs"
CABINET_THEME_DATA_CS = SCRIPTS_DIR / "CabinetThemeData.cs"
PRIZE_PILE_SPAWNER_CS = SCRIPTS_DIR / "PrizePileSpawner.cs"
PRIZE_CS = SCRIPTS_DIR / "Prize.cs"

# Valores padrão para bichinhos de pelúcia arcade
DEFAULT_TARGET_HEIGHT = 0.55  # 55 cm de altura no mundo real
DEFAULT_MAX_POLYGONS = 25000  # Limite para rodar a 60fps no WebGL Mobile
