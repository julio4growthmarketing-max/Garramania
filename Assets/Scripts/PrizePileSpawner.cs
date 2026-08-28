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
    [SerializeField, Min(20)] private int initialBoardCount = 28;
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
    private PhysicsMaterial prizeMaterial;
    private PhysicsMaterial floorMaterial;

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

        // 18 comuns distribuídos no piso
        for (int i = 0; i < 18; i++, index++)
        {
            SpawnInitialPrize(common[i % common.Length], PrizeRarity.Common, index);
            yield return null;
        }
        // 8 incomuns
        for (int i = 0; i < 8; i++, index++)
        {
            SpawnInitialPrize(uncommon[i % uncommon.Length], PrizeRarity.Uncommon, index);
            yield return null;
        }
        // 2 raros no topo central
        for (int i = 0; i < 2; i++, index++)
        {
            SpawnInitialPrize("Porky", PrizeRarity.Rare, index);
            yield return null;
        }

        yield return new WaitForSeconds(0.4f);
        StabilizePile();
        pileReady = true;
        Debug.Log($"[PrizePileSpawner] Monte pronto: {VisibleCount}/{initialBoardCount} prêmios posicionados no solo.");
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
        // 20 prêmios na camada inferior (base no solo da arena)
        if (index < 20)
        {
            int col = index % 5;
            int row = index / 5;
            float x = Mathf.Lerp(-0.55f, 1.25f, col / 4f) + Mathf.Lerp(-0.06f, 0.06f, Mathf.Repeat(index * 0.381f, 1f));
            float z = Mathf.Lerp(-0.55f, 1.25f, row / 3f) + Mathf.Lerp(-0.06f, 0.06f, Mathf.Repeat(index * 0.517f, 1f));
            float y = FloorY + 0.18f;
            return new Vector3(x, y, z);
        }
        else
        {
            // 8 prêmios na camada superior (monte central leve, altura baixa)
            int sub = index - 20;
            int col = sub % 3;
            int row = sub / 3;
            float x = Mathf.Lerp(-0.15f, 0.85f, col / 2f) + Mathf.Lerp(-0.05f, 0.05f, Mathf.Repeat(index * 0.23f, 1f));
            float z = Mathf.Lerp(-0.15f, 0.85f, row / 2f) + Mathf.Lerp(-0.05f, 0.05f, Mathf.Repeat(index * 0.47f, 1f));
            float y = FloorY + 0.42f;
            return new Vector3(x, y, z);
        }
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
        body.linearDamping = 0.6f;
        body.angularDamping = 0.8f;
        body.collisionDetectionMode = CollisionDetectionMode.Continuous;

        BoxCollider collider = CreateGameplayCollider(wrapper.transform, visualRoot.transform);
        collider.material = GetPrizeMaterial();
        body.centerOfMass = collider.center;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.maxAngularVelocity = 18f;

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

    private BoxCollider CreateGameplayCollider(Transform wrapper, Transform visual)
    {
        Bounds localBounds = CalculateLocalVisualBounds(wrapper, visual);
        float width = Mathf.Clamp(localBounds.size.x, 0.36f, 0.72f);
        float depth = Mathf.Clamp(localBounds.size.z, 0.36f, 0.72f);
        float height = Mathf.Clamp(localBounds.size.y, 0.40f, 0.76f);
        BoxCollider collider = wrapper.gameObject.AddComponent<BoxCollider>();
        collider.center = new Vector3(localBounds.center.x, Mathf.Max(0.20f, localBounds.center.y), localBounds.center.z);
        collider.size = new Vector3(width, height, depth);
        return collider;
    }

    private Bounds CalculateLocalVisualBounds(Transform wrapper, Transform visual)
    {
        Renderer[] renderers = visual.GetComponentsInChildren<Renderer>(true);
        Bounds result = new Bounds(Vector3.zero, Vector3.zero);
        bool hasBounds = false;
        for (int i = 0; i < renderers.Length; i++)
        {
            Bounds world = renderers[i].bounds;
            Vector3 extents = world.extents;
            for (int x = -1; x <= 1; x += 2)
            for (int y = -1; y <= 1; y += 2)
            for (int z = -1; z <= 1; z += 2)
            {
                Vector3 corner = world.center + Vector3.Scale(extents, new Vector3(x, y, z));
                Vector3 local = wrapper.InverseTransformPoint(corner);
                if (!hasBounds) { result = new Bounds(local, Vector3.zero); hasBounds = true; }
                else result.Encapsulate(local);
            }
        }
        return result;
    }

    private PhysicsMaterial GetPrizeMaterial()
    {
        if (prizeMaterial != null) return prizeMaterial;
        prizeMaterial = new PhysicsMaterial("Pelucia_Atrito")
        {
            staticFriction = 0.92f,
            dynamicFriction = 0.78f,
            bounciness = 0.02f,
            frictionCombine = PhysicsMaterialCombine.Multiply,
            bounceCombine = PhysicsMaterialCombine.Minimum
        };
        return prizeMaterial;
    }

    private PhysicsMaterial GetFloorMaterial()
    {
        if (floorMaterial != null) return floorMaterial;
        floorMaterial = new PhysicsMaterial("Base_Monte_Atrito")
        {
            staticFriction = 1.0f,
            dynamicFriction = 0.88f,
            bounciness = 0f,
            frictionCombine = PhysicsMaterialCombine.Multiply,
            bounceCombine = PhysicsMaterialCombine.Minimum
        };
        return floorMaterial;
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
            if (body.position.y < FloorY + 0.12f)
            {
                Vector3 safePosition = body.position;
                safePosition.y = FloorY + 0.18f;
                body.position = safePosition;
            }
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.isKinematic = false;
            body.useGravity = true;
            body.WakeUp();
        }
        Physics.SyncTransforms();
    }

    private void SeatVisualOnCollider(Transform wrapper, Transform visual)
    {
        Renderer[] renderers = visual.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) return;
        Bounds visualBounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) visualBounds.Encapsulate(renderers[i].bounds);

        Collider support = wrapper.GetComponent<Collider>();
        if (support == null) return;
        float correction = support.bounds.min.y - visualBounds.min.y;
        if (Mathf.Abs(correction) > 0.0005f) visual.position += Vector3.up * correction;
    }

    private void CreatePileFloor()
    {
        GameObject floor = new GameObject("Monte_Base_Fisica");
        floor.transform.SetParent(pileRoot, false);
        floor.transform.position = new Vector3(0.35f, FloorY - 0.06f, 0.35f);
        BoxCollider collider = floor.AddComponent<BoxCollider>();
        collider.size = new Vector3(3.6f, 0.12f, 3.6f);
        collider.material = GetFloorMaterial();
    }
}
