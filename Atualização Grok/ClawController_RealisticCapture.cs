// ============================================================
// INSTRUÇÕES DE INTEGRAÇÃO NO ClawController.cs
// ============================================================
// 
// 1. Adicione os campos abaixo na classe ClawController (junto com os outros [Header])
// 2. Substitua o método FecharGarraFisica() pela versão nova
// 3. Adicione os novos métodos (EvaluateBestCandidate, TryGrabRealistic, etc.)
// 4. Dentro da coroutine RotinaCicloFliperama, durante a subida e viagem,
//    chame UpdateHeldPrizePhysics() a cada frame (depois de AtualizarCabo())
// 5. No AbrirGarraFisica, trate o currentHeldPrize corretamente
//
// Layer: crie uma Layer chamada "Prize" e configure o campo prizeLayer.
// ============================================================

using UnityEngine;

public partial class ClawController
{
    // ========== CAMPOS NOVOS (cole no ClawController) ==========

    /*
    [Header("Captura Realista")]
    [SerializeField] private float captureRadius = 0.55f;
    [SerializeField] private float maxHorizontalAlignDistance = 0.48f;
    [SerializeField] private float idealVerticalOffset = -0.38f;
    [SerializeField] private float verticalTolerance = 0.35f;
    [SerializeField] private LayerMask prizeLayer;

    [Header("Grip")]
    [SerializeField, Range(0.3f, 1.2f)] private float baseGripForce = 0.85f;
    [SerializeField] private float gripLossPerSecondWhileMoving = 0.18f;
    [SerializeField] private float gripLossPerSwayDegree = 0.012f;

    private float currentGripForce;
    private Prize currentHeldPrize;
    private float slipTimer;
    private bool isSlipping;
    */

    public struct CaptureEvaluation
    {
        public Prize prize;
        public float score;
        public float horizontalAlign;
        public float verticalProximity;
        public float stability;
        public bool isValid;
    }

    // ========== MÉTODOS NOVOS ==========

    private CaptureEvaluation EvaluateBestCandidate()
    {
        CaptureEvaluation best = new CaptureEvaluation { score = -1f, isValid = false };

        Vector3 origin = transform.position + Vector3.down * 0.35f;
        Collider[] hits = Physics.OverlapSphere(origin, captureRadius, prizeLayer);

        if (hits == null || hits.Length == 0)
        {
            // Fallback: se a layer não estiver configurada, busca todos os Prize
            hits = Physics.OverlapSphere(origin, captureRadius);
        }

        if (hits == null || hits.Length == 0) return best;

        Vector3 clawPos = transform.position;
        float clawSpeed = clawVelocity.magnitude;

        foreach (var col in hits)
        {
            Prize prize = col.GetComponentInParent<Prize>();
            if (prize == null || prize.State != PrizeState.InPile || prize.Body == null) continue;

            Vector3 prizePos = prize.transform.position;

            float horizDist = Vector2.Distance(
                new Vector2(clawPos.x, clawPos.z),
                new Vector2(prizePos.x, prizePos.z)
            );
            float horizontalAlign = 1f - Mathf.Clamp01(horizDist / maxHorizontalAlignDistance);

            float verticalDelta = Mathf.Abs((prizePos.y - clawPos.y) - idealVerticalOffset);
            float verticalProximity = 1f - Mathf.Clamp01(verticalDelta / verticalTolerance);

            float stability = 1f - Mathf.Clamp01(clawSpeed / 1.8f);

            float score = (horizontalAlign * 0.55f) + (verticalProximity * 0.30f) + (stability * 0.15f);

            if (score > best.score)
            {
                best.prize = prize;
                best.score = score;
                best.horizontalAlign = horizontalAlign;
                best.verticalProximity = verticalProximity;
                best.stability = stability;
                best.isValid = score > 0.28f;
            }
        }

        return best;
    }

