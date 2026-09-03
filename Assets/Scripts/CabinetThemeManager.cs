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
        Transform poster = root.Find("Poster_Neon_Garramania");
        if (poster == null)
        {
            Transform paredes = root.Find("04_Paredes_Vidros");
            if (paredes != null) poster = paredes.Find("Poster_Neon_Garramania");
        }

        if (poster == null)
        {
            GameObject pObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            pObj.name = "Poster_Neon_Garramania";
            pObj.transform.SetParent(root, false);
            pObj.transform.position = new Vector3(0, 1.25f, 2.52f);
            pObj.transform.localScale = new Vector3(3.8f, 1.9f, 0.02f);
            pObj.transform.rotation = Quaternion.identity;
            Destroy(pObj.GetComponent<Collider>());

            Material m = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            Texture2D tex = Resources.Load<Texture2D>("Textures/MarqueeBanner");
            if (tex != null)
            {
                m.mainTexture = tex;
                m.mainTextureScale = new Vector2(1, -1);
                m.mainTextureOffset = new Vector2(0, 1);
                m.EnableKeyword("_EMISSION");
                m.SetTexture("_EmissionMap", tex);
                if (m.HasProperty("_EmissionMap"))
                {
                    m.SetTextureScale("_EmissionMap", new Vector2(1, -1));
                    m.SetTextureOffset("_EmissionMap", new Vector2(0, 1));
                }
                m.SetColor("_EmissionColor", Color.white * 2.2f);
            }
            pObj.GetComponent<MeshRenderer>().material = m;
            poster = pObj.transform;

            GameObject trimTop = GameObject.CreatePrimitive(PrimitiveType.Cube);
            trimTop.name = "Moldura_Poster_Neon_Top";
            trimTop.transform.SetParent(root, false);
            trimTop.transform.position = new Vector3(0, 2.22f, 2.51f);
            trimTop.transform.localScale = new Vector3(3.9f, 0.06f, 0.04f);
            Destroy(trimTop.GetComponent<Collider>());

            GameObject trimBot = GameObject.CreatePrimitive(PrimitiveType.Cube);
            trimBot.name = "Moldura_Poster_Neon_Bot";
            trimBot.transform.SetParent(root, false);
            trimBot.transform.position = new Vector3(0, 0.28f, 2.51f);
            trimBot.transform.localScale = new Vector3(3.9f, 0.06f, 0.04f);
            Destroy(trimBot.GetComponent<Collider>());
        }

        if (poster != null)
        {
            Renderer r = poster.GetComponent<Renderer>();
            if (r != null)
            {
                Texture2D tex = Resources.Load<Texture2D>("Textures/MarqueeBanner");
                if (tex != null)
                {
                    r.material.mainTexture = tex;
                    r.material.mainTextureScale = new Vector2(1, -1);
                    r.material.mainTextureOffset = new Vector2(0, 1);
                    r.material.EnableKeyword("_EMISSION");
                    r.material.SetTexture("_EmissionMap", tex);
                    if (r.material.HasProperty("_EmissionMap"))
                    {
                        r.material.SetTextureScale("_EmissionMap", new Vector2(1, -1));
                        r.material.SetTextureOffset("_EmissionMap", new Vector2(0, 1));
                    }
                    r.material.SetColor("_EmissionColor", Color.white * 2.2f);
                }
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
