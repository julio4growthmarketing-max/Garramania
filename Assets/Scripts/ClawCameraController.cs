using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Controlador de Câmera em Primeira Pessoa Arcade (Estilo Claw Machine Sim).
/// Recursos:
/// 1. VISÃO EM 1ª PESSOA COLADA NO VIDRO: O jogador fica em pé de frente para a máquina,
///    com o vidro e as pelúcias preenchendo a visão e o console logo abaixo.
/// 2. SISTEMA DE ESPIADA SUAVE (LEAN / PEEK): O jogador pode inclinar o olhar para
///    a esquerda ou direita arrastando na tela ou pressionando Q/E para checar a profundidade.
/// 3. DYNAMIC TENSION ZOOM: Quando a garra desce, o zoom se aproxima do vidro gerando suspense.
/// 4. FOLLOW PARALLAX: Acompanhamento sutil dos olhos do jogador ao mover a garra.
/// 5. SHAKE & TRAUMA: Ruído Perlin com decaimento quadrático nas colisões e pegadas.
/// </summary>
public class ClawCameraController : MonoBehaviour
{
    public static ClawCameraController Instance { get; private set; }

    public enum CameraViewAngle { Front, Diagonal, TopDown }
    public CameraViewAngle CurrentAngle { get; private set; } = CameraViewAngle.Front;

    public string CurrentAngleDisplayName
    {
        get
        {
            switch (CurrentAngle)
            {
                case CameraViewAngle.Diagonal: return "DIAGONAL";
                case CameraViewAngle.TopDown: return "SUPERIOR";
                default: return "FRENTE";
            }
        }
    }

    [Header("Eventos")]
    public UnityEvent<CameraViewAngle> OnCameraAngleChanged = new UnityEvent<CameraViewAngle>();

    [Header("Alvo")]
    public Transform clawTarget;

    [Header("Posições em 1ª Pessoa Focadas na Frontal")]
    // 1. Frontal Padrão: Centralizada no vidro, garra e monte
    public Vector3 frontPosition = new Vector3(0f, 0.70f, -4.80f);
    public Vector3 frontEuler = new Vector3(7f, 0f, 0f);
    // 2. Diagonal Frontal 3/4: Deslocada para o canto frontal direito para verificar profundidade (Z)
    public Vector3 diagonalPosition = new Vector3(1.50f, 1.10f, -4.40f);
    public Vector3 diagonalEuler = new Vector3(12f, -18f, 0f);
    // 3. Superior Frontal (Top-Down): Elevada e inclinada para ver o cesto de cima
    public Vector3 topDownPosition = new Vector3(0f, 2.05f, -4.00f);
    public Vector3 topDownEuler = new Vector3(27f, 0f, 0f);

    [Header("Follow Damping")]
    public float followWeightX = 0.12f;
    public float followWeightZ = 0.08f;
    public float followDamping = 5.0f;

    [Header("Dynamic Tension Zoom")]
    public float defaultFOV = 62f;
    public float tensionFOV = 50f;
    public float zoomSpeed = 3.5f;

    [Header("Sistema de Espiada Livre (First Person Lean)")]
    [Range(-1f, 1f)] public float currentLean = 0f;
    public float leanMaxOffset = 0.95f;
    public float leanMaxYaw = 16f;
    private float targetLean = 0f;

    [Header("Impulse & Trauma Shake")]
    public float traumaDecay = 1.8f;
    public float maxShakeTranslation = 0.35f;
    public float maxShakeRotation = 3.5f;

    [Header("Direct Shake & FOV Punch")]
    public float normalFOV = 62f;
    public float punchFOV = 54f;
    private float directShakeIntensity = 0.08f;
    private float directShakeDuration = 0.15f;
    private float directShakeTimer = 0f;
    private Coroutine fovPunchCoroutine;
    private float fovPunchOffset = 0f;