    private void TryGrabRealistic()
    {
        CaptureEvaluation eval = EvaluateBestCandidate();

        if (!eval.isValid || eval.prize == null)
        {
            OnGrabAttempt?.Invoke(false);
            AudioFeedbackController.Instance?.PlayClank();
            GameJuice.Instance?.HapticsLight();
            return;
        }

        currentGripForce = baseGripForce * Mathf.Lerp(0.55f, 1.05f, eval.score);
        currentHeldPrize = eval.prize;
        premioAgarrado = eval.prize.gameObject; // mantém compatibilidade com código antigo

        currentHeldPrize.Attach(
            clawVisualContainer != null ? clawVisualContainer : transform,
            eval.score,
            currentGripForce
        );

        isSlipping = false;
        slipTimer = 0f;

        OnGrabAttempt?.Invoke(true);
        AudioFeedbackController.Instance?.PlayClank(); // ou PlayGrabSuccess se existir
        GameJuice.Instance?.HapticsMedium();
        GameJuice.Instance?.ScreenShake(0.12f, 0.08f);
        GameJuice.Instance?.PunchScale(currentHeldPrize.transform, 1.18f, 0.22f);
        GameJuice.Instance?.PlaySparkles(transform.position);
        SetTrailColorGrabbing();

        // Câmera (se existir)
        var cam = FindFirstObjectByType<ClawCameraController>();
        if (cam != null)
        {
            cam.Shake(0.11f, 0.13f);
            cam.PunchFOV(0.7f);
        }

        Debug.Log($"[Claw] Captura qualidade {eval.score:P0} | Grip {currentGripForce:F2} | {currentHeldPrize.prizeId}");
    }

    private void UpdateHeldPrizePhysics()
    {
        if (currentHeldPrize == null) return;
        if (currentHeldPrize.State == PrizeState.Delivered || currentHeldPrize.State == PrizeState.Dropped) return;

        float swayPenalty = currentSwayAngle.magnitude / Mathf.Max(0.01f, swayMaxAngle);
        float movementPenalty = Mathf.Clamp01(clawVelocity.magnitude / 2.2f);

        float gripLoss = (gripLossPerSecondWhileMoving * movementPenalty +
                          gripLossPerSwayDegree * currentSwayAngle.magnitude) * Time.deltaTime;

        currentGripForce = Mathf.Max(0.05f, currentGripForce - gripLoss);

        bool stillHolding = currentHeldPrize.IsGripSufficient(currentGripForce, swayPenalty, movementPenalty);

        if (!stillHolding)
        {
            if (!isSlipping)
            {
                isSlipping = true;
                currentHeldPrize.BeginSlip();
                slipTimer = 0f;

                AudioFeedbackController.Instance?.PlayThud(); // ideal: PlaySlipStart
                GameJuice.Instance?.HapticsSlip();

                var cam = FindFirstObjectByType<ClawCameraController>();
                cam?.Shake(0.07f, 0.45f);

                Debug.Log("[Claw] Prêmio começou a escorregar!");
            }

            slipTimer += Time.deltaTime;

            if (slipTimer > 0.35f)
            {
                ReleasePrizeWithPhysics();
            }
        }
        else
        {
            isSlipping = false;
            slipTimer = 0f;
        }
    }

    private void ReleasePrizeWithPhysics()
    {
        if (currentHeldPrize == null) return;

        Prize p = currentHeldPrize;
        currentHeldPrize = null;
        premioAgarrado = null;
        isSlipping = false;

        p.Detach();

        if (p.Body != null)
        {
            Vector3 slipDir = (Random.insideUnitSphere + Vector3.down * 1.4f).normalized;
            p.Body.AddForce(slipDir * 1.8f, ForceMode.Impulse);
            p.Body.AddTorque(Random.insideUnitSphere * 2.5f, ForceMode.Impulse);
        }

        AudioFeedbackController.Instance?.PlayThud();
        GameJuice.Instance?.HapticsHeavy();
        GameJuice.Instance?.ScreenShake(0.18f, 0.12f);

        var cam = FindFirstObjectByType<ClawCameraController>();
        cam?.Shake(0.17f, 0.2f);

        Debug.Log("[Claw] Prêmio escorregou e caiu.");
    }
}
