using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Ferramenta de Otimização Extrema para Mobile / iOS Safari.
/// Reduz o consumo de memória VRAM e heap para rodar liso no Safari do iPhone sem estourar limites de memória.
/// </summary>
public static class MobileOptimizationTool
{
    [MenuItem("GarraMania/Otimizar para Mobile e iOS")]
    public static void OptimizeForMobileAndIOS()
    {
        Debug.Log("[MobileOptimizationTool] Iniciando otimização para iOS Safari...");

        // 1. Move modelos 3D não utilizados fora de Resources para não inflar Build_Web.data
        string unusedGlb = "Assets/Resources/Cabinet/ClawAnimated.glb";
        string targetGlb = "Assets/ClawArcadeAimated/ClawAnimated_Backup.glb";
        if (File.Exists(unusedGlb))
        {
            AssetDatabase.MoveAsset(unusedGlb, targetGlb);
            Debug.Log("[MobileOptimizationTool] Movido ClawAnimated.glb fora de Resources (-22MB salvos!).");
        }

        // 2. Configura tamanho de memória WebGL no PlayerSettings (256MB seguro para iOS)
        PlayerSettings.WebGL.memorySize = 256;
        Debug.Log("[MobileOptimizationTool] PlayerSettings.WebGL.memorySize configurado para 256 MB.");

        // 3. Otimiza todas as texturas dos bichinhos e do gabinete para 512x512 com Crunch Compression
        string[] textureGuids = AssetDatabase.FindAssets("t:Texture2D", new string[] { 
            "Assets/TeddySuitKid/Assets/Textures", 
            "Assets/Resources/Textures" 
        });

        int optimizedCount = 0;
        foreach (string guid in textureGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) continue;

            bool modified = false;

            // Limita textura padrão
            if (importer.maxTextureSize > 1024)
            {
                importer.maxTextureSize = 1024;
                modified = true;
            }

            // Configuração específica para WebGL
            TextureImporterPlatformSettings webglSettings = importer.GetPlatformTextureSettings("WebGL");
            if (!webglSettings.overridden || webglSettings.maxTextureSize > 512)
            {
                webglSettings.overridden = true;
                webglSettings.maxTextureSize = path.Contains("Porky") || path.Contains("Fox") ? 1024 : 512;
                webglSettings.textureCompression = TextureImporterCompression.Compressed;
                webglSettings.crunchedCompression = true;
                webglSettings.compressionQuality = 80;
                importer.SetPlatformTextureSettings(webglSettings);
                modified = true;
            }

            if (modified)
            {
                importer.SaveAndReimport();
                optimizedCount++;
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[MobileOptimizationTool] Otimização concluída com sucesso! {optimizedCount} texturas otimizadas.");
    }
}