    private Camera cam;
    private float trauma = 0f;
    private Vector3 currentSmoothPos;
    private Quaternion currentSmoothRot;
    private float currentFOV;

    // Seeds para Perlin Noise
    private float seedX, seedY, seedZ, seedPitch, seedYaw, seedRoll;

    // Drag para espiar com touch / mouse
    private Vector2 lastTouchPos;
    private bool isDraggingPeek;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;

        cam = GetComponent<Camera>();
        if (cam == null) cam = Camera.main;

        // Configura enquadramento frontal perfeito
        frontPosition = new Vector3(0f, 0.70f, -4.80f);
        frontEuler = new Vector3(7f, 0f, 0f);
        diagonalPosition = new Vector3(1.50f, 1.10f, -4.40f);
        diagonalEuler = new Vector3(12f, -18f, 0f);
        topDownPosition = new Vector3(0f, 2.05f, -4.00f);
        topDownEuler = new Vector3(27f, 0f, 0f);
        defaultFOV = 64f;
        tensionFOV = 52f;
        normalFOV = 64f;
        punchFOV = 56f;

        if (cam != null)
        {
            cam.fieldOfView = defaultFOV;
            cam.transform.position = frontPosition;
            cam.transform.eulerAngles = frontEuler;
        }

        seedX = Random.Range(0f, 100f);
        seedY = Random.Range(100f, 200f);
        seedZ = Random.Range(200f, 300f);
        seedPitch = Random.Range(300f, 400f);
        seedYaw = Random.Range(400f, 500f);
        seedRoll = Random.Range(500f, 600f);

