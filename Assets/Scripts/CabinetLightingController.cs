using System.Collections;
using UnityEngine;

/// <summary>
/// Controlador de Iluminação Cênica e Efeitos Visuais do Gabinete.
/// Controla o Spotlight teatral focado na descida da garra e o show de luzes na vitória.
/// </summary>
public sealed class CabinetLightingController : MonoBehaviour
{
    public static CabinetLightingController Instance { get; private set; }

    private Light clawSpotlight;
    private Light mainCabinetLight;
    private float defaultClawSpotIntensity = 1.4f;
    private float dramaticClawSpotIntensity = 5.2f;
    private float defaultMainLightIntensity = 1.0f;

    private Coroutine spotlightRoutine;
    private Coroutine celebrationRoutine;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        SetupLights();
    }

    private void Start()
    {
        if (GameSession.Instance != null)
        {
            GameSession.Instance.OnPrizeDelivered.AddListener((p, total) => TriggerCelebrationLights());
        }
    }

    private void SetupLights()
    {
        // Encontra a luz principal do gabinete
        var allLights = FindObjectsByType<Light>(FindObjectsSortMode.None);
        foreach (var l in allLights)
        {
            if (l.type == LightType.Directional || l.name.Contains("Cabinet") || l.name.Contains("Ceiling"))
            {
                mainCabinetLight = l;
                defaultMainLightIntensity = l.intensity;
                break;
            }
        }

        // Cria ou localiza o Spotlight da garra
        var claw = FindFirstObjectByType<ClawController>();
        if (claw != null)
        {
            Transform spotTarget = claw.transform.Find("ClawSpotlight");
            if (spotTarget == null)
            {
                GameObject spotObj = new GameObject("ClawSpotlight");
                spotObj.transform.SetParent(claw.transform, false);
                spotObj.transform.localPosition = new Vector3(0f, -0.15f, 0f);
                spotObj.transform.localRotation = Quaternion.Euler(90f, 0f, 0f); // Apontando para o chão

                clawSpotlight = spotObj.AddComponent<Light>();
                clawSpotlight.type = LightType.Spot;
                clawSpotlight.spotAngle = 55f;
                clawSpotlight.innerSpotAngle = 35f;
                clawSpotlight.range = 5.5f;
                clawSpotlight.color = new Color(1.0f, 0.96f, 0.88f); // Branco quente halogênio
                clawSpotlight.intensity = defaultClawSpotIntensity;
                clawSpotlight.shadows = LightShadows.None;
            }
            else
            {
                clawSpotlight = spotTarget.GetComponent<Light>();
            }
        }
    }

    /// <summary>
    /// Ativa o modo de foco dramático (aumenta o spotlight na garra e atenua a luz ambiente)
    /// </summary>
    public void SetDramaticFocus(bool active)
    {
        if (spotlightRoutine != null) StopCoroutine(spotlightRoutine);
        spotlightRoutine = StartCoroutine(SmoothSpotlightTransition(active));
    }

    private IEnumerator SmoothSpotlightTransition(bool active)
    {
        float targetClaw = active ? dramaticClawSpotIntensity : defaultClawSpotIntensity;
        float targetMain = active ? (defaultMainLightIntensity * 0.55f) : defaultMainLightIntensity;

        float duration = 0.35f;
        float elapsed = 0f;

        float startClaw = clawSpotlight != null ? clawSpotlight.intensity : targetClaw;
        float startMain = mainCabinetLight != null ? mainCabinetLight.intensity : targetMain;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            if (clawSpotlight != null) clawSpotlight.intensity = Mathf.Lerp(startClaw, targetClaw, t);
            if (mainCabinetLight != null) mainCabinetLight.intensity = Mathf.Lerp(startMain, targetMain, t);

            yield return null;
        }

        if (clawSpotlight != null) clawSpotlight.intensity = targetClaw;
        if (mainCabinetLight != null) mainCabinetLight.intensity = targetMain;
    }

    /// <summary>
    /// Show de luzes pulsantes comemorativas ao entregar um prêmio na calha
    /// </summary>
    public void TriggerCelebrationLights()
    {
        if (celebrationRoutine != null) StopCoroutine(celebrationRoutine);
        celebrationRoutine = StartCoroutine(CelebrationLightsRoutine());
    }

    private IEnumerator CelebrationLightsRoutine()
    {
        float duration = 2.4f;
        float elapsed = 0f;

        Color[] rainbow = new Color[] {
            new Color(0f, 1f, 1f),      // Ciano
            new Color(1f, 0.1f, 0.6f),  // Magenta
            new Color(1f, 0.85f, 0.1f), // Dourado
            new Color(0.2f, 1f, 0.4f)   // Verde
        };

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            int idx = Mathf.FloorToInt((elapsed * 6f) % rainbow.Length);
            if (clawSpotlight != null)
            {
                clawSpotlight.color = Color.Lerp(clawSpotlight.color, rainbow[idx], Time.deltaTime * 12f);
                clawSpotlight.intensity = 4.0f + Mathf.Sin(elapsed * 16f) * 1.5f;
            }
            yield return null;
        }

        // Restaura a cor normal
        if (clawSpotlight != null)
        {
            clawSpotlight.color = new Color(1.0f, 0.96f, 0.88f);
            clawSpotlight.intensity = defaultClawSpotIntensity;
        }
    }
}
