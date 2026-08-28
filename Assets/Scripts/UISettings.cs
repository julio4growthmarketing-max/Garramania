using UnityEngine;

[CreateAssetMenu(fileName = "UISettings", menuName = "GarraMania/UI Settings", order = 1)]
public class UISettings : ScriptableObject
{
    private static UISettings _instance;
    public static UISettings Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = Resources.Load<UISettings>("UISettings");
                if (_instance == null)
                {
                    _instance = CreateInstance<UISettings>();
                }
            }
            return _instance;
        }
    }

    [Header("Cores Neon Arcade")]
    public Color neonCyan = new Color(0f, 0.95f, 1f, 1f);
    public Color neonMagenta = new Color(1f, 0.08f, 0.58f, 1f);
    public Color neonGold = new Color(1f, 0.88f, 0.12f, 1f);
    public Color neonGreen = new Color(0.1f, 1f, 0.55f, 1f);
    public Color neonRed = new Color(1f, 0.15f, 0.25f, 1f);

    [Header("Cores de Painel / Vidro")]
    public Color bgDarkGlass = new Color(0.04f, 0.05f, 0.09f, 0.85f);
    public Color bgCardSlate = new Color(0.08f, 0.11f, 0.18f, 0.92f);
    public Color borderCyanGlow = new Color(0f, 0.95f, 1f, 0.55f);
    public Color borderGoldGlow = new Color(1f, 0.85f, 0.1f, 0.65f);

    [Header("Curva de Força da Garra")]
    [Tooltip("Curva aplicada à barra de força ajustável do mobile")]
    public AnimationCurve clawForceCurve = AnimationCurve.EaseInOut(0f, 0.2f, 1f, 1.0f);
    public float defaultClawForce = 1.0f;

    public float EvaluateClawForce(float rawNormalized)
    {
        if (clawForceCurve == null || clawForceCurve.length == 0)
            return Mathf.Clamp01(rawNormalized);
        return Mathf.Clamp01(clawForceCurve.Evaluate(Mathf.Clamp01(rawNormalized)));
    }
}
