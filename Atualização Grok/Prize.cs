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
        if (Body == null) Body = GetComponentInChildren<Rigidbody>();
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

        // Ajusta dificuldade física por raridade
        switch (rarity)
        {
            case PrizeRarity.Common:
                gripRequired = 0.42f;
                massFeel = 1.0f;
                slipperiness = 0.18f;
                break;
            case PrizeRarity.Uncommon:
                gripRequired = 0.58f;
                massFeel = 1.25f;
                slipperiness = 0.32f;
                break;
            case PrizeRarity.Rare:
                gripRequired = 0.78f;
                massFeel = 1.55f;
                slipperiness = 0.48f;
                break;
            case PrizeRarity.Legendary:
                gripRequired = 0.92f;
                massFeel = 1.9f;
                slipperiness = 0.62f;
                break;
        }

        if (Body != null) Body.mass = massFeel;
    }

    public void Attach(Transform anchor, float quality, float gripForce)
    {
        RefreshPhysicsReferences();
        State = PrizeState.Attached;
        CaptureQuality = Mathf.Clamp01(quality);
        CurrentGripHold = gripForce;

        if (Body != null)
        {
            Body.isKinematic = true;
            Body.useGravity = false;
            Body.linearVelocity = Vector3.zero;
            Body.angularVelocity = Vector3.zero;
        }

        transform.SetParent(anchor, true);
        // Posição centralizada entre os dentes
        transform.localPosition = new Vector3(0f, -0.42f, 0f);
        transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
    }

    /// <summary>
    /// Compatibilidade com código antigo que chama Attach(Transform) sem qualidade/grip.
    /// </summary>
    public void Attach(Transform anchor)
    {
        Attach(anchor, 0.75f, 0.8f);
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
        transform.SetParent(null);
    }

    public void MarkDelivered()
    {
        RefreshPhysicsReferences();
        State = PrizeState.Delivered;
        if (Body != null)
        {
            Body.isKinematic = false;
            Body.useGravity = true;
            Body.WakeUp();
        }
        transform.SetParent(null);
    }

    /// <summary>
    /// Retorna true se o grip atual ainda é suficiente para segurar.
    /// </summary>
    public bool IsGripSufficient(float currentGrip, float swayPenalty, float movementPenalty)
    {
        float effectiveRequired = gripRequired * (1f + slipperiness * 0.6f);
        effectiveRequired *= (1f + (1f - CaptureQuality) * 0.45f); // captura ruim exige mais força
        effectiveRequired *= (1f + swayPenalty * 0.35f);
        effectiveRequired *= (1f + movementPenalty * 0.25f);

        return currentGrip >= effectiveRequired;
    }
}
