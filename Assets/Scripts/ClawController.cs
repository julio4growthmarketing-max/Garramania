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
    private TrailRenderer trailRenderer;

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
        
        cable = gameObject.AddComponent<LineRenderer>();
        cable.startWidth = 0.05f;
        cable.endWidth = 0.05f;
        cable.material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        cable.material.color = Color.black;
        cable.positionCount = 2;

        ConfigurarRastroLuminoso();

        // A UI é inicializada pelo bootstrap da sessão.
        // O controlador da garra não deve destruir nem criar sistemas de apresentação.
    }

    private void OnDestroy()
    {
        // O PrizePileSpawner possui a própria inscrição no evento de reposição.
        // O controlador da garra não deve construir nem repor prêmios.
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
        if (isExecutingCycle) return;
        if (GameSession.Instance != null && !GameSession.Instance.CanMoveClaw()) return;

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

        // Fliperama Autêntico: jogador manobra a garra no plano horizontal X / Z
        Vector3 nova = transform.position + new Vector3(moveInput.x, 0f, moveInput.z) * 2.8f * Time.deltaTime;
        nova.x = Mathf.Clamp(nova.x, -LIM_X, LIM_X);
        nova.z = Mathf.Clamp(nova.z, -LIM_Z, LIM_Z);
        nova.y = LIM_YMAX; // Sempre na altura padrão de mira no topo da vitrine!
        transform.position = nova;

        // 🔊 JUICE: Som do servo ao mover
        if (moveInput.sqrMagnitude > 0.01f && servoSoundTimer <= 0f)
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
        GameSession.Instance?.SetState(GameState.Capturing);
        OnClawStateChanged?.Invoke(true);

        // 1. FASE DE DESCIDA: desce continuamente até mergulhar fundo nas pelúcias (-0.92f)
        float targetY = -0.92f;
        while (transform.position.y > targetY + 0.02f)
        {
            // Se detectar prêmio abaixo da garra quando já estiver baixo, envolve e fecha
            int pLayer = LayerMask.NameToLayer("Prize");
            int mask = prizeLayer.value != 0 ? prizeLayer.value : (pLayer != -1 ? (1 << pLayer) : ~0);
            if (transform.position.y <= -0.80f && Physics.CheckSphere(transform.position + Vector3.down * 0.35f, 0.30f, mask))
            {
                transform.position = Vector3.MoveTowards(transform.position, 
                    new Vector3(transform.position.x, Mathf.Max(targetY, transform.position.y - 0.06f), transform.position.z), 
                    2.0f * Time.deltaTime);
                AtualizarCabo();
                yield return new WaitForSeconds(0.08f);
                break;
            }

            transform.position = Vector3.MoveTowards(transform.position, 
                new Vector3(transform.position.x, targetY, transform.position.z), 
                3.0f * Time.deltaTime);

            AtualizarCabo();
            yield return null;
        }

        yield return new WaitForSeconds(0.20f);

        // 2. FASE DE FECHAMENTO & AGARRE MECÂNICO
        FecharGarraFisica();
        yield return new WaitForSeconds(0.35f);

        // 3. FASE DE SUBIDA COM A PELÚCIA PRESA NA GARRA
        GameSession.Instance?.SetState(GameState.Returning);
        while (transform.position.y < LIM_YMAX - 0.04f)
        {
            transform.position = Vector3.MoveTowards(transform.position, 
                new Vector3(transform.position.x, LIM_YMAX, transform.position.z), 
                2.6f * Time.deltaTime);

            UpdateHeldPrizePhysics();
            AtualizarCabo();
            yield return null;
        }

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

    private void FecharGarraFisica()
    {
        isClosed = true;
        if (dentes != null)
        {
            foreach (var d in dentes) d.localRotation = Quaternion.Euler(0, d.localEulerAngles.y, 10f);
        }

        TryGrabRealistic();
    }

    private CaptureEvaluation EvaluateBestCandidate()
    {
        CaptureEvaluation best = new CaptureEvaluation { score = -1f, isValid = false };

        Vector3 capturePoint = carrySocket != null ? carrySocket.position : (transform.position + Vector3.down * 0.35f);
        Vector3 clawPos = transform.position;
        float clawSpeed = clawVelocity.magnitude;

        if (prizeLayer.value == 0)
        {
            int pLayer = LayerMask.NameToLayer("Prize");
            if (pLayer != -1) prizeLayer = 1 << pLayer;
        }

        Collider[] hits = prizeLayer.value != 0 ? Physics.OverlapSphere(capturePoint, captureRadius, prizeLayer) : Physics.OverlapSphere(capturePoint, captureRadius);
        if (hits == null || hits.Length == 0) hits = Physics.OverlapSphere(capturePoint, captureRadius);

        System.Collections.Generic.HashSet<Prize> evaluated = new System.Collections.Generic.HashSet<Prize>();

        if (hits != null && hits.Length > 0)
        {
            foreach (var col in hits)
            {
                Prize prize = col.GetComponentInParent<Prize>();
                if (prize == null || evaluated.Contains(prize) || prize.State == PrizeState.Delivered || prize.State == PrizeState.Attached || prize.Body == null) continue;
                evaluated.Add(prize);

                Vector3 prizePos = prize.transform.position;
                float horizDist = Vector2.Distance(
                    new Vector2(capturePoint.x, capturePoint.z),
                    new Vector2(prizePos.x, prizePos.z)
                );
                float horizontalAlign = 1f - Mathf.Clamp01(horizDist / maxHorizontalAlignDistance);

                float verticalDelta = Mathf.Abs(prizePos.y - capturePoint.y);
                float verticalProximity = 1f - Mathf.Clamp01(verticalDelta / verticalTolerance);

                float stability = 1f - Mathf.Clamp01(clawSpeed / 2.2f);

                float score = (horizontalAlign * 0.55f) + (verticalProximity * 0.30f) + (stability * 0.15f);

                if (score > best.score)
                {
                    best.prize = prize;
                    best.score = score;
                    best.horizontalAlign = horizontalAlign;
                    best.verticalProximity = verticalProximity;
                    best.stability = stability;
                    best.isValid = score > 0.15f;
                }
            }
        }

        // Fallback Infalível: se o OverlapSphere da física não pegou o colisor por problema de camada ou escala, busca na área
        if (!best.isValid || best.prize == null)
        {
            Prize[] allPrizes = Object.FindObjectsByType<Prize>(FindObjectsSortMode.None);
            foreach (var prize in allPrizes)
            {
                if (prize == null || evaluated.Contains(prize) || prize.State == PrizeState.Delivered || prize.State == PrizeState.Attached || prize.Body == null) continue;

                Vector3 prizePos = prize.transform.position;
                float horizDist = Vector2.Distance(
                    new Vector2(capturePoint.x, capturePoint.z),
                    new Vector2(prizePos.x, prizePos.z)
                );
                float verticalDist = Mathf.Abs(prizePos.y - capturePoint.y);

                if (horizDist > maxHorizontalAlignDistance || verticalDist > verticalTolerance) continue;

                float horizontalAlign = 1f - Mathf.Clamp01(horizDist / maxHorizontalAlignDistance);
                float verticalProximity = 1f - Mathf.Clamp01(verticalDist / verticalTolerance);
                float stability = 1f - Mathf.Clamp01(clawSpeed / 2.2f);

                float score = (horizontalAlign * 0.55f) + (verticalProximity * 0.30f) + (stability * 0.15f);

                if (score > best.score)
                {
                    best.prize = prize;
                    best.score = score;
                    best.horizontalAlign = horizontalAlign;
                    best.verticalProximity = verticalProximity;
                    best.stability = stability;
                    best.isValid = score > 0.15f;
                }
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
            if (eval.score > 0.08f) AudioFeedbackController.Instance?.PlayNearMiss();
            return;
        }

        currentGripForce = baseGripForce * clawForce * Mathf.Lerp(0.80f, 1.25f, eval.score);
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
        GameJuice.Instance?.PunchScale(currentHeldPrize.transform, 1.18f, 0.22f);
        GameJuice.Instance?.PlaySparkles(transform.position);
        SetTrailColorGrabbing();

        // Câmera
        var cam = FindFirstObjectByType<ClawCameraController>();
        if (cam != null)
        {
            cam.Shake(0.11f, 0.13f);
            cam.PunchFOV(0.7f);
        }

        Debug.Log($"[Claw] Captura firme qualidade {eval.score:P0} | Grip {currentGripForce:F2} | {currentHeldPrize.prizeId}");
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

                AudioFeedbackController.Instance?.PlaySlipStart();
                GameJuice.Instance?.HapticsSlip();

                var cam = FindFirstObjectByType<ClawCameraController>();
                cam?.Shake(0.07f, 0.45f);

                Debug.Log("[Claw] Prêmio começou a escorregar!");
            }

            slipTimer += Time.deltaTime;

            if (slipTimer > 0.85f)
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
        if (dentes != null)
        {
            foreach (var d in dentes) d.localRotation = Quaternion.Euler(0, d.localEulerAngles.y, 45f);
        }

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
        if (cable != null)
        {
            Vector3 topAnchor = new Vector3(transform.position.x, LIM_YMAX, transform.position.z);
            Vector3 bottomAnchor = clawVisualContainer != null
                ? clawVisualContainer.TransformPoint(new Vector3(0, 0.38f, 0))
                : transform.position;
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
        if (dentes != null) foreach (var d in dentes) d.localRotation = Quaternion.Euler(0, d.localEulerAngles.y, 45f);
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

    // ====== CONSTRUÇÃO VISUAL DA GARRA MECÂNICA 3D ======
    void ConstruirGarra()
    {
        // Esconde o cubo original da cena: a garra é formada pelos elementos abaixo.
        // Fazemos isso de forma explícita porque o bloco-base era o objeto que aparecia no lugar da garra.
        MeshRenderer[] rootRenderers = GetComponents<MeshRenderer>();
        foreach (MeshRenderer rendererBase in rootRenderers) rendererBase.enabled = false;
        BoxCollider rootCollider = GetComponent<BoxCollider>();
        if (rootCollider != null) rootCollider.enabled = false;

        // Materiais PBR Mecânicos
        Material mCromo = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mCromo.color = new Color(0.92f, 0.94f, 0.98f);
        mCromo.SetFloat("_Metallic", 0.95f);
        mCromo.SetFloat("_Smoothness", 0.92f);

        Material mChassisPreto = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mChassisPreto.color = new Color(0.12f, 0.13f, 0.16f);
        mChassisPreto.SetFloat("_Metallic", 0.5f);
        mChassisPreto.SetFloat("_Smoothness", 0.75f);

        Material mDouradoPistao = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mDouradoPistao.color = new Color(1.0f, 0.82f, 0.25f);
        mDouradoPistao.SetFloat("_Metallic", 0.92f);
        mDouradoPistao.SetFloat("_Smoothness", 0.88f);

        Material mBorrachaVermelha = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mBorrachaVermelha.color = new Color(0.92f, 0.15f, 0.20f);
        mBorrachaVermelha.SetFloat("_Smoothness", 0.45f);

        Material mNeonRing = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mNeonRing.color = new Color(0f, 0.95f, 1f);
        mNeonRing.EnableKeyword("_EMISSION");
        mNeonRing.SetColor("_EmissionColor", new Color(0f, 0.95f, 1f) * 3.2f);

        // Container Pendular para Inércia da Garra
        GameObject visualContainerObj = new GameObject("Garra_Visual_Sway");
        visualContainerObj.transform.SetParent(transform, false);
        clawVisualContainer = visualContainerObj.transform;

        // 1. CABEÇOTE CENTRAL
        GameObject carcase = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        carcase.name = "Garra_Carcaça";
        carcase.transform.SetParent(clawVisualContainer, false);
        carcase.transform.localPosition = new Vector3(0, 0.18f, 0);
        carcase.transform.localScale = new Vector3(0.52f, 0.18f, 0.52f);
        carcase.GetComponent<MeshRenderer>().material = mCromo;
        Destroy(carcase.GetComponent<Collider>());

        // Anel Neon de Status no centro da garra
        GameObject ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        ring.name = "Garra_Anel_Neon";
        ring.transform.SetParent(clawVisualContainer, false);
        ring.transform.localPosition = new Vector3(0, 0.18f, 0);
        ring.transform.localScale = new Vector3(0.55f, 0.04f, 0.55f);
        ring.GetComponent<MeshRenderer>().material = mNeonRing;
        Destroy(ring.GetComponent<Collider>());

        // Tampa Superior Cônica
        GameObject topCone = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        topCone.name = "Garra_Tampa_Top";
        topCone.transform.SetParent(clawVisualContainer, false);
        topCone.transform.localPosition = new Vector3(0, 0.30f, 0);
        topCone.transform.localScale = new Vector3(0.35f, 0.08f, 0.35f);
        topCone.GetComponent<MeshRenderer>().material = mChassisPreto;
        Destroy(topCone.GetComponent<Collider>());

        // Olhal de Aço onde prende o cabo
        GameObject eyelet = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        eyelet.name = "Garra_Olhal_Cabo";
        eyelet.transform.SetParent(clawVisualContainer, false);
        eyelet.transform.localPosition = new Vector3(0, 0.38f, 0);
        eyelet.transform.localScale = new Vector3(0.12f, 0.12f, 0.12f);
        eyelet.GetComponent<MeshRenderer>().material = mCromo;
        Destroy(eyelet.GetComponent<Collider>());

        // Socket central do prêmio: pertence à cabeça móvel da garra, não a um dente.
        // O prêmio capturado acompanha este transform durante subida e transporte.
        GameObject carrySocketObj = new GameObject("CarrySocket_Premio");
        carrySocketObj.transform.SetParent(clawVisualContainer, false);
        // Centro físico entre as pontas dos dentes; o prêmio não fica preso na carcaça.
        carrySocketObj.transform.localPosition = new Vector3(0f, -0.42f, 0f);
        carrySocketObj.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
        carrySocket = carrySocketObj.transform;

        // 2. OS 3 DENTES ARTICULADOS COM PISTÕES E PONTAS EMBORRACHADAS
        dentes = new Transform[3];
        for (int i = 0; i < 3; i++)
        {
            float anguloY = i * 120f;
            GameObject pivo = new GameObject("PivoDente_" + i);
            pivo.transform.SetParent(clawVisualContainer, false);
            pivo.transform.localPosition = new Vector3(0, 0.06f, 0);
            pivo.transform.localRotation = Quaternion.Euler(0, anguloY, 45f);

            // Bloco de articulação do ombro
            GameObject ombro = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ombro.name = "Ombro_" + i;
            ombro.transform.SetParent(pivo.transform, false);
            ombro.transform.localPosition = new Vector3(0.20f, 0, 0);
            ombro.transform.localScale = new Vector3(0.14f, 0.12f, 0.10f);
            ombro.GetComponent<MeshRenderer>().material = mChassisPreto;
            Destroy(ombro.GetComponent<Collider>());

            // Haste Superior do Braço
            GameObject haste = GameObject.CreatePrimitive(PrimitiveType.Cube);
            haste.name = "HasteSuperior_" + i;
            haste.transform.SetParent(pivo.transform, false);
            haste.transform.localPosition = new Vector3(0.32f, -0.28f, 0);
            haste.transform.localRotation = Quaternion.Euler(0, 0, 16f);
            haste.transform.localScale = new Vector3(0.09f, 0.48f, 0.08f);
            haste.GetComponent<MeshRenderer>().material = mCromo;
            Destroy(haste.GetComponent<Collider>());

            // Mini Cilindro / Pistão Hidráulico no Braço
            GameObject pistao = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pistao.name = "Pistao_" + i;
            pistao.transform.SetParent(haste.transform, false);
            pistao.transform.localPosition = new Vector3(0.04f, 0.05f, 0);
            pistao.transform.localScale = new Vector3(0.5f, 0.35f, 0.5f);
            pistao.GetComponent<MeshRenderer>().material = mDouradoPistao;
            Destroy(pistao.GetComponent<Collider>());

            // Dente Inferior Curvado para dentro
            GameObject garraInferior = GameObject.CreatePrimitive(PrimitiveType.Cube);
            garraInferior.name = "GarraInferior_" + i;
            garraInferior.transform.SetParent(haste.transform, false);
            garraInferior.transform.localPosition = new Vector3(-0.16f, -0.45f, 0);
            garraInferior.transform.localRotation = Quaternion.Euler(0, 0, 48f);
            garraInferior.transform.localScale = new Vector3(0.85f, 0.42f, 0.85f);
            garraInferior.GetComponent<MeshRenderer>().material = mCromo;
            Destroy(garraInferior.GetComponent<Collider>());

            // Ponta Emborrachada Antiderrapante
            GameObject pontaBorracha = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            pontaBorracha.name = "Ponta_Borracha_" + i;
            pontaBorracha.transform.SetParent(garraInferior.transform, false);
            pontaBorracha.transform.localPosition = new Vector3(0, -0.42f, 0);
            pontaBorracha.transform.localScale = new Vector3(0.9f, 0.28f, 0.9f);
            pontaBorracha.GetComponent<MeshRenderer>().material = mBorrachaVermelha;
            Destroy(pontaBorracha.GetComponent<Collider>());

            dentes[i] = pivo.transform;
        }
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

        // 1. CONSTRUÇÃO DO GABINETE MODULAR ARQUITETURAL DE ALTA FIDELIDADE
        ArcadeCabinetBuilder cabinetBuilder = ArcadeCabinetBuilder.Build();

        // 2. MONTE DE PELÚCIAS: responsabilidade isolada no spawner físico.
        // O ClawController fica responsável somente por movimento, garra e captura.
        pileSpawner = gameObject.AddComponent<PrizePileSpawner>();
        pileSpawner.Build();
        Debug.Log("[ClawController] Spawner físico de pelúcias conectado ao gabinete.");

    }

    private void BuildInitialPrizeBoard()
    {
        if (stockManager == null || prizePileRoot == null) return;
        StartCoroutine(BuildInitialPrizeBoardRoutine());
    }

    private System.Collections.IEnumerator BuildInitialPrizeBoardRoutine()
    {
        // Mistura as raridades no volume para não formar fileiras temáticas.
        string[] common = { "Fox", "GreenBear", "BalloonFish" };
        string[] uncommon = { "Koala", "Badger" };
        int index = 0;

        for (int i = 0; i < 48; i++, index++)
        {
            SpawnInitialPrize(common[i % common.Length], PrizeRarity.Common, index, true);
            if (i % 3 == 2) yield return new WaitForSeconds(0.035f);
        }
        for (int i = 0; i < 18; i++, index++)
        {
            SpawnInitialPrize(uncommon[i % uncommon.Length], PrizeRarity.Uncommon, index, true);
            if (i % 3 == 2) yield return new WaitForSeconds(0.035f);
        }
        for (int i = 0; i < 6; i++, index++)
        {
            SpawnInitialPrize("Porky", PrizeRarity.Rare, index, true);
            yield return new WaitForSeconds(0.035f);
        }

        // Dá tempo para a última leva tocar o platô e se acomodar antes do HUD liberar a partida.
        yield return new WaitForSeconds(2.2f);
        StabilizePrizePile();
        prizeBoardBuilt = true;
        Debug.Log($"[ClawController] Abastecimento concluído. Filhos no monte: {prizePileRoot.childCount}; estoque ativo: {stockManager.ActiveCount}/{stockManager.TargetBoardCount}.");
    }

    // Compatibilidade temporária com o bloco legado abaixo; o fluxo real usa PrizePileSpawner.
    private void SpawnInitialPrize(string resourceName, PrizeRarity rarity, int index)
    {
        SpawnInitialPrize(resourceName, rarity, index, true);
    }

    private void SpawnInitialPrize(string resourceName, PrizeRarity rarity, int index, bool dropIn)
    {
        GameObject prefab = Resources.Load<GameObject>("Prizes/" + resourceName);
        Debug.Log($"[ClawController] Tentando spawn inicial: {resourceName} ({rarity}) — prefab {(prefab != null ? "OK" : "AUSENTE")}");
        PrizeStockEntry definition = stockManager != null ? stockManager.ReserveDirect(resourceName, rarity) : null;
        if (definition == null)
        {
            Debug.LogError($"[ClawController] Definição de estoque ausente para {resourceName}; usando fallback visual.");
            SpawnFallbackPrize(resourceName, rarity, index, dropIn);
            return;
        }

        try
        {
            if (prefab == null) throw new System.InvalidOperationException($"Prefab ausente em Resources/Prizes/{resourceName}");
            SpawnPrizeInstance(prefab, definition, index, dropIn);
        }
        catch (System.Exception ex)
        {
            // Um rig individual nunca pode abortar a montagem dos outros 35 prêmios.
            Debug.LogError($"[ClawController] Falha no prefab {resourceName} no slot {index}: {ex.Message}. Usando fallback.");
            SpawnFallbackPrize(resourceName, rarity, index, dropIn);
        }
    }

    private Vector3 CalculatePilePosition(int index, bool dropIn)
    {
        // Sequência de baixa discrepância: espalha os prêmios sem formar linhas,
        // mas continua determinística para que cada abertura seja reproduzível.
        float u = Mathf.Repeat((index + 1) * 0.6180339887f, 1f);
        float v = Mathf.Repeat((index + 1) * 0.7548776662f, 1f);
        int layer = index / 18;
        float x = Mathf.Lerp(-1.12f, 1.55f, u);
        float z = Mathf.Lerp(-0.98f, 1.58f, v);
        float moundBias = Mathf.Abs(x - 0.20f) * 0.10f + Mathf.Abs(z - 0.25f) * 0.05f;
        float y = PRIZE_FLOOR_Y + (dropIn ? 1.05f + (index % 7) * 0.17f : 0.10f + layer * 0.28f);
        return new Vector3(x, y + moundBias, z);
    }

    private Quaternion CalculatePileRotation(int index)
    {
        float yaw = Mathf.Repeat(index * 137.50776f, 360f);
        float pitch = Mathf.Lerp(-18f, 18f, Mathf.Repeat(index * 0.381966f, 1f));
        float roll = Mathf.Lerp(-16f, 16f, Mathf.Repeat(index * 0.517638f, 1f));
        return Quaternion.Euler(pitch, yaw, roll);
    }

    private Vector3 CalculateSettledPilePosition(int index)
    {
        // Layout de repouso em camadas sobrepostas. Não é uma grade: usa duas
        // sequências diferentes para espalhar o recheio e manter o contorno de monte.
        int layer = index / 18;
        float u = Mathf.Repeat((index + 11) * 0.6180339887f, 1f);
        float v = Mathf.Repeat((index + 7) * 0.4142135623f, 1f);
        float x = Mathf.Lerp(-1.20f, 1.40f, u);
        float z = Mathf.Lerp(-1.08f, 1.48f, v);
        float centerFalloff = Mathf.Abs(x - 0.10f) * 0.06f + Mathf.Abs(z - 0.18f) * 0.04f;
        float verticalJitter = Mathf.Lerp(-0.025f, 0.025f, Mathf.Repeat(index * 0.271828f, 1f));
        float y = PRIZE_FLOOR_Y + 0.015f + layer * 0.36f + centerFalloff + verticalJitter;
        return new Vector3(x, y, z);
    }

    private void SpawnFallbackPrize(string resourceName, PrizeRarity rarity, int index, bool dropIn)
    {
        if (prizePileRoot == null) return;
        Vector3 position = CalculatePilePosition(index, dropIn);
        GameObject fallback = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        fallback.name = $"PeluciaFallback_{resourceName}_{rarity}_{index}";
        fallback.transform.SetParent(prizePileRoot, false);
        fallback.transform.SetPositionAndRotation(position, CalculatePileRotation(index));
        fallback.transform.localScale = Vector3.one * 0.48f;
        Color color = rarity == PrizeRarity.Rare ? new Color(1f, 0.72f, 0.05f) : rarity == PrizeRarity.Uncommon ? new Color(0.45f, 0.9f, 1f) : new Color(1f, 0.35f, 0.55f);
        Renderer renderer = fallback.GetComponent<Renderer>();
        if (renderer != null) renderer.material.color = color;
        Prize prize = fallback.AddComponent<Prize>();
        prize.ConfigureFromStock(resourceName, rarity, rarity == PrizeRarity.Rare ? 0.34f : rarity == PrizeRarity.Uncommon ? 0.78f : 0.94f);
        Rigidbody body = fallback.GetComponent<Rigidbody>();
        body.isKinematic = !dropIn;
        body.useGravity = dropIn;
        if (dropIn) body.WakeUp();
        Debug.LogWarning($"[ClawController] Fallback visual criado para {resourceName} no slot {index}.");
    }

    private void StabilizePrizePile()
    {
        if (prizePileRoot == null) return;
        Physics.SyncTransforms();

        Prize[] prizes = prizePileRoot.GetComponentsInChildren<Prize>(true);
        int stabilized = 0;
        for (int i = 0; i < prizes.Length; i++)
        {
            Prize prize = prizes[i];
            if (prize == null || prize.State != PrizeState.InPile) continue;
            Rigidbody body = prize.Body != null ? prize.Body : prize.GetComponent<Rigidbody>();
            if (body == null) continue;

            // Após a queda, damos ao monte um repouso determinístico e orgânico.
            // Isso elimina lacunas de simulação que fariam uma pelúcia parecer suspensa,
            // sem voltar às fileiras: as posições continuam espalhadas e sobrepostas.
            body.position = CalculateSettledPilePosition(i);
            body.rotation = CalculatePileRotation(i);
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.isKinematic = true;
            body.useGravity = false;
            body.Sleep();

            Transform visual = prize.transform.Find("Visual");
            if (visual != null)
            {
                Renderer[] renderers = visual.GetComponentsInChildren<Renderer>();
                if (renderers.Length > 0)
                {
                    Bounds bounds = renderers[0].bounds;
                    for (int r = 1; r < renderers.Length; r++) bounds.Encapsulate(renderers[r].bounds);
                    // Corrige qualquer pequeno drift introduzido pela simulação ou pelo pivot do rig.
                    visual.position += Vector3.up * (body.position.y - bounds.min.y);
                }
            }
            stabilized++;
        }

        Debug.Log($"[ClawController] Monte estabilizado: {stabilized} prêmios assentados e congelados após a queda.");
    }

    private void ReplenishVisiblePrizes()
    {
        if (!prizeBoardBuilt || stockManager == null) return;
        int missing = Mathf.Max(0, stockManager.TargetBoardCount - stockManager.ActiveCount);
        int batch = Mathf.Min(stockManager.ActiveRefillBatch, missing);
        for (int i = 0; i < batch; i++) SpawnPrizeFromStock(false, stockManager.ActiveCount + i);
        Debug.Log($"[ClawController] Reposição aplicada: +{batch} bichinho(s); ativos {stockManager.ActiveCount}/{stockManager.TargetBoardCount}.");
    }

    private void SpawnPrizeFromStock(bool initialBuild, int index)
    {
        PrizeStockEntry definition = stockManager != null ? stockManager.TakeNextDefinition(initialBuild) : null;
        if (definition == null || definition.prefab == null || prizePileRoot == null) return;
        SpawnPrizeInstance(definition.prefab, definition, index, true);
    }

    private System.Collections.IEnumerator ReleaseInitialPrizePhysics()
    {
        yield return new WaitForSeconds(0.75f);
        if (prizePileRoot == null) yield break;
        Rigidbody[] bodies = prizePileRoot.GetComponentsInChildren<Rigidbody>();
        foreach (Rigidbody body in bodies)
        {
            if (body == null) continue;
            body.isKinematic = false;
            body.WakeUp();
        }
        Debug.Log($"[ClawController] Física do monte liberada: {bodies.Length} rigidbodies.");
    }

    private void SpawnPrizeInstance(GameObject prefab, PrizeStockEntry definition, int index, bool dropIn)
    {
        if (prefab == null || definition == null || prizePileRoot == null) return;

        // O wrapper é o objeto de gameplay. O prefab riggado fica isolado como visual,
        // evitando que pivôs e colliders internos alterem a posição física do prêmio.
        Vector3 position = CalculatePilePosition(index, dropIn);
        Quaternion rotation = CalculatePileRotation(index);

        GameObject instance = new GameObject($"Pelucia_{definition.resourceName}_{definition.rarity}_{index}");
        instance.transform.SetParent(prizePileRoot, false);
        instance.transform.SetPositionAndRotation(position, rotation);

        int prizeLayerIdx = LayerMask.NameToLayer("Prize");
        if (prizeLayerIdx != -1) instance.layer = prizeLayerIdx;

        GameObject visualRoot = Instantiate(prefab, instance.transform, false);
        visualRoot.name = "Visual";
        Debug.Log($"[ClawController] Spawned {instance.name} usando visual {prefab.name}");

        foreach (Animator anim in visualRoot.GetComponentsInChildren<Animator>()) anim.enabled = false;

        Renderer[] renderers = visualRoot.GetComponentsInChildren<Renderer>();
        if (renderers.Length > 0)
        {
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            float maxDimension = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
            if (maxDimension > 0.0001f) visualRoot.transform.localScale *= 0.58f / maxDimension;

            // Corrige o pivot deslocado do rig sem mover o wrapper físico.
            Renderer[] scaledRenderers = visualRoot.GetComponentsInChildren<Renderer>();
            Bounds scaledBounds = scaledRenderers[0].bounds;
            for (int i = 1; i < scaledRenderers.Length; i++) scaledBounds.Encapsulate(scaledRenderers[i].bounds);
            // O ponto de apoio é o fundo visual, não o centro arbitrário do rig.
            // Cada rig Blender possui um pivot diferente; alinhar min.y ao fundo
            // do collider evita que alguns modelos fiquem suspensos no monte.
            float desiredVisualBottomY = instance.transform.position.y;
            float visualBottomDeltaY = desiredVisualBottomY - scaledBounds.min.y;
            visualRoot.transform.localPosition += instance.transform.InverseTransformVector(Vector3.up * visualBottomDeltaY);
        }
        else
        {
            visualRoot.transform.localScale = Vector3.one * 0.35f;
        }

        Prize prize = instance.AddComponent<Prize>();
        prize.ConfigureFromStock(definition.resourceName, definition.rarity, definition.baseCaptureChance);

        Rigidbody body = instance.GetComponent<Rigidbody>();
        body.mass = definition.rarity == PrizeRarity.Rare ? 1.65f : definition.rarity == PrizeRarity.Uncommon ? 1.45f : 1.25f;
        // O collider só é liberado depois de existir, evitando um frame sem contato.
        body.isKinematic = true;
        body.useGravity = false;
        body.linearVelocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;
        body.linearDamping = 2f;
        body.angularDamping = 2.2f;
        body.collisionDetectionMode = CollisionDetectionMode.Continuous;

        foreach (Collider existing in visualRoot.GetComponentsInChildren<Collider>())
        {
            if (existing != null) Destroy(existing);
        }
        BoxCollider boxCollider = instance.GetComponent<BoxCollider>();
        if (boxCollider == null) boxCollider = instance.AddComponent<BoxCollider>();
        boxCollider.center = new Vector3(0f, 0.27f, 0f);
        boxCollider.size = new Vector3(0.50f, 0.54f, 0.50f);

        if (dropIn)
        {
            body.isKinematic = false;
            body.useGravity = true;
            body.WakeUp();
        }

        stockManager.RegisterSpawned(prize, definition);
    }

    void ConfigurarRastroLuminoso()
    {
        GameObject trailObj = new GameObject("RastroLuminoso_Garra");
        trailObj.transform.SetParent(transform, false);
        trailObj.transform.localPosition = new Vector3(0, -0.2f, 0);

        trailRenderer = trailObj.AddComponent<TrailRenderer>();
        trailRenderer.time = 0.45f;
        trailRenderer.startWidth = 0.22f;
        trailRenderer.endWidth = 0.01f;
        trailRenderer.minVertexDistance = 0.02f;
        trailRenderer.autodestruct = false;

        Material mTrail = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
        mTrail.color = new Color(0f, 0.95f, 1f, 0.95f);
        mTrail.EnableKeyword("_EMISSION");
        mTrail.SetColor("_EmissionColor", new Color(0f, 0.95f, 1f) * 3.5f);
        trailRenderer.material = mTrail;

        SetTrailColorDefault();

        // Luz pontual neon na garra para iluminar o poço de pelúcias ao descer
        GameObject luzGarraObj = new GameObject("LuzNeon_Garra");
        luzGarraObj.transform.SetParent(transform, false);
        luzGarraObj.transform.localPosition = new Vector3(0, -0.3f, 0);
        Light luzGarra = luzGarraObj.AddComponent<Light>();
        luzGarra.type = LightType.Point;
        luzGarra.color = new Color(0f, 0.95f, 1f);
        luzGarra.range = 3.5f;
        luzGarra.intensity = 2.2f;
    }

    void SetTrailColorDefault()
    {
        if (trailRenderer == null) return;
        Gradient grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[] {
                new GradientColorKey(new Color(0f, 0.95f, 1f), 0f),      // Neon Cyan
                new GradientColorKey(new Color(1f, 0.1f, 0.85f), 0.5f),   // Electric Pink
                new GradientColorKey(new Color(0.5f, 0f, 1f), 1f)        // Purple
            },
            new GradientAlphaKey[] {
                new GradientAlphaKey(0.9f, 0f),
                new GradientAlphaKey(0.6f, 0.6f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        trailRenderer.colorGradient = grad;
    }

    void SetTrailColorGrabbing()
    {
        if (trailRenderer == null) return;
        Gradient grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[] {
                new GradientColorKey(new Color(1f, 0.9f, 0.2f), 0f),     // Neon Gold
                new GradientColorKey(new Color(1f, 0.4f, 0.1f), 0.5f),   // Orange
                new GradientColorKey(new Color(1f, 0.1f, 0.3f), 1f)     // Amber/Red
            },
            new GradientAlphaKey[] {
                new GradientAlphaKey(1.0f, 0f),
                new GradientAlphaKey(0.7f, 0.6f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        trailRenderer.colorGradient = grad;
    }
}