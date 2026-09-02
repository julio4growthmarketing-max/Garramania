using UnityEngine;
using System.Collections.Generic;

public class ClawController : MonoBehaviour
{
    private float LIM_X = 1.3f;
    private float LIM_Z = 1.3f;
    private float LIM_YMAX = 1.65f;
    private float LIM_YMIN = -0.95f;

    // Juice: throttle para som do servo não spammar
    private float servoSoundTimer = 0f;
    private const float SERVO_SOUND_COOLDOWN = 0.2f;

    private bool isClosed = false;
    private Transform[] dentes;
    private GameObject premioAgarrado;
    private Transform carrySocket;
    private LineRenderer cable;
    private GameObject cable3D;
    private const float GANTRY_CEILING_Y = 2.78f;
    private RealisticClawMeshBuilder.ClawRig clawRig;
    private float currentOpenFactor = 1.0f;
    private Coroutine clawAnimationRoutine;

    [Header("Eventos")]
    public UnityEngine.Events.UnityEvent<bool> OnClawStateChanged = new UnityEngine.Events.UnityEvent<bool>();
    public UnityEngine.Events.UnityEvent<bool> OnGrabAttempt = new UnityEngine.Events.UnityEvent<bool>();
    public bool IsClosed => isClosed;
    public bool HasPrize => currentHeldPrize != null || premioAgarrado != null;

    [Header("Força da Garra")]
    [Range(0.1f, 1.0f)]
    [SerializeField] private float clawForce = 1.0f;
    public float ClawForce => clawForce;

    public void SetForce(float force)
    {
        clawForce = Mathf.Clamp(force, 0.1f, 1.0f);
        Debug.Log($"[ClawController] Força da garra ajustada para: {clawForce:P0}");
    }

    [Header("Captura Realista")]
    [SerializeField] private float captureRadius = 0.86f;
    [SerializeField] private float maxHorizontalAlignDistance = 0.72f;
    [SerializeField] private float idealVerticalOffset = -0.22f;
    [SerializeField] private float verticalTolerance = 0.60f;
    [SerializeField] private LayerMask prizeLayer;

    [Header("Eletromecânica do Solenoide & Modulação PWM (Engenharia Real)")]
    [SerializeField] private float solenoidMaxVoltage = 1.0f; // 100% pulso de fechamento no solo
    [SerializeField] private float solenoidMinHoldVoltage = 0.38f; // Tensão reduzida PWM durante a subida
    private float currentSolenoidVoltage = 1.0f;
    private float captureQuality = 1.0f;
    private float prizeMass = 1.8f;
    private float frictionCoeff = 0.85f;

    [Header("Grip")]
    [SerializeField, Range(0.3f, 2.0f)] private float baseGripForce = 1.15f;
    [SerializeField] private float gripLossPerSecondWhileMoving = 0.028f;
    [SerializeField] private float gripLossPerSwayDegree = 0.0024f;

    private float currentGripForce;
    private Prize currentHeldPrize;
    private float slipTimer;
    private bool isSlipping;
    private bool earlyAscentJerkApplied;
    private bool midAscentJerkApplied;

    public struct CaptureEvaluation
    {
        public Prize prize;
        public GrabKind kind;
        public float score;
        public float horizontalAlign;
        public float verticalProximity;
        public float stability;
        public int prongCount;
        public Vector3 contactPoint;
        public bool isValid;
    }

    [Header("Arraste o Urso (Prefab) para cá no Inspector (Opcional):")]
    public GameObject prizePrefab;

    private Transform prizePileRoot;
    private PrizeStockManager stockManager;
    private PrizePileSpawner pileSpawner;

    [Header("Inércia e Pêndulo da Garra (Claw Sway)")]
    [SerializeField] private float swaySpring = 28f;
    [SerializeField] private float swayDamping = 4.2f;
    [SerializeField] private float swayMaxAngle = 20f;
    private Transform clawVisualContainer;
    private Vector3 lastClawPos;
    private Vector3 clawVelocity;
    private Vector3 lastClawVelocity;
    private Vector2 currentSwayAngle;
    private Vector2 swayVelocity;

    // === ANTI-ESPIRRO: Tracking de colisões ignoradas durante o ciclo de captura ===
    private readonly List<Collider> ignoredPrizeColliders = new List<Collider>();
    private bool prongsInTriggerMode = false;

    void Start()
    {
        transform.position = new Vector3(0f, LIM_YMAX, 0f);
        stockManager = PrizeStockManager.Instance;

        // === FASE 3: Physics Settings Globais para depenetração suave ===
        Physics.defaultContactOffset = 0.03f; // Margem maior = resolve contato mais cedo e suavemente
        Physics.defaultSolverIterations = 4;   // Menos iterações = menos força de depenetração explosiva

        try
        {
            ConstruirGabinete();
        }
        catch (System.Exception ex)
        {
            Debug.LogError("[ClawController] Falha no gabinete visual. O gameplay continuará com o fallback: " + ex);
        }

        try
        {
            ConstruirGarra();
        }
        catch (System.Exception ex)
        {
            Debug.LogError("[ClawController] Falha na garra visual: " + ex);
        }
        
        // Cabo de Aço Realista 3D
        cable3D = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        cable3D.name = "Cabo_Aco_Guindaste_3D";
        Destroy(cable3D.GetComponent<Collider>());
        
        Material mCable = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
        mCable.color = new Color(0.76f, 0.78f, 0.82f, 1.0f);
        mCable.SetFloat("_Metallic", 0.95f);
        mCable.SetFloat("_Smoothness", 0.80f);
        cable3D.GetComponent<MeshRenderer>().material = mCable;

        cable = gameObject.AddComponent<LineRenderer>();
        cable.startWidth = 0.024f;
        cable.endWidth = 0.024f;
        Shader sLine = Shader.Find("Universal Render Pipeline/Unlit") 
                    ?? Shader.Find("Sprites/Default") 
                    ?? Shader.Find("Universal Render Pipeline/Particles/Unlit");
        cable.material = new Material(sLine != null ? sLine : Shader.Find("Hidden/InternalErrorShader"));
        cable.material.color = new Color(0.82f, 0.85f, 0.90f, 1.0f);
        cable.positionCount = 2;

        AtualizarCabo();
    }

