using System;
using System.Collections.Generic;
using UnityEngine;

public enum PrizeRarity
{
    Common,
    Uncommon,
    Rare,
    Legendary
}

[Serializable]
public sealed class PrizeStockEntry
{
    [Tooltip("Nome do prefab dentro de Resources/Prizes, sem extensão.")]
    public string resourceName;
    public PrizeRarity rarity;
    [Min(0)] public int reserveCapacity;
    [Min(0)] public int initialVisibleCount;
    [Min(0.01f)] public float spawnWeight = 1f;
    [Range(0.05f, 1f)] public float baseCaptureChance = 0.8f;
    [NonSerialized] public GameObject prefab;
    [NonSerialized] public int available;
    [NonSerialized] public int active;
    [NonSerialized] public int initialRemaining;
}

/// <summary>
/// Estoque vivo da máquina. O estoque é separado do visual e do gameplay:
/// o gabinete apenas pede o próximo prefab e registra nascimentos/entregas.
///
/// Regras do protótipo:
/// - A máquina começa com 24 comuns, 9 incomuns e 3 raros em 36 posições.
/// - Comuns, incomuns e raros possuem reservas de 100, 50 e 10 unidades.
/// - Entregas consomem o estoque; reposição temporal devolve unidades à reserva.
/// - Cada tentativa sem raro aumenta a proteção de raridade (pity system).
/// - A proteção é aplicada tanto ao sorteio de reposição quanto à captura de raros.
/// </summary>
public sealed class PrizeStockManager : MonoBehaviour
{
    private const string StockKeyPrefix = "GarraMania.Stock.Available.";
    private const string ActiveKeyPrefix = "GarraMania.Stock.Active.";
    private const string PityKey = "GarraMania.Stock.RarePityMisses";
    private const string LastRefillKey = "GarraMania.Stock.LastRefillUtc";
    private const string StockVersionKey = "GarraMania.Stock.Version";
    private const int CurrentStockVersion = 3;

    private static PrizeStockManager _instance;
    public static PrizeStockManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindFirstObjectByType<PrizeStockManager>();
                if (_instance == null)
                {
                    _instance = new GameObject("PrizeStockManager").AddComponent<PrizeStockManager>();
                }
            }
            return _instance;
        }
    }

    [Header("Estoque visível")]
    [SerializeField, Min(1)] private int targetBoardCount = 36;
    [SerializeField, Min(1)] private int activeRefillBatch = 3;
    [SerializeField, Min(1f)] private float activeRefillIntervalSeconds = 90f;

    [Header("Reposição fora do jogo")]
    [SerializeField, Min(0.1f)] private float offlineRefillIntervalHours = 6f;
    [SerializeField, Min(0)] private int commonRefillPerOfflineCycle = 12;
    [SerializeField, Min(0)] private int uncommonRefillPerOfflineCycle = 4;
    [SerializeField, Min(0)] private int rareRefillPerOfflineCycle = 1;

    [Header("Pity system de raridade")]
    [SerializeField, Min(0)] private int maxRarePityMisses = 12;
    [SerializeField, Range(0f, 0.25f)] private float rareSpawnBonusPerMiss = 0.08f;
    [SerializeField, Range(0f, 0.25f)] private float rareCaptureBonusPerMiss = 0.05f;
    [SerializeField, Range(0f, 1f)] private float maxRareCaptureChance = 0.88f;

    [Header("Configuração dos bichinhos")]
    [SerializeField] private List<PrizeStockEntry> entries = new List<PrizeStockEntry>();

    public event Action OnRefillRequested;
    public int TargetBoardCount => targetBoardCount;
    public int ActiveRefillBatch => activeRefillBatch;
    public int RarePityMisses => rarePityMisses;
    public float RareCaptureChance
    {
        get
        {
            PrizeStockEntry rare = FindEntry(PrizeRarity.Rare);
            float baseChance = rare != null ? rare.baseCaptureChance : 0.30f;
            return Mathf.Clamp(baseChance + rarePityMisses * rareCaptureBonusPerMiss, 0.05f, maxRareCaptureChance);
        }
    }

    private int rarePityMisses;
    private float nextActiveRefillAt;
    private bool initialized;
    private Transform stockRoot;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        EnsureDefaultEntries();
    }

    public void Initialize(Transform root, int desiredBoardCount)
    {
        if (initialized) return;
        initialized = true;
        stockRoot = root;
        targetBoardCount = Mathf.Max(1, desiredBoardCount);
        EnsureDefaultEntries();
        LoadPersistentState();
        ApplyOfflineRefill();
        EnsureMinimumBoardReserve();
        nextActiveRefillAt = Time.unscaledTime + activeRefillIntervalSeconds;

        if (GameSession.Instance != null)
        {
            GameSession.Instance.OnPrizeDelivered.AddListener(HandlePrizeDelivered);
        }

        Debug.Log($"[PrizeStock] Estoque inicializado: {DescribeStock()} | pity raro: {rarePityMisses}");
    }

    private void Update()
    {
        if (!initialized || stockRoot == null) return;
        if (GameSession.Instance != null && GameSession.Instance.CurrentState != GameState.Playing) return;
        if (Time.unscaledTime < nextActiveRefillAt) return;
        if (ActiveCount >= targetBoardCount) return;

        ApplyRefill(commonRefillPerOfflineCycle / 2, uncommonRefillPerOfflineCycle / 2, rareRefillPerOfflineCycle);
        nextActiveRefillAt = Time.unscaledTime + activeRefillIntervalSeconds;
        OnRefillRequested?.Invoke();
    }

    public int ActiveCount
    {
        get
        {
            int total = 0;
            for (int i = 0; i < entries.Count; i++) total += entries[i].active;
            return total;
        }
    }

    public PrizeStockEntry TakeNextDefinition(bool initialBuild)
    {
        EnsureDefaultEntries();
        PrizeStockEntry selected = initialBuild ? TakeInitialDefinition() : TakeWeightedDefinition();
        if (selected == null) selected = TakeAnyAvailableDefinition();
        // Fallback defensivo: se PlayerPrefs deixou todas as reservas zeradas,
        // ainda construímos o tabuleiro usando um prefab conhecido, sem travar a cena.
        if (selected == null) selected = TakeAnyPrefabDefinition();
        if (selected == null) return null;

        selected.available = Mathf.Max(0, selected.available - 1);
        selected.active++;
        SaveEntryState(selected);
        return selected;
    }

    public PrizeStockEntry ReserveDirect(string resourceName, PrizeRarity rarity)
    {
        EnsureDefaultEntries();
        PrizeStockEntry entry = FindEntry(resourceName);
        if (entry == null) entry = FindEntry(rarity);
        if (entry == null) return null;
        entry.available = Mathf.Max(0, entry.available - 1);
        entry.active++;
        SaveEntryState(entry);
        return entry;
    }

    public void RegisterSpawned(Prize prize, PrizeStockEntry entry)
    {
        if (prize == null || entry == null) return;
        string variantId = DetermineVariantForSpawn(entry.resourceName);
        PrizeRarity rarity = GetVariantRarity(variantId, entry.rarity);
        prize.ConfigureFromStock(variantId, rarity, entry.baseCaptureChance);
    }

    public string DetermineVariantForSpawn(string baseName)
    {
        var theme = CabinetThemeManager.Instance != null ? CabinetThemeManager.Instance.CurrentTheme : null;
        float r = UnityEngine.Random.value;
        string b = baseName.ToLowerInvariant();

        if (b.Contains("fox"))
        {
            if (theme != null && theme.themeType == CabinetThemeType.KawaiiPastel && r < 0.60f) return "Fox_Arctic";
            if (theme != null && theme.themeType == CabinetThemeType.GoldCasino && r < 0.40f) return "Fox_Shadow";
            if (r < 0.30f) return "Fox_Arctic";
            if (r < 0.45f) return "Fox_Shadow";
            return "Fox";
        }
        else if (b.Contains("bear") || b.Contains("green"))
        {
            if (theme != null && theme.themeType == CabinetThemeType.KawaiiPastel)
            {
                if (r < 0.45f) return "Bear_Panda";
                if (r < 0.80f) return "Bear_Polar";
            }
            if (theme != null && theme.themeType == CabinetThemeType.GoldCasino && r < 0.30f) return "Bear_Galaxy";
            if (r < 0.25f) return "Bear_Panda";
            if (r < 0.50f) return "Bear_Polar";
            if (r < 0.65f) return "Bear_Galaxy";
            return "GreenBear";
        }
        else if (b.Contains("fish") || b.Contains("balloon"))
        {
            if (theme != null && theme.themeType == CabinetThemeType.GoldCasino && r < 0.40f) return "Fish_Gold";
            if (r < 0.35f) return "Fish_Clown";
            if (r < 0.50f) return "Fish_Gold";
            return "BalloonFish";
        }
        else if (b.Contains("koala"))
        {
            if (theme != null && theme.themeType == CabinetThemeType.GoldCasino && r < 0.35f) return "Koala_King";
            if (r < 0.35f) return "Koala_Eucalyptus";
            if (r < 0.50f) return "Koala_King";
            return "Koala";
        }
        else if (b.Contains("badger"))
        {
            if (r < 0.40f) return "Badger_Honey";
            return "Badger";
        }
        else if (b.Contains("porky"))
        {
            if (theme != null && theme.themeType == CabinetThemeType.KawaiiPastel && r < 0.60f) return "Porky_Classic";
            if (theme != null && theme.themeType == CabinetThemeType.GoldCasino && r < 0.40f) return "Porky_Diamond";
            if (r < 0.30f) return "Porky_Classic";
            if (r < 0.50f) return "Porky_Diamond";
            return "Porky";
        }

        return baseName;
    }

    private PrizeRarity GetVariantRarity(string variantId, PrizeRarity fallback)
    {
        var item = CollectionManager.Instance != null ? CollectionManager.Instance.GetItem(variantId) : null;
        return item != null ? item.rarity : fallback;
    }

    public void RegisterAttemptStarted()
    {
        rarePityMisses = Mathf.Min(maxRarePityMisses, rarePityMisses + 1);
        PlayerPrefs.SetInt(PityKey, rarePityMisses);
        PersistentSaveManager.MarkDirty();
    }

    public bool CanAttemptCapture(Prize prize)
    {
        if (prize == null) return false;
        PrizeStockEntry entry = FindEntry(prize.StockId);
        if (entry == null) entry = FindEntry(prize.Rarity);
        if (entry == null) return true;

        if (entry.rarity == PrizeRarity.Common) return UnityEngine.Random.value <= entry.baseCaptureChance;
        if (entry.rarity == PrizeRarity.Uncommon) return UnityEngine.Random.value <= entry.baseCaptureChance;

        float chance = entry.baseCaptureChance + rarePityMisses * rareCaptureBonusPerMiss;
        chance = Mathf.Clamp(chance, 0.05f, maxRareCaptureChance);
        bool captured = UnityEngine.Random.value <= chance;
        if (!captured)
        {
            Debug.Log($"[PrizeStock] Raro resistiu à captura. Chance atual: {chance:P0}; pity: {rarePityMisses}");
        }
        return captured;
    }

    public float GetRareSpawnWeightMultiplier()
    {
        return 1f + rarePityMisses * rareSpawnBonusPerMiss;
    }

    private void HandlePrizeDelivered(Prize prize, int total)
    {
        if (prize == null) return;
        PrizeStockEntry entry = FindEntry(prize.StockId);
        if (entry == null) entry = FindEntry(prize.Rarity);
        if (entry == null) return;

        entry.active = Mathf.Max(0, entry.active - 1);
        entry.available = Mathf.Max(0, entry.available);
        if (entry.rarity == PrizeRarity.Rare || entry.rarity == PrizeRarity.Legendary)
        {
            rarePityMisses = 0;
            PlayerPrefs.SetInt(PityKey, rarePityMisses);
        }
        SaveEntryState(entry);
        nextActiveRefillAt = Mathf.Min(nextActiveRefillAt, Time.unscaledTime + activeRefillIntervalSeconds);
        PersistentSaveManager.MarkDirty();

        Debug.Log($"[PrizeStock] Entrega {entry.rarity}: {entry.resourceName}. Ativos: {ActiveCount}/{targetBoardCount}. {DescribeStock()}");
    }

    private PrizeStockEntry TakeInitialDefinition()
    {
        List<PrizeStockEntry> initial = new List<PrizeStockEntry>();
        for (int i = 0; i < entries.Count; i++)
        {
            PrizeStockEntry entry = entries[i];
            if (entry.prefab != null && entry.initialRemaining > 0 && entry.available > 0)
            {
                for (int j = 0; j < entry.initialRemaining; j++) initial.Add(entry);
            }
        }

        if (initial.Count == 0) return null;
        PrizeStockEntry selected = initial[UnityEngine.Random.Range(0, initial.Count)];
        selected.initialRemaining = Mathf.Max(0, selected.initialRemaining - 1);
        return selected;
    }

    private PrizeStockEntry TakeWeightedDefinition()
    {
        float totalWeight = 0f;
        for (int i = 0; i < entries.Count; i++)
        {
            PrizeStockEntry entry = entries[i];
            if (entry.prefab == null || entry.available <= 0) continue;
            float multiplier = entry.rarity == PrizeRarity.Rare || entry.rarity == PrizeRarity.Legendary
                ? GetRareSpawnWeightMultiplier()
                : 1f;
            totalWeight += Mathf.Max(0.01f, entry.spawnWeight * multiplier);
        }

        if (totalWeight <= 0f) return null;
        float roll = UnityEngine.Random.value * totalWeight;
        for (int i = 0; i < entries.Count; i++)
        {
            PrizeStockEntry entry = entries[i];
            if (entry.prefab == null || entry.available <= 0) continue;
            float multiplier = entry.rarity == PrizeRarity.Rare || entry.rarity == PrizeRarity.Legendary
                ? GetRareSpawnWeightMultiplier()
                : 1f;
            roll -= Mathf.Max(0.01f, entry.spawnWeight * multiplier);
            if (roll <= 0f) return entry;
        }
        return TakeAnyAvailableDefinition();
    }

    private PrizeStockEntry TakeAnyAvailableDefinition()
    {
        for (int i = 0; i < entries.Count; i++)
        {
            if (entries[i].prefab != null && entries[i].available > 0) return entries[i];
        }
        return null;
    }

    private PrizeStockEntry TakeAnyPrefabDefinition()
    {
        for (int i = 0; i < entries.Count; i++)
        {
            if (entries[i].prefab != null) return entries[i];
        }
        return null;
    }

    private void EnsureDefaultEntries()
    {
        bool hasUsable = entries != null && entries.Exists(e => e != null && !string.IsNullOrEmpty(e.resourceName));
        if (!hasUsable)
        {
            entries = new List<PrizeStockEntry>
            {
                // 100 comuns: 40 + 35 + 25. No tabuleiro inicial: 9 + 8 + 7 = 24.
                new PrizeStockEntry { resourceName = "Fox", rarity = PrizeRarity.Common, reserveCapacity = 40, initialVisibleCount = 9, spawnWeight = 100f, baseCaptureChance = 0.94f },
                new PrizeStockEntry { resourceName = "GreenBear", rarity = PrizeRarity.Common, reserveCapacity = 35, initialVisibleCount = 8, spawnWeight = 100f, baseCaptureChance = 0.94f },
                new PrizeStockEntry { resourceName = "BalloonFish", rarity = PrizeRarity.Common, reserveCapacity = 25, initialVisibleCount = 7, spawnWeight = 100f, baseCaptureChance = 0.94f },
                // 50 incomuns: 30 + 20. No tabuleiro inicial: 5 + 4 = 9.
                new PrizeStockEntry { resourceName = "Koala", rarity = PrizeRarity.Uncommon, reserveCapacity = 30, initialVisibleCount = 5, spawnWeight = 38f, baseCaptureChance = 0.78f },
                new PrizeStockEntry { resourceName = "Badger", rarity = PrizeRarity.Uncommon, reserveCapacity = 20, initialVisibleCount = 4, spawnWeight = 38f, baseCaptureChance = 0.78f },
                // 10 raros. No tabuleiro inicial: 3.
                new PrizeStockEntry { resourceName = "Porky", rarity = PrizeRarity.Rare, reserveCapacity = 10, initialVisibleCount = 3, spawnWeight = 8f, baseCaptureChance = 0.34f }
            };
        }

        GameObject[] loadedPrefabs = Resources.LoadAll<GameObject>("Prizes");
        for (int i = 0; i < entries.Count; i++)
        {
            PrizeStockEntry entry = entries[i];
            if (entry == null) continue;
            if (entry.prefab == null && !string.IsNullOrEmpty(entry.resourceName))
                entry.prefab = Resources.Load<GameObject>("Prizes/" + entry.resourceName);
            if (entry.prefab == null && loadedPrefabs != null)
            {
                for (int j = 0; j < loadedPrefabs.Length; j++)
                {
                    if (string.Equals(loadedPrefabs[j].name, entry.resourceName, StringComparison.OrdinalIgnoreCase))
                    {
                        entry.prefab = loadedPrefabs[j];
                        break;
                    }
                }
            }
        }

        int loadedCount = 0;
        for (int i = 0; i < entries.Count; i++) if (entries[i] != null && entries[i].prefab != null) loadedCount++;
        Debug.Log($"[PrizeStock] Prefabs encontrados em Resources/Prizes: {loadedCount}/{entries.Count}.");
    }

    private void LoadPersistentState()
    {
        if (PlayerPrefs.GetInt(StockVersionKey, 0) != CurrentStockVersion)
        {
            ClearPersistentState();
            PlayerPrefs.SetInt(StockVersionKey, CurrentStockVersion);
        }

        rarePityMisses = Mathf.Clamp(PlayerPrefs.GetInt(PityKey, 0), 0, maxRarePityMisses);
        for (int i = 0; i < entries.Count; i++)
        {
            PrizeStockEntry entry = entries[i];
            if (entry == null) continue;
            entry.initialRemaining = entry.initialVisibleCount;
            int available = PlayerPrefs.HasKey(StockKeyPrefix + entry.resourceName)
                ? PlayerPrefs.GetInt(StockKeyPrefix + entry.resourceName)
                : entry.reserveCapacity;
            int activeFromPreviousSession = PlayerPrefs.GetInt(ActiveKeyPrefix + entry.resourceName, 0);
            entry.available = Mathf.Clamp(available + activeFromPreviousSession, 0, entry.reserveCapacity);
            entry.active = 0;
            PlayerPrefs.SetInt(ActiveKeyPrefix + entry.resourceName, 0);
        }
        PersistentSaveManager.MarkDirty();
    }

    private void ApplyOfflineRefill()
    {
        DateTime now = DateTime.UtcNow;
        DateTime last = now;
        string raw = PlayerPrefs.GetString(LastRefillKey, string.Empty);
        if (!string.IsNullOrEmpty(raw) && long.TryParse(raw, out long ticks))
        {
            try { last = new DateTime(ticks, DateTimeKind.Utc); } catch { last = now; }
        }

        double elapsedHours = Math.Max(0d, (now - last).TotalHours);
        int cycles = string.IsNullOrEmpty(raw) ? 0 : Mathf.Clamp((int)Math.Floor(elapsedHours / offlineRefillIntervalHours), 0, 30);
        if (cycles > 0)
        {
            ApplyRefill(commonRefillPerOfflineCycle * cycles, uncommonRefillPerOfflineCycle * cycles, rareRefillPerOfflineCycle * cycles);
            Debug.Log($"[PrizeStock] Reposição offline aplicada: {cycles} ciclo(s).");
        }
        PlayerPrefs.SetString(LastRefillKey, now.Ticks.ToString());
        PersistentSaveManager.MarkDirty();
    }

    private void EnsureMinimumBoardReserve()
    {
        int availableTotal = 0;
        for (int i = 0; i < entries.Count; i++) availableTotal += entries[i].available;
        if (availableTotal <= 0)
        {
            for (int i = 0; i < entries.Count; i++) entries[i].available = entries[i].reserveCapacity;
            SaveAllEntryStates();
            return;
        }
        int missing = Mathf.Max(0, targetBoardCount - availableTotal);
        if (missing <= 0) return;

        // O fallback repõe primeiro comuns, depois incomuns e por fim raros.
        AddToRarity(PrizeRarity.Common, missing);
        availableTotal = 0;
        for (int i = 0; i < entries.Count; i++) availableTotal += entries[i].available;
        missing = Mathf.Max(0, targetBoardCount - availableTotal);
        if (missing > 0) AddToRarity(PrizeRarity.Uncommon, missing);
        availableTotal = 0;
        for (int i = 0; i < entries.Count; i++) availableTotal += entries[i].available;
        missing = Mathf.Max(0, targetBoardCount - availableTotal);
        if (missing > 0) AddToRarity(PrizeRarity.Rare, missing);
        SaveAllEntryStates();
    }

    private void ApplyRefill(int commonAmount, int uncommonAmount, int rareAmount)
    {
        AddToRarity(PrizeRarity.Common, Mathf.Max(0, commonAmount));
        AddToRarity(PrizeRarity.Uncommon, Mathf.Max(0, uncommonAmount));
        AddToRarity(PrizeRarity.Rare, Mathf.Max(0, rareAmount));
        SaveAllEntryStates();
    }

    private void AddToRarity(PrizeRarity rarity, int amount)
    {
        if (amount <= 0) return;
        List<PrizeStockEntry> matching = new List<PrizeStockEntry>();
        for (int i = 0; i < entries.Count; i++)
        {
            if (entries[i] != null && entries[i].rarity == rarity && entries[i].prefab != null)
                matching.Add(entries[i]);
        }
        if (matching.Count == 0) return;

        for (int i = 0; i < amount; i++)
        {
            PrizeStockEntry entry = matching[i % matching.Count];
            if (entry.available < entry.reserveCapacity) entry.available++;
        }
    }

    private PrizeStockEntry FindEntry(string stockId)
    {
        if (string.IsNullOrEmpty(stockId)) return null;
        for (int i = 0; i < entries.Count; i++)
            if (string.Equals(entries[i].resourceName, stockId, StringComparison.OrdinalIgnoreCase)) return entries[i];
        return null;
    }

    private PrizeStockEntry FindEntry(PrizeRarity rarity)
    {
        for (int i = 0; i < entries.Count; i++)
            if (entries[i].rarity == rarity) return entries[i];
        return null;
    }

    private void SaveEntryState(PrizeStockEntry entry)
    {
        if (entry == null) return;
        PlayerPrefs.SetInt(StockKeyPrefix + entry.resourceName, entry.available);
        PlayerPrefs.SetInt(ActiveKeyPrefix + entry.resourceName, entry.active);
        PersistentSaveManager.MarkDirty();
    }

    private void SaveAllEntryStates()
    {
        for (int i = 0; i < entries.Count; i++) SaveEntryState(entries[i]);
    }

    private void ClearPersistentState()
    {
        EnsureDefaultEntries();
        for (int i = 0; i < entries.Count; i++)
        {
            PlayerPrefs.DeleteKey(StockKeyPrefix + entries[i].resourceName);
            PlayerPrefs.DeleteKey(ActiveKeyPrefix + entries[i].resourceName);
        }
        PlayerPrefs.DeleteKey(PityKey);
        PlayerPrefs.DeleteKey(LastRefillKey);
    }

    [ContextMenu("Reset Persistent Stock (Playtest)")]
    public void ResetPersistentStockForPlaytest()
    {
        ClearPersistentState();
        PlayerPrefs.DeleteKey(StockVersionKey);
        PersistentSaveManager.MarkDirty();
        Debug.Log("[PrizeStock] Estoque persistido resetado para playtest. Recarregue a cena para reconstruir o monte.");
    }

    private string DescribeStock()
    {
        int common = 0, uncommon = 0, rare = 0;
        for (int i = 0; i < entries.Count; i++)
        {
            if (entries[i].rarity == PrizeRarity.Common) common += entries[i].available;
            else if (entries[i].rarity == PrizeRarity.Uncommon) uncommon += entries[i].available;
            else rare += entries[i].available;
        }
        return $"reserva comum={common}, incomum={uncommon}, raro={rare}";
    }
}