        currentSmoothPos = frontPosition;
        currentSmoothRot = Quaternion.Euler(frontEuler);
        currentFOV = defaultFOV;
        if (cam != null) cam.fieldOfView = defaultFOV;
    }

    void Start()
    {
        if (clawTarget == null)
        {
            ClawController claw = FindFirstObjectByType<ClawController>();
            if (claw != null) clawTarget = claw.transform;
        }
    }

    /// <summary>
    /// Define a inclinação da cabeça (-1 esquerdo, 0 centro, +1 direito)
    /// </summary>
    public void SetLean(float value)
    {
        targetLean = Mathf.Clamp(value, -1f, 1f);
    }

    public void LeanLeft()
    {
        targetLean = targetLean < -0.1f ? 0f : -1f;
    }

    public void LeanRight()
    {
        targetLean = targetLean > 0.1f ? 0f : 1f;
    }

    public void ResetLean()
    {
        targetLean = 0f;
    }

    /// <summary>
    /// Alterna em ciclo entre as 3 câmeras principais
    /// </summary>
    public void ToggleCameraAngle()
    {
        switch (CurrentAngle)
        {
            case CameraViewAngle.Front:
                SetCameraAngle(CameraViewAngle.Diagonal);
                break;
            case CameraViewAngle.Diagonal:
                SetCameraAngle(CameraViewAngle.TopDown);
                break;
            case CameraViewAngle.TopDown:
                SetCameraAngle(CameraViewAngle.Front);
                break;
        }
    }

    public void SetCameraAngle(CameraViewAngle angle)
    {
        CurrentAngle = angle;
        AudioFeedbackController.Instance?.PlayServo();
        GenerateImpulse(0.12f);
        OnCameraAngleChanged?.Invoke(CurrentAngle);
        Debug.Log($"[ClawCameraController] Ângulo de câmera: {CurrentAngle}");
    }

    public void GenerateImpulse(float force = 0.5f, Vector3 direction = default)
    {
        trauma = Mathf.Clamp01(trauma + force);
    }

    public void AddTrauma(float amount)
    {
        trauma = Mathf.Clamp01(trauma + amount);
    }

    /// <summary>
    /// Tremor de câmera direto com intensidade e duração específicas
    /// </summary>
    public void Shake(float intensity, float duration)
    {
        directShakeIntensity = intensity;
        directShakeDuration = Mathf.Max(0.01f, duration);
        directShakeTimer = directShakeDuration;
        AddTrauma(intensity * 1.8f);
    }

    public void ShakeCamera(float duration, float intensity)
    {
        Shake(intensity, duration);
    }

    /// <summary>
    /// Efeito visual de impacto no campo de visão (Punch FOV)
    /// </summary>
    public void PunchFOV(float intensity = 1f)
    {
        if (fovPunchCoroutine != null) StopCoroutine(fovPunchCoroutine);
        fovPunchCoroutine = StartCoroutine(FOVPunchRoutine(intensity));
    }

    private System.Collections.IEnumerator FOVPunchRoutine(float intensity)
    {
        if (cam == null) yield break;

        float start = cam.fieldOfView;
        float target = normalFOV - (normalFOV - punchFOV) * intensity;
        float t = 0f;

        while (t < 1f)
        {
            t += Time.unscaledDeltaTime * 8f;
            fovPunchOffset = Mathf.Lerp(0f, target - normalFOV, t);
            yield return null;
        }

        t = 0f;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime * 4f;
            fovPunchOffset = Mathf.Lerp(target - normalFOV, 0f, t);
            yield return null;
        }

        fovPunchOffset = 0f;
        fovPunchCoroutine = null;
    }

    private void HandlePeekInput()
    {
        // 1. Teclado Q e E para espiar rapidamente
        if (Input.GetKey(KeyCode.Q))
        {
            targetLean = -1f;
            return;
        }
        if (Input.GetKey(KeyCode.E))
        {
            targetLean = 1f;
            return;
        }

        // 2. Arrastar na área superior da tela (vidro) para olhar ao redor / espiar
        if (Input.GetMouseButtonDown(0))
        {
            // Ignora se tocou na área dos controles inferiores (abaixo de 32% da tela)
            if (Input.mousePosition.y > Screen.height * 0.32f)
            {
                isDraggingPeek = true;
                lastTouchPos = Input.mousePosition;
            }
        }
        else if (Input.GetMouseButton(0) && isDraggingPeek)
        {
            float deltaX = Input.mousePosition.x - lastTouchPos.x;
            targetLean = Mathf.Clamp(targetLean - (deltaX / Screen.width) * 3.2f, -1f, 1f);
            lastTouchPos = Input.mousePosition;
        }
        else if (Input.GetMouseButtonUp(0))
        {
            isDraggingPeek = false;
        }

        // Quando solta, retorna suavemente à posição central
        if (!isDraggingPeek && Mathf.Abs(targetLean) > 0.02f)
        {
            targetLean = Mathf.MoveTowards(targetLean, 0f, Time.unscaledDeltaTime * 2.8f);
        }
    }

    void LateUpdate()
    {
        if (cam == null) return;

        HandlePeekInput();

        // 1. SELEÇÃO DO ÂNGULO BASE
        Vector3 basePos = frontPosition;
        Vector3 baseRot = frontEuler;

        switch (CurrentAngle)
        {
            case CameraViewAngle.Front:
                basePos = frontPosition;
                baseRot = frontEuler;
                break;
            case CameraViewAngle.Diagonal:
                basePos = diagonalPosition;
                baseRot = diagonalEuler;
                break;
            case CameraViewAngle.TopDown:
                basePos = topDownPosition;
                baseRot = topDownEuler;
                break;
        }

        Vector3 targetPos = basePos;
        Vector3 targetEuler = baseRot;
        float targetFOV = defaultFOV;

        // Adaptação suave de enquadramento para tela vertical (Mobile Portrait)
        float aspect = (float)Screen.width / Mathf.Max(1, Screen.height);
        if (aspect < 1.0f)
        {
            float portraitFactor = Mathf.Clamp01((1.0f - aspect) / 0.55f);
            targetPos.z -= Mathf.Lerp(0f, 1.20f, portraitFactor);
            targetPos.y += Mathf.Lerp(0f, 0.35f, portraitFactor);
            targetFOV = Mathf.Lerp(defaultFOV, 76f, portraitFactor);
        }

        // 2. ESPIADA DE PRIMEIRA PESSOA (LEAN)
        float dt = Time.unscaledDeltaTime;
        currentLean = Mathf.Lerp(currentLean, targetLean, dt * 7.5f);

        if (CurrentAngle == CameraViewAngle.Front)
        {
            // Deslocamento lateral e giro da cabeça para inspecionar o dente da garra
            targetPos.x += currentLean * leanMaxOffset;
            targetEuler.y += -currentLean * leanMaxYaw;
        }

        // 3. PARALAXE DA GARRA E ZOOM DE TENSÃO
        if (clawTarget != null)
        {
            float tDescida = Mathf.InverseLerp(2.5f, -1.0f, clawTarget.position.y);
            targetFOV = Mathf.Lerp(targetFOV, tensionFOV, tDescida * 0.45f);

            // Acompanhamento sempre focado na frente da máquina
            targetPos.x += (clawTarget.position.x * followWeightX);
            targetPos.z += (clawTarget.position.z * followWeightZ);
            targetEuler.y += (clawTarget.position.x * 1.0f);
            targetEuler.x -= (clawTarget.position.z * 0.7f);

            // Quando a garra desce, o jogador se inclina sutilmente em direção ao vidro
            targetPos.y += Mathf.Lerp(0f, -0.10f, tDescida);
            targetPos.z += Mathf.Lerp(0f, 0.35f, tDescida);
        }

        // 4. SUAVIZAÇÃO E DAMPING
        currentSmoothPos = Vector3.Lerp(currentSmoothPos, targetPos, dt * followDamping);
        currentSmoothRot = Quaternion.Slerp(currentSmoothRot, Quaternion.Euler(targetEuler), dt * followDamping);
        currentFOV = Mathf.Lerp(currentFOV, targetFOV, dt * zoomSpeed);
        cam.fieldOfView = currentFOV + fovPunchOffset;

        // Direct Shake timer calculation
        Vector3 directShakeOffset = Vector3.zero;
        if (directShakeTimer > 0f)
        {
            directShakeTimer -= dt;
            float strength = directShakeIntensity * (directShakeTimer / directShakeDuration);
            directShakeOffset = Random.insideUnitSphere * strength;
        }

        // 5. SHAKE COM TRAUMA QUADRÁTICO
        if (trauma > 0f)
        {
            trauma = Mathf.Clamp01(trauma - (dt * traumaDecay));
            float shake = trauma * trauma;

            float time = Time.unscaledTime * 30f;
            float offsetX = (Mathf.PerlinNoise(seedX, time) - 0.5f) * 2f * maxShakeTranslation * shake;
            float offsetY = (Mathf.PerlinNoise(seedY, time) - 0.5f) * 2f * maxShakeTranslation * shake;
            float offsetZ = (Mathf.PerlinNoise(seedZ, time) - 0.5f) * 2f * (maxShakeTranslation * 0.5f) * shake;

            float rotPitch = (Mathf.PerlinNoise(seedPitch, time) - 0.5f) * 2f * maxShakeRotation * shake;
            float rotYaw = (Mathf.PerlinNoise(seedYaw, time) - 0.5f) * 2f * maxShakeRotation * shake;
            float rotRoll = (Mathf.PerlinNoise(seedRoll, time) - 0.5f) * 2f * (maxShakeRotation * 1.5f) * shake;

            transform.position = currentSmoothPos + new Vector3(offsetX, offsetY, offsetZ) + directShakeOffset;
            transform.rotation = currentSmoothRot * Quaternion.Euler(rotPitch, rotYaw, rotRoll);
        }
        else
        {
            transform.position = currentSmoothPos + directShakeOffset;
            transform.rotation = currentSmoothRot;
        }
    }
}
