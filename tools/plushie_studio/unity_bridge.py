import re
import os
import shutil
from pathlib import Path
from .config import (
    COLLECTION_MANAGER_CS,
    PRIZE_STOCK_MANAGER_CS,
    CABINET_THEME_DATA_CS,
    PRIZE_PILE_SPAWNER_CS,
    PRIZE_CS,
    UNITY_RESOURCES_PRIZES
)

class UnityBridge:
    def __init__(self):
        self.prizes_dir = UNITY_RESOURCES_PRIZES
        self.prizes_dir.mkdir(parents=True, exist_ok=True)

    def register_in_collection(self, prize_id: str, display_name: str, rarity: str, lore: str, emoji: str = "🧸"):
        """Registra o novo item no CollectionManager.cs"""
        if not COLLECTION_MANAGER_CS.exists():
            return False, f"Arquivo {COLLECTION_MANAGER_CS} não encontrado."

        content = COLLECTION_MANAGER_CS.read_text(encoding="utf-8")

        # 1. Adiciona no orderedIds se não existir
        if f'"{prize_id}"' not in content:
            ordered_pattern = r'(private readonly List<string> orderedIds = new List<string>\s*\{\s*)'
            replacement = f'\\1"{prize_id}",\n        '
            content = re.sub(ordered_pattern, replacement, content, count=1)

        # 2. Adiciona no items Dictionary em InitializeItems()
        if f'items["{prize_id}"]' not in content:
            color_var = f"Color{rarity}" # ColorCommon, ColorUncommon, ColorRare, ColorLegendary
            new_item_code = f'''
        // --- {display_name.upper()} ---
        items["{prize_id}"] = new CollectionItem
        {{
            id = "{prize_id}",
            displayName = "{display_name} {emoji}",
            rarity = PrizeRarity.{rarity},
            lore = "{lore}",
            themeColor = {color_var}
        }};
'''
            init_pattern = r'(private void InitializeItems\(\)\s*\{\s*items\.Clear\(\);\s*)'
            content = re.sub(init_pattern, f'\\1{new_item_code}', content, count=1)

        COLLECTION_MANAGER_CS.write_text(content, encoding="utf-8")
        return True, "Registrado na Coleção com sucesso!"

    def register_in_stock(self, prize_id: str, rarity: str, capacity: int = 25, visible: int = 4, spawn_weight: float = 35.0, capture_chance: float = 0.75):
        """Registra no PrizeStockManager.cs e incrementa a versão de estoque para auto-atualizar o jogo"""
        if not PRIZE_STOCK_MANAGER_CS.exists():
            return False, f"Arquivo {PRIZE_STOCK_MANAGER_CS} não encontrado."

        content = PRIZE_STOCK_MANAGER_CS.read_text(encoding="utf-8")

        # Incrementa CurrentStockVersion
        version_match = re.search(r'private const int CurrentStockVersion = (\d+);', content)
        if version_match:
            current_ver = int(version_match.group(1))
            new_ver = current_ver + 1
            content = content.replace(version_match.group(0), f'private const int CurrentStockVersion = {new_ver};')

        # Adiciona na lista de entries se não existir
        if f'resourceName = "{prize_id}"' not in content:
            new_stock_entry = f'                new PrizeStockEntry {{ resourceName = "{prize_id}", rarity = PrizeRarity.{rarity}, reserveCapacity = {capacity}, initialVisibleCount = {visible}, spawnWeight = {spawn_weight:0.1f}f, baseCaptureChance = {capture_chance:0.2f}f }},\n'
            entries_pattern = r'(entries = new List<PrizeStockEntry>\s*\{\s*)'
            content = re.sub(entries_pattern, f'\\1{new_stock_entry}', content, count=1)

        PRIZE_STOCK_MANAGER_CS.write_text(content, encoding="utf-8")
        return True, "Registrado no Estoque de Prêmios com sucesso!"

    def register_in_cabinet_themes(self, prize_id: str, theme_target: str = "ALL"):
        """Adiciona o prêmio aos temas de gabinete (CyberNeon, KawaiiPastel, GoldCasino ou ALL)"""
        if not CABINET_THEME_DATA_CS.exists():
            return False, f"Arquivo {CABINET_THEME_DATA_CS} não encontrado."

        content = CABINET_THEME_DATA_CS.read_text(encoding="utf-8")

        def add_to_theme_block(method_name: str, text: str):
            if f'"{prize_id}"' in text:
                return text
            pattern = rf'(public static CabinetThemeData {method_name}\(\)[\s\S]*?exclusivePrizeIds = new List<string>\s*\{{)'
            return re.sub(pattern, f'\\1 "{prize_id}",', text, count=1)

        if theme_target in ["ALL", "CyberNeon"]:
            content = add_to_theme_block("CreateCyberNeon", content)
        if theme_target in ["ALL", "KawaiiPastel"]:
            content = add_to_theme_block("CreateKawaiiPastel", content)
        if theme_target in ["ALL", "GoldCasino"]:
            content = add_to_theme_block("CreateGoldCasino", content)

        CABINET_THEME_DATA_CS.write_text(content, encoding="utf-8")
        return True, f"Associado aos gabinetes ({theme_target}) com sucesso!"

    def register_in_spawner_and_physics(self, prize_id: str, rarity: str, grip_required: float = 0.20, mass: float = 0.85, capture_chance: float = 0.90):
        """Registra no spawner e ajusta a calibração de aderência e peso na garra"""
        # 1. Prize.cs
        if PRIZE_CS.exists():
            prize_code = PRIZE_CS.read_text(encoding="utf-8")
            if f'"{prize_id.lower()}"' not in prize_code:
                calibration_block = f'''
        // Calibração especial para {prize_id}
        if (StockId != null && StockId.IndexOf("{prize_id}", System.StringComparison.OrdinalIgnoreCase) >= 0)
        {{
            gripRequired = {grip_required:0.2f}f;
            massFeel = {mass:0.2f}f;
            slipperiness = 0.05f;
            BaseCaptureChance = {capture_chance:0.2f}f;
        }}
'''
                pattern = r'(if \(Body != null\) Body\.mass = massFeel;)'
                prize_code = re.sub(pattern, f'{calibration_block}\n        \\1', prize_code, count=1)
                PRIZE_CS.write_text(prize_code, encoding="utf-8")

        # 2. PrizePileSpawner.cs
        if PRIZE_PILE_SPAWNER_CS.exists():
            spawner_code = PRIZE_PILE_SPAWNER_CS.read_text(encoding="utf-8")
            # Prefab name mapping
            if f'"{prize_id.lower()}"' not in spawner_code:
                pattern_base = r'(private string GetBasePrefabName\(string variantId\)\s*\{\s*string lower = variantId\.ToLowerInvariant\(\);\s*)'
                spawner_code = re.sub(pattern_base, f'\\1if (lower.Contains("{prize_id.lower()}")) return "{prize_id}";\n        ', spawner_code, count=1)

                pattern_rarity = r'(private PrizeRarity GetVariantRarity\(string variantId\)\s*\{[\s\S]*?string lower = variantId\.ToLowerInvariant\(\);\s*)'
                spawner_code = re.sub(pattern_rarity, f'\\1if (lower.Contains("{prize_id.lower()}")) return PrizeRarity.{rarity};\n        ', spawner_code, count=1)

                PRIZE_PILE_SPAWNER_CS.write_text(spawner_code, encoding="utf-8")

        return True, "Física e Spawner calibrados com sucesso!"
