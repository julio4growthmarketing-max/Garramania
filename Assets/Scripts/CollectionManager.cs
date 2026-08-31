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

    private readonly Dictionary<string, CollectionItem> items = new Dictionary<string, CollectionItem>(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> orderedIds = new List<string> { "Fox", "GreenBear", "BalloonFish", "Koala", "Badger", "Porky" };

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

        items["Fox"] = new CollectionItem
        {
            id = "Fox",
            displayName = "Raposa Astuta",
            rarity = PrizeRarity.Common,
            lore = "Esperta e veloz! Adora se aconchegar nos cantos da vitrine. Um dos clássicos favoritos dos fliperamas.",
            themeColor = ColorCommon
        };

        items["GreenBear"] = new CollectionItem
        {
            id = "GreenBear",
            displayName = "Ursinho Menta",
            rarity = PrizeRarity.Common,
            lore = "O clássico mascote com pelo aveludado e cheirinho de hortelã. Muito fofo e ótimo para treinar a mira.",
            themeColor = ColorCommon
        };

        items["BalloonFish"] = new CollectionItem
        {
            id = "BalloonFish",
            displayName = "Peixe Balão",
            rarity = PrizeRarity.Common,
            lore = "Redondinho e rechonchudo, rola pelas curvas do monte. Seu formato de esfera facilita o encaixe nas pinças.",
            themeColor = ColorCommon
        };

        items["Koala"] = new CollectionItem
        {
            id = "Koala",
            displayName = "Coala Sonolento",
            rarity = PrizeRarity.Uncommon,
            lore = "Dorme abraçado aos vizinhos. Possui pegada firme e exige um centro de massa certeiro para ser erguido!",
            themeColor = ColorUncommon
        };

        items["Badger"] = new CollectionItem
        {
            id = "Badger",
            displayName = "Texugo Valente",
            rarity = PrizeRarity.Uncommon,
            lore = "Pequeno mas com grande atitude! Suas listras marcantes chamam atenção de qualquer colecionador experiente.",
            themeColor = ColorUncommon
        };

        items["Porky"] = new CollectionItem
        {
            id = "Porky",
            displayName = "Porky, o Magnata",
            rarity = PrizeRarity.Rare,
            lore = "O lendário porquinho listrado de ouro! O prêmio mais cobiçado de toda a máquina. Quem o captura é um verdadeiro Mestre da Garra.",
            themeColor = ColorRare
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

    private void SaveItem(CollectionItem item)
    {
        PlayerPrefs.SetInt(PREF_PREFIX_COUNT + item.id, item.count);
        PlayerPrefs.SetString(PREF_PREFIX_DATE + item.id, item.firstCapturedAt);
        PlayerPrefs.Save();
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

        OnPrizeCaptured?.Invoke(res);
        OnCollectionUpdated?.Invoke();

        Debug.Log($"[CollectionManager] Prêmio Registrado: {item.displayName} (Total: {item.count}, Primeira vez: {firstTime}) | Desbloqueados: {GetUnlockedCount()}/{GetTotalCount()}");

        return res;
    }

    public string NormalizeStockId(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return "Fox";
        string lower = raw.ToLowerInvariant();
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
        PlayerPrefs.Save();

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

