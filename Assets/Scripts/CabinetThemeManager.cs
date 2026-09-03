using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Gerenciador dinâmico de temas do gabinete arcade.
/// Permite alternar entre Cyber Neon, Kawaii Pastel e Gold Casino em tempo real.
/// </summary>
public sealed class CabinetThemeManager : MonoBehaviour
{
    private static CabinetThemeManager _instance;
    public static CabinetThemeManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindFirstObjectByType<CabinetThemeManager>();
                if (_instance == null)
                {
                    GameObject go = new GameObject("CabinetThemeManager");
                    _instance = go.AddComponent<CabinetThemeManager>();
                    DontDestroyOnLoad(go);
                }
            }
            return _instance;
        }
    }

    private const string PREF_SELECTED_THEME = "GarraMania_CabinetTheme";

    public CabinetThemeData CurrentTheme { get; private set; }
    public CabinetThemeType CurrentThemeType { get; private set; } = CabinetThemeType.CyberNeon;

    private readonly List<CabinetThemeData> availableThemes = new List<CabinetThemeData>();
    private int currentThemeIndex = 0;

    public UnityEvent<CabinetThemeData> OnThemeChanged = new UnityEvent<CabinetThemeData>();

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        DontDestroyOnLoad(gameObject);

        InitializeThemes();
        LoadSavedTheme();
    }

    private void Start()
    {
        ApplyCurrentTheme();
    }

    private void InitializeThemes()
    {
        availableThemes.Clear();
        availableThemes.Add(CabinetThemeData.CreateCyberNeon());
        availableThemes.Add(CabinetThemeData.CreateKawaiiPastel());
        availableThemes.Add(CabinetThemeData.CreateGoldCasino());
    }

    private void LoadSavedTheme()
    {
        int savedType = PlayerPrefs.GetInt(PREF_SELECTED_THEME, (int)CabinetThemeType.CyberNeon);
        savedType = Mathf.Clamp(savedType, 0, availableThemes.Count - 1);
        currentThemeIndex = savedType;
        CurrentTheme = availableThemes[currentThemeIndex];
        CurrentThemeType = CurrentTheme.themeType;
    }

    public void NextTheme()
    {
        currentThemeIndex = (currentThemeIndex + 1) % availableThemes.Count;
        SetThemeByIndex(currentThemeIndex);
    }

    public void PreviousTheme()
    {
        currentThemeIndex = (currentThemeIndex - 1 + availableThemes.Count) % availableThemes.Count;
        SetThemeByIndex(currentThemeIndex);
    }

    public void SetTheme(CabinetThemeType type)
    {
        int idx = availableThemes.FindIndex(t => t.themeType == type);
        if (idx >= 0) SetThemeByIndex(idx);
    }

    private void SetThemeByIndex(int idx)
    {
        currentThemeIndex = idx;
        CurrentTheme = availableThemes[currentThemeIndex];
        CurrentThemeType = CurrentTheme.themeType;

        PlayerPrefs.SetInt(PREF_SELECTED_THEME, (int)CurrentThemeType);
        PlayerPrefs.Save();

        ApplyCurrentTheme();
        OnThemeChanged?.Invoke(CurrentTheme);
    }

    public void ApplyCurrentTheme()
    {
        if (CurrentTheme == null) return;

        GameObject cabinet = GameObject.Find("Gabinete_Arcade_Modular");
        if (cabinet != null)
        {
            ApplyThemeToCabinetHierarchy(cabinet.transform, CurrentTheme);
        }

        // Atualiza iluminação cênica
        UpdateLighting(CurrentTheme);
    }

    private void ApplyThemeToCabinetHierarchy(Transform root, CabinetThemeData theme)
    {
        Renderer[] allRenderers = root.GetComponentsInChildren<Renderer>(true);

        foreach (Renderer rend in allRenderers)
        {
            if (rend == null) continue;
            string n = rend.gameObject.name.ToLowerInvariant();

            // Neons e faixas de luz
            if (n.Contains("neon") || n.Contains("tube") || n.Contains("led") || n.Contains("glow"))
            {
                Color targetNeon = n.Contains("magenta") || n.Contains("pink") || n.Contains("accent") 
                    ? theme.neonColor2 
                    : theme.neonColor1;
                ApplyEmissionColor(rend, targetNeon);
            }
            // Chassis / Estrutura do Gabinete
            else if (n.Contains("chassis") || n.Contains("pedestal") || n.Contains("canopy") || n.Contains("moldura") || n.Contains("coluna"))
            {
                ApplyBaseColor(rend, theme.chassisColor);
            }
            // Peças metálicas e frisos
            else if (n.Contains("metal") || n.Contains("friso") || n.Contains("trim") || n.Contains("handle") || n.Contains("trilho"))
            {
                ApplyBaseColor(rend, theme.accentMetalColor);
            }
            // Piso / Chão interno da vitrine
            else if (n.Contains("piso") || n.Contains("floor"))
            {
                ApplyBaseColor(rend, theme.floorColorA);
            }
        }

        // Letreiro superior (Marquee)
        Transform marqueeTransform = root.Find("MarqueeText") ?? root.Find("Dossel/MarqueeText");
        if (marqueeTransform != null)
        {
            var textComp = marqueeTransform.GetComponent<TextMesh>();
            if (textComp != null) textComp.text = theme.marqueeTitle;
        }

        // Pôster Neon Retroiluminado de Fundo da Máquina
        EnsureNeonWallpaperPoster(root, theme);
    }

    private void EnsureNeonWallpaperPoster(Transform root, CabinetThemeData theme)
    {
        // 1. Limpa molduras e pôsteres flutuantes antigos
        Transform trimT = root.Find("Moldura_Poster_Neon_Top") ?? root.Find("04_Paredes_Vidros/Moldura_Poster_Neon_Top");
        if (trimT != null) Destroy(trimT.gameObject);
        Transform trimB = root.Find("Moldura_Poster_Neon_Bot") ?? root.Find("04_Paredes_Vidros/Moldura_Poster_Neon_Bot");
        if (trimB != null) Destroy(trimB.gameObject);
        Transform poster = root.Find("Poster_Neon_Garramania") ?? root.Find("04_Paredes_Vidros/Poster_Neon_Garramania");
        if (poster != null) Destroy(poster.gameObject);

        // 2. Carrega a textura exclusiva do tema
        string texPath = !string.IsNullOrEmpty(theme.wallpaperResourcePath) ? theme.wallpaperResourcePath : "Textures/Wallpaper_CyberNeon";
        Texture2D tex = Resources.Load<Texture2D>(texPath) ?? Resources.Load<Texture2D>("Textures/Wallpaper_CyberNeon");

        // 3. Aplica diretamente na parede traseira existente de 5x5m (Mural_Galaxy_Fundo)
        Transform mural = root.Find("04_Paredes_Vidros/Mural_Galaxy_Fundo") ?? root.Find("Mural_Galaxy_Fundo");
        if (mural == null)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "Mural_Galaxy_Fundo") { mural = t; break; }
            }
        }

        if (mural != null && tex != null)
        {
            Renderer r = mural.GetComponent<Renderer>();
            if (r != null)
            {
                r.material.mainTexture = tex;
                // No cubo frontal (-Z) do Unity: scale (-1, -1) e offset (1, 1) garante orientação em pé e leitura correta da esquerda para a direita (sem espelhamento)
                r.material.mainTextureScale = new Vector2(-1, -1);
                r.material.mainTextureOffset = new Vector2(1, 1);
                r.material.EnableKeyword("_EMISSION");
                r.material.SetTexture("_EmissionMap", tex);
                if (r.material.HasProperty("_EmissionMap"))
                {
                    r.material.SetTextureScale("_EmissionMap", new Vector2(-1, -1));
                    r.material.SetTextureOffset("_EmissionMap", new Vector2(1, 1));
                }
                r.material.SetColor("_BaseColor", Color.white);
                r.material.SetColor("_EmissionColor", Color.white * 1.5f);
            }
        }
    }

    private void ApplyBaseColor(Renderer rend, Color color)
    {
        foreach (Material mat in rend.materials)
        {
            if (mat == null) continue;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            else if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
        }
    }

    private void ApplyEmissionColor(Renderer rend, Color color)
    {
        foreach (Material mat in rend.materials)
        {
            if (mat == null) continue;
            mat.EnableKeyword("_EMISSION");
            if (mat.HasProperty("_EmissionColor")) mat.SetColor("_EmissionColor", color);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color * 0.5f);
        }
    }

    private void UpdateLighting(CabinetThemeData theme)
    {
        var allLights = FindObjectsByType<Light>(FindObjectsSortMode.None);
        foreach (var l in allLights)
        {
            if (l == null) continue;
            if (l.type == LightType.Directional)
            {
                l.color = theme.spotlightColor;
            }
            else if (l.type == LightType.Spot)
            {
                l.color = theme.spotlightColor;
            }
            else if (l.type == LightType.Point && l.name.Contains("Neon"))
            {
                l.color = theme.neonColor1;
            }
        }
    }

    public List<CabinetThemeData> GetAllThemes() => availableThemes;
}