    private void OnDestroy()
    {
        if (cable3D != null) Destroy(cable3D);
    }

    private bool isExecutingCycle = false;
    public bool IsExecutingCycle => isExecutingCycle;

    void Update()
    {
        AtualizarCabo();
        UpdateClawSway();

        if (currentHeldPrize != null)
        {
            UpdateHeldPrizePhysics();
        }

        // Se a garra está no ciclo de descida/captura/retorno à calha, bloqueia comandos manuais
        if (isExecutingCycle)
        {
            AudioFeedbackController.Instance?.SetMotorMoving(false);
            return;
        }
        if (GameSession.Instance != null && !GameSession.Instance.CanMoveClaw())
        {
            AudioFeedbackController.Instance?.SetMotorMoving(false);
            return;
        }

        // Servo sound throttle
        if (servoSoundTimer > 0f) servoSoundTimer -= Time.deltaTime;

        Vector3 moveInput = Vector3.zero;
        bool space = false;

        // Lê Roteador (Celular / Teclado)
        if (InputRouter.Instance != null)
        {
            moveInput = InputRouter.Instance.Movement;
            space = InputRouter.Instance.ActionTriggered;
        }

        bool isMoving = moveInput.sqrMagnitude > 0.01f;
        AudioFeedbackController.Instance?.SetMotorMoving(isMoving);

        // Movimentação suave no plano horizontal X / Z
        Vector3 nova = transform.position + new Vector3(moveInput.x, 0f, moveInput.z) * 2.8f * Time.deltaTime;
        nova.x = Mathf.Clamp(nova.x, -LIM_X, LIM_X);
        nova.z = Mathf.Clamp(nova.z, -LIM_Z, LIM_Z);
        nova.y = LIM_YMAX;
        transform.position = nova;

        // Som do servo ao mover
        if (isMoving && servoSoundTimer <= 0f)
        {
            AudioFeedbackController.Instance?.PlayServo();
            servoSoundTimer = SERVO_SOUND_COOLDOWN;
        }

        if (space) AcionarGarra();
    }

    private void LateUpdate()
    {
        Prize prizeToTrack = currentHeldPrize != null ? currentHeldPrize : (premioAgarrado != null ? premioAgarrado.GetComponent<Prize>() : null);
        if (prizeToTrack == null || (prizeToTrack.State != PrizeState.Attached && prizeToTrack.State != PrizeState.Slipping)) return;
        
        // APENAS FirmBasket usa fixação rígida no carrySocket. SidePinch e LimbTip usam física pura e joints!
        if (prizeToTrack.CurrentGrabKind == GrabKind.FirmBasket)
        {
            Transform anchor = carrySocket != null ? carrySocket : clawVisualContainer;
            if (anchor != null)
            {
                prizeToTrack.transform.position = anchor.position + new Vector3(0f, -0.05f, 0f);
            }
        }
    }

    private void UpdateClawSway()
    {
        float dt = Time.deltaTime;
        if (dt <= 0.0001f) return;

        clawVelocity = (transform.position - lastClawPos) / dt;
        Vector3 clawAcceleration = (clawVelocity - lastClawVelocity) / dt;
        lastClawPos = transform.position;
        lastClawVelocity = clawVelocity;

        float swayMultiplier = isExecutingCycle ? 0.30f : 1.0f;

        Vector2 targetAngle = new Vector2(
            Mathf.Clamp(-clawAcceleration.z * 2.2f * swayMultiplier, -swayMaxAngle, swayMaxAngle),
            Mathf.Clamp(clawAcceleration.x * 2.2f * swayMultiplier, -swayMaxAngle, swayMaxAngle)
        );

        Vector2 force = (targetAngle - currentSwayAngle) * swaySpring;
        swayVelocity += force * dt;
        swayVelocity -= swayVelocity * swayDamping * dt;
        currentSwayAngle += swayVelocity * dt;

        if (clawVisualContainer != null)
        {
            clawVisualContainer.localRotation = Quaternion.Euler(currentSwayAngle.x, 0f, currentSwayAngle.y);
        }
    }

    public void AcionarGarra()
    {
        if (isExecutingCycle) return;
        if (GameSession.Instance != null && !GameSession.Instance.CanMoveClaw()) return;

        PrizeStockManager.Instance.RegisterAttemptStarted();
        StartCoroutine(RotinaCicloFliperama());
    }

