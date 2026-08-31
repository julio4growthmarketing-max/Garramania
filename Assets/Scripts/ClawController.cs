using UnityEngine;

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
    [SerializeField] private float captureRadius = 0.78f;
    [SerializeField] private float maxHorizontalAlignDistance = 0.65f;
    [SerializeField] private float idealVerticalOffset = -0.22f;
    [SerializeField] private float verticalTolerance = 0.55f;
    [SerializeField] private LayerMask prizeLayer;

    [Header("Eletromecânica do Solenoide & Modulação PWM (Engenharia Real)")]
    [SerializeField] private float solenoidMaxVoltage = 1.0f; // 100% pulso de fechamento no solo
    [SerializeField] private float solenoidMinHoldVoltage = 0.38f; // Tensão reduzida PWM durante a subida
    private float currentSolenoidVoltage = 1.0f;
    private float captureQuality = 1.0f;
    private float prizeMass = 1.8f;
    private float frictionCoeff = 0.85f;

    [Header("Grip")]
    [SerializeField, Range(0.3f, 2.0f)] private float baseGripForce = 1.05f;
    [SerializeField] private float gripLossPerSecondWhileMoving = 0.035f;
    [SerializeField] private float gripLossPerSwayDegree = 0.003f;

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

    void Start()
    {
        transform.position = new Vector3(0f, LIM_YMAX, 0f);
        stockManager = PrizeStockManager.Instance;

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
        float floorLimitY = -0.70f;
        float targetY = floorLimitY;

        int pLayer = LayerMask.NameToLayer("Prize");
        int mask = prizeLayer.value != 0 ? prizeLayer.value : (pLayer != -1 ? (1 << pLayer) : ~0);
        RaycastHit hit;
        if (Physics.SphereCast(transform.position, 0.24f, Vector3.down, out hit, 3.5f, mask))
        {
            targetY = Mathf.Max(hit.point.y - 0.22f, floorLimitY);
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
                currentHeldPrize.Body.AddForce(Vector3.down * 3.8f + Random.insideUnitSphere * 1.5f, ForceMode.Impulse);
                currentHeldPrize.Body.AddTorque(Random.insideUnitSphere * 4.0f, ForceMode.Impulse);
            }
            // 70% de chance de soltar imediatamente no início da subida
            if (Random.value < 0.70f)
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
                currentHeldPrize.Body.AddForce(Vector3.down * 2.2f + Random.insideUnitSphere * 1.0f, ForceMode.Impulse);
                currentHeldPrize.Body.AddTorque(Random.insideUnitSphere * 3.0f, ForceMode.Impulse);
            }
            if (Random.value < 0.35f)
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
            Debug.Log("[Claw] LimbTip não resistiu ao tranco do topo!");
            ReleasePrizeWithPhysics();
        }
        else if (currentHeldPrize.CurrentGrabKind == GrabKind.SidePinch)
        {
            if (captureQuality < 0.60f || Random.value < 0.45f)
            {
                Debug.Log($"[Claw] SidePinch ({captureQuality:P0}) desarmado pelo tranco do topo!");
                ReleasePrizeWithPhysics();
            }
        }
        else if (currentHeldPrize.CurrentGrabKind == GrabKind.FirmBasket)
        {
            if (captureQuality < 0.25f)
            {
                ReleasePrizeWithPhysics();
            }
        }
    }

    private void FecharGarraFisica()
    {
        isClosed = true;
        AudioFeedbackController.Instance?.PlaySolenoidClamp();
        if (clawAnimationRoutine != null) StopCoroutine(clawAnimationRoutine);
        clawAnimationRoutine = StartCoroutine(AnimateClaw(0.0f, 0.35f));

        StartCoroutine(ExecuteGrabMidClamp(0.20f));
    }

    private System.Collections.IEnumerator ExecuteGrabMidClamp(float delay)
    {
        yield return new WaitForSeconds(delay);
        TryGrabRealistic();
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
        CaptureEvaluation best = new CaptureEvaluation { score = -1f, isValid = false, kind = GrabKind.None };

        Vector3 prongTipsPos = transform.position + Vector3.down * 0.70f;
        Vector3 clawPos = transform.position;

        int pLayer = LayerMask.NameToLayer("Prize");
        int mask = prizeLayer.value != 0 ? prizeLayer.value : (pLayer != -1 ? (1 << pLayer) : ~0);

        Collider[] hits = Physics.OverlapSphere(prongTipsPos, 0.65f, mask);
        if (hits == null || hits.Length == 0) return best;

        System.Collections.Generic.HashSet<Prize> evaluated = new System.Collections.Generic.HashSet<Prize>();

        // Posições das pontas dos 3 dentes da garra
        Vector3[] tipPositions = new Vector3[3];
        for (int i = 0; i < 3; i++)
        {
            if (dentes != null && i < dentes.Length && dentes[i] != null)
            {
                Transform tip = dentes[i].Find("CurvedBlade_Steel/ProngCollider_Tip");
                tipPositions[i] = tip != null ? tip.position : dentes[i].position + dentes[i].forward * 0.15f + Vector3.down * 0.45f;
            }
            else
            {
                float rad = (i * 120f) * Mathf.Deg2Rad;
                tipPositions[i] = prongTipsPos + new Vector3(Mathf.Sin(rad) * 0.22f, 0f, Mathf.Cos(rad) * 0.22f);
            }
        }

        foreach (var col in hits)
        {
            Prize prize = col.GetComponentInParent<Prize>();
            if (prize == null || evaluated.Contains(prize) || prize.State == PrizeState.Delivered || prize.State == PrizeState.Attached || prize.Body == null) continue;
            evaluated.Add(prize);

            Collider prizeCol = prize.GetComponentInChildren<Collider>();
            Bounds bounds = prizeCol != null ? prizeCol.bounds : new Bounds(prize.transform.position, Vector3.one * 0.4f);
            Vector3 prizeCoM = prize.Body != null ? prize.Body.worldCenterOfMass : prize.transform.position;

            // Contagem de prongs que tocam/envolvem este prêmio
            int prongsTouching = 0;
            Vector3 avgContact = Vector3.zero;

            for (int i = 0; i < 3; i++)
            {
                Vector3 closest = prizeCol != null ? prizeCol.ClosestPoint(tipPositions[i]) : prizeCoM;
                float d = Vector3.Distance(tipPositions[i], closest);
                if (d < 0.28f)
                {
                    prongsTouching++;
                    avgContact += closest;
                }
            }

            if (prongsTouching > 0)
            {
                avgContact /= prongsTouching;
            }
            else
            {
                avgContact = prizeCol != null ? prizeCol.ClosestPoint(prongTipsPos) : prizeCoM;
            }

            float horizDist = Vector2.Distance(
                new Vector2(clawPos.x, clawPos.z),
                new Vector2(prizeCoM.x, prizeCoM.z)
            );

            // Altura relativa do contato no corpo da pelúcia (0 = pés/base, 1 = topo/cabeça)
            float relHeight = Mathf.Clamp01((avgContact.y - bounds.min.y) / Mathf.Max(0.01f, bounds.size.y));
            float horizontalAlign = 1f - Mathf.Clamp01(horizDist / 0.48f);
            float verticalProximity = 1f - Mathf.Clamp01(Mathf.Abs(prizeCoM.y - prongTipsPos.y) / 0.60f);

            GrabKind kind = GrabKind.None;
            float score = 0f;

            // Decisão precisa do GrabKind baseada em geometria de contato
            if (PlayerEconomyManager.Instance != null && PlayerEconomyManager.Instance.IsGoldenClawActive)
            {
                kind = GrabKind.FirmBasket;
                score = 1.0f;
            }
            else if (prongsTouching == 3 && relHeight >= 0.48f && horizDist <= 0.26f)
            {
                kind = GrabKind.FirmBasket;
                score = Mathf.Lerp(0.72f, 0.98f, horizontalAlign * 0.7f + verticalProximity * 0.3f);
            }
            else if (prongsTouching >= 2 || (prongsTouching == 3 && relHeight < 0.48f))
            {
                kind = GrabKind.SidePinch;
                score = Mathf.Lerp(0.42f, 0.68f, horizontalAlign * 0.6f + verticalProximity * 0.4f);
            }
            else if (prongsTouching == 1 || (prongsTouching >= 1 && horizDist > 0.34f))
            {
                kind = GrabKind.LimbTip;
                score = Mathf.Lerp(0.22f, 0.39f, horizontalAlign * 0.5f + verticalProximity * 0.5f);
            }
            else if (horizDist < 0.45f)
            {
                kind = GrabKind.ShoveOnly;
                score = 0.12f;
            }

            if (score > best.score)
            {
                best.prize = prize;
                best.kind = kind;
                best.score = score;
                best.horizontalAlign = horizontalAlign;
                best.verticalProximity = verticalProximity;
                best.prongCount = prongsTouching;
                best.contactPoint = avgContact;
                best.stability = kind == GrabKind.FirmBasket ? 1f : (kind == GrabKind.SidePinch ? 0.5f : 0.2f);
                best.isValid = kind != GrabKind.None && kind != GrabKind.ShoveOnly;
            }
        }

        return best;
    }

    private void TryGrabRealistic()
    {
        CaptureEvaluation eval = EvaluateBestCandidate();

        // Se encostou de raspão (ShoveOnly): empurra a pelúcia no monte e faz o monte se mexer!
        if (eval.kind == GrabKind.ShoveOnly && eval.prize != null && eval.prize.Body != null)
        {
            Vector3 shoveDir = (eval.prize.transform.position - transform.position).normalized + Vector3.down * 0.4f;
            eval.prize.Body.AddForce(shoveDir * 2.8f, ForceMode.Impulse);
            eval.prize.Body.AddTorque(Random.insideUnitSphere * 3.5f, ForceMode.Impulse);

            if (clawAnimationRoutine != null) StopCoroutine(clawAnimationRoutine);
            clawAnimationRoutine = StartCoroutine(AnimateClaw(0.22f, 0.15f));

            AudioFeedbackController.Instance?.PlayClank();
            AudioFeedbackController.Instance?.PlayNearMiss();
            GameJuice.Instance?.HapticsLight();

            currentHeldPrize = null;
            premioAgarrado = null;
            OnGrabAttempt?.Invoke(false);
            Debug.Log("[Claw] Dentes rasparam no prêmio (ShoveOnly) — monte empurrado.");
            return;
        }

        if (!eval.isValid || eval.prize == null)
        {
            currentHeldPrize = null;
            premioAgarrado = null;
            OnGrabAttempt?.Invoke(false);
            AudioFeedbackController.Instance?.PlayClank();
            GameJuice.Instance?.HapticsLight();
            if (eval.score > 0.10f) AudioFeedbackController.Instance?.PlayNearMiss();
            return;
        }

        // Parâmetros de Captura
        captureQuality = eval.score;
        currentSolenoidVoltage = solenoidMaxVoltage;
        prizeMass = eval.prize.Body != null ? eval.prize.Body.mass : 1.8f;
        frictionCoeff = 0.85f;

        if (PlayerEconomyManager.Instance != null && PlayerEconomyManager.Instance.IsGoldenClawActive)
        {
            eval.kind = GrabKind.FirmBasket;
            eval.score = Mathf.Max(eval.score, 0.95f);
            captureQuality = 1.0f;
            frictionCoeff = 1.8f;
            PlayerEconomyManager.Instance.ConsumeGoldenToken();
            Debug.Log("[ClawController] 🌟 FICHA DOURADA ATIVA! FirmBasket forçado!");
        }

        baseGripForce = 1.05f;
        currentGripForce = baseGripForce * clawForce * Mathf.Lerp(0.85f, 1.20f, eval.score);
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

        // Animação proporcional da garra: cesto fecha mais (0.08), pinch fecha na ponta (0.18)
        float targetClamp = (eval.kind == GrabKind.FirmBasket) ? 0.08f : 0.18f;
        if (clawAnimationRoutine != null) StopCoroutine(clawAnimationRoutine);
        clawAnimationRoutine = StartCoroutine(AnimateClaw(targetClamp, 0.20f));

        OnGrabAttempt?.Invoke(true);
        AudioFeedbackController.Instance?.PlayGrabSuccess();

        if (eval.kind == GrabKind.FirmBasket)
        {
            GameJuice.Instance?.HapticsMedium();
            GameJuice.Instance?.PunchScale(currentHeldPrize.transform, 1.10f, 0.20f);
            GameJuice.Instance?.PlaySparkles(transform.position);
        }
        else if (eval.kind == GrabKind.SidePinch)
        {
            GameJuice.Instance?.HapticsMedium();
            GameJuice.Instance?.ScreenShake(0.09f, 0.10f);
        }
        else // LimbTip
        {
            GameJuice.Instance?.HapticsSlip(); // Feedback imediato de instabilidade
        }

        var cam = FindFirstObjectByType<ClawCameraController>();
        if (cam != null)
        {
            cam.Shake(0.10f, 0.12f);
            cam.PunchFOV(0.5f);
        }

        Debug.Log($"[Claw] Captura iniciada! Tipo: {eval.kind} | Qualidade: {eval.score:P0} | Prongs: {eval.prongCount} | {currentHeldPrize.prizeId}");
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
                maxSlipDuration = 0.70f;
                currentGripLossPerSec = 0.035f;
                currentGripLossPerSway = 0.003f;
                break;
            case GrabKind.SidePinch:
                maxSlipDuration = 0.35f;
                currentGripLossPerSec = 0.060f;
                currentGripLossPerSway = 0.006f; // x2
                break;
            case GrabKind.LimbTip:
                maxSlipDuration = 0.18f;
                currentGripLossPerSec = 0.100f;
                currentGripLossPerSway = 0.009f; // x3
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

        Prize p = currentHeldPrize != null ? currentHeldPrize : (premioAgarrado != null ? premioAgarrado.GetComponent<Prize>() : null);
        if (p != null)
        {
            currentHeldPrize = null;
            premioAgarrado = null;
            isSlipping = false;
            slipTimer = 0f;

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
        
        if (currentHeldPrize != null)
        {
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