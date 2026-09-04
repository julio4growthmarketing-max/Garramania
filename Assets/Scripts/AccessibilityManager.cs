using System;
using UnityEngine;

/// <summary>
/// Gerenciador de Acessibilidade do GarraMania.
/// Fornece opções de alto contraste, controle de vibração tátil, redução de movimento
/// e garantia de áreas mínimas de toque (48x48dp) de acordo com diretrizes a11y.
/// </summary>
public class AccessibilityManager : MonoBehaviour
{
    public static AccessibilityManager Instance { get; private set; }

    public const string PREF_HIGH_CONTRAST = "A11Y_HighContrast";
    public const string PREF_HAPTICS       = "A11Y_Haptics";
    public const string PREF_REDUCE_MOTION = "A11Y_ReduceMotion";
    public const string PREF_LARGE_TOUCH   = "A11Y_LargeTouch";

    public bool HighContrast { get; private set; }
    public bool HapticsEnabled { get; private set; }
    public bool ReduceMotion { get; private set; }
    public bool LargeTouchTargets { get; private set; }

    public event Action OnSettingsChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        LoadPreferences();
    }

    public void LoadPreferences()
    {
        HighContrast = PlayerPrefs.GetInt(PREF_HIGH_CONTRAST, 0) == 1;
        HapticsEnabled = PlayerPrefs.GetInt(PREF_HAPTICS, 1) == 1;
        ReduceMotion = PlayerPrefs.GetInt(PREF_REDUCE_MOTION, 0) == 1;
        LargeTouchTargets = PlayerPrefs.GetInt(PREF_LARGE_TOUCH, 1) == 1;
    }

    public void SetHighContrast(bool enabled)
    {
        HighContrast = enabled;
        PlayerPrefs.SetInt(PREF_HIGH_CONTRAST, enabled ? 1 : 0);
        PersistentSaveManager.MarkDirty();
        OnSettingsChanged?.Invoke();
    }

    public void SetHaptics(bool enabled)
    {
        HapticsEnabled = enabled;
        PlayerPrefs.SetInt(PREF_HAPTICS, enabled ? 1 : 0);
        PersistentSaveManager.MarkDirty();
        OnSettingsChanged?.Invoke();
    }

    public void SetReduceMotion(bool enabled)
    {
        ReduceMotion = enabled;
        PlayerPrefs.SetInt(PREF_REDUCE_MOTION, enabled ? 1 : 0);
        PersistentSaveManager.MarkDirty();
        OnSettingsChanged?.Invoke();
    }

    public void SetLargeTouchTargets(bool enabled)
    {
        LargeTouchTargets = enabled;
        PlayerPrefs.SetInt(PREF_LARGE_TOUCH, enabled ? 1 : 0);
        PersistentSaveManager.MarkDirty();
        OnSettingsChanged?.Invoke();
    }

    /// <summary>
    /// Dispara vibração tátil no celular se habilitada nas preferências.
    /// </summary>
    public void TriggerHaptic(long durationMs = 40)
    {
        if (!HapticsEnabled) return;

#if UNITY_ANDROID || UNITY_IOS
        try
        {
            Handheld.Vibrate();
        }
        catch { }
#endif
    }
}