    private System.Collections.IEnumerator RotinaCicloFliperama()
    {
        isExecutingCycle = true;
        earlyAscentJerkApplied = false;
        midAscentJerkApplied = false;

        AudioFeedbackController.Instance?.SetMotorMoving(false);
        CabinetLightingController.Instance?.SetDramaticFocus(true);
        GameSession.Instance?.SetState(GameState.Capturing);
        OnClawStateChanged?.Invoke(true);

        // 1. FASE DE DESCIDA E IMPACTO NO MONTE (Garra desce ABERTA empurrando o monte com colliders reais)
        // A garra precisa mergulhar NA pilha para as pinças envolverem os bonecos
        float floorLimitY = -0.82f;
        float targetY = floorLimitY;

        int pLayer = LayerMask.NameToLayer("Prize");
        int mask = prizeLayer.value != 0 ? prizeLayer.value : (pLayer != -1 ? (1 << pLayer) : ~0);
        RaycastHit hit;
        if (Physics.SphereCast(transform.position, 0.24f, Vector3.down, out hit, 3.5f, mask))
        {
            // Mergulha 0.35 abaixo do topo da pilha para as pinças envolverem os bonecos
            targetY = Mathf.Max(hit.point.y - 0.35f, floorLimitY);
        }

        while (transform.position.y > targetY + 0.02f)
        {
            transform.position = Vector3.MoveTowards(transform.position, 
                new Vector3(transform.position.x, targetY, transform.position.z), 
                2.8f * Time.deltaTime);

            AtualizarCabo();
            yield return null;
        }

        yield return new WaitForSeconds(0.18f);

        // 2. FASE DE FECHAMENTO (Pulso elétrico solenoide nos dentes)
        FecharGarraFisica();
        yield return new WaitForSeconds(0.45f);

        // 3. FASE DE ELEVAÇÃO (Z) COM JERKS E REDUÇÃO PWM
        GameSession.Instance?.SetState(GameState.Returning);
        float ascentStartY = transform.position.y;
        float totalAscentDist = LIM_YMAX - ascentStartY;

        while (transform.position.y < LIM_YMAX - 0.04f)
        {
            transform.position = Vector3.MoveTowards(transform.position, 
                new Vector3(transform.position.x, LIM_YMAX, transform.position.z), 
                2.5f * Time.deltaTime);

            float ascentProgress = totalAscentDist > 0.05f 
                ? Mathf.Clamp01((transform.position.y - ascentStartY) / totalAscentDist) 
                : 1.0f;

            // Jerk na subida: LimbTip sofre tranco forte no início; SidePinch sofre no meio
            if (!earlyAscentJerkApplied && ascentProgress >= 0.18f)
            {
                earlyAscentJerkApplied = true;
                ApplyEarlyAscentJerk();
            }
            if (!midAscentJerkApplied && ascentProgress >= 0.55f)
            {
                midAscentJerkApplied = true;
                ApplyMidAscentJerk();
            }

            UpdateSolenoidPWM(ascentProgress);
            UpdateHeldPrizePhysics();
            AtualizarCabo();
            yield return null;
        }

        // TRANCO DE TOPO: Aceleração inercial brusca ao atingir o batente superior
        ApplyTopJerkInertia();
        yield return new WaitForSeconds(0.25f);

        // 4. FASE DE VIAGEM ATÉ A CALHA DE PRÊMIOS (-1.75, LIM_YMAX, -1.75)
        Vector3 posCalha = new Vector3(-1.75f, LIM_YMAX, -1.75f);
        while (Vector3.Distance(new Vector3(transform.position.x, 0, transform.position.z), 
                              new Vector3(posCalha.x, 0, posCalha.z)) > 0.05f)
        {
            transform.position = Vector3.MoveTowards(transform.position, 
                new Vector3(posCalha.x, LIM_YMAX, posCalha.z), 
                3.0f * Time.deltaTime);

            UpdateHeldPrizePhysics();
            AtualizarCabo();
            yield return null;
        }

        yield return new WaitForSeconds(0.35f);

        // 5. FASE DE ENTREGA FÍSICA
        bool haviaPremio = currentHeldPrize != null || premioAgarrado != null;
        if (haviaPremio) GameSession.Instance?.SetState(GameState.Delivering);
        AbrirGarraFisica();
        yield return new WaitForSeconds(1.25f);

        // 6. RETORNO DA GARRA AO CENTRO DA VITRINE
        Vector3 posCentro = new Vector3(0f, LIM_YMAX, 0f);
        while (Vector3.Distance(new Vector3(transform.position.x, 0, transform.position.z), 
                              new Vector3(posCentro.x, 0, posCentro.z)) > 0.05f)
        {
            transform.position = Vector3.MoveTowards(transform.position, 
                new Vector3(posCentro.x, LIM_YMAX, posCentro.z), 
                3.2f * Time.deltaTime);

            AtualizarCabo();
            yield return null;
        }

        isExecutingCycle = false;
        CabinetLightingController.Instance?.SetDramaticFocus(false);
        if (GameSession.Instance != null && GameSession.Instance.CurrentState != GameState.GameOver)
        {
            GameSession.Instance.SetState(GameState.Playing);
        }
        if (InputRouter.Instance != null && (GameSession.Instance == null || GameSession.Instance.CurrentState == GameState.Playing))
        {
            InputRouter.Instance.SetBlocked(false);
        }
        OnClawStateChanged?.Invoke(false);
    }

    private void UpdateSolenoidPWM(float ascentProgress)
    {
        float targetHold = Mathf.Lerp(0.85f, 1.0f, captureQuality);
        currentSolenoidVoltage = Mathf.Lerp(solenoidMaxVoltage, targetHold, Mathf.SmoothStep(0f, 1f, ascentProgress));
    }

    private void ApplyEarlyAscentJerk()
    {
        if (currentHeldPrize == null) return;

        if (currentHeldPrize.CurrentGrabKind == GrabKind.LimbTip)
        {
            Debug.Log("[Claw] Tranco inicial de subida em ponta de membro (LimbTip)!");
            if (currentHeldPrize.Body != null)
            {
                currentHeldPrize.Body.AddForce(Vector3.down * 1.8f + Random.insideUnitSphere * 0.7f, ForceMode.Impulse);
                currentHeldPrize.Body.AddTorque(Random.insideUnitSphere * 2.0f, ForceMode.Impulse);
            }
            // 30% de chance de soltar no início da subida para dar suspense
            if (Random.value < 0.30f)
            {
                ReleasePrizeWithPhysics();
            }
        }
    }

