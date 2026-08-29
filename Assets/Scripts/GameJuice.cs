using UnityEngine;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// Sistema central de "Game Juice" do GarraMania.
/// Controla: Screen Shake, Slow Motion, Haptics, Partículas (Confete e Sparkles),
/// Punch Scale, Flash de tela.
/// Tudo criado via código — zero dependências externas.
/// </summary>
public class GameJuice : MonoBehaviour
{
    public static GameJuice Instance { get; private set; }

    private Camera mainCam;
    private Vector3 originalCamPos;
    private bool hasOriginalPos = false;

    // Screen Shake
    private float shakeTimer = 0f;
    private float shakeMagnitude = 0f;

    // Slow Motion
    private Coroutine slowMoCoroutine;

    // Partículas
    private ParticleSystem confettiPS;
    private ParticleSystem sparklePS;

    // Flash de tela
    private Image flashImage;
    private Coroutine flashCoroutine;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        mainCam = Camera.main;
        CreateParticleSystems();
        CreateFlashOverlay();
    }

    void LateUpdate()
    {
        if (mainCam == null) return;

        // Sempre atualizar posição original quando não está em shake
        // (para acompanhar movimento de câmera externo)
        if (shakeTimer <= 0f)
        {
            originalCamPos = mainCam.transform.position;
            hasOriginalPos = true;
        }
        else
        {
            // Aplicar shake
            shakeTimer -= Time.unscaledDeltaTime;
            float decay = shakeTimer > 0f ? shakeTimer : 0f;
            Vector3 offset = Random.insideUnitSphere * shakeMagnitude * decay * 10f;
            offset.z = 0f; // Manter Z estável
            mainCam.transform.position = originalCamPos + offset;

            if (shakeTimer <= 0f && hasOriginalPos)
            {
                mainCam.transform.position = originalCamPos;
            }
        }
    }

    // ======================== API PÚBLICA ========================

    /// <summary>Treme a câmera por uma duração com uma magnitude.</summary>
    public void ScreenShake(float duration = 0.2f, float magnitude = 0.15f)
    {
        if (ClawCameraController.Instance != null)
        {
            ClawCameraController.Instance.GenerateImpulse(magnitude * 3f);
            return;
        }

        if (mainCam == null) return;
        if (shakeTimer <= 0f)
        {
            originalCamPos = mainCam.transform.position;
            hasOriginalPos = true;
        }
        shakeTimer = duration;
        shakeMagnitude = magnitude;
    }

    /// <summary>Feedback visual e tátil específico para captura da garra.</summary>
    public void ShakeOnCapture(bool success)
    {
        if (success)
        {
            ScreenShake(0.3f, 0.25f);
            Haptics();
        }
        else
        {
            ScreenShake(0.12f, 0.08f);
            HapticsLight();
        }
    }

    /// <summary>Câmera lenta temporária. Usa unscaled time para funcionar.</summary>
    public void SlowMotion(float duration = 0.4f, float timeScale = 0.3f)
    {
        if (slowMoCoroutine != null) StopCoroutine(slowMoCoroutine);
        slowMoCoroutine = StartCoroutine(SlowMotionRoutine(duration, timeScale));
    }

    /// <summary>Vibra o celular/gamepad.</summary>
    public void Haptics()
    {
#if UNITY_ANDROID || UNITY_IOS
        Handheld.Vibrate();
#endif
    }

    /// <summary>Vibração leve.</summary>
    public void HapticsLight()
    {
#if UNITY_ANDROID || UNITY_IOS
        Handheld.Vibrate();
#endif
    }

    /// <summary>Vibração média (duplo pulso).</summary>
    public void HapticsMedium()
    {
#if UNITY_ANDROID || UNITY_IOS
        StartCoroutine(DoublePulse(0.04f, 0.06f));
#endif
    }

    /// <summary>Vibração pesada (triplo pulso forte).</summary>
    public void HapticsHeavy()
    {
#if UNITY_ANDROID || UNITY_IOS
        StartCoroutine(HeavyPulse());
#endif
    }

    /// <summary>Vibração de escorregamento mecânico (5 pulsos cadenciados gerando alta tensão).</summary>
    public void HapticsSlip()
    {
#if UNITY_ANDROID || UNITY_IOS
        StartCoroutine(SlipPulsePattern());
#endif
    }

    /// <summary>Vibração triunfal de sucesso.</summary>
    public void HapticsSuccess()
    {
#if UNITY_ANDROID || UNITY_IOS
        StartCoroutine(SuccessPattern());
#endif
    }

    private IEnumerator DoublePulse(float duration, float gap)
    {
#if UNITY_ANDROID || UNITY_IOS
        Handheld.Vibrate();
        yield return new WaitForSecondsRealtime(gap);
        Handheld.Vibrate();
#else
        yield break;
#endif
    }

    private IEnumerator HeavyPulse()
    {
#if UNITY_ANDROID || UNITY_IOS
        Handheld.Vibrate();
        yield return new WaitForSecondsRealtime(0.05f);
        Handheld.Vibrate();
        yield return new WaitForSecondsRealtime(0.04f);
        Handheld.Vibrate();
#else
        yield break;
#endif
    }

    private IEnumerator SlipPulsePattern()
    {
#if UNITY_ANDROID || UNITY_IOS
        for (int i = 0; i < 5; i++)
        {
            Handheld.Vibrate();
            yield return new WaitForSecondsRealtime(0.07f + i * 0.015f);
        }
#else
        yield break;
#endif
    }

    private IEnumerator SuccessPattern()
    {
#if UNITY_ANDROID || UNITY_IOS
        Handheld.Vibrate();
        yield return new WaitForSecondsRealtime(0.08f);
        Handheld.Vibrate();
        yield return new WaitForSecondsRealtime(0.05f);
        Handheld.Vibrate();
#else
        yield break;
#endif
    }

    /// <summary>Explosão de confete colorido na posição especificada.</summary>
    public void PlayConfetti(Vector3 worldPosition)
    {
        if (confettiPS == null) return;
        confettiPS.transform.position = worldPosition;
        confettiPS.Clear();
        confettiPS.Play();
    }

    /// <summary>Faíscas brilhantes na posição da garra.</summary>
    public void PlaySparkles(Vector3 worldPosition)
    {
        if (sparklePS == null) return;
        sparklePS.transform.position = worldPosition;
        sparklePS.Clear();
        sparklePS.Play();
    }

    /// <summary>Efeito "punch" no tamanho do objeto (escala cresce e volta).</summary>
    public void PunchScale(Transform target, float punch = 1.4f, float duration = 0.3f)
    {
        if (target == null) return;
        StartCoroutine(PunchScaleRoutine(target, punch, duration));
    }

    /// <summary>Flash de cor na tela inteira (feedback visual de impacto).</summary>
    public void FlashScreen(Color color, float duration = 0.2f)
    {
        if (flashImage == null) return;
        if (flashCoroutine != null) StopCoroutine(flashCoroutine);
        flashCoroutine = StartCoroutine(FlashRoutine(color, duration));
    }

    // ======================== COROUTINES ========================

    IEnumerator SlowMotionRoutine(float duration, float scale)
    {
        Time.timeScale = scale;
        Time.fixedDeltaTime = 0.02f * scale;
        yield return new WaitForSecondsRealtime(duration);
        Time.timeScale = 1f;
        Time.fixedDeltaTime = 0.02f;
        slowMoCoroutine = null;
    }

    IEnumerator PunchScaleRoutine(Transform target, float punchMultiplier, float duration)
    {
        if (target == null) yield break;

        Vector3 originalScale = target.localScale;
        Vector3 punchedScale = originalScale * punchMultiplier;
        float halfDuration = duration * 0.3f; // Punch rápido
        float returnDuration = duration * 0.7f; // Volta suave

        // Fase 1: Escala cresce rapidamente
        float t = 0f;
        while (t < halfDuration && target != null)
        {
            t += Time.unscaledDeltaTime;
            float progress = t / halfDuration;
            target.localScale = Vector3.Lerp(originalScale, punchedScale, progress);
            yield return null;
        }

        // Fase 2: Escala volta suavemente (com easing)
        t = 0f;
        while (t < returnDuration && target != null)
        {
            t += Time.unscaledDeltaTime;
            float progress = t / returnDuration;
            // Ease-out cubic
            float eased = 1f - Mathf.Pow(1f - progress, 3f);
            target.localScale = Vector3.Lerp(punchedScale, originalScale, eased);
            yield return null;
        }

        if (target != null) target.localScale = originalScale;
    }

    IEnumerator FlashRoutine(Color color, float duration)
    {
        flashImage.gameObject.SetActive(true);
        flashImage.color = color;

        float half = duration * 0.3f;
        float fadeOut = duration * 0.7f;

        // Fade In
        float t = 0f;
        while (t < half)
        {
            t += Time.unscaledDeltaTime;
            float a = Mathf.Lerp(0f, color.a, t / half);
            flashImage.color = new Color(color.r, color.g, color.b, a);
            yield return null;
        }

        // Fade Out
        t = 0f;
        while (t < fadeOut)
        {
            t += Time.unscaledDeltaTime;
            float a = Mathf.Lerp(color.a, 0f, t / fadeOut);
            flashImage.color = new Color(color.r, color.g, color.b, a);
            yield return null;
        }

        flashImage.color = Color.clear;
        flashImage.gameObject.SetActive(false);
        flashCoroutine = null;
    }

    // ======================== CRIAÇÃO VIA CÓDIGO ========================

    void CreateParticleSystems()
    {
        // ---- CONFETE ----
        GameObject confettiObj = new GameObject("PS_Confetti");
        confettiObj.transform.SetParent(transform);
        confettiPS = confettiObj.AddComponent<ParticleSystem>();

        var mainConf = confettiPS.main;
        mainConf.duration = 1.5f;
        mainConf.loop = false;
        mainConf.startLifetime = 2f;
        mainConf.startSpeed = new ParticleSystem.MinMaxCurve(3f, 8f);
        mainConf.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.15f);
        mainConf.startColor = new ParticleSystem.MinMaxGradient(
            new Color(1f, 0.2f, 0.2f), // Vermelho
            new Color(1f, 0.85f, 0.1f)  // Amarelo
        );
        mainConf.gravityModifier = 0.8f;
        mainConf.maxParticles = 80;
        mainConf.simulationSpace = ParticleSystemSimulationSpace.World;
        mainConf.playOnAwake = false;

        var emission = confettiPS.emission;
        emission.enabled = true;
        emission.rateOverTime = 0;
        emission.SetBursts(new ParticleSystem.Burst[] {
            new ParticleSystem.Burst(0f, 60)
        });

        var shape = confettiPS.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 45f;
        shape.radius = 0.5f;

        var colorOverLifetime = confettiPS.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[] {
                new GradientColorKey(Color.white, 0f),
                new GradientColorKey(Color.white, 1f)
            },
            new GradientAlphaKey[] {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(1f, 0.7f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        colorOverLifetime.color = grad;

        var renderer = confettiObj.GetComponent<ParticleSystemRenderer>();
        renderer.material = CreateSafeParticleMaterial();
        renderer.material.color = Color.white;

        confettiPS.Stop();

        // ---- SPARKLES ----
        GameObject sparkObj = new GameObject("PS_Sparkles");
        sparkObj.transform.SetParent(transform);
        sparklePS = sparkObj.AddComponent<ParticleSystem>();

        var mainSpark = sparklePS.main;
        mainSpark.duration = 0.5f;
        mainSpark.loop = false;
        mainSpark.startLifetime = 0.5f;
        mainSpark.startSpeed = new ParticleSystem.MinMaxCurve(1f, 3f);
        mainSpark.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.08f);
        mainSpark.startColor = new Color(1f, 0.95f, 0.5f, 1f); // Amarelo brilhante
        mainSpark.gravityModifier = -0.2f; // Sobem levemente
        mainSpark.maxParticles = 30;
        mainSpark.simulationSpace = ParticleSystemSimulationSpace.World;
        mainSpark.playOnAwake = false;

        var emSpark = sparklePS.emission;
        emSpark.enabled = true;
        emSpark.rateOverTime = 0;
        emSpark.SetBursts(new ParticleSystem.Burst[] {
            new ParticleSystem.Burst(0f, 20)
        });

        var shapeSpark = sparklePS.shape;
        shapeSpark.shapeType = ParticleSystemShapeType.Sphere;
        shapeSpark.radius = 0.3f;

        var sizeOverLife = sparklePS.sizeOverLifetime;
        sizeOverLife.enabled = true;
        AnimationCurve sizeCurve = new AnimationCurve();
        sizeCurve.AddKey(0f, 1f);
        sizeCurve.AddKey(1f, 0f);
        sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        var rendSpark = sparkObj.GetComponent<ParticleSystemRenderer>();
        rendSpark.material = CreateSafeParticleMaterial();
        rendSpark.material.color = new Color(1f, 0.95f, 0.5f);
        rendSpark.material.EnableKeyword("_EMISSION");
        rendSpark.material.SetColor("_EmissionColor", new Color(1f, 0.9f, 0.3f) * 3f);

        sparklePS.Stop();
    }

    private Material CreateSafeParticleMaterial()
    {
        Shader s = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (s == null) s = Shader.Find("Universal Render Pipeline/Lit");
        if (s == null) s = Shader.Find("Universal Render Pipeline/Unlit");
        if (s == null) s = Shader.Find("Sprites/Default");
        if (s == null) s = Shader.Find("Standard");
        return s != null ? new Material(s) : new Material(Shader.Find("Hidden/InternalErrorShader"));
    }

    void CreateFlashOverlay()
    {
        // Procura Canvas existente ou cria um novo
        Canvas existingCanvas = FindFirstObjectByType<Canvas>();
        Transform canvasTransform;

        if (existingCanvas != null)
        {
            canvasTransform = existingCanvas.transform;
        }
        else
        {
            // Será criado depois pelo UIManager, então criamos nosso próprio temporário
            GameObject tempCanvas = new GameObject("JuiceCanvas");
            Canvas c = tempCanvas.AddComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            c.sortingOrder = 100; // Acima de tudo
            tempCanvas.AddComponent<CanvasScaler>();
            canvasTransform = tempCanvas.transform;
        }

        // Cria o overlay de flash
        GameObject flashObj = new GameObject("FlashOverlay");
        flashObj.transform.SetParent(canvasTransform, false);
        RectTransform rt = flashObj.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        flashImage = flashObj.AddComponent<Image>();
        flashImage.color = Color.clear;
        flashImage.raycastTarget = false; // Não interceptar cliques
        flashObj.SetActive(false);

        // Garantir que fica no topo do Canvas
        flashObj.transform.SetAsLastSibling();
    }
}
