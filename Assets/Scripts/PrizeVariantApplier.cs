using UnityEngine;

/// <summary>
/// Aplica skins, recolors PBR e efeitos especiais procedurais às 18 variantes de prêmios.
/// Transforma os 6 modelos 3D base em 18 itens colecionáveis distintos no álbum.
/// </summary>
public static class PrizeVariantApplier
{
    public static void ApplyVariantStyle(GameObject prizeObj, string variantId, PrizeRarity rarity)
    {
        if (prizeObj == null || string.IsNullOrEmpty(variantId)) return;

        Renderer[] renderers = prizeObj.GetComponentsInChildren<Renderer>(true);
        if (renderers == null || renderers.Length == 0) return;

        string id = variantId.ToLowerInvariant();

        // ======================== FAMÍLIA RAPOSA ========================
        if (id.Contains("fox_arctic"))
        {
            // Raposa do Ártico: Branco neve luminoso puro com emissão ciano gélida
            SetColors(renderers, new Color(1.0f, 1.0f, 1.0f), 0.05f, 0.90f, new Color(0.2f, 0.7f, 1.0f) * 0.7f, true);
        }
        else if (id.Contains("fox_shadow"))
        {
            // Raposa Sombria: Preto carvão profundo com detalhes e olhos magenta neon brilhantes
            SetColors(renderers, new Color(0.06f, 0.06f, 0.08f), 0.35f, 0.85f, new Color(2.5f, 0.0f, 1.2f), true);
        }

        // ======================== FAMÍLIA URSO ========================
        else if (id.Contains("bear_panda"))
        {
            // Urso Panda: Branco puro com alto contraste
            SetColors(renderers, new Color(1.0f, 1.0f, 1.0f), 0.02f, 0.85f, null, true);
        }
        else if (id.Contains("bear_polar"))
        {
            // Urso Polar Glacial: Branco glacial puro com reflexos de gelo
            SetColors(renderers, new Color(0.98f, 1.0f, 1.0f), 0.10f, 0.95f, new Color(0.4f, 0.8f, 1.2f) * 0.4f, true);
        }
        else if (id.Contains("bear_galaxy"))
        {
            // Urso Cósmico Galaxy: Roxo estelar profundo com emissão cósmica ultravioleta vibrante
            SetColors(renderers, new Color(0.40f, 0.08f, 0.70f), 0.60f, 0.90f, new Color(2.2f, 0.4f, 3.2f), true);
        }

        // ======================== FAMÍLIA PEIXE BALÃO ========================
        else if (id.Contains("fish_clown"))
        {
            // Peixe Palhaço: Laranja tropical elétrico super vivo (estilo Nemo)
            SetColors(renderers, new Color(1.0f, 0.40f, 0.0f), 0.05f, 0.85f, null, true);
        }
        else if (id.Contains("fish_gold"))
        {
            // Peixinho Dourado: Ouro 24k metálico espelhado puro com brilho radiante
            SetColors(renderers, new Color(1.0f, 0.85f, 0.10f), 0.98f, 0.96f, new Color(2.2f, 1.8f, 0.3f), true);
        }

        // ======================== FAMÍLIA COALA ========================
        else if (id.Contains("koala_eucalyptus"))
        {
            // Coala Eucalipto: Verde menta fresco alegre e vivo
            SetColors(renderers, new Color(0.15f, 0.90f, 0.60f), 0.05f, 0.85f, null, true);
        }
        else if (id.Contains("koala_king"))
        {
            // Coala Real Supremo: Dourado imperial com coroa reluzente
            SetColors(renderers, new Color(0.95f, 0.88f, 0.65f), 0.92f, 0.92f, new Color(2.4f, 1.9f, 0.5f), true);
        }

        // ======================== FAMÍLIA TEXUGO ========================
        else if (id.Contains("badger_honey"))
        {
            // Texugo do Mel: Amarelo mel / âmbar solar reluzente
            SetColors(renderers, new Color(1.0f, 0.75f, 0.08f), 0.20f, 0.88f, new Color(1.2f, 0.8f, 0.1f), true);
        }

        // ======================== FAMÍLIA PORQUINHO ========================
        else if (id.Contains("porky_classic") || id.Contains("porky_pink"))
        {
            // Porquinho Rosa Chiclete doce e super fofo
            SetColors(renderers, new Color(1.0f, 0.45f, 0.72f), 0.05f, 0.88f, null, true);
        }
        else if (id.Contains("porky_diamond"))
        {
            // Porquinho Diamante: Cristal de joia ciano puro reluzente
            SetColors(renderers, new Color(0.70f, 0.95f, 1.0f), 0.95f, 0.98f, new Color(1.5f, 2.4f, 3.5f), true);
        }
    }

    private static void SetColors(Renderer[] renderers, Color baseColor, float metallic, float smoothness, Color? emission = null, bool unbindBaseMap = false)
    {
        foreach (var rend in renderers)
        {
            if (rend == null) continue;
            foreach (var mat in rend.materials)
            {
                if (mat == null) continue;

                if (unbindBaseMap)
                {
                    if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", Texture2D.whiteTexture);
                    if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", Texture2D.whiteTexture);
                }

                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", baseColor);
                else if (mat.HasProperty("_Color")) mat.SetColor("_Color", baseColor);

                if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", metallic);
                if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);

                if (emission.HasValue)
                {
                    mat.EnableKeyword("_EMISSION");
                    if (mat.HasProperty("_EmissionColor")) mat.SetColor("_EmissionColor", emission.Value);
                }
            }
        }
    }
}
