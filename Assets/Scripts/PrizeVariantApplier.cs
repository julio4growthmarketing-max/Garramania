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
            // Raposa do Ártico: Branca pura com toque ciano gélido
            SetColors(renderers, new Color(0.92f, 0.97f, 1.0f), 0.1f, 0.8f);
        }
        else if (id.Contains("fox_shadow"))
        {
            // Raposa Sombria: Preto carvão com detalhes magenta neon
            SetColors(renderers, new Color(0.12f, 0.12f, 0.15f), 0.3f, 0.6f, new Color(1.8f, 0.1f, 0.8f));
        }

        // ======================== FAMÍLIA URSO ========================
        else if (id.Contains("bear_panda"))
        {
            // Urso Panda: Corpo branco e detalhes pretos
            SetColors(renderers, new Color(0.95f, 0.95f, 0.95f), 0.05f, 0.85f);
        }
        else if (id.Contains("bear_polar"))
        {
            // Urso Polar Glacial: Branco neve reluzente
            SetColors(renderers, new Color(0.98f, 0.98f, 1.0f), 0.2f, 0.9f);
        }
        else if (id.Contains("bear_galaxy"))
        {
            // Urso Cósmico: Roxo galáctico profundo com emissão estelar
            SetColors(renderers, new Color(0.25f, 0.05f, 0.45f), 0.6f, 0.7f, new Color(1.2f, 0.3f, 2.2f));
        }

        // ======================== FAMÍLIA PEIXE BALÃO ========================
        else if (id.Contains("fish_clown"))
        {
            // Peixe Palhaço: Laranja tropical vibrante
            SetColors(renderers, new Color(1.0f, 0.42f, 0.05f), 0.1f, 0.8f);
        }
        else if (id.Contains("fish_gold"))
        {
            // Peixinho Dourado: Ouro metálico polido
            SetColors(renderers, new Color(1.0f, 0.82f, 0.15f), 0.92f, 0.90f, new Color(1.5f, 1.2f, 0.2f));
        }

        // ======================== FAMÍLIA COALA ========================
        else if (id.Contains("koala_eucalyptus"))
        {
            // Coala Eucalipto: Tons de verde floresta suave
            SetColors(renderers, new Color(0.40f, 0.65f, 0.48f), 0.05f, 0.8f);
        }
        else if (id.Contains("koala_king"))
        {
            // Coala Real: Prateado com coroa e brilho dourado
            SetColors(renderers, new Color(0.85f, 0.88f, 0.95f), 0.75f, 0.85f, new Color(2.0f, 1.6f, 0.3f));
        }

        // ======================== FAMÍLIA TEXUGO ========================
        else if (id.Contains("badger_honey"))
        {
            // Texugo do Mel: Âmbar e mel quente
            SetColors(renderers, new Color(0.92f, 0.68f, 0.25f), 0.2f, 0.8f);
        }

        // ======================== FAMÍLIA PORQUINHO ========================
        else if (id.Contains("porky_classic") || id.Contains("porky_pink"))
        {
            // Porquinho Rosa Chiclete clássico
            SetColors(renderers, new Color(1.0f, 0.65f, 0.78f), 0.05f, 0.85f);
        }
        else if (id.Contains("porky_diamond"))
        {
            // Porquinho Diamante: Cristalino brilhante com reflexos iridescentes
            SetColors(renderers, new Color(0.85f, 0.95f, 1.0f), 0.98f, 0.98f, new Color(1.0f, 1.8f, 2.5f));
        }
    }

    private static void SetColors(Renderer[] renderers, Color baseColor, float metallic, float smoothness, Color? emission = null)
    {
        foreach (var rend in renderers)
        {
            if (rend == null) continue;
            foreach (var mat in rend.materials)
            {
                if (mat == null) continue;
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
