using UnityEngine;
using UnityEditor;
using System.IO;

public static class BakeTeddyPrizePrefab
{
    [InitializeOnLoadMethod]
    [MenuItem("GarraMania/Criar Prefab do Teddy para o Fliperama")]
    public static void CreateTeddyPrefab()
    {
        string fbxPath = "Assets/DroneClaw/Models/Teddy.fbx";
        string targetDir = "Assets/Resources/Prizes";
        string prefabPath = $"{targetDir}/Teddy.prefab";

        if (!File.Exists(fbxPath))
        {
            Debug.LogWarning($"[GarraMania] FBX do Teddy não encontrado em {fbxPath}");
            return;
        }

        if (!Directory.Exists(targetDir))
        {
            Directory.CreateDirectory(targetDir);
            AssetDatabase.Refresh();
        }

        GameObject fbxAsset = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
        if (fbxAsset == null)
        {
            Debug.LogWarning("[GarraMania] Não foi possível carregar Teddy.fbx pelo AssetDatabase.");
            return;
        }

        // Instancia cópia limpa para prefab
        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(fbxAsset);
        instance.name = "Teddy";

        // Salva prefab em Resources/Prizes/Teddy.prefab
        PrefabUtility.SaveAsPrefabAsset(instance, prefabPath);
        Object.DestroyImmediate(instance);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[GarraMania] 🧸 Prefab do Teddy criado com sucesso em: {prefabPath}!");
    }
}