    private void ApplyMidAscentJerk()
    {
        if (currentHeldPrize == null) return;

        if (currentHeldPrize.CurrentGrabKind == GrabKind.SidePinch)
        {
            Debug.Log("[Claw] Tranco de meio de subida (SidePinch)!");
            if (currentHeldPrize.Body != null)
            {
                currentHeldPrize.Body.AddForce(Vector3.down * 1.3f + Random.insideUnitSphere * 0.6f, ForceMode.Impulse);
                currentHeldPrize.Body.AddTorque(Random.insideUnitSphere * 1.6f, ForceMode.Impulse);
            }
            if (Random.value < 0.10f)
            {
                currentHeldPrize.BeginSlip();
            }
        }
    }

    private void ApplyTopJerkInertia()
    {
        if (currentHeldPrize == null) return;

        if (currentHeldPrize.CurrentGrabKind == GrabKind.LimbTip)
        {
            // 70% de chance de soltar no topo (antes era 100%)
            if (Random.value < 0.70f)
            {
                Debug.Log("[Claw] LimbTip não resistiu ao tranco do topo!");
                ReleasePrizeWithPhysics();
            }
        }
        else if (currentHeldPrize.CurrentGrabKind == GrabKind.SidePinch)
        {
            if (captureQuality < 0.35f || Random.value < 0.20f)
            {
                Debug.Log($"[Claw] SidePinch ({captureQuality:P0}) desarmado pelo tranco do topo!");
                ReleasePrizeWithPhysics();
            }
        }
        else if (currentHeldPrize.CurrentGrabKind == GrabKind.FirmBasket)
        {
            if (captureQuality < 0.12f)
            {
                ReleasePrizeWithPhysics();
            }
        }
    }

    private void FecharGarraFisica()
    {
        isClosed = true;
        AudioFeedbackController.Instance?.PlaySolenoidClamp();

        // === FASE 1 + 2: ANTI-ESPIRRO ===
        // ANTES de fechar, converter colliders das pinças para trigger (fantasma)
        // e ignorar colisões com TODOS os prêmios próximos.
        // Isso impede que as pinças empurrem os bonecos durante o fechamento.
        SetProngCollidersTrigger(true);
        IgnoreAllNearbyPrizeCollisions(true);
        Debug.Log("[ClawCapture] Fase de fechamento: pinças em modo fantasma + colisões ignoradas.");

        if (clawAnimationRoutine != null) StopCoroutine(clawAnimationRoutine);
        clawAnimationRoutine = StartCoroutine(AnimateClaw(0.0f, 0.35f));

        StartCoroutine(ExecuteGrabMidClamp(0.20f));
    }

    private System.Collections.IEnumerator ExecuteGrabMidClamp(float delay)
    {
        yield return new WaitForSeconds(delay);
        TryGrabRealistic();

        // === ANTI-ESPIRRO: Restaurar colliders das pinças para sólidos ===
        // Neste ponto, se houve captura, o boneco já é kinematic e parentado,
        // então restaurar os colliders é seguro (não vai empurrar nada).
        SetProngCollidersTrigger(false);
        Debug.Log($"[ClawCapture] Fase pós-grab: pinças restauradas para sólido. Captura={currentHeldPrize != null}");

        // NÃO restauramos IgnoreCollision aqui — mantemos ignorando o prêmio capturado
        // durante toda a subida para evitar micro-colisões. Será restaurado ao soltar.
        // MAS restauramos colisões com prêmios que NÃO foram capturados.
        RestoreNonCapturedPrizeCollisions();
    }

