using UnityEngine;
using UnityEditor;

public static class BakeClawPrefab
{
    [MenuItem("GarraMania/Gerar Prefab da Nova Garra (Estilo Fab)")]
    public static void CreateClawPrefab()
    {
        Debug.Log("[GarraMania] Gerando Prefab da nova garra de alta fidelidade...");

        GameObject tempRoot = new GameObject("GarraArcade_FabStyle");
        RealisticClawMeshBuilder.ClawRig rig = RealisticClawMeshBuilder.Build(tempRoot.transform);

        // Salva a malha curva para que o Prefab seja independente
        if (!AssetDatabase.IsValidFolder("Assets/Meshes"))
        {
            AssetDatabase.CreateFolder("Assets", "Meshes");
        }

        MeshFilter mf = rig.Prongs[0].GetComponentInChildren<MeshFilter>();
        if (mf != null && mf.sharedMesh != null)
        {
            string meshPath = "Assets/Meshes/Curved_Arcade_Prong_Blade.asset";
            Mesh existingMesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            if (existingMesh == null)
            {
                AssetDatabase.CreateAsset(Object.Instantiate(mf.sharedMesh), meshPath);
            }
            Mesh savedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);

            foreach (var p in rig.Prongs)
            {
                MeshFilter pmf = p.GetComponentInChildren<MeshFilter>();
                if (pmf != null) pmf.sharedMesh = savedMesh;
            }
        }

        string prefabPath = "Assets/Prefabs/GarraArcade_FabStyle.prefab";
        PrefabUtility.SaveAsPrefabAsset(tempRoot, prefabPath);
        Object.DestroyImmediate(tempRoot);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[GarraMania] Prefab da Garra criado com sucesso em: {prefabPath}!");
    }
}
