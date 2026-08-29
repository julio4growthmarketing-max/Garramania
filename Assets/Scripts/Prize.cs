using UnityEngine;

public enum PrizeState { InPile, Attached, Slipping, Delivering, Delivered, Dropped }

[RequireComponent(typeof(Rigidbody))]
public class Prize : MonoBehaviour
{
    [Header("Identidade")]
    public string prizeId = "urso_comum";
    public string StockId { get; private set; } = "urso_comum";
    public PrizeRarity Rarity { get; private set; } = PrizeRarity.Common;
    public float BaseCaptureChance { get; private set; } = 0.94f;

    [Header("Física de Captura")]
    [Tooltip("Força mínima necessária para segurar este prêmio com estabilidade")]
    public float gripRequired = 0.55f;

    [Tooltip("Quanto o prêmio resiste a ser levantado (influencia escorregamento)")]
    public float massFeel = 1.2f;

    [Tooltip("Quão escorregadio é o material (0 = gruda fácil, 1 = muito escorregadio)")]
    [Range(0f, 1f)] public float slipperiness = 0.25f;

    public Rigidbody Body { get; private set; }
    public PrizeState State { get; private set; } = PrizeState.InPile;

    // Dados da captura atual (preenchidos pela garra)
    public float CaptureQuality { get; private set; }
    public float CurrentGripHold { get; private set; }

    void Awake()
    {
        RefreshPhysicsReferences();
        ApplyDefaultPhysics();
    }

    public void RefreshPhysicsReferences()
    {
        Body = GetComponent<Rigidbody>();
        if (Body == null) Body = gameObject.AddComponent<Rigidbody>();
    }

    void ApplyDefaultPhysics()
    {
        if (Body == null) return;
        Body.linearDamping = 0.65f;
        Body.angularDamping = 0.85f;
        Body.mass = massFeel;
        Body.interpolation = RigidbodyInterpolation.Interpolate;
        Body.collisionDetectionMode = CollisionDetectionMode.Continuous;
    }

    public void ConfigureFromStock(string stockId, PrizeRarity rarity, float captureChance)
    {
        StockId = string.IsNullOrEmpty(stockId) ? prizeId : stockId;
        prizeId = StockId.ToLowerInvariant();
        Rarity = rarity;
        BaseCaptureChance = Mathf.Clamp(captureChance, 0.05f, 1f);

        // Ajusta dificuldade física calibrada por raridade
        switch (rarity)
        {
            case PrizeRarity.Common:
                gripRequired = 0.32f;
                massFeel = 0.95f;
                slipperiness = 0.12f;
                break;
            case PrizeRarity.Uncommon:
                gripRequired = 0.45f;
                massFeel = 1.15f;
                slipperiness = 0.20f;
                break;
            case PrizeRarity.Rare:
                gripRequired = 0.62f;
                massFeel = 1.40f;
                slipperiness = 0.32f;
                break;
            case PrizeRarity.Legendary:
                gripRequired = 0.82f;
                massFeel = 1.70f;
                slipperiness = 0.45f;
                break;
        }

        if (Body != null) Body.mass = massFeel;
    }

    public void Attach(Transform anchor, float quality, float gripForce)
    {
        if (anchor == null) return;
        RefreshPhysicsReferences();
        State = PrizeState.Attached;
        CaptureQuality = Mathf.Clamp01(quality);
        CurrentGripHold = gripForce;

        if (Body != null)
        {
            Body.linearVelocity = Vector3.zero;
            Body.angularVelocity = Vector3.zero;
            Body.isKinematic = true;
            Body.useGravity = false;
        }

        transform.SetParent(anchor, false);

        // Compensa a escala do parent para NUNCA encolher o boneco (mantém escala 1.0 real no mundo)
        Vector3 pScale = anchor.lossyScale;
        if (pScale.x > 0.001f && pScale.y > 0.001f && pScale.z > 0.001f)
        {
            transform.localScale = new Vector3(1f / pScale.x, 1f / pScale.y, 1f / pScale.z);
        }
        else
        {
            transform.localScale = Vector3.one;
        }

        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
    }

    /// <summary>
    /// Compatibilidade com código antigo que chama Attach(Transform) sem qualidade/grip.
    /// </summary>
    public void Attach(Transform anchor)
    {
        Attach(anchor, 0.85f, 1.0f);
    }

    public void BeginSlip()
    {
        if (State != PrizeState.Attached) return;
        State = PrizeState.Slipping;
    }

    public void Detach()
    {
        RefreshPhysicsReferences();
        if (State != PrizeState.Delivered) State = PrizeState.Dropped;

        if (Body != null)
        {
            Body.isKinematic = false;
            Body.useGravity = true;
            Body.WakeUp();
        }
        transform.SetParent(null, true);
        transform.localScale = Vector3.one;
        StartCoroutine(CheckSettleRoutine());
    }

    private System.Collections.IEnumerator CheckSettleRoutine()
    {
        yield return new WaitForSeconds(0.5f);
        while (Body != null && Body.linearVelocity.sqrMagnitude > 0.05f)
        {
            yield return new WaitForSeconds(0.2f);
        }
        if (State == PrizeState.Dropped)
        {
            State = PrizeState.InPile;
        }
    }

    public void MarkDelivered()
    {
        RefreshPhysicsReferences();
        State = PrizeState.Delivered;
        if (Body != null)
        {
            Body.linearVelocity = Vector3.zero;
            Body.angularVelocity = Vector3.zero;
            Body.isKinematic = false;
            Body.useGravity = true;
            Body.WakeUp();
        }
        transform.SetParent(null, true);
        transform.localScale = Vector3.one;
    }

    /// <summary>
    /// Retorna true se o grip atual ainda é suficiente para segurar.
    /// </summary>
    public bool IsGripSufficient(float currentGrip, float swayPenalty, float movementPenalty)
    {
        float effectiveRequired = gripRequired * (1f + slipperiness * 0.35f);
        effectiveRequired *= Mathf.Lerp(1.20f, 0.85f, CaptureQuality);
        effectiveRequired += (swayPenalty * 0.15f) + (movementPenalty * 0.10f);

        return currentGrip >= effectiveRequired;
    }
}
