using UnityEngine;

public enum PrizeState { InPile, Attached, Slipping, Delivering, Delivered, Dropped }

public enum GrabKind
{
    None,
    FirmBasket,   // 3 dentes em volta da cabeça/tronco — estável
    SidePinch,    // 1–2 dentes na lateral — pende, gira e balança
    LimbTip,      // orelha, pata, extremidade — quase sempre cai
    ShoveOnly     // encostou mas não fechou em volta — só empurra o monte
}

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
    public GrabKind CurrentGrabKind { get; private set; } = GrabKind.None;
    public FixedJoint CurrentJoint { get; private set; }

    // Dados da captura atual (preenchidos pela garra)
    public float CaptureQuality { get; private set; }
    public float CurrentGripHold { get; private set; }
    public Vector3 GripContactPoint { get; private set; }

    private static PhysicsMaterial plushieMaterial;
    private bool hasLandedDropImpact = false;

    public static PhysicsMaterial GetPlushiePhysicsMaterial()
    {
        if (plushieMaterial == null)
        {
            plushieMaterial = new PhysicsMaterial("Plushie_Fabric");
            plushieMaterial.dynamicFriction = 0.85f;
            plushieMaterial.staticFriction = 0.95f;
            plushieMaterial.bounciness = 0.05f;
            plushieMaterial.frictionCombine = PhysicsMaterialCombine.Average;
            plushieMaterial.bounceCombine = PhysicsMaterialCombine.Minimum;
        }
        return plushieMaterial;
    }

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

    public void ApplyDefaultPhysics()
    {
        if (Body == null) return;
        Body.linearDamping = 1.25f; // Arrasto de pano no ar (evita queda como pedra/lua)
        Body.angularDamping = 0.65f; // Permite 1-2 tombos visíveis sem girar para sempre
        Body.mass = massFeel;
        Body.maxAngularVelocity = 8.0f; // Tumble suave, não liquidificador
        Body.centerOfMass = new Vector3(0f, -0.06f, 0f); // Peso concentrado na base (tomba naturalmente)
        Body.interpolation = RigidbodyInterpolation.Interpolate;
        Body.collisionDetectionMode = CollisionDetectionMode.Continuous;

        // Aplica material de pelúcia em todos os colisores
        PhysicsMaterial mat = GetPlushiePhysicsMaterial();
        foreach (Collider c in GetComponentsInChildren<Collider>(true))
        {
            c.sharedMaterial = mat;
        }
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
            case PrizeRarity.Common: // Fox / GreenBear
                gripRequired = 0.32f;
                massFeel = 0.95f;
                slipperiness = 0.12f;
                break;
            case PrizeRarity.Uncommon: // Koala / BalloonFish
                gripRequired = 0.45f;
                massFeel = 1.15f;
                slipperiness = 0.20f;
                break;
            case PrizeRarity.Rare: // Porky
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

    /// <summary>
    /// Attach realista com suporte a FirmBasket, SidePinch e LimbTip.
    /// </summary>
    public void Attach(Transform anchor, float quality, float gripForce, GrabKind kind, Vector3 contactPoint, Quaternion currentClawRot)
    {
        if (anchor == null) return;
        RefreshPhysicsReferences();
        State = PrizeState.Attached;
        CurrentGrabKind = kind;
        CaptureQuality = Mathf.Clamp01(quality);
        CurrentGripHold = gripForce;
        GripContactPoint = contactPoint;
        hasLandedDropImpact = false;

        DestroyJoint();

        if (kind == GrabKind.FirmBasket)
        {
            if (Body != null)
            {
                Body.linearVelocity = Vector3.zero;
                Body.angularVelocity = Vector3.zero;
                Body.isKinematic = true;
                Body.useGravity = false;
            }

            // Preserva a rotação relativa do fechamento (NÃO força 0, 180, 0)
            Quaternion relativeRot = Quaternion.Inverse(anchor.rotation) * transform.rotation;
            transform.SetParent(anchor, false);

            Vector3 pScale = anchor.lossyScale;
            if (pScale.x > 0.001f && pScale.y > 0.001f && pScale.z > 0.001f)
            {
                transform.localScale = new Vector3(1f / pScale.x, 1f / pScale.y, 1f / pScale.z);
            }
            else
            {
                transform.localScale = Vector3.one;
            }

            transform.localPosition = new Vector3(0f, -0.05f, 0f);
            transform.localRotation = relativeRot;
        }
        else
        {
            // SidePinch ou LimbTip: física ativa com FixedJoint articulado
            transform.SetParent(null, true);
            transform.localScale = Vector3.one;

            if (Body != null)
            {
                Body.isKinematic = false;
                Body.useGravity = true;
                Body.linearDamping = 0.50f;
                Body.angularDamping = 1.20f;
                Body.WakeUp();

                Rigidbody anchorRb = anchor.GetComponentInParent<Rigidbody>();
                if (anchorRb != null)
                {
                    CurrentJoint = gameObject.AddComponent<FixedJoint>();
                    CurrentJoint.connectedBody = anchorRb;
                    CurrentJoint.autoConfigureConnectedAnchor = false;
                    CurrentJoint.anchor = transform.InverseTransformPoint(contactPoint);
                    CurrentJoint.connectedAnchor = anchor.InverseTransformPoint(contactPoint);

                    if (kind == GrabKind.SidePinch)
                    {
                        CurrentJoint.breakForce = 18f;
                        CurrentJoint.breakTorque = 14f;
                    }
                    else // LimbTip
                    {
                        CurrentJoint.breakForce = 6.5f;
                        CurrentJoint.breakTorque = 4.5f;
                    }
                }
            }
        }
    }

    public void Attach(Transform anchor, float quality, float gripForce)
    {
        Attach(anchor, quality, gripForce, GrabKind.FirmBasket, anchor.position, anchor.rotation);
    }

    public void Attach(Transform anchor)
    {
        Attach(anchor, 0.85f, 1.0f, GrabKind.FirmBasket, anchor.position, anchor.rotation);
    }

    void OnJointBreak(float breakForce)
    {
        Debug.Log($"[Prize] Joint de pegada quebrou ({breakForce:F1}N)! Soltando prêmio.");
        if (State == PrizeState.Attached)
        {
            BeginSlip();
            Detach();
        }
    }

    void OnCollisionEnter(Collision collision)
    {
        // 1. Se estiver caindo (Dropped), toca o som de Thud e acorda vizinhos NO IMPACTO com o piso/monte
        if (State == PrizeState.Dropped && !hasLandedDropImpact)
        {
            hasLandedDropImpact = true;
            AudioFeedbackController.Instance?.PlayDropThud();
            GameJuice.Instance?.HapticsLight();

            var cam = FindFirstObjectByType<ClawCameraController>();
            cam?.Shake(0.08f, 0.12f);

            // Clamp de velocidade no impacto: pano não é meteoro
            if (Body != null && Body.linearVelocity.magnitude > 2.2f)
            {
                Body.linearVelocity = Body.linearVelocity.normalized * 2.2f;
            }

            // Acorda vizinhos da pilha num raio de 0.6m para a pilha acomodar realisticamente
            Collider[] neighbors = Physics.OverlapSphere(transform.position, 0.60f, 1 << gameObject.layer);
            if (neighbors != null)
            {
                foreach (var n in neighbors)
                {
                    Rigidbody rb = n.attachedRigidbody;
                    if (rb != null && rb != Body && !rb.isKinematic)
                    {
                        rb.WakeUp();
                    }
                }
            }
        }

        // 2. Se estiver subindo por SidePinch ou LimbTip e bater com força no monte
        if (State == PrizeState.Attached && (CurrentGrabKind == GrabKind.SidePinch || CurrentGrabKind == GrabKind.LimbTip))
        {
            Prize otherPrize = collision.gameObject.GetComponentInParent<Prize>();
            if (otherPrize != null && otherPrize != this)
            {
                if (collision.relativeVelocity.sqrMagnitude > 0.5f)
                {
                    Debug.Log($"[Prize] Colisão com a pilha roubou a pelúcia! ({collision.relativeVelocity.magnitude:F2} m/s)");
                    BeginSlip();
                    if (CurrentGrabKind == GrabKind.LimbTip || collision.relativeVelocity.sqrMagnitude > 1.2f)
                    {
                        Detach();
                    }
                }
            }
        }
    }

    public void BeginSlip()
    {
        if (State != PrizeState.Attached) return;
        State = PrizeState.Slipping;
    }

    public void Detach()
    {
        RefreshPhysicsReferences();
        DestroyJoint();

        if (State != PrizeState.Delivered) State = PrizeState.Dropped;
        hasLandedDropImpact = false;

        if (Body != null)
        {
            Body.isKinematic = false;
            Body.useGravity = true;
            ApplyDefaultPhysics();
            Body.WakeUp();
        }
        transform.SetParent(null, true);
        transform.localScale = Vector3.one;
        CurrentGrabKind = GrabKind.None;
        StartCoroutine(CheckSettleRoutine());
    }

    private void DestroyJoint()
    {
        if (CurrentJoint != null)
        {
            Destroy(CurrentJoint);
            CurrentJoint = null;
        }
    }

    /// <summary>
    /// Aguarda a pelúcia parar suavemente sobre a pilha e retorna ao estado InPile (NUNCA destrói).
    /// </summary>
    private System.Collections.IEnumerator CheckSettleRoutine()
    {
        yield return new WaitForSeconds(0.25f);
        float elapsed = 0f;
        while (elapsed < 3.5f)
        {
            if (Body != null)
            {
                if (Body.linearVelocity.sqrMagnitude < 0.08f && Body.angularVelocity.sqrMagnitude < 0.15f)
                {
                    break;
                }
            }
            yield return new WaitForSeconds(0.15f);
            elapsed += 0.15f;
        }

        if (State == PrizeState.Dropped)
        {
            State = PrizeState.InPile;
        }
    }

    public void MarkDelivered()
    {
        RefreshPhysicsReferences();
        DestroyJoint();
        State = PrizeState.Delivered;
        CurrentGrabKind = GrabKind.None;

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

    public bool IsGripSufficient(float currentGrip, float swayPenalty, float movementPenalty)
    {
        float effectiveRequired = gripRequired * (1f + slipperiness * 0.35f);
        effectiveRequired *= Mathf.Lerp(1.20f, 0.85f, CaptureQuality);

        float kindMultiplier = 1.0f;
        if (CurrentGrabKind == GrabKind.SidePinch) kindMultiplier = 2.0f;
        else if (CurrentGrabKind == GrabKind.LimbTip) kindMultiplier = 3.2f;

        effectiveRequired += (swayPenalty * 0.15f * kindMultiplier) + (movementPenalty * 0.10f * kindMultiplier);

        return currentGrip >= effectiveRequired;
    }
}
