using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

[System.Serializable]
public class CollectionItem
{
    public string id;
    public string displayName;
    public PrizeRarity rarity;
    public string lore;
    public Color themeColor;
    public int count;
    public string firstCapturedAt;

    public bool IsUnlocked => count > 0;
}

public struct CaptureResult
{
    public CollectionItem item;
    public bool isFirstTime;
    public int totalOfThisType;
    public int totalUniqueUnlocked;
}

/// <summary>
/// Gerenciador da coleção permanente do GarraMania.
/// Registra o progresso dos 6 bichinhos oficiais, contagem de duplicatas e timestamps.
/// </summary>
public sealed class CollectionManager : MonoBehaviour
{
    private static CollectionManager _instance;
    public static CollectionManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindFirstObjectByType<CollectionManager>();
                if (_instance == null)
                {
                    GameObject go = new GameObject("CollectionManager");
                    _instance = go.AddComponent<CollectionManager>();
                    DontDestroyOnLoad(go);
                }
            }
            return _instance;
        }
    }

    public static readonly Color ColorCommon = new Color(0.00f, 0.93f, 1.00f, 1f);     // Ciano Neon
    public static readonly Color ColorUncommon = new Color(0.78f, 0.25f, 1.00f, 1f);   // Violeta Cósmico
    public static readonly Color ColorRare = new Color(1.00f, 0.82f, 0.10f, 1f);       // Ouro 24k
    public static readonly Color ColorLegendary = new Color(1.00f, 0.20f, 0.85f, 1f);  // Prisma Neon / Magenta Estelar

    private readonly Dictionary<string, CollectionItem> items = new Dictionary<string, CollectionItem>(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> orderedIds = new List<string> 
    { 
        "Fox", "Fox_Arctic", "Fox_Shadow",
        "GreenBear", "Bear_Panda", "Bear_Polar", "Bear_Galaxy",
        "BalloonFish", "Fish_Clown", "Fish_Gold",
        "Koala", "Koala_Eucalyptus", "Koala_King",
        "Badger", "Badger_Honey",
        "Porky_Classic", "Porky", "Porky_Diamond"
    };

    public UnityEvent<CaptureResult> OnPrizeCaptured = new UnityEvent<CaptureResult>();
    public UnityEvent OnCollectionUpdated = new UnityEvent();

    private const string PREF_PREFIX_COUNT = "GarraMania_Col_Count_";
    private const string PREF_PREFIX_DATE = "GarraMania_Col_Date_";

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        DontDestroyOnLoad(gameObject);

        InitializeItems();
        LoadCollection();
    }

    private void Start()
    {
        // Conecta ao GameSession para escutar entregas de prêmio
        if (GameSession.Instance != null)
        {
            GameSession.Instance.OnPrizeDelivered.AddListener(HandlePrizeDelivered);
        }
    }

    private void InitializeItems()
    {
        items.Clear();

        // --- FAMÍLIA RAPOSA ---
        items["Fox"] = new CollectionItem
        {
            id = "Fox",
            displayName = "Raposa Astuta",
            rarity = PrizeRarity.Common,
            lore = "Esperta e veloz! Adora se aconchegar nos cantos da vitrine. Um dos clássicos favoritos dos fliperamas.",
            themeColor = ColorCommon
        };
        items["Fox_Arctic"] = new CollectionItem
        {
            id = "Fox_Arctic",
            displayName = "Raposa do Ártico",
            rarity = PrizeRarity.Uncommon,
            lore = "Pelagem branca pura como a neve e olhos ciano. Rara de ser avistada fora das montanhas geladas.",
            themeColor = ColorUncommon
        };
        items["Fox_Shadow"] = new CollectionItem
        {
            id = "Fox_Shadow",
            displayName = "Raposa Sombria",
            rarity = PrizeRarity.Rare,
            lore = "Misteriosa e furtiva como uma ninja. Emite um brilho espectral magenta que fascina colecionadores experientes.",
            themeColor = ColorRare
        };

        // --- FAMÍLIA URSO ---
        items["GreenBear"] = new CollectionItem
        {
            id = "GreenBear",
            displayName = "Ursinho Menta",
            rarity = PrizeRarity.Common,
            lore = "O clássico mascote com pelo aveludado e cheirinho de hortelã. Muito fofo e ótimo para treinar a mira.",
            themeColor = ColorCommon
        };
        items["Bear_Panda"] = new CollectionItem
        {
            id = "Bear_Panda",
            displayName = "Urso Panda Zen",
            rarity = PrizeRarity.Uncommon,
            lore = "Tranquilo e comilão de bambu. Seu corpinho fofo em preto e branco rola fácil pela pilha de prêmios.",
            themeColor = ColorUncommon
        };
        items["Bear_Polar"] = new CollectionItem
        {
            id = "Bear_Polar",
            displayName = "Urso Polar Glacial",
            rarity = PrizeRarity.Uncommon,
            lore = "Firme e imponente, resiste a qualquer tranco com sua pelagem aveludada do Círculo Polar.",
            themeColor = ColorUncommon
        };
        items["Bear_Galaxy"] = new CollectionItem
        {
            id = "Bear_Galaxy",
            displayName = "Urso Cósmico Galaxy",
            rarity = PrizeRarity.Legendary,
            lore = "Forjado nas nebulosas do espaço profundo! Seu corpo translúcido brilha com constelações estelares vivas.",
            themeColor = ColorLegendary
        };

        // --- FAMÍLIA PEIXE BALÃO ---
        items["BalloonFish"] = new CollectionItem
        {
            id = "BalloonFish",
            displayName = "Peixe Balão",
            rarity = PrizeRarity.Common,
            lore = "Redondinho e rechonchudo, rola pelas curvas do monte. Seu formato de esfera facilita o encaixe nas pinças.",
            themeColor = ColorCommon
        };
        items["Fish_Clown"] = new CollectionItem
        {
            id = "Fish_Clown",
            displayName = "Peixe Palhaço",
            rarity = PrizeRarity.Common,
            lore = "Direto dos recifes de corais tropicais! Suas listras marcantes trazem sorte aos novos jogadores.",
            themeColor = ColorCommon
        };
        items["Fish_Gold"] = new CollectionItem
        {
            id = "Fish_Gold",
            displayName = "Peixinho Dourado",
            rarity = PrizeRarity.Rare,
            lore = "Lenda dos fliperamas orientais: capturar este peixe banhado a ouro 24k concede prosperidade e fichas!",
            themeColor = ColorRare
        };

        // --- FAMÍLIA COALA ---
        items["Koala"] = new CollectionItem
        {
            id = "Koala",
            displayName = "Coala Sonolento",
            rarity = PrizeRarity.Uncommon,
            lore = "Dorme abraçado aos vizinhos. Possui pegada firme e exige um centro de massa certeiro para ser erguido!",
            themeColor = ColorUncommon
        };
        items["Koala_Eucalyptus"] = new CollectionItem
        {
            id = "Koala_Eucalyptus",
            displayName = "Coala Eucalipto",
            rarity = PrizeRarity.Uncommon,
            lore = "Camuflado entre os galhos e folhas perfumadas. Sempre relaxado, mesmo suspenso no ar.",
            themeColor = ColorUncommon
        };
        items["Koala_King"] = new CollectionItem
        {
            id = "Koala_King",
            displayName = "Coala Real Supremo",
            rarity = PrizeRarity.Legendary,
            lore = "O soberano supremo dos coalas! Ostenta uma aura radiante e comanda a corte de pelúcias da vitrine.",
            themeColor = ColorLegendary
        };

        // --- FAMÍLIA TEXUGO ---
        items["Badger"] = new CollectionItem
        {
            id = "Badger",
            displayName = "Texugo Valente",
            rarity = PrizeRarity.Uncommon,
            lore = "Pequeno mas com grande atitude! Suas listras marcantes chamam atenção de qualquer colecionador experiente.",
            themeColor = ColorUncommon
        };
        items["Badger_Honey"] = new CollectionItem
        {
            id = "Badger_Honey",
            displayName = "Texugo do Mel",
            rarity = PrizeRarity.Rare,
            lore = "O animal mais destemido da savana! Com pelagem cor de mel dourado, desafia as garras mais apertadas.",
            themeColor = ColorRare
        };

        // --- FAMÍLIA PORQUINHO ---
        items["Porky_Classic"] = new CollectionItem
        {
            id = "Porky_Classic",
            displayName = "Porquinho Chiclete",
            rarity = PrizeRarity.Common,
            lore = "Fofinho, macio e rosa chiclete! A companhia perfeita para qualquer fã de doces e arcades.",
            themeColor = ColorCommon
        };
        items["Porky"] = new CollectionItem
        {
            id = "Porky",
            displayName = "Porky, o Magnata",
            rarity = PrizeRarity.Rare,
            lore = "O lendário porquinho listrado de ouro! O prêmio clássico mais cobiçado de toda a máquina.",
            themeColor = ColorRare
        };
        items["Porky_Diamond"] = new CollectionItem
        {
            id = "Porky_Diamond",
            displayName = "Porky Diamante Cristal",
            rarity = PrizeRarity.Legendary,
            lore = "Lapidado em cristal puro com reflexos prismáticos! A joia suprema do GarraMania. Uma captura para a história.",
            themeColor = ColorLegendary
        };
    }

    private void LoadCollection()
    {
        foreach (var id in orderedIds)
        {
            if (items.TryGetValue(id, out var item))
            {
                item.count = PlayerPrefs.GetInt(PREF_PREFIX_COUNT + id, 0);
                item.firstCapturedAt = PlayerPrefs.GetString(PREF_PREFIX_DATE + id, "");
            }
        }
    }

    public CaptureResult LastCaptureResult { get; private set; }

    private void SaveItem(CollectionItem item)
    {
        PlayerPrefs.SetInt(PREF_PREFIX_COUNT + item.id, item.count);
        PlayerPrefs.SetString(PREF_PREFIX_DATE + item.id, item.firstCapturedAt);
        PersistentSaveManager.MarkDirty();
    }

    public void HandlePrizeDelivered(Prize prize, int totalDelivered)
    {
        if (prize == null) return;
        string stockId = prize.StockId;
        if (string.IsNullOrEmpty(stockId)) stockId = prize.prizeId;

        // Normalização de nomes (caso venha urso_comum ou prefixado)
        stockId = NormalizeStockId(stockId);

        RegisterCapture(stockId);
    }

    public CaptureResult RegisterCapture(string rawId)
    {
        string id = NormalizeStockId(rawId);
        if (!items.TryGetValue(id, out var item))
        {
            // Fallback para primeiro item se desconhecido
            id = "Fox";
            item = items[id];
        }

        bool firstTime = (item.count == 0);
        item.count++;
        if (firstTime)
        {
            item.firstCapturedAt = DateTime.Now.ToString("dd/MM/yyyy HH:mm");
        }

        SaveItem(item);

        CaptureResult res = new CaptureResult
        {
            item = item,
            isFirstTime = firstTime,
            totalOfThisType = item.count,
            totalUniqueUnlocked = GetUnlockedCount()
        };

        LastCaptureResult = res;

        OnPrizeCaptured?.Invoke(res);
        OnCollectionUpdated?.Invoke();

        Debug.Log($"[CollectionManager] Prêmio Registrado: {item.displayName} (Total: {item.count}, Primeira vez: {firstTime}) | Desbloqueados: {GetUnlockedCount()}/{GetTotalCount()}");

        return res;
    }

    public string NormalizeStockId(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return "Fox";
        if (items.ContainsKey(raw)) return raw;

        string lower = raw.ToLowerInvariant();
        // Variantes específicas primeiro
        if (lower.Contains("fox_arctic")) return "Fox_Arctic";
        if (lower.Contains("fox_shadow")) return "Fox_Shadow";
        if (lower.Contains("bear_panda")) return "Bear_Panda";
        if (lower.Contains("bear_polar")) return "Bear_Polar";
        if (lower.Contains("bear_galaxy")) return "Bear_Galaxy";
        if (lower.Contains("fish_clown")) return "Fish_Clown";
        if (lower.Contains("fish_gold")) return "Fish_Gold";
        if (lower.Contains("koala_eucalyptus")) return "Koala_Eucalyptus";
        if (lower.Contains("koala_king")) return "Koala_King";
        if (lower.Contains("badger_honey")) return "Badger_Honey";
        if (lower.Contains("porky_classic") || lower.Contains("porky_pink")) return "Porky_Classic";
        if (lower.Contains("porky_diamond")) return "Porky_Diamond";

        // Bases padrão
        if (lower.Contains("fox") || lower.Contains("raposa")) return "Fox";
        if (lower.Contains("green") || lower.Contains("menta") || lower.Contains("bear")) return "GreenBear";
        if (lower.Contains("balloon") || lower.Contains("fish") || lower.Contains("peixe")) return "BalloonFish";
        if (lower.Contains("koala") || lower.Contains("coala")) return "Koala";
        if (lower.Contains("badger") || lower.Contains("texugo")) return "Badger";
        if (lower.Contains("porky") || lower.Contains("porco") || lower.Contains("rare")) return "Porky";
        return "Fox";
    }

    public CollectionItem GetItem(string id)
    {
        string norm = NormalizeStockId(id);
        return items.TryGetValue(norm, out var item) ? item : null;
    }

    public List<CollectionItem> GetAllItems()
    {
        List<CollectionItem> list = new List<CollectionItem>();
        foreach (var id in orderedIds)
        {
            if (items.TryGetValue(id, out var it)) list.Add(it);
        }
        return list;
    }

    public int GetUnlockedCount()
    {
        int c = 0;
        foreach (var it in items.Values)
        {
            if (it.IsUnlocked) c++;
        }
        return c;
    }

    public int TotalCount => orderedIds.Count;
    public int GetTotalCount() => orderedIds.Count;

    public bool IsComplete() => GetUnlockedCount() >= GetTotalCount();

    // ==================== SETS / COLEÇÕES TEMÁTICAS (COLLECT ALL 3) ====================
    [System.Serializable]
    public class PrizeSet
    {
        public string setId;
        public string title;
        public string subtitle;
        public string[] itemIds;
        public int rewardTokens;
        public bool hasClaimedSetReward;
    }

    private readonly List<PrizeSet> prizeSets = new List<PrizeSet>
    {
        new PrizeSet
        {
            setId = "jungle_trio",
            title = "TRIO DA FLORESTA",
            subtitle = "Raposa Astuta, Ursinho Menta & Peixe Balão",
            itemIds = new string[] { "Fox", "GreenBear", "BalloonFish" },
            rewardTokens = 35
        },
        new PrizeSet
        {
            setId = "legend_trio",
            title = "MESTRES RAROS",
            subtitle = "Coala Sonolento, Texugo & Porky Magnata",
            itemIds = new string[] { "Koala", "Badger", "Porky" },
            rewardTokens = 60
        }
    };

    public List<PrizeSet> GetAllSets()
    {
        foreach (var set in prizeSets)
        {
            set.hasClaimedSetReward = PlayerPrefs.GetInt("GarraMania_SetClaimed_" + set.setId, 0) == 1;
        }
        return prizeSets;
    }

    public int GetSetProgress(PrizeSet set)
    {
        if (set == null || set.itemIds == null) return 0;
        int count = 0;
        foreach (var id in set.itemIds)
        {
            var it = GetItem(id);
            if (it != null && it.IsUnlocked) count++;
        }
        return count;
    }

    public bool IsSetComplete(PrizeSet set)
    {
        if (set == null || set.itemIds == null) return false;
        return GetSetProgress(set) >= set.itemIds.Length;
    }

    public bool ClaimSetReward(PrizeSet set)
    {
        if (set == null || !IsSetComplete(set) || set.hasClaimedSetReward) return false;
        set.hasClaimedSetReward = true;
        PlayerPrefs.SetInt("GarraMania_SetClaimed_" + set.setId, 1);
        PersistentSaveManager.MarkDirty();

        if (GameSession.Instance != null)
        {
            GameSession.Instance.AddCredits(set.rewardTokens);
        }

        AudioFeedbackController.Instance?.PlayCoin();
        GameJuice.Instance?.HapticsSuccess();
        OnCollectionUpdated?.Invoke();
        return true;
    }
}

