using UnityEngine;

public enum PrizeState { InPile, Attached, Delivering, Delivered, Dropped }

[RequireComponent(typeof(Rigidbody), typeof(Collider))]
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

        // FIM DO TREME-TREME: Aumenta o atrito e peso para os ursos "dormirem" no monte
        Body.linearDamping = 2f; 
        Body.angularDamping = 2f;
        if (Body != null) Body.mass = 1.5f;
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
        RefreshPhysicsReferences();
        State = PrizeState.Attached;
        Body.isKinematic = true;
        transform.SetParent(anchor, true);
        
        // FIM DO URSO DE LADO: Força ele a ficar reto e centralizado embaixo da garra
        transform.localPosition = new Vector3(0, -0.6f, 0); 
        transform.localRotation = Quaternion.Euler(0, 180, 0);
    }

    public void Detach()
    {
        if (State == PrizeState.Delivered) return;
        RefreshPhysicsReferences();
        State = PrizeState.Dropped;
        Body.isKinematic = false;
        transform.SetParent(null);
    }

    public void MarkDelivered()
    {
        RefreshPhysicsReferences();
        State = PrizeState.Delivered;
        Body.isKinematic = false;
        transform.SetParent(null);
    }
}