    private System.Collections.IEnumerator AnimateClaw(float targetOpen, float duration)
    {
        float start = currentOpenFactor;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float easedT = t * t * (3f - 2f * t);
            currentOpenFactor = Mathf.Lerp(start, targetOpen, easedT);
            clawRig.SetOpenAmount?.Invoke(currentOpenFactor);
            yield return null;
        }
        currentOpenFactor = targetOpen;
        clawRig.SetOpenAmount?.Invoke(currentOpenFactor);
    }

    /// <summary>
    /// Avalia a geometria de contato REAL entre as 3 lâminas e os prêmios na pilha.
    /// Decide entre FirmBasket, SidePinch, LimbTip ou ShoveOnly.
    /// </summary>
    private CaptureEvaluation EvaluateBestCandidate()
    {
        CaptureEvaluation best = new CaptureEvaluation { isValid = false, score = -1f, kind = GrabKind.None };

        Vector3 clawPos = transform.position;

        // === CORREÇÃO DE GEOMETRIA ===
        // O carrySocket está em localPos (0, -2.42, 0) no VC (scale 0.58) = 1.40 abaixo da garra.
        // Mas as pontas REAIS das pinças convergem em ~(0, -1.20, 0) no VC = 0.70 abaixo da garra.
        // Usar o carrySocket colocava o centro de detecção 0.70 unidades ABAIXO das pontas reais,
        // fazendo a esfera de busca ficar inteiramente abaixo dos bonecos.
        //
        // A posição correta das pontas no mundo:
        //   VC local Y = -1.20 (hub -0.22 + blade tip -0.98)
        //   World offset = -1.20 * 0.58 (scale) = -0.696 abaixo do transform
        Vector3 prongTipsPos;
        if (clawVisualContainer != null)
        {
            prongTipsPos = clawVisualContainer.TransformPoint(new Vector3(0f, -1.20f, 0f));
        }
        else
        {
            prongTipsPos = clawPos + Vector3.down * 0.70f;
        }

        // Esfera de detecção generosa para encontrar todos os prêmios ao redor das pontas
        // Detecção de prêmios com fallback robusto de layer
        int pLayer = LayerMask.NameToLayer("Prize");
        int detectMask = prizeLayer.value;
        if (detectMask == 0) detectMask = pLayer != -1 ? (1 << pLayer) : ~0; // fallback: layer Prize ou tudo

        Collider[] hits = Physics.OverlapSphere(prongTipsPos, 0.60f, detectMask);
        Debug.Log($"[ClawCapture] Detecção: clawY={clawPos.y:F3}, tipsY={prongTipsPos.y:F3}, mask={detectMask}, hits={hits.Length}");
        HashSet<Prize> evaluated = new HashSet<Prize>();

        foreach (var col in hits)
        {
            Prize prize = col.GetComponentInParent<Prize>();
            if (prize == null || evaluated.Contains(prize) || prize.State == PrizeState.Delivered || prize.State == PrizeState.Attached || prize.Body == null) continue;
            evaluated.Add(prize);

            Vector3 prizeCoM = prize.Body != null ? prize.Body.worldCenterOfMass : prize.transform.position;

            float horizDist = Vector2.Distance(
                new Vector2(clawPos.x, clawPos.z),
                new Vector2(prizeCoM.x, prizeCoM.z)
            );
            float vertDist = Mathf.Abs(prongTipsPos.y - prizeCoM.y);

            // Cutoff externo: descarta candidatos claramente fora do alcance
            if (horizDist > 0.50f || vertDist > 0.75f) continue;

            Debug.Log($"[ClawCapture] Candidato: {prize.prizeId} | hDist={horizDist:F3} vDist={vertDist:F3}");

            GrabKind kind = GrabKind.None;
            float score = 0f;

            if (PlayerEconomyManager.Instance != null && PlayerEconomyManager.Instance.IsGoldenClawActive)
            {
                kind = GrabKind.FirmBasket;
                score = 1.0f;
            }
            // === GRAB KINDS: horizDist E vertDist obrigatórios para cada tipo ===
            // FirmBasket: boneco centralizado entre as pinças
            else if (horizDist <= 0.28f && vertDist <= 0.65f)
            {
                kind = GrabKind.FirmBasket;
                score = Mathf.Lerp(0.87f, 0.98f, 1f - (horizDist / 0.28f));
            }
            // SidePinch: boneco pego de lado, pinças apertam as laterais
            else if (horizDist <= 0.40f && vertDist <= 0.70f)
            {
                kind = GrabKind.SidePinch;
                score = Mathf.Lerp(0.55f, 0.75f, 1f - ((horizDist - 0.28f) / 0.12f));
            }
            // LimbTip: ponta da pinça pega uma extremidade (braço, perna)
            else if (horizDist <= 0.48f && vertDist <= 0.70f)
            {
                kind = GrabKind.LimbTip;
                score = Mathf.Lerp(0.30f, 0.45f, 1f - ((horizDist - 0.40f) / 0.08f));
            }

            if (score > best.score)
            {
                best.prize = prize;
                best.kind = kind;
                best.score = score;
                best.horizontalAlign = 1f - Mathf.Clamp01(horizDist / 0.40f);
                best.verticalProximity = 1f - Mathf.Clamp01(vertDist / 0.65f);
                best.prongCount = 3;
                best.contactPoint = prizeCoM;
                best.stability = kind == GrabKind.FirmBasket ? 1f : (kind == GrabKind.SidePinch ? 0.75f : 0.40f);
                best.isValid = kind != GrabKind.None;
                Debug.Log($"[ClawCapture] Melhor até agora: {prize.prizeId} | kind={kind} score={score:F3}");
            }
        }

        return best;
    }

    private void SetClawPrizeCollisionIgnored(Prize prize, bool ignore)
    {
        if (prize == null) return;
        Collider prizeCol = prize.GetComponentInChildren<Collider>();
        if (prizeCol == null) return;

        Collider[] clawColliders = clawVisualContainer != null 
            ? clawVisualContainer.GetComponentsInChildren<Collider>() 
            : GetComponentsInChildren<Collider>();

        foreach (var c in clawColliders)
        {
            if (c != null && c.enabled && prizeCol != null && prizeCol.enabled)
            {
                Physics.IgnoreCollision(c, prizeCol, ignore);
            }
        }
    }

    // === ANTI-ESPIRRO: Converte TODOS os colliders das pinças entre sólido e trigger ===
    private void SetProngCollidersTrigger(bool isTrigger)
    {
        if (prongsInTriggerMode == isTrigger) return;
        prongsInTriggerMode = isTrigger;

        Collider[] clawColliders = clawVisualContainer != null
            ? clawVisualContainer.GetComponentsInChildren<Collider>()
            : GetComponentsInChildren<Collider>();

        foreach (var c in clawColliders)
        {
            if (c != null && c.enabled)
            {
                c.isTrigger = isTrigger;
            }
        }
    }

    // === ANTI-ESPIRRO: Ignora colisões entre TODAS as pinças e TODOS os prêmios próximos ===
    private void IgnoreAllNearbyPrizeCollisions(bool ignore)
    {
        Vector3 center = carrySocket != null ? carrySocket.position
            : (clawVisualContainer != null ? clawVisualContainer.position + Vector3.down * 0.35f
            : transform.position + Vector3.down * 0.35f);

        int pLayer = LayerMask.NameToLayer("Prize");
        int mask = prizeLayer.value != 0 ? prizeLayer.value : (pLayer != -1 ? (1 << pLayer) : ~0);
        Collider[] nearby = Physics.OverlapSphere(center, 1.2f, mask);

        Collider[] clawColliders = clawVisualContainer != null
            ? clawVisualContainer.GetComponentsInChildren<Collider>()
            : GetComponentsInChildren<Collider>();

        if (ignore) ignoredPrizeColliders.Clear();

        foreach (var prizeCol in nearby)
        {
            if (prizeCol == null || !prizeCol.enabled) continue;

            foreach (var clawCol in clawColliders)
            {
                if (clawCol == null || !clawCol.enabled) continue;
                Physics.IgnoreCollision(clawCol, prizeCol, ignore);
            }

            if (ignore && !ignoredPrizeColliders.Contains(prizeCol))
            {
                ignoredPrizeColliders.Add(prizeCol);
            }
        }

        if (!ignore) ignoredPrizeColliders.Clear();
    }

    // === ANTI-ESPIRRO: Restaura colisões apenas dos prêmios que NÃO foram capturados ===
    private void RestoreNonCapturedPrizeCollisions()
    {
        Collider capturedCol = currentHeldPrize != null ? currentHeldPrize.GetComponentInChildren<Collider>() : null;

        Collider[] clawColliders = clawVisualContainer != null
            ? clawVisualContainer.GetComponentsInChildren<Collider>()
            : GetComponentsInChildren<Collider>();

        for (int i = ignoredPrizeColliders.Count - 1; i >= 0; i--)
        {
            Collider pc = ignoredPrizeColliders[i];
            if (pc == null || pc == capturedCol) continue;

            foreach (var clawCol in clawColliders)
            {
                if (clawCol == null || !clawCol.enabled) continue;
                Physics.IgnoreCollision(clawCol, pc, false);
            }
            ignoredPrizeColliders.RemoveAt(i);
        }
    }

    private void TryGrabRealistic()
    {
        CaptureEvaluation eval = EvaluateBestCandidate();

        if (!eval.isValid || eval.prize == null)
        {
            Debug.Log($"[ClawCapture] FALHA NA CAPTURA: isValid={eval.isValid}, prize={eval.prize}, score={eval.score:F3}, kind={eval.kind}");
            currentHeldPrize = null;
            premioAgarrado = null;
            OnGrabAttempt?.Invoke(false);
            AudioFeedbackController.Instance?.PlayClank();
            GameJuice.Instance?.HapticsLight();
            if (eval.score > 0.10f) AudioFeedbackController.Instance?.PlayNearMiss();
            return;
        }

        // Ignora colisão física entre os dentes da garra e o prêmio agarrado para evitar qualquer repulsão violenta
        SetClawPrizeCollisionIgnored(eval.prize, true);

        // Parâmetros de Captura
        captureQuality = eval.score;
        currentSolenoidVoltage = solenoidMaxVoltage;
        prizeMass = eval.prize.Body != null ? eval.prize.Body.mass : 1.2f;
        frictionCoeff = 0.95f;

        if (PlayerEconomyManager.Instance != null && PlayerEconomyManager.Instance.IsGoldenClawActive)
        {
            eval.kind = GrabKind.FirmBasket;
            eval.score = Mathf.Max(eval.score, 0.95f);
            captureQuality = 1.0f;
            frictionCoeff = 1.8f;
            PlayerEconomyManager.Instance.ConsumeGoldenToken();
            Debug.Log("[ClawController] 🌟 FICHA DOURADA ATIVA! FirmBasket forçado!");
        }

        baseGripForce = 1.50f;
        currentGripForce = baseGripForce * clawForce * Mathf.Lerp(0.95f, 1.40f, eval.score);
        currentHeldPrize = eval.prize;
        premioAgarrado = eval.prize.gameObject;

        Transform anchor = carrySocket != null ? carrySocket : (clawVisualContainer != null ? clawVisualContainer : transform);
        currentHeldPrize.Attach(
            anchor,
            eval.score,
            currentGripForce,
            eval.kind,
            eval.contactPoint,
            clawVisualContainer != null ? clawVisualContainer.rotation : transform.rotation
        );

        isSlipping = false;
        slipTimer = 0f;

        // Animação da garra envolvendo o prêmio
        float targetClamp = (eval.kind == GrabKind.FirmBasket) ? 0.06f : 0.16f;
        if (clawAnimationRoutine != null) StopCoroutine(clawAnimationRoutine);
        clawAnimationRoutine = StartCoroutine(AnimateClaw(targetClamp, 0.20f));

        OnGrabAttempt?.Invoke(true);
        AudioFeedbackController.Instance?.PlayGrabSuccess();

        if (eval.kind == GrabKind.FirmBasket)
        {
            GameJuice.Instance?.HapticsMedium();
            GameJuice.Instance?.PunchScale(currentHeldPrize.transform, 1.08f, 0.20f);
            GameJuice.Instance?.PlaySparkles(transform.position);
        }
        else if (eval.kind == GrabKind.SidePinch)
        {
            GameJuice.Instance?.HapticsMedium();
            GameJuice.Instance?.ScreenShake(0.08f, 0.10f);
        }
        else // LimbTip
        {
            GameJuice.Instance?.HapticsSlip();
        }

        var cam = FindFirstObjectByType<ClawCameraController>();
        if (cam != null)
        {
            cam.Shake(0.10f, 0.12f);
            cam.PunchFOV(0.5f);
        }

        Debug.Log($"[Claw] Captura bem-sucedida! Tipo: {eval.kind} | Qualidade: {eval.score:P0} | {currentHeldPrize.prizeId}");
    }

    private void UpdateHeldPrizePhysics()
    {
        if (currentHeldPrize == null) return;
        if (currentHeldPrize.State == PrizeState.Delivered || currentHeldPrize.State == PrizeState.Dropped) return;

        GrabKind kind = currentHeldPrize.CurrentGrabKind;

        // Calibração de timers e perdas por tipo de pegada
        float maxSlipDuration = 0.70f;
        float currentGripLossPerSec = gripLossPerSecondWhileMoving;
        float currentGripLossPerSway = gripLossPerSwayDegree;

        switch (kind)
        {
            case GrabKind.FirmBasket:
                maxSlipDuration = 0.90f;
                currentGripLossPerSec = 0.025f;
                currentGripLossPerSway = 0.002f;
                break;
            case GrabKind.SidePinch:
                maxSlipDuration = 0.50f;
                currentGripLossPerSec = 0.045f;
                currentGripLossPerSway = 0.004f;
                break;
            case GrabKind.LimbTip:
                maxSlipDuration = 0.28f;
                currentGripLossPerSec = 0.075f;
                currentGripLossPerSway = 0.006f;
                break;
        }

        // Decaimento contínuo do grip com movimento e balanço
        float movementPenalty = clawVelocity.magnitude * currentGripLossPerSec;
        float swayPenalty = currentSwayAngle.magnitude * currentGripLossPerSway;
        currentGripForce -= (movementPenalty + swayPenalty) * Time.deltaTime;

        bool stillHolding = currentHeldPrize.IsGripSufficient(currentGripForce, swayPenalty, movementPenalty);

        // Se for FirmBasket, mantém suporte com inclinação suave
        if (kind == GrabKind.FirmBasket && currentHeldPrize.State == PrizeState.Attached)
        {
            Quaternion swayTilt = Quaternion.Euler(currentSwayAngle.x * 0.30f, 180f, currentSwayAngle.y * 0.30f);
            currentHeldPrize.transform.localRotation = Quaternion.Slerp(
                currentHeldPrize.transform.localRotation,
                swayTilt,
                8f * Time.deltaTime
            );
        }

        if (!stillHolding)
        {
            if (!isSlipping)
            {
                isSlipping = true;
                currentHeldPrize.BeginSlip();
                slipTimer = 0f;

                // Afrouxamento visual dos dentes no slip
                if (clawRig.SetOpenAmount != null)
                {
                    clawRig.SetOpenAmount(Mathf.Clamp01(currentOpenFactor + 0.08f));
                }

                AudioFeedbackController.Instance?.PlaySlipStart();
                GameJuice.Instance?.HapticsSlip();
                var cam = FindFirstObjectByType<ClawCameraController>();
                cam?.Shake(0.07f, 0.45f);

                Debug.Log($"[Claw] Instabilidade Mecânica ({kind}): Objeto escorregando.");
            }

            slipTimer += Time.deltaTime;
            if (slipTimer > maxSlipDuration)
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

        // === ANTI-ESPIRRO: Restaurar colisões do prêmio que está sendo solto ===
        SetClawPrizeCollisionIgnored(currentHeldPrize, false);

        Prize p = currentHeldPrize;
        currentHeldPrize = null;
        premioAgarrado = null;
        isSlipping = false;

        GrabKind kind = p.CurrentGrabKind;
        Vector3 clawPos = transform.position;

        // 1. Desconecta o prêmio (retoma física e gravidade)
        p.Detach();

        // 2. Temporariamente ignora colisão entre as lâminas da garra e a pelúcia por 0.12s para não telefragar
        StartCoroutine(IgnoreClawCollisionsRoutine(p, 0.12f));

        // 3. Aplica velocidade herdada e impulso suave de pano escorregando (sem explosão de canhão)
        if (p.Body != null)
        {
            Vector3 baseVelocity = clawVelocity * 0.65f;

            // Direção de saída: para baixo + afastamento sutil do centro da garra
            Vector3 horizDir = (p.transform.position - clawPos);
            horizDir.y = 0f;
            if (horizDir.sqrMagnitude < 0.001f) horizDir = Random.insideUnitSphere;
            horizDir.y = 0f;
            horizDir.Normalize();

            Vector3 slipDir = (Vector3.down * 0.88f + horizDir * 0.32f).normalized;

            // Velocidade linear e torque calibrados por GrabKind
            float linearSpeed = 0.55f;
            float torqueSpeed = 0.85f;

            if (kind == GrabKind.FirmBasket)
            {
                linearSpeed = 0.25f;
                torqueSpeed = 0.40f;
            }
            else if (kind == GrabKind.SidePinch)
            {
                linearSpeed = 0.55f;
                torqueSpeed = 0.90f;
            }
            else if (kind == GrabKind.LimbTip)
            {
                linearSpeed = 0.85f;
                torqueSpeed = 1.30f;
            }

            // Se soltou no topo da vitrine
            if (clawPos.y > LIM_YMAX - 0.15f)
            {
                linearSpeed += 0.25f;
                torqueSpeed += 0.30f;
            }

            p.Body.linearVelocity = baseVelocity + slipDir * linearSpeed;

            // Torque ao redor do eixo horizontal (tumble natural de pelúcia)
            Vector3 tumbleAxis = Vector3.Cross(Vector3.up, horizDir).normalized;
            tumbleAxis += Random.insideUnitSphere * 0.20f;
            p.Body.angularVelocity = tumbleAxis.normalized * torqueSpeed;
        }

        Debug.Log($"[Claw] Pelúcia ({p.prizeId}, {kind}) solta suavemente com física realista de pano.");
    }

    private System.Collections.IEnumerator IgnoreClawCollisionsRoutine(Prize prize, float duration)
    {
        if (prize == null) yield break;
        Collider prizeCol = prize.GetComponentInChildren<Collider>();
        if (prizeCol == null) yield break;

        Collider[] clawColliders = clawVisualContainer != null 
            ? clawVisualContainer.GetComponentsInChildren<Collider>() 
            : GetComponentsInChildren<Collider>();

        foreach (var c in clawColliders)
        {
            if (c != null && c.enabled && prizeCol != null && prizeCol.enabled)
            {
                Physics.IgnoreCollision(c, prizeCol, true);
            }
        }

        yield return new WaitForSeconds(duration);

        if (prizeCol != null && prizeCol.enabled)
        {
            foreach (var c in clawColliders)
            {
                if (c != null && c.enabled && prizeCol != null && prizeCol.enabled)
                {
                    Physics.IgnoreCollision(c, prizeCol, false);
                }
            }
        }
    }

    private void AbrirGarraFisica()
    {
        isClosed = false;
        AudioFeedbackController.Instance?.PlaySolenoidRelease();
        if (clawAnimationRoutine != null) StopCoroutine(clawAnimationRoutine);
        clawAnimationRoutine = StartCoroutine(AnimateClaw(1.0f, 0.35f));

        // === ANTI-ESPIRRO: Restaurar TODAS as colisões pendentes ===
        SetClawPrizeCollisionIgnored(currentHeldPrize, false);
        IgnoreAllNearbyPrizeCollisions(false);
        SetProngCollidersTrigger(false);

        // Só entrega se o prêmio REALMENTE chegou preso na garra
        Prize p = currentHeldPrize;
        currentHeldPrize = null;
        premioAgarrado = null;
        isSlipping = false;
        slipTimer = 0f;

        if (p != null && p.State == PrizeState.Attached)
        {
            p.MarkDelivered();
            if (GameSession.Instance != null)
            {
                GameSession.Instance.RegisterPrizeDelivered(p);
            }

            AudioFeedbackController.Instance?.PlayDeliverySuccess();
            GameJuice.Instance?.HapticsSuccess();
            GameJuice.Instance?.ScreenShake(0.12f, 0.08f);

            Destroy(p.gameObject, 2.0f);
        }
    }

    private void AtualizarCabo()
    {
        Vector3 topAnchor = new Vector3(transform.position.x, GANTRY_CEILING_Y, transform.position.z);
        Vector3 bottomAnchor = clawVisualContainer != null
            ? clawVisualContainer.TransformPoint(new Vector3(0f, 0.52f, 0f))
            : transform.position + Vector3.up * 0.52f;

        Vector3 dir = bottomAnchor - topAnchor;
        float dist = dir.magnitude;

        if (cable3D != null)
        {
            if (dist > 0.01f)
            {
                if (!cable3D.activeSelf) cable3D.SetActive(true);
                cable3D.transform.position = topAnchor + dir * 0.5f;
                cable3D.transform.rotation = Quaternion.FromToRotation(Vector3.up, dir.normalized);
                cable3D.transform.localScale = new Vector3(0.024f, dist * 0.5f, 0.024f);
            }
            else
            {
                if (cable3D.activeSelf) cable3D.SetActive(false);
            }
        }

        if (cable != null)
        {
            cable.SetPosition(0, topAnchor);
            cable.SetPosition(1, bottomAnchor);
        }
    }

    public void ResetarGarra()
    {
        StopAllCoroutines();
        isExecutingCycle = false;
        transform.position = new Vector3(0, LIM_YMAX, 0);
        isClosed = false;
        if (clawAnimationRoutine != null) StopCoroutine(clawAnimationRoutine);
        currentOpenFactor = 1.0f;
        clawRig.SetOpenAmount?.Invoke(1.0f);
        OnClawStateChanged?.Invoke(false);

        // === ANTI-ESPIRRO: Restaurar estado completo ===
        SetProngCollidersTrigger(false);
        IgnoreAllNearbyPrizeCollisions(false);
        
        if (currentHeldPrize != null)
        {
            SetClawPrizeCollisionIgnored(currentHeldPrize, false);
            currentHeldPrize.Detach();
            currentHeldPrize = null;
        }
        if (premioAgarrado != null)
        {
            Prize p = premioAgarrado.GetComponent<Prize>();
            if (p != null) p.Detach();
            premioAgarrado = null;
        }
        isSlipping = false;
        slipTimer = 0f;
    }

    void ConstruirGarra()
    {
        MeshRenderer[] rootRenderers = GetComponents<MeshRenderer>();
        foreach (MeshRenderer rendererBase in rootRenderers) rendererBase.enabled = false;
        BoxCollider rootCollider = GetComponent<BoxCollider>();
        if (rootCollider != null) rootCollider.enabled = false;

        // Container Pendular para Inércia da Garra
        GameObject visualContainerObj = new GameObject("Garra_Visual_Sway");
        visualContainerObj.transform.SetParent(transform, false);
        clawVisualContainer = visualContainerObj.transform;
        clawVisualContainer.localScale = Vector3.one * 0.58f;

        // Rigidbody cinemático no container para suporte a FixedJoint dos prêmios
        Rigidbody clawRb = visualContainerObj.AddComponent<Rigidbody>();
        clawRb.isKinematic = true;
        clawRb.useGravity = false;

        clawRig = RealisticClawMeshBuilder.Build(clawVisualContainer);
        dentes = clawRig.Prongs;
        carrySocket = clawRig.CarrySocket;
    }

    void ConstruirGabinete()
    {
        Camera mainCam = Camera.main;
        if (mainCam != null)
        {
            mainCam.clearFlags = CameraClearFlags.SolidColor;
            mainCam.backgroundColor = new Color(0.05f, 0.06f, 0.09f, 1.0f);
        }
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.18f, 0.20f, 0.28f);

        GameObject existingCabinet = GameObject.Find("Gabinete_Arcade_Modular");
        if (existingCabinet == null)
        {
            ArcadeCabinetBuilder.Build();
        }
        else
        {
            Debug.Log("[ClawController] Gabinete pré-assado/existente na cena reutilizado.");
        }

        GameObject adesivoVidro = GameObject.Find("Adesivo_Instrucoes_Arcade");
        if (adesivoVidro != null)
        {
            adesivoVidro.SetActive(false);
            Destroy(adesivoVidro);
        }

        pileSpawner = gameObject.AddComponent<PrizePileSpawner>();
        pileSpawner.Build();
        Debug.Log("[ClawController] Spawner físico de pelúcias conectado ao gabinete.");
    }
}