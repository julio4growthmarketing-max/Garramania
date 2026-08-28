using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Monta e estabiliza o volume de prêmios da máquina.
/// O wrapper é o objeto físico e o rig Blender é apenas o filho visual.
/// </summary>
public sealed class PrizePileSpawner : MonoBehaviour
{
    [Header("Volume da máquina")]
    [SerializeField, Min(24)] private int initialBoardCount = 72;
    [SerializeField, Min(1)] private int activeRefillBatch = 3;
    [SerializeField, Min(0.01f)] private float dropInterval = 0.035f;
    private const float FloorY = -1.325f;
    private const float VisualSize = 0.58f;
    private const float ColliderWidth = 0.50f;
    private const float ColliderHeight = 0.54f;
    private const float AreaMinX = -1.25f;
    private const float AreaMaxX = 1.45f;
    private const float AreaMinZ = -1.10f;
    private const float AreaMaxZ = 1.50f;

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

        pileRoot = new GameObject("Monte_De_Ursos").transform;
        pileRoot.SetParent(null, false);
        CreatePileFloor();
        stockManager.Initialize(pileRoot, initialBoardCount);
        stockManager.OnRefillRequested += ReplenishVisiblePrizes;
        StartCoroutine(BuildInitialPileRoutine());
    }

    private void OnDestroy()
    {
        if (stockManager != null) stockManager.OnRefillRequested -= ReplenishVisiblePrizes;
    }

    private IEnumerator BuildInitialPileRoutine()
    {
        string[] common = { "Fox", "GreenBear", "BalloonFish" };
        string[] uncommon = { "Koala", "Badger" };
        int index = 0;

        // A mistura de raridades acontece durante o abastecimento, não em fileiras.
        for (int i = 0; i < 48; i++, index++)
        {
            SpawnInitialPrize(common[i % common.Length], PrizeRarity.Common, index);
            if (i % 3 == 2) yield return new WaitForSeconds(dropInterval);
        }
        for (int i = 0; i < 18; i++, index++)
        {
            SpawnInitialPrize(uncommon[i % uncommon.Length], PrizeRarity.Uncommon, index);
            if (i % 3 == 2) yield return new WaitForSeconds(dropInterval);
        }
        for (int i = 0; i < 6; i++, index++)
        {
            SpawnInitialPrize("Porky", PrizeRarity.Rare, index);
            yield return new WaitForSeconds(dropInterval);
        }

        yield return StartCoroutine(WaitForPileToSettle());
        StabilizePile();
        pileReady = true;
        Debug.Log($"[PrizePileSpawner] Monte pronto: {VisibleCount}/{initialBoardCount} prêmios, com queda e acomodação concluídas.");
    }

    private void SpawnInitialPrize(string resourceName, PrizeRarity rarity, int index)
    {
        PrizeStockEntry definition = stockManager.ReserveDirect(resourceName, rarity);
        GameObject prefab = Resources.Load<GameObject>("Prizes/" + resourceName);
        if (definition == null || prefab == null)
        {
            Debug.LogWarning($"[PrizePileSpawner] Sem definição/prefab para {resourceName}; slot {index} ignorado.");
            return;
        }

        try
        {
            SpawnPrize(prefab, definition, index, true);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[PrizePileSpawner] Falha isolada em {resourceName} no slot {index}: {ex.Message}");
            SpawnFallback(resourceName, rarity, definition.baseCaptureChance, index);
        }
    }

    private void ReplenishVisiblePrizes()
    {
        if (!pileReady || stockManager == null) return;
        int missing = Mathf.Max(0, stockManager.TargetBoardCount - stockManager.ActiveCount);
        int batch = Mathf.Min(activeRefillBatch, missing);
        for (int i = 0; i < batch; i++)
        {
            PrizeStockEntry definition = stockManager.TakeNextDefinition(false);
            if (definition == null || definition.prefab == null) continue;
            SpawnPrize(definition.prefab, definition, VisibleCount + i, true);
        }
        if (batch > 0) Debug.Log($"[PrizePileSpawner] Reposição: +{batch}; ativos {stockManager.ActiveCount}/{stockManager.TargetBoardCount}.");
    }

    private Vector3 DropPosition(int index)
    {
        float u = Mathf.Repeat((index + 1) * 0.6180339887f, 1f);
        float v = Mathf.Repeat((index + 1) * 0.7548776662f, 1f);
        float x = Mathf.Lerp(AreaMinX, AreaMaxX, u);
        float z = Mathf.Lerp(AreaMinZ, AreaMaxZ, v);
        float y = FloorY + 1.0f + (index % 8) * 0.15f;
        return new Vector3(x, y, z);
    }

    private Quaternion PrizeRotation(int index)
    {
        float yaw = Mathf.Repeat(index * 137.50776f, 360f);
        float pitch = Mathf.Lerp(-18f, 18f, Mathf.Repeat(index * 0.381966f, 1f));
        float roll = Mathf.Lerp(-16f, 16f, Mathf.Repeat(index * 0.517638f, 1f));
        return Quaternion.Euler(pitch, yaw, roll);
    }

    private void SpawnPrize(GameObject prefab, PrizeStockEntry definition, int index, bool dropIn)
    {
        if (pileRoot == null || prefab == null || definition == null) return;
        GameObject wrapper = new GameObject($"Pelucia_{definition.resourceName}_{definition.rarity}_{index}");
        wrapper.transform.SetParent(pileRoot, false);
        wrapper.transform.SetPositionAndRotation(DropPosition(index), PrizeRotation(index));

        GameObject visualRoot = Instantiate(prefab, wrapper.transform, false);
        visualRoot.name = "Visual";
        DisableInternalPhysics(visualRoot);
        NormalizeAndSeatVisual(wrapper.transform, visualRoot.transform);

        Prize prize = wrapper.AddComponent<Prize>();
        prize.ConfigureFromStock(definition.resourceName, definition.rarity, definition.baseCaptureChance);
        Rigidbody body = prize.Body;
        body.mass = definition.rarity == PrizeRarity.Rare ? 1.65f : definition.rarity == PrizeRarity.Uncommon ? 1.45f : 1.25f;
        body.linearDamping = 2f;
        body.angularDamping = 2.2f;
        body.collisionDetectionMode = CollisionDetectionMode.Continuous;

        BoxCollider collider = wrapper.AddComponent<BoxCollider>();
        collider.center = new Vector3(0f, ColliderHeight * 0.5f, 0f);
        collider.size = new Vector3(ColliderWidth, ColliderHeight, ColliderWidth);

        body.linearVelocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;
        body.isKinematic = !dropIn;
        body.useGravity = dropIn;
        if (dropIn) body.WakeUp();
        stockManager.RegisterSpawned(prize, definition);
    }

    private void SpawnFallback(string resourceName, PrizeRarity rarity, float captureChance, int index)
    {
        GameObject fallback = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        fallback.name = $"PeluciaFallback_{resourceName}_{rarity}_{index}";
        fallback.transform.SetParent(pileRoot, false);
        fallback.transform.SetPositionAndRotation(DropPosition(index), PrizeRotation(index));
        fallback.transform.localScale = Vector3.one * 0.50f;
        Renderer renderer = fallback.GetComponent<Renderer>();
        if (renderer != null) renderer.material.color = rarity == PrizeRarity.Rare ? new Color(1f, 0.72f, 0.05f) : rarity == PrizeRarity.Uncommon ? new Color(0.45f, 0.9f, 1f) : new Color(1f, 0.35f, 0.55f);
        Prize prize = fallback.AddComponent<Prize>();
        prize.ConfigureFromStock(resourceName, rarity, captureChance);
        prize.Body.isKinematic = false;
        prize.Body.useGravity = true;
        // A definição já foi reservada antes da tentativa do rig; não reservar novamente.
    }

    private void DisableInternalPhysics(GameObject visualRoot)
    {
        foreach (Animator animator in visualRoot.GetComponentsInChildren<Animator>(true)) animator.enabled = false;
        foreach (Prize nestedPrize in visualRoot.GetComponentsInChildren<Prize>(true)) Destroy(nestedPrize);
        foreach (Collider collider in visualRoot.GetComponentsInChildren<Collider>(true))
        {
            collider.enabled = false;
            Destroy(collider);
        }
        foreach (Rigidbody rigidbody in visualRoot.GetComponentsInChildren<Rigidbody>(true))
        {
            rigidbody.isKinematic = true;
            rigidbody.useGravity = false;
            rigidbody.detectCollisions = false;
            Destroy(rigidbody);
        }
    }

    private void NormalizeAndSeatVisual(Transform wrapper, Transform visual)
    {
        Renderer[] renderers = visual.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
        {
            visual.localScale = Vector3.one * 0.35f;
            return;
        }

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
        float maxDimension = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
        if (maxDimension > 0.0001f) visual.localScale *= VisualSize / maxDimension;

        Physics.SyncTransforms();
        renderers = visual.GetComponentsInChildren<Renderer>(true);
        bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
        float verticalOffset = wrapper.position.y - bounds.min.y;
        visual.localPosition += wrapper.InverseTransformVector(Vector3.up * verticalOffset);
    }

    private IEnumerator WaitForPileToSettle()
    {
        float elapsed = 0f;
        float stableFor = 0f;
        const float maxWait = 8f;
        const float requiredStableTime = 0.45f;

        while (elapsed < maxWait)
        {
            bool stable = true;
            Rigidbody[] bodies = pileRoot == null ? Array.Empty<Rigidbody>() : pileRoot.GetComponentsInChildren<Rigidbody>(true);
            for (int i = 0; i < bodies.Length; i++)
            {
                Rigidbody body = bodies[i];
                if (body == null || body.isKinematic) continue;
                if (body.linearVelocity.sqrMagnitude > 0.0025f || body.angularVelocity.sqrMagnitude > 0.02f)
                {
                    stable = false;
                    break;
                }
            }

            stableFor = stable ? stableFor + Time.deltaTime : 0f;
            if (stableFor >= requiredStableTime) break;
            elapsed += Time.deltaTime;
            yield return null;
        }
    }

    private void StabilizePile()
    {
        if (pileRoot == null) return;
        Physics.SyncTransforms();
        Prize[] prizes = pileRoot.GetComponentsInChildren<Prize>(true);
        for (int i = 0; i < prizes.Length; i++)
        {
            Prize prize = prizes[i];
            if (prize == null || prize.State != PrizeState.InPile || prize.Body == null) continue;
            Rigidbody body = prize.Body;
            // Preserva a posição encontrada pela física. Só corrige uma eventual
            // travessia do piso invisível, sem formar uma grade artificial.
            if (body.position.y < FloorY)
            {
                Vector3 safePosition = body.position;
                safePosition.y = FloorY;
                body.position = safePosition;
            }
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.isKinematic = true;
            body.useGravity = false;
            body.Sleep();
        }

        Physics.SyncTransforms();
        for (int i = 0; i < prizes.Length; i++)
        {
            Prize prize = prizes[i];
            if (prize == null || prize.State != PrizeState.InPile) continue;
            Transform visual = prize.transform.Find("Visual");
            if (visual == null) continue;
            Renderer[] renderers = visual.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) continue;
            Bounds bounds = renderers[0].bounds;
            for (int r = 1; r < renderers.Length; r++) bounds.Encapsulate(renderers[r].bounds);
            visual.position += Vector3.up * (prize.Body.position.y - bounds.min.y);
        }
    }

    private void CreatePileFloor()
    {
        GameObject floor = new GameObject("Monte_Base_Fisica");
        floor.transform.SetParent(pileRoot, false);
        floor.transform.position = new Vector3(0.2f, FloorY - 0.06f, 0.2f);
        BoxCollider collider = floor.AddComponent<BoxCollider>();
        collider.size = new Vector3(3.35f, 0.12f, 3.35f);
    }
}
