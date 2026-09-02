using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Sistema Central de Design System & Estilos do GarraMania.
/// Centraliza cores, fontes, gradientes e geração de sprites procedurais com cache de alta performance.
/// </summary>
public static class UITheme
{
    // =========================================================================
    // PALETA DE CORES OFICIAL (GarraMania Cyber-Arcade)
    // =========================================================================
    public static readonly Color ColorBgDeepNavy   = new Color(0.08f, 0.09f, 0.18f, 0.95f);  // #14172E (Fundo de modais e painéis)
    public static readonly Color ColorCardDark     = new Color(0.11f, 0.13f, 0.25f, 0.96f);  // #1C2140 (Fundo de cards e slots)
    public static readonly Color ColorCardSlot     = new Color(0.15f, 0.18f, 0.32f, 0.90f);  // #262E52 (Fundo de pedestais de prêmios)
    public static readonly Color ColorNeonGold     = new Color(1.00f, 0.85f, 0.12f, 1.00f);  // #FFD91F (Acentos VIP, moedas e rankings)
    public static readonly Color ColorNeonCyan     = new Color(0.20f, 0.88f, 1.00f, 1.00f);  // #33E0FF (Bordas cibernéticas e tecnologia)
    public static readonly Color ColorNeonPink     = new Color(1.00f, 0.25f, 0.60f, 1.00f);  // #FF4099 (Destaques e botões de impacto)
    public static readonly Color ColorNeonPurple   = new Color(0.60f, 0.25f, 1.00f, 1.00f);  // #9940FF (Cards de raridade épica)
    public static readonly Color ColorNeonGreen    = new Color(0.12f, 0.92f, 0.45f, 1.00f);  // #1FEB73 (Botões de compra e jogar)
    public static readonly Color ColorNeonRed      = new Color(1.00f, 0.22f, 0.25f, 1.00f);  // #FF3840 (Botão Sanwa de agarrar e alertas)
    public static readonly Color ColorTextOutline  = new Color(0.04f, 0.05f, 0.12f, 0.98f);  // Contorno escuro pesado para alto contraste
    public static readonly Color ColorTextLight    = new Color(0.95f, 0.97f, 1.00f, 1.00f);  // Texto claro primário

    // =========================================================================
    // CACHE DE FONTES
    // =========================================================================
    private static Font cachedArcadeFont;

    public static Font GetArcadeFont()
    {
        if (cachedArcadeFont == null)
        {
            cachedArcadeFont = Resources.Load<Font>("Fonts/LilitaOne-Regular");
            if (cachedArcadeFont == null)
            {
                // Fallback dinâmico para fonte padrão do Unity se Lilita One não estiver em Resources
                cachedArcadeFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
            }
        }
        return cachedArcadeFont;
    }

    // =========================================================================
    // CACHE DE SPRITES PROCEDURAIS (Zero Alocação em Tempo de Execução)
    // =========================================================================
    private static Sprite cachedRoundedRectSprite;
    private static Sprite cachedCircleSprite;
    private static Sprite cachedGradientPinkPurpleSprite;
    private static Sprite cachedYellowDropSprite;
    private static Sprite cachedGreenBuySprite;
    private static Sprite cachedWhiteGhostSprite;

    private static readonly Dictionary<string, Sprite> uiSpriteCache = new Dictionary<string, Sprite>();
    private static readonly Dictionary<string, Sprite> portraitCache = new Dictionary<string, Sprite>();

    public static Sprite GetRoundedRectSprite()
    {
        if (cachedRoundedRectSprite != null) return cachedRoundedRectSprite;

        int size = 32;
        int cornerRadius = 9;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;

        Color[] pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                int dx = Mathf.Max(0, Mathf.Max(cornerRadius - x, x - (size - 1 - cornerRadius)));
                int dy = Mathf.Max(0, Mathf.Max(cornerRadius - y, y - (size - 1 - cornerRadius)));
                float dist = Mathf.Sqrt(dx * dx + dy * dy);

                float alpha = Mathf.Clamp01((cornerRadius - dist) + 0.5f);
                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        }
        tex.SetPixels(pixels);
        tex.Apply(false, true); // makeNoLongerReadable = true para liberar RAM

        cachedRoundedRectSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(10, 10, 10, 10));
        return cachedRoundedRectSprite;
    }

    public static Sprite GetCircleSprite()
    {
        if (cachedCircleSprite != null) return cachedCircleSprite;

        int size = 64;
        float radius = (size - 2) * 0.5f;
        float center = (size - 1) * 0.5f;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;

        Color[] pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                float alpha = Mathf.Clamp01((radius - dist) + 0.5f);
                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        }
        tex.SetPixels(pixels);
        tex.Apply(false, true);

        cachedCircleSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        return cachedCircleSprite;
    }

    public static Sprite GetGradientPinkPurpleSprite()
    {
        if (cachedGradientPinkPurpleSprite != null) return cachedGradientPinkPurpleSprite;
        cachedGradientPinkPurpleSprite = CreateGradientRoundedSprite(new Color(1.0f, 0.20f, 0.55f), new Color(0.55f, 0.15f, 0.95f), new Color(1.0f, 0.50f, 0.75f, 0.4f));
        return cachedGradientPinkPurpleSprite;
    }

    public static Sprite GetYellowDropSprite()
    {
        if (cachedYellowDropSprite != null) return cachedYellowDropSprite;
        cachedYellowDropSprite = CreateGradientRoundedSprite(new Color(1.0f, 0.82f, 0.05f), new Color(0.95f, 0.55f, 0.02f), new Color(1.0f, 1.0f, 0.6f, 0.5f));
        return cachedYellowDropSprite;
    }

    public static Sprite GetGreenBuySprite()
    {
        if (cachedGreenBuySprite != null) return cachedGreenBuySprite;
        cachedGreenBuySprite = CreateGradientRoundedSprite(new Color(0.10f, 0.88f, 0.40f), new Color(0.04f, 0.62f, 0.25f), new Color(0.50f, 1.0f, 0.70f, 0.45f));
        return cachedGreenBuySprite;
    }

    public static Sprite GetWhiteGhostSprite()
    {
        if (cachedWhiteGhostSprite != null) return cachedWhiteGhostSprite;
        cachedWhiteGhostSprite = CreateGradientRoundedSprite(new Color(0.88f, 0.92f, 0.98f, 0.92f), new Color(0.72f, 0.78f, 0.88f, 0.92f), new Color(1.0f, 1.0f, 1.0f, 0.5f));
        return cachedWhiteGhostSprite;
    }

    private static Sprite CreateGradientRoundedSprite(Color topColor, Color bottomColor, Color highlightColor)
    {
        int w = 64;
        int h = 64;
        int cornerRadius = 16;
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;

        Color[] pixels = new Color[w * h];
        for (int y = 0; y < h; y++)
        {
            float t = (float)y / (h - 1);
            Color baseC = Color.Lerp(bottomColor, topColor, t);

            for (int x = 0; x < w; x++)
            {
                int dx = Mathf.Max(0, Mathf.Max(cornerRadius - x, x - (w - 1 - cornerRadius)));
                int dy = Mathf.Max(0, Mathf.Max(cornerRadius - y, y - (h - 1 - cornerRadius)));
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                float alpha = Mathf.Clamp01((cornerRadius - dist) + 0.5f);

                Color c = baseC;
                if (y >= h - cornerRadius && dist < cornerRadius)
                {
                    c = Color.Lerp(c, highlightColor, 0.35f);
                }
                c.a *= alpha;
                pixels[y * w + x] = c;
            }
        }
        tex.SetPixels(pixels);
        tex.Apply(false, true);

        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(18, 18, 18, 18));
    }

    public static Sprite GetUISprite(string name, Vector4 border = default)
    {
        if (string.IsNullOrEmpty(name)) return null;
        if (uiSpriteCache.TryGetValue(name, out Sprite cached) && cached != null) return cached;

        Sprite sp = Resources.Load<Sprite>($"KitUI/{name}") ?? Resources.Load<Sprite>($"UI/{name}");
        if (sp != null)
        {
            if (border != default)
            {
                sp = Sprite.Create(sp.texture, sp.rect, new Vector2(0.5f, 0.5f), sp.pixelsPerUnit, 0, SpriteMeshType.FullRect, border);
            }
            uiSpriteCache[name] = sp;
            return sp;
        }
        return GetRoundedRectSprite();
    }

    public static Sprite GetPlushiePortrait(string id)
    {
        if (string.IsNullOrEmpty(id)) id = "fox";
        string key = id.ToLowerInvariant();
        if (key.Contains("fox")) key = "fox";
        else if (key.Contains("green") || key.Contains("bear")) key = "greenbear";
        else if (key.Contains("fish") || key.Contains("balloon")) key = "balloonfish";
        else if (key.Contains("koala")) key = "koala";
        else if (key.Contains("badger")) key = "badger";
        else if (key.Contains("pork") || key.Contains("pig")) key = "porky";
        else key = "fox";

        if (portraitCache.TryGetValue(key, out Sprite cached) && cached != null) return cached;

        Texture2D tex = Resources.Load<Texture2D>($"Textures/Portraits/portrait_{key}");
        if (tex != null)
        {
            Sprite sp = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
            portraitCache[key] = sp;
            return sp;
        }
        return null;
    }

    // =========================================================================
    // RESOLUÇÃO DE TEMAS DE BOTÕES 3D ARCADE
    // =========================================================================
    public struct ButtonStyle
    {
        public Color BgColor;
        public Color LabelColor;
        public Color BevelColor;
        public Color OutlineColor;
    }

    public static ButtonStyle GetButtonStyle(Button3DTheme theme)
    {
        ButtonStyle style = new ButtonStyle();
        style.OutlineColor = ColorTextOutline;

        switch (theme)
        {
            case Button3DTheme.Emerald:
                style.BgColor = ColorNeonGreen * 0.88f;
                style.LabelColor = Color.white;
                style.BevelColor = new Color(1f, 1f, 1f, 0.30f);
                break;
            case Button3DTheme.Gold:
                style.BgColor = ColorNeonGold;
                style.LabelColor = new Color(0.08f, 0.08f, 0.12f, 1f);
                style.BevelColor = new Color(1f, 1f, 1f, 0.45f);
                break;
            case Button3DTheme.SanwaRed:
                style.BgColor = ColorNeonRed;
                style.LabelColor = Color.white;
                style.BevelColor = new Color(1f, 1f, 1f, 0.28f);
                break;
            case Button3DTheme.PurplePink:
                style.BgColor = ColorNeonPink;
                style.LabelColor = Color.white;
                style.BevelColor = new Color(1f, 1f, 1f, 0.30f);
                break;
            case Button3DTheme.YellowDrop:
                style.BgColor = new Color(1.0f, 0.88f, 0.15f, 1f);
                style.LabelColor = new Color(0.08f, 0.09f, 0.18f, 1f);
                style.BevelColor = new Color(1f, 1f, 1f, 0.40f);
                break;
            case Button3DTheme.WhiteGhost:
                style.BgColor = new Color(0.88f, 0.92f, 0.98f, 0.92f);
                style.LabelColor = new Color(0.08f, 0.12f, 0.24f, 1f);
                style.BevelColor = new Color(1f, 1f, 1f, 0.50f);
                break;
            case Button3DTheme.Sapphire:
            default:
                style.BgColor = ColorCardDark;
                style.LabelColor = ColorNeonCyan;
                style.BevelColor = ColorNeonCyan * 0.45f;
                break;
        }

        return style;
    }
}
