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
    public float gripRequired = 0.55f;
    public float massFeel = 1.2f;
    [Range(0f, 1f)] public float slipperiness = 0.25f;

    public Rigidbody Body { get; private set; }
    public PrizeState State { get; private set; } = PrizeState.InPile;

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

        switch (rarity)
        {
            case PrizeRarity.Common:
                gripRequired = 0.32f;
                massFeel = 0.9f;
                slipperiness = 0.12f;
                break;
            case PrizeRarity.Uncommon:
                gripRequired = 0.42f;
                massFeel = 1.1f;
                slipperiness = 0.18f;
                break;
            case PrizeRarity.Rare:
                gripRequired = 0.55f;
                massFeel = 1.35f;
                slipperiness = 0.28f;
                break;
            case PrizeRarity.Legendary:
                gripRequired = 0.70f;
                massFeel = 1.7f;
                slipperiness = 0.38f;
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
            Body.linearVelocity = Vector3.zero;
            Body.angularVelocity = Vector3.zero;
            Body.isKinematic = true;
            Body.useGravity = false;
        }

        transform.SetParent(anchor, true);
        transform.localPosition = new Vector3(0f, -0.42f, 0f);
        transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
    }

    public void Attach(Transform anchor)
    {
        Attach(anchor, 1.0f, 1.0f);
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
        transform.SetParent(null);
    }

    public bool IsGripSufficient(float currentGrip, float swayPenalty, float movementPenalty)
    {
        float effectiveRequired = gripRequired * (1f + slipperiness * 0.30f);
        effectiveRequired *= Mathf.Lerp(1.20f, 0.85f, CaptureQuality);
        effectiveRequired += (swayPenalty * 0.12f) + (movementPenalty * 0.08f);

        return currentGrip >= effectiveRequired;
    }
}