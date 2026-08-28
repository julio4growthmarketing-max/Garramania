using UnityEngine;

public class ClawController : MonoBehaviour
{
    private float LIM_X = 1.3f;
    private float LIM_Z = 1.3f;
    private float LIM_YMAX = 2.78f;
    private float LIM_YMIN = -0.95f;

    // Juice: throttle para som do servo não spammar
    private float servoSoundTimer = 0f;
    private const float SERVO_SOUND_COOLDOWN = 0.2f;

    private bool isClosed = false;
    private Transform[] dentes;
    private GameObject premioAgarrado;
    private LineRenderer cable;
    private TrailRenderer trailRenderer;

    [Header("Eventos")]
    public UnityEngine.Events.UnityEvent<bool> OnClawStateChanged = new UnityEngine.Events.UnityEvent<bool>();
    public bool IsClosed => isClosed;
    public bool HasPrize => premioAgarrado != null;

    [Header("Arraste o Urso (Prefab) para cá no Inspector (Opcional):")]
    public GameObject prizePrefab;

    private Transform prizePileRoot;
    private PrizeStockManager stockManager;
    private bool prizeBoardBuilt;

    void Start()
    {
        stockManager = PrizeStockManager.Instance;
        stockManager.OnRefillRequested += ReplenishVisiblePrizes;

        ConstruirGabinete();
        ConstruirGarra();
        
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
        if (stockManager != null) stockManager.OnRefillRequested -= ReplenishVisiblePrizes;
    }

    private bool isExecutingCycle = false;
    public bool IsExecutingCycle => isExecutingCycle;

    void Update()
    {
        AtualizarCabo();

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

        Vector3 nova = transform.position + moveInput * 3.0f * Time.deltaTime;
        nova.x = Mathf.Clamp(nova.x, -LIM_X, LIM_X);
        nova.z = Mathf.Clamp(nova.z, -LIM_Z, LIM_Z);
        nova.y = Mathf.Clamp(nova.y, LIM_YMIN, LIM_YMAX);
        transform.position = nova;

        // 🔊 JUICE: Som do servo ao mover
        if (moveInput.sqrMagnitude > 0.01f && servoSoundTimer <= 0f)
        {
            AudioFeedbackController.Instance?.PlayServo();
            servoSoundTimer = SERVO_SOUND_COOLDOWN;
        }

        if (space) AcionarGarra();
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

        // 1. FASE DE DESCIDA SUAVE EM DIREÇÃO ÀS PELÚCIAS
        float targetY = LIM_YMIN;
        while (transform.position.y > targetY + 0.04f)
        {
            transform.position = Vector3.MoveTowards(transform.position, 
                new Vector3(transform.position.x, targetY, transform.position.z), 
                2.2f * Time.deltaTime);

            AtualizarCabo();

            // Se tocar numa pelúcia pelo caminho, interrompe a descida
            Collider[] hitsDescida = Physics.OverlapSphere(transform.position, 0.40f);
            bool tocouPelucia = false;
            foreach (var h in hitsDescida)
            {
                if (h.GetComponentInParent<Prize>() != null)
                {
                    tocouPelucia = true;
                    break;
                }
            }
            if (tocouPelucia) break;

            yield return null;
        }

        yield return new WaitForSeconds(0.2f);

        // 2. FASE DE FECHAMENTO & AGARRE MECÂNICO
        FecharGarraFisica();
        yield return new WaitForSeconds(0.5f);

        // 3. FASE DE SUBIDA DE VOLTA AO TETO
        GameSession.Instance?.SetState(GameState.Returning);
        while (transform.position.y < LIM_YMAX - 0.04f)
        {
            transform.position = Vector3.MoveTowards(transform.position, 
                new Vector3(transform.position.x, LIM_YMAX, transform.position.z), 
                2.0f * Time.deltaTime);

            AtualizarCabo();
            yield return null;
        }

        yield return new WaitForSeconds(0.3f);

        // 4. FASE DE VIAGEM AUTOMÁTICA ATÉ A CALHA DE PRÊMIOS (-1.8, LIM_YMAX, -1.8)
        Vector3 posCalha = new Vector3(-1.8f, LIM_YMAX, -1.8f);
        while (Vector3.Distance(new Vector3(transform.position.x, 0, transform.position.z), 
                              new Vector3(posCalha.x, 0, posCalha.z)) > 0.05f)
        {
            transform.position = Vector3.MoveTowards(transform.position, 
                new Vector3(posCalha.x, LIM_YMAX, posCalha.z), 
                2.4f * Time.deltaTime);

            AtualizarCabo();
            yield return null;
        }

        yield return new WaitForSeconds(0.4f);

        // 5. FASE DE ENTREGA: abre sobre o duto somente quando existe um prêmio preso.
        bool haviaPremio = premioAgarrado != null;
        if (haviaPremio) GameSession.Instance?.SetState(GameState.Delivering);
        AbrirGarraFisica();
        yield return new WaitForSeconds(1.2f);

        // 6. FASE DE RETORNO AO CENTRO DA MÁQUINA
        Vector3 posCentro = new Vector3(0f, LIM_YMAX, 0f);
        while (Vector3.Distance(new Vector3(transform.position.x, 0, transform.position.z), 
                              new Vector3(posCentro.x, 0, posCentro.z)) > 0.05f)
        {
            transform.position = Vector3.MoveTowards(transform.position, 
                new Vector3(posCentro.x, LIM_YMAX, posCentro.z), 
                2.5f * Time.deltaTime);

            AtualizarCabo();
            yield return null;
        }

        isExecutingCycle = false;
        if (GameSession.Instance != null && GameSession.Instance.CurrentState != GameState.Delivering)
        {
            GameSession.Instance.SetState(GameState.Playing);
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

        AudioFeedbackController.Instance?.PlayClank();
        GameJuice.Instance?.ScreenShake(0.15f, 0.1f);
        GameJuice.Instance?.HapticsLight();

        Collider[] hits = Physics.OverlapSphere(transform.position, 1.0f);
        foreach (var hit in hits)
        {
            Prize p = hit.GetComponentInParent<Prize>();
            if (p != null && premioAgarrado == null && p.State != PrizeState.Delivered)
            {
                if (!PrizeStockManager.Instance.CanAttemptCapture(p)) continue;
                premioAgarrado = p.gameObject;
                p.Attach(transform);

                GameJuice.Instance?.PunchScale(premioAgarrado.transform, 1.3f, 0.3f);
                GameJuice.Instance?.SlowMotion(0.4f, 0.3f);
                GameJuice.Instance?.PlaySparkles(transform.position);
                GameJuice.Instance?.Haptics();

                SetTrailColorGrabbing();
                break;
            }
        }
    }

    private void AbrirGarraFisica()
    {
        isClosed = false;
        if (dentes != null)
        {
            foreach (var d in dentes) d.localRotation = Quaternion.Euler(0, d.localEulerAngles.y, 45f);
        }

        SetTrailColorDefault();

        if (premioAgarrado != null)
        {
            Prize p = premioAgarrado.GetComponentInParent<Prize>();
            if (p != null) p.Detach();
            premioAgarrado = null;

            AudioFeedbackController.Instance?.PlayThud();
            GameJuice.Instance?.ScreenShake(0.1f, 0.05f);
        }
    }

    private void AtualizarCabo()
    {
        if (cable != null)
        {
            cable.SetPosition(0, new Vector3(transform.position.x, LIM_YMAX, transform.position.z));
            cable.SetPosition(1, transform.position);
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
        
        if (premioAgarrado != null)
        {
            Prize p = premioAgarrado.GetComponentInParent<Prize>();
            if (p != null) p.Detach();
            premioAgarrado = null;
        }
    }

    // ====== CONSTRUÇÃO VISUAL DA GARRA MECÂNICA 3D ======
    void ConstruirGarra()
    {
        // Esconde o cubo original
        MeshRenderer rendererBase = GetComponent<MeshRenderer>();
        if (rendererBase != null) rendererBase.enabled = false;

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

        // 1. CABEÇOTE CENTRAL
        GameObject carcase = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        carcase.name = "Garra_Carcaça";
        carcase.transform.SetParent(transform, false);
        carcase.transform.localPosition = new Vector3(0, 0.18f, 0);
        carcase.transform.localScale = new Vector3(0.52f, 0.18f, 0.52f);
        carcase.GetComponent<MeshRenderer>().material = mCromo;
        Destroy(carcase.GetComponent<Collider>());

        // Anel Neon de Status no centro da garra
        GameObject ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        ring.name = "Garra_Anel_Neon";
        ring.transform.SetParent(transform, false);
        ring.transform.localPosition = new Vector3(0, 0.18f, 0);
        ring.transform.localScale = new Vector3(0.55f, 0.04f, 0.55f);
        ring.GetComponent<MeshRenderer>().material = mNeonRing;
        Destroy(ring.GetComponent<Collider>());

        // Tampa Superior Cônica
        GameObject topCone = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        topCone.name = "Garra_Tampa_Top";
        topCone.transform.SetParent(transform, false);
        topCone.transform.localPosition = new Vector3(0, 0.30f, 0);
        topCone.transform.localScale = new Vector3(0.35f, 0.08f, 0.35f);
        topCone.GetComponent<MeshRenderer>().material = mChassisPreto;
        Destroy(topCone.GetComponent<Collider>());

        // Olhal de Aço onde prende o cabo
        GameObject eyelet = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        eyelet.name = "Garra_Olhal_Cabo";
        eyelet.transform.SetParent(transform, false);
        eyelet.transform.localPosition = new Vector3(0, 0.38f, 0);
        eyelet.transform.localScale = new Vector3(0.12f, 0.12f, 0.12f);
        eyelet.GetComponent<MeshRenderer>().material = mCromo;
        Destroy(eyelet.GetComponent<Collider>());

        // 2. OS 3 DENTES ARTICULADOS COM PISTÕES E PONTAS EMBORRACHADAS
        dentes = new Transform[3];
        for (int i = 0; i < 3; i++)
        {
            float anguloY = i * 120f;
            GameObject pivo = new GameObject("PivoDente_" + i);
            pivo.transform.SetParent(transform, false);
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

                // 2. MONTE DE PELÚCIAS: o estoque decide quantidade, raridade e reposição.
        prizePileRoot = new GameObject("Monte_De_Ursos").transform;
        prizePileRoot.SetParent(transform.parent, false);
        stockManager.Initialize(prizePileRoot, 36);
        prizeBoardBuilt = true;
        BuildInitialPrizeBoard();
        Debug.Log($"[ClawController] Monte distribuído pelo estoque vivo: {stockManager.ActiveCount}/{stockManager.TargetBoardCount} posições.");

    }

    private void BuildInitialPrizeBoard()
    {
        if (stockManager == null) return;
        int attempts = stockManager.TargetBoardCount;
        for (int i = 0; i < attempts; i++) SpawnPrizeFromStock(true, i);
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

        float x = Random.Range(-1.15f, 1.45f);
        float z = Random.Range(-1.15f, 1.45f);
        if (x < -0.6f && z < -0.6f) x += 1.0f;
        float y = -1.25f + ((index % 12) * 0.055f) + Random.Range(-0.025f, 0.025f);
        Vector3 position = new Vector3(x, y, z);
        Quaternion rotation = Quaternion.Euler(Random.Range(-22f, 22f), Random.Range(0f, 360f), Random.Range(-22f, 22f));

        GameObject instance = Instantiate(definition.prefab, position, rotation, prizePileRoot);
        instance.name = $"Pelucia_{definition.resourceName}_{definition.rarity}_{index}";

        foreach (Animator anim in instance.GetComponentsInChildren<Animator>()) anim.enabled = false;

        Renderer[] renderers = instance.GetComponentsInChildren<Renderer>();
        if (renderers != null && renderers.Length > 0)
        {
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            float maxDimension = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
            if (maxDimension > 0.05f) instance.transform.localScale *= 0.58f / maxDimension;
        }
        else
        {
            instance.transform.localScale = Vector3.one * 0.35f;
        }

        Prize prize = instance.GetComponent<Prize>();
        if (prize == null) prize = instance.AddComponent<Prize>();
        prize.ConfigureFromStock(definition.resourceName, definition.rarity, definition.baseCaptureChance);

        Rigidbody body = instance.GetComponent<Rigidbody>();
        if (body == null) body = instance.AddComponent<Rigidbody>();
        body.mass = definition.rarity == PrizeRarity.Rare ? 1.65f : definition.rarity == PrizeRarity.Uncommon ? 1.45f : 1.25f;
        body.linearDamping = 2f;
        body.angularDamping = 2.2f;
        body.collisionDetectionMode = CollisionDetectionMode.Continuous;

        Collider collider = instance.GetComponent<Collider>();
        if (collider == null)
        {
            BoxCollider box = instance.AddComponent<BoxCollider>();
            box.size = new Vector3(0.55f, 0.55f, 0.55f);
            box.center = new Vector3(0f, 0.28f, 0f);
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