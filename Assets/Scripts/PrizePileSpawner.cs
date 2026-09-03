using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Monta e distribui as pelúcias de forma estável sobre a arena do fliperama.
/// Cada pelúcia é um wrapper físico com Rigidbody e BoxCollider bem dimensionados,
/// assentados diretamente sobre o piso elevado sem colisões explosivas ou flutuação.
/// </summary>
public sealed class PrizePileSpawner : MonoBehaviour
{
    private const float FloorY = -1.325f; // Piso do platô elevado da arena
    private const float VisualSize = 1.00f; // Escala ~2x dos modelos
    private const int TotalPrizes = 16; // 12 na base + 4 no topo

    private PrizeStockManager stockManager;
    private Transform pileRoot;
    private bool buildStarted;
    private bool pileReady;

    public Transform PileRoot => pileRoot;
    public bool IsReady => pileReady;
    public int VisibleCount => pileRoot == null ? 0 : pileRoot.GetComponentsInChildren<Prize>(true).Length;

    public void Build()
    {
        if (buildStarted) return;
        buildStarted = true;
        stockManager = PrizeStockManager.Instance;
        if (stockManager == null)
        {
            Debug.LogError("[PrizePileSpawner] PrizeStockManager não encontrado; monte não criado.");
            return;
        }

        // Limpa monte anterior se existir
        GameObject oldRoot = GameObject.Find("Monte_De_Ursos");
        if (oldRoot != null) DestroyImmediate(oldRoot);

        pileRoot = new GameObject("Monte_De_Ursos").transform;
        pileRoot.SetParent(null, false);

        CreatePileFloor();
        stockManager.Initialize(pileRoot, TotalPrizes);
        stockManager.OnRefillRequested += ReplenishVisiblePrizes;

        if (CabinetThemeManager.Instance != null)
        {
            CabinetThemeManager.Instance.OnThemeChanged.AddListener(RespawnThemePile);
        }

        StartCoroutine(BuildInitialPileRoutine());
    }

    private void OnDestroy()
    {
        if (stockManager != null) stockManager.OnRefillRequested -= ReplenishVisiblePrizes;
        if (CabinetThemeManager.Instance != null)
        {
            CabinetThemeManager.Instance.OnThemeChanged.RemoveListener(RespawnThemePile);
        }
    }

    public void RespawnThemePile(CabinetThemeData theme)
    {
        if (pileRoot != null)
        {
            for (int i = pileRoot.childCount - 1; i >= 0; i--)
            {
                Destroy(pileRoot.GetChild(i).gameObject);
            }
        }

        StopAllCoroutines();
        pileReady = false;
        StartCoroutine(BuildInitialPileRoutine());
    }

    private IEnumerator BuildInitialPileRoutine()
    {
        CabinetThemeData theme = CabinetThemeManager.Instance != null ? CabinetThemeManager.Instance.CurrentTheme : null;
        List<string> themePrizes = theme != null && theme.exclusivePrizeIds != null && theme.exclusivePrizeIds.Count > 0
            ? theme.exclusivePrizeIds
            : new List<string> { "Fox", "GreenBear", "BalloonFish", "Koala", "Badger", "Porky" };

        for (int i = 0; i < TotalPrizes; i++)
        {
            string variantId = themePrizes[i % themePrizes.Count];
            SpawnThemedPrize(variantId, i);
            yield return null; // 1 frame entre spawns para acomodação suave
        }

        yield return new WaitForSeconds(0.3f);
        pileReady = true;
        Debug.Log($"[PrizePileSpawner] Monte de pelúcias assentado para cabine '{theme?.displayName}': {VisibleCount}/{TotalPrizes} unidades.");
    }

    private void SpawnThemedPrize(string variantId, int index)
    {
        string basePrefabName = GetBasePrefabName(variantId);
        PrizeRarity rarity = GetVariantRarity(variantId);
        GameObject prefab = Resources.Load<GameObject>("Prizes/" + basePrefabName);
        if (prefab == null)
        {
            Debug.LogWarning($"[PrizePileSpawner] Prefab Prizes/{basePrefabName} não encontrado para variante {variantId}.");
            return;
        }

        PrizeStockEntry definition = stockManager.ReserveDirect(basePrefabName, rarity);
        if (definition == null)
        {
            definition = new PrizeStockEntry
            {
                resourceName = basePrefabName,
                rarity = rarity,
                baseCaptureChance = rarity == PrizeRarity.Legendary ? 0.28f : rarity == PrizeRarity.Rare ? 0.38f : rarity == PrizeRarity.Uncommon ? 0.72f : 0.94f
            };
        }

        try
        {
            SpawnPrize(prefab, definition, variantId, rarity, index);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[PrizePileSpawner] Falha ao spawnar {variantId} no slot {index}: {ex.Message}");
        }
    }

    private string GetBasePrefabName(string variantId)
    {
        string lower = variantId.ToLowerInvariant();
        if (lower.Contains("fox")) return "Fox";
        if (lower.Contains("bear")) return "GreenBear";
        if (lower.Contains("fish")) return "BalloonFish";
        if (lower.Contains("koala")) return "Koala";
        if (lower.Contains("badger")) return "Badger";
        if (lower.Contains("porky")) return "Porky";
        return "Fox";
    }

    private PrizeRarity GetVariantRarity(string variantId)
    {
        var item = CollectionManager.Instance != null ? CollectionManager.Instance.GetItem(variantId) : null;
        if (item != null) return item.rarity;
        string lower = variantId.ToLowerInvariant();
        if (lower.Contains("galaxy") || lower.Contains("king") || lower.Contains("diamond")) return PrizeRarity.Legendary;
        if (lower.Contains("shadow") || lower.Contains("gold") || lower.Contains("honey") || lower.Contains("rare")) return PrizeRarity.Rare;
        if (lower.Contains("arctic") || lower.Contains("polar") || lower.Contains("panda") || lower.Contains("eucalyptus")) return PrizeRarity.Uncommon;
        return PrizeRarity.Common;
    }

    private void ReplenishVisiblePrizes()
    {
        if (!pileReady || stockManager == null) return;
        CabinetThemeData theme = CabinetThemeManager.Instance != null ? CabinetThemeManager.Instance.CurrentTheme : null;
        List<string> themePrizes = theme != null && theme.exclusivePrizeIds != null && theme.exclusivePrizeIds.Count > 0
            ? theme.exclusivePrizeIds
            : new List<string> { "Fox", "GreenBear", "BalloonFish", "Koala", "Badger", "Porky" };

        int missing = Mathf.Max(0, stockManager.TargetBoardCount - stockManager.ActiveCount);
        int batch = Mathf.Min(2, missing);
        for (int i = 0; i < batch; i++)
        {
            string variantId = themePrizes[UnityEngine.Random.Range(0, themePrizes.Count)];
            SpawnThemedPrize(variantId, VisibleCount + i);
        }
    }

    /// <summary>
    /// Distribui as 16 pelúcias em grade organizada e orgânica sem sobreposição.
    /// Camada 1: 12 unidades cobrindo o platô (-0.35m a +1.05m em X, -0.45m a +1.05m em Z).
    /// Camada 2: 4 unidades no centro, aninhadas entre as da base.
    /// </summary>
    private Vector3 CalculatePosition(int index)
    {
        if (index < 12)
        {
            // Grade 3 colunas x 4 fileiras na base
            int col = index % 3; // 0, 1, 2
            int row = index / 3; // 0, 1, 2, 3

            float x = Mathf.Lerp(-0.35f, 1.05f, col / 2.0f) + Mathf.Sin(index * 1.7f) * 0.04f;
            float z = Mathf.Lerp(-0.45f, 1.05f, row / 3.0f) + Mathf.Cos(index * 2.1f) * 0.04f;
            float y = FloorY + 0.02f; // Assentado rente ao piso

            return new Vector3(x, y, z);
        }
        else
        {
            // 4 no topo (relevo da pilha)
            int sub = index - 12;
            int col = sub % 2;
            int row = sub / 2;

            float x = Mathf.Lerp(0.0f, 0.70f, col / 1.0f) + Mathf.Sin(index * 3.1f) * 0.03f;
            float z = Mathf.Lerp(-0.10f, 0.70f, row / 1.0f) + Mathf.Cos(index * 2.7f) * 0.03f;
            float y = FloorY + 0.52f; // Descansando suavemente sobre a camada inferior

            return new Vector3(x, y, z);
        }
    }

    private Quaternion CalculateRotation(int index)
    {
        float yaw = Mathf.Repeat(index * 137.5f, 360f);
        float pitch = Mathf.Lerp(-12f, 12f, Mathf.Repeat(index * 0.381f, 1f));
        float roll = Mathf.Lerp(-10f, 10f, Mathf.Repeat(index * 0.517f, 1f));
        return Quaternion.Euler(pitch, yaw, roll);
    }

    private void SpawnPrize(GameObject prefab, PrizeStockEntry definition, string variantId, PrizeRarity rarity, int index)
    {
        if (pileRoot == null || prefab == null || definition == null) return;

        // 1. Wrapper físico (onde operam Rigidbody e Collider)
        GameObject wrapper = new GameObject($"Pelucia_{variantId}_{rarity}_{index}");
        wrapper.transform.SetParent(pileRoot, false);
        wrapper.transform.SetPositionAndRotation(CalculatePosition(index), CalculateRotation(index));

        int prizeLayerIdx = LayerMask.NameToLayer("Prize");
        if (prizeLayerIdx != -1) wrapper.layer = prizeLayerIdx;

        // 2. Instancia o modelo visual 3D como filho limpo
        GameObject visualRoot = Instantiate(prefab, wrapper.transform, false);
        visualRoot.name = "Visual";
        visualRoot.transform.localPosition = Vector3.zero;
        visualRoot.transform.localRotation = Quaternion.identity;

        // Destrói IMEDIATAMENTE qualquer física interna ou scripts do pacote FBX
        CleanVisualHierarchy(visualRoot);

        // 3. Normaliza a escala visual para ~1.0m
        ScaleVisualUniformly(wrapper.transform, visualRoot.transform);

        // 4. Configura Rigidbody no wrapper
        Rigidbody body = wrapper.GetComponent<Rigidbody>();
        if (body == null) body = wrapper.AddComponent<Rigidbody>();
        body.mass = rarity == PrizeRarity.Legendary ? 1.55f : rarity == PrizeRarity.Rare ? 1.40f : rarity == PrizeRarity.Uncommon ? 1.15f : 0.95f;
        body.linearDamping = 1.25f;
        body.angularDamping = 0.65f;
        body.collisionDetectionMode = CollisionDetectionMode.Continuous;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.maxAngularVelocity = 8.0f;
        body.centerOfMass = new Vector3(0f, -0.06f, 0f);
        body.isKinematic = false;
        body.useGravity = true;

        // 5. Adiciona BoxCollider proporcional e sem folga no wrapper
        BoxCollider box = wrapper.AddComponent<BoxCollider>();
        box.size = new Vector3(0.56f, 0.72f, 0.56f);
        box.center = new Vector3(0f, 0.36f, 0f);
        box.sharedMaterial = Prize.GetPlushiePhysicsMaterial();

        // 6. Conecta o componente Prize de gameplay
        Prize prize = wrapper.AddComponent<Prize>();
        float chance = rarity == PrizeRarity.Legendary ? 0.28f : rarity == PrizeRarity.Rare ? 0.38f : rarity == PrizeRarity.Uncommon ? 0.72f : 0.94f;
        prize.ConfigureFromStock(variantId, rarity, chance);
        PrizeVariantApplier.ApplyVariantStyle(wrapper, variantId, rarity);
    }

    private void CleanVisualHierarchy(GameObject visualRoot)
    {
        foreach (Animator a in visualRoot.GetComponentsInChildren<Animator>(true)) a.enabled = false;
        foreach (Prize p in visualRoot.GetComponentsInChildren<Prize>(true)) DestroyImmediate(p);
        foreach (Collider c in visualRoot.GetComponentsInChildren<Collider>(true)) DestroyImmediate(c);
        foreach (Rigidbody r in visualRoot.GetComponentsInChildren<Rigidbody>(true)) DestroyImmediate(r);
    }

    private void ScaleVisualUniformly(Transform wrapper, Transform visual)
    {
        Renderer[] renderers = visual.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) return;

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);

        float maxDim = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
        if (maxDim > 0.001f)
        {
            visual.localScale *= VisualSize / maxDim;
        }

        // Alinha os pés da pelúcia exatamente na base do wrapper
        Physics.SyncTransforms();
        renderers = visual.GetComponentsInChildren<Renderer>(true);
        bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);

        float deltaY = wrapper.position.y - bounds.min.y;
        visual.position += Vector3.up * deltaY;
    }

    private void CreatePileFloor()
    {
        GameObject floor = new GameObject("Monte_Base_Fisica");
        floor.transform.SetParent(pileRoot, false);
        floor.transform.position = new Vector3(0.35f, FloorY - 0.06f, 0.35f);
        BoxCollider collider = floor.AddComponent<BoxCollider>();
        collider.size = new Vector3(3.6f, 0.12f, 3.6f);
        collider.sharedMaterial = Prize.GetPlushiePhysicsMaterial();
    }
}
