using UnityEngine;

public enum PrizeState { InPile, Attached, Delivering, Delivered, Dropped }

[RequireComponent(typeof(Rigidbody))]
public class Prize : MonoBehaviour
{
    public string prizeId = "urso_comum";
    public string StockId { get; private set; } = "urso_comum";
    public PrizeRarity Rarity { get; private set; } = PrizeRarity.Common;
    public float BaseCaptureChance { get; private set; } = 0.94f;
    public Rigidbody Body { get; private set; }
    public PrizeState State { get; private set; } = PrizeState.InPile;

    void Awake()
    {
        RefreshPhysicsReferences();

        Body.linearDamping = 0.6f; 
        Body.angularDamping = 0.8f;
        if (Body != null) Body.mass = 1.2f;
    }

    public void RefreshPhysicsReferences()
    {
        Body = GetComponent<Rigidbody>();
        if (Body == null) Body = GetComponentInChildren<Rigidbody>();
        if (Body == null) Body = gameObject.AddComponent<Rigidbody>();
    }

    public void ConfigureFromStock(string stockId, PrizeRarity rarity, float captureChance)
    {
        StockId = string.IsNullOrEmpty(stockId) ? prizeId : stockId;
        prizeId = StockId.ToLowerInvariant();
        Rarity = rarity;
        BaseCaptureChance = Mathf.Clamp(captureChance, 0.05f, 1f);
    }

    public void Attach(Transform anchor)
    {
        if (anchor == null) return;
        RefreshPhysicsReferences();
        State = PrizeState.Attached;
        if (Body != null)
        {
            Body.isKinematic = true;
            Body.useGravity = false;
            Body.linearVelocity = Vector3.zero;
            Body.angularVelocity = Vector3.zero;
        }
        // O wrapper é anexado ao socket central da cabeça móvel. O visual continua
        // sendo filho do wrapper, portanto rig e collider sobem juntos.
        transform.SetParent(anchor, false);
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;
    }

    public void Detach()
    {
        RefreshPhysicsReferences();
        if (State != PrizeState.Delivered) State = PrizeState.Dropped;
        transform.SetParent(null, true);
        if (Body != null)
        {
            Body.isKinematic = false;
            Body.useGravity = true;
            Body.linearVelocity = Vector3.zero;
            Body.angularVelocity = Vector3.zero;
            Body.WakeUp();
        }
    }

    public void MarkDelivered()
    {
        RefreshPhysicsReferences();
        State = PrizeState.Delivered;
        Body.isKinematic = false;
        Body.useGravity = true;
        Body.WakeUp();
        transform.SetParent(null);
    }
}