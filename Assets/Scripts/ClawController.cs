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
    private TrailRenderer trailRenderer;
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
    [SerializeField, Range(0.3f, 2.0f)] private float baseGripForce = 1.30f;
    [SerializeField] private float gripLossPerSecondWhileMoving = 0.035f;
    [SerializeField] private float gripLossPerSwayDegree = 0.003f;

    private float currentGripForce;
    private Prize currentHeldPrize;
    private float slipTimer;
    private bool isSlipping;

    public struct CaptureEvaluation
    {
        public Prize prize;
        public float score;
        public float horizontalAlign;
        public float verticalProximity;
        public float stability;
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
    private bool prizeBoardBuilt;

    private const int INITIAL_BOARD_COUNT = 72;
    private const float PRIZE_FLOOR_Y = -1.325f;
    private const float PRIZE_AREA_HALF_EXTENT = 1.38f;

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
        
        // Cabo de Aço Realista 3D (Cilindro Metálico com espessura e acabamento industrial)
        cable3D = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        cable3D.name = "Cabo_Aco_Guindaste_3D";
        Destroy(cable3D.GetComponent<Collider>());
        
        Material mCable = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
        mCable.color = new Color(0.76f, 0.78f, 0.82f, 1.0f); // Aço galvanizado trançado com brilho real
        mCable.SetFloat("_Metallic", 0.95f);
        mCable.SetFloat("_Smoothness", 0.80f);
        cable3D.GetComponent<MeshRenderer>().material = mCable;

        // LineRenderer auxiliar para antialiasing e renderização nítida em qualquer ângulo
        cable = gameObject.AddComponent<LineRenderer>();
        cable.startWidth = 0.024f;
        cable.endWidth = 0.024f;
        Shader sLine = Shader.Find("Universal Render Pipeline/Unlit") 
                    ?? Shader.Find("Sprites/Default") 
                    ?? Shader.Find("Universal Render Pipeline/Particles/Unlit");
        cable.material = new Material(sLine != null ? sLine : Shader.Find("Hidden/InternalErrorShader"));
        cable.material.color = new Color(0.82f, 0.85f, 0.90f, 1.0f); // Cabo prateado visível
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

        // Fliperama Autêntico: jogador manobra a garra no plano horizontal X / Z
        Vector3 nova = transform.position + new Vector3(moveInput.x, 0f, moveInput.z) * 2.8f * Time.deltaTime;
        nova.x = Mathf.Clamp(nova.x, -LIM_X, LIM_X);
        nova.z = Mathf.Clamp(nova.z, -LIM_Z, LIM_Z);
        nova.y = LIM_YMAX; // Sempre na altura padrão de mira no topo da vitrine!
        transform.position = nova;

        // 🔊 JUICE: Som do servo ao mover
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
        Transform anchor = carrySocket != null ? carrySocket : clawVisualContainer;
        if (anchor != null)
        {
            prizeToTrack.transform.SetPositionAndRotation(anchor.position, anchor.rotation);
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

        clawVisualContainer.localRotation = Quaternion.Euler(currentSwayAngle.x, 0f, currentSwayAngle.y);
    }

    /// <summary>
    /// Inicia o ciclo real de fliperama:
    /// Desce -> Agarra com física -> Sobe ao teto -> Viaja até a calha -> Solta o prêmio no duto -> Retorna ao centro.
    /// </summary>
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
        AudioFeedbackController.Instance?.SetMotorMoving(false);
        CabinetLightingController.Instance?.SetDramaticFocus(true);
        GameSession.Instance?.SetState(GameState.Capturing);
        OnClawStateChanged?.Invoke(true);

        // 1. FASE DE DESCIDA E IMPACTO (Garra desce ABERTA)
        // Limite de piso para a garra compacta (escala 0.58x, prongs medem ~0.60m)
        float floorLimitY = -0.70f;
        float targetY = floorLimitY;

        int pLayer = LayerMask.NameToLayer("Prize");
        int mask = prizeLayer.value != 0 ? prizeLayer.value : (pLayer != -1 ? (1 << pLayer) : ~0);
        RaycastHit hit;
        if (Physics.SphereCast(transform.position, 0.24f, Vector3.down, out hit, 3.5f, mask))
        {
            // Desce até as pinças abraçarem o corpo da pelúcia (alinhando o berço ao centro de massa)
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

        // 2. FASE DE FECHAMENTO (Pulso elétrico solenoide N_max)
        FecharGarraFisica();
        yield return new WaitForSeconds(0.45f);

        // 3. FASE DE ELEVAÇÃO (Z) COM REDUÇÃO DE VOLTAGEM PWM & INÉRCIA
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

            // PWM: enfraquecimento magnético gradual do solenoide durante a subida
            UpdateSolenoidPWM(ascentProgress);
            UpdateHeldPrizePhysics();
            AtualizarCabo();
            yield return null;
        }

        // TRANCO DE TOPO: Aceleração/desaceleração inercial brusca ao atingir LIM_YMAX
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
        // Fase 3: Modulação PWM com retenção de voltagem segura para sustentação na subida
        float targetHold = Mathf.Lerp(0.85f, 1.0f, captureQuality);
        currentSolenoidVoltage = Mathf.Lerp(solenoidMaxVoltage, targetHold, Mathf.SmoothStep(0f, 1f, ascentProgress));
    }

    private void ApplyTopJerkInertia()
    {
        if (currentHeldPrize == null) return;

        // Tranco Inercial no topo da subida: só solta se a captura foi extremamente raspada na ponta
        if (captureQuality < 0.28f)
        {
            Debug.Log($"[Claw] Captura raspada na ponta não resistiu ao tranco do topo! ({captureQuality:P0})");
            ReleasePrizeWithPhysics();
            AudioFeedbackController.Instance?.PlaySlipStart();
            GameJuice.Instance?.HapticsSlip();
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

        // Se capturou um prêmio, as pinças curvadas ajustam o abraço firme no corpo da pelúcia
        if (currentHeldPrize != null)
        {
            if (clawAnimationRoutine != null) StopCoroutine(clawAnimationRoutine);
            clawAnimationRoutine = StartCoroutine(AnimateClaw(0.12f, 0.20f));
        }
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

    private CaptureEvaluation EvaluateBestCandidate()
    {
        CaptureEvaluation best = new CaptureEvaluation { score = -1f, isValid = false };

        // O ponto de contato são as pontas das pinças que descem e beliscam a pelúcia
        Vector3 prongTipsPos = transform.position + Vector3.down * 0.70f;
        Vector3 clawPos = transform.position;

        int pLayer = LayerMask.NameToLayer("Prize");
        int mask = prizeLayer.value != 0 ? prizeLayer.value : (pLayer != -1 ? (1 << pLayer) : ~0);

        // Perímetro natural de captura abrangendo as 3 pinças abertas (raio 0.65m)
        Collider[] hits = Physics.OverlapSphere(prongTipsPos, 0.65f, mask);
        if (hits == null || hits.Length == 0) return best;

        System.Collections.Generic.HashSet<Prize> evaluated = new System.Collections.Generic.HashSet<Prize>();

        foreach (var col in hits)
        {
            Prize prize = col.GetComponentInParent<Prize>();
            if (prize == null || evaluated.Contains(prize) || prize.State == PrizeState.Delivered || prize.State == PrizeState.Attached || prize.Body == null) continue;
            evaluated.Add(prize);

            Vector3 prizeCoM = prize.Body != null ? prize.Body.worldCenterOfMass : prize.transform.position;

            // Distância radial ao eixo vertical da garra
            float horizDist = Vector2.Distance(
                new Vector2(clawPos.x, clawPos.z),
                new Vector2(prizeCoM.x, prizeCoM.z)
            );

            // Se estiver além do alcance das pinças (0.52m), desconsidera
            if (horizDist > 0.52f) continue;

            float horizontalAlign = 1f - Mathf.Clamp01(horizDist / 0.52f);
            float verticalDelta = Mathf.Abs(prizeCoM.y - prongTipsPos.y);
            float verticalProximity = 1f - Mathf.Clamp01(verticalDelta / 0.65f);

            // Qualidade composta da captura
            float score = (horizontalAlign * 0.65f) + (verticalProximity * 0.35f);

            if (score > best.score)
            {
                best.prize = prize;
                best.score = score;
                best.horizontalAlign = horizontalAlign;
                best.verticalProximity = verticalProximity;
                best.stability = 1f;
                best.isValid = score >= 0.28f;
            }
        }

        return best;
    }

    private void TryGrabRealistic()
    {
        CaptureEvaluation eval = EvaluateBestCandidate();

        if (!eval.isValid || eval.prize == null)
        {
            currentHeldPrize = null;
            premioAgarrado = null;
            OnGrabAttempt?.Invoke(false);
            AudioFeedbackController.Instance?.PlayClank();
            GameJuice.Instance?.HapticsLight();
            if (eval.score > 0.15f) AudioFeedbackController.Instance?.PlayNearMiss();
            return;
        }

        // Parâmetros de Captura
        captureQuality = eval.score;
        currentSolenoidVoltage = solenoidMaxVoltage;
        prizeMass = eval.prize.Body != null ? eval.prize.Body.mass : 1.8f;
        frictionCoeff = 0.85f;

        if (PlayerEconomyManager.Instance != null && PlayerEconomyManager.Instance.IsGoldenClawActive)
        {
            eval.score = Mathf.Max(eval.score, 0.95f);
            captureQuality = 1.0f;
            frictionCoeff = 1.8f;
            PlayerEconomyManager.Instance.ConsumeGoldenToken();
            Debug.Log("[ClawController] 🌟 FICHA DOURADA ATIVA! Força magnética máxima calibrada!");
        }

        currentGripForce = baseGripForce * clawForce * Mathf.Lerp(0.85f, 1.20f, eval.score);
        currentHeldPrize = eval.prize;
        premioAgarrado = eval.prize.gameObject;

        Transform anchor = carrySocket != null ? carrySocket : (clawVisualContainer != null ? clawVisualContainer : transform);
        currentHeldPrize.Attach(
            anchor,
            eval.score,
            currentGripForce
        );

        isSlipping = false;
        slipTimer = 0f;

        OnGrabAttempt?.Invoke(true);
        AudioFeedbackController.Instance?.PlayGrabSuccess();
        GameJuice.Instance?.HapticsMedium();
        GameJuice.Instance?.ScreenShake(0.12f, 0.08f);
        GameJuice.Instance?.PunchScale(currentHeldPrize.transform, 1.12f, 0.20f);
        GameJuice.Instance?.PlaySparkles(transform.position);

        var cam = FindFirstObjectByType<ClawCameraController>();
        if (cam != null)
        {
            cam.Shake(0.11f, 0.13f);
            cam.PunchFOV(0.7f);
        }

        Debug.Log($"[Claw] Captura bem-sucedida! Score: {eval.score:P0} | Solenoide: {currentSolenoidVoltage:P0} | {currentHeldPrize.prizeId}");
    }

    private void UpdateHeldPrizePhysics()
    {
        if (currentHeldPrize == null) return;
        if (currentHeldPrize.State == PrizeState.Delivered || currentHeldPrize.State == PrizeState.Dropped) return;

        // Inércia vertical e peso: F_down = m * g
        float fDown = prizeMass * 9.81f;

        // Força centrífuga e oscilações pendulares
        float swayRad = currentSwayAngle.magnitude * Mathf.Deg2Rad;
        float fLateral = prizeMass * (9.81f * Mathf.Sin(swayRad) + swayVelocity.sqrMagnitude * 0.20f);

        // Suporte mecânico do cesto formado pelas 3 pinças curvadas:
        // As lâminas de aço curvadas sustentam o prêmio por baixo
        float basketSupport = 35.0f * Mathf.Clamp01(captureQuality + 0.30f);
        float frictionHold = frictionCoeff * (baseGripForce * 22.0f * currentSolenoidVoltage * captureQuality);
        float totalHold = basketSupport + frictionHold;

        // Se a captura foi segura (score >= 0.28f), o cesto transporta com sucesso
        bool stillHolding = captureQuality >= 0.28f || (fDown + fLateral) <= totalHold;

        // Efeito visual: a pelúcia balança naturalmente acompanhando o berço das pinças
        if (currentHeldPrize != null && currentHeldPrize.State == PrizeState.Attached)
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

                AudioFeedbackController.Instance?.PlaySlipStart();
                GameJuice.Instance?.HapticsSlip();
                var cam = FindFirstObjectByType<ClawCameraController>();
                cam?.Shake(0.07f, 0.45f);

                Debug.Log($"[Claw] Instabilidade Mecânica: Objeto escorregando.");
            }

            slipTimer += Time.deltaTime;
            if (slipTimer > 0.45f)
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

        AudioFeedbackController.Instance?.PlayDropThud();
        GameJuice.Instance?.HapticsHeavy();
        GameJuice.Instance?.ScreenShake(0.18f, 0.12f);

        var cam = FindFirstObjectByType<ClawCameraController>();
        cam?.Shake(0.17f, 0.2f);

        SetTrailColorDefault();

        Debug.Log("[Claw] Prêmio escorregou e caiu.");
    }

    private void AbrirGarraFisica()
    {
        isClosed = false;
        AudioFeedbackController.Instance?.PlaySolenoidRelease();
        if (clawAnimationRoutine != null) StopCoroutine(clawAnimationRoutine);
        clawAnimationRoutine = StartCoroutine(AnimateClaw(1.0f, 0.35f));

        SetTrailColorDefault();

        Prize p = currentHeldPrize != null ? currentHeldPrize : (premioAgarrado != null ? premioAgarrado.GetComponent<Prize>() : null);
        if (p != null)
        {
            currentHeldPrize = null;
            premioAgarrado = null;
            isSlipping = false;
            slipTimer = 0f;

            // Marca o prêmio como entregue e registra a vitória na GameSession
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
        SetTrailColorDefault();
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

    // ====== CONSTRUÇÃO VISUAL DA GARRA MECÂNICA 3D (FAB.COM STYLE) ======
    void ConstruirGarra()
    {
        // Esconde o cubo original da cena: a garra é formada pelos elementos de alta fidelidade
        MeshRenderer[] rootRenderers = GetComponents<MeshRenderer>();
        foreach (MeshRenderer rendererBase in rootRenderers) rendererBase.enabled = false;
        BoxCollider rootCollider = GetComponent<BoxCollider>();
        if (rootCollider != null) rootCollider.enabled = false;

        // Container Pendular para Inércia da Garra
        GameObject visualContainerObj = new GameObject("Garra_Visual_Sway");
        visualContainerObj.transform.SetParent(transform, false);
        clawVisualContainer = visualContainerObj.transform;
        // Escala compacta proporcional (58% do tamanho anterior = ~1/3 do volume da garra antiga)
        clawVisualContainer.localScale = Vector3.one * 0.58f;

        // Constrói a nova garra profissional com lâminas curvadas em aço, pistão central e bielas articuladas
        clawRig = RealisticClawMeshBuilder.Build(clawVisualContainer);
        dentes = clawRig.Prongs;
        carrySocket = clawRig.CarrySocket;
    }

    void ConstruirGabinete()
    {
        // 0. ATMOSFERA ARCADE JAPONÊS (Fundo escuro estiloso, sem void cinza)
        Camera mainCam = Camera.main;
        if (mainCam != null)
        {
            mainCam.clearFlags = CameraClearFlags.SolidColor;
            mainCam.backgroundColor = new Color(0.05f, 0.06f, 0.09f, 1.0f);
        }
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.18f, 0.20f, 0.28f);

        // 1. GABINETE MODULAR ARQUITETURAL DE ALTA FIDELIDADE (Reutiliza existente ou cria fallback)
        GameObject existingCabinet = GameObject.Find("Gabinete_Arcade_Modular");
        if (existingCabinet == null)
        {
            ArcadeCabinetBuilder.Build();
        }
        else
        {
            Debug.Log("[ClawController] Gabinete pré-assado/existente na cena reutilizado.");
        }

        // 2. MONTE DE PELÚCIAS: gerenciado pelo PrizePileSpawner físico dedicado
        pileSpawner = gameObject.AddComponent<PrizePileSpawner>();
        pileSpawner.Build();
        Debug.Log("[ClawController] Spawner físico de pelúcias conectado ao gabinete.");
    }

    void ConfigurarRastroLuminoso()
    {
        // Garra 100% mecânica: sem luzes neon ou rastros
    }

    void SetTrailColorDefault()
    {
    }

    void SetTrailColorGrabbing()
    {
    }
}