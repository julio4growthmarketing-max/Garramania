using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.Rendering;
using System.Collections.Generic;

public static class ArcadeLayout2Setup
{
    [MenuItem("GarraMania/1. Materializar Gabinete e Configurar Probes")]
    public static void SetupSceneAndProbes()
    {
        if (EditorApplication.isPlaying)
        {
            EditorApplication.isPlaying = false;
            Debug.LogWarning("[GarraMania] Saindo do Play Mode antes de materializar o gabinete...");
        }

        Debug.Log("[GarraMania] Iniciando materialização do Gabinete e configuração de Probes para Layout 2...");

        // 1. Abrir a cena principal SampleScene
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity", OpenSceneMode.Single);

        // 2. Limpar qualquer gabinete ou probe pré-existente para reconstrução limpa
        GameObject oldCabinet = GameObject.Find("Gabinete_Arcade_Modular");
        if (oldCabinet != null) Object.DestroyImmediate(oldCabinet);

        GameObject oldRP = GameObject.Find("ArcadeReflectionProbe");
        if (oldRP != null) Object.DestroyImmediate(oldRP);

        GameObject oldLPG = GameObject.Find("ArcadeLightProbes");
        if (oldLPG != null) Object.DestroyImmediate(oldLPG);

        // 3. Construir a estrutura física completa do Gabinete na cena
        ArcadeCabinetBuilder builder = ArcadeCabinetBuilder.Build();
        GameObject rootCabinet = builder.RootCabinet;
        rootCabinet.name = "Gabinete_Arcade_Modular";

        // 4. Marcar todas as partes fixas do Gabinete como estáticas para GI e Reflection Probes
        SetStaticRecursively(rootCabinet.transform);

        // 5. Configurar a luz Direcional da cena como Baked suave
        GameObject dirLightObj = GameObject.Find("Directional Light");
        if (dirLightObj != null)
        {
            Light dirLight = dirLightObj.GetComponent<Light>();
            if (dirLight != null)
            {
                dirLight.lightmapBakeType = LightmapBakeType.Mixed;
                dirLight.color = new Color(1f, 0.96f, 0.90f);
                dirLight.intensity = 1.2f;
                dirLight.shadows = LightShadows.Soft;
            }
        }

        // 6. Configurar Reflection Probe (Baked) cobrindo todo o volume do gabinete
        GameObject rpObj = new GameObject("ArcadeReflectionProbe");
        rpObj.transform.position = new Vector3(0f, 0.5f, 0f);
        ReflectionProbe rp = rpObj.AddComponent<ReflectionProbe>();
        rp.mode = ReflectionProbeMode.Baked;
        rp.boxProjection = true;
        rp.size = new Vector3(5.6f, 6.5f, 5.6f);
        rp.center = Vector3.zero;
        rp.resolution = 256;
        rp.intensity = 1.0f;
        rp.clearFlags = ReflectionProbeClearFlags.SolidColor;
        rp.backgroundColor = new Color(0.05f, 0.06f, 0.09f, 1f);

        // 7. Configurar Light Probe Group (Grade 3D cobrindo o interior, o curso da garra e a calha)
        GameObject lpgObj = new GameObject("ArcadeLightProbes");
        LightProbeGroup lpg = lpgObj.AddComponent<LightProbeGroup>();
        List<Vector3> probePositions = new List<Vector3>();

        float[] xCoords = new float[] { -2.2f, -1.1f, 0f, 1.1f, 2.2f };
        float[] yCoords = new float[] { -2.0f, -1.2f, -0.4f, 0.4f, 1.2f, 2.0f, 2.85f };
        float[] zCoords = new float[] { -2.2f, -1.1f, 0f, 1.1f, 2.2f };

        foreach (float y in yCoords)
        {
            foreach (float x in xCoords)
            {
                foreach (float z in zCoords)
                {
                    probePositions.Add(new Vector3(x, y, z));
                }
            }
        }

        // Adiciona densidade concentrada na calha de entrega (-1.8, -1.8)
        probePositions.Add(new Vector3(-1.8f, -1.4f, -1.8f));
        probePositions.Add(new Vector3(-1.8f, -1.9f, -1.8f));
        probePositions.Add(new Vector3(-1.3f, -1.6f, -1.3f));
        probePositions.Add(new Vector3(0f, 0f, 0f));

        lpg.probePositions = probePositions.ToArray();

        // 8. Configurar Lighting Settings progressivo
        ConfigureSceneLightingSettings();

        // 9. Salvar cena e assets
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();

        Debug.Log($"[GarraMania] Gabinete materializado com {probePositions.Count} Light Probes e Reflection Probe configurada!");
    }

    private static void SetStaticRecursively(Transform t)
    {
        // Elementos mecânicos dinâmicos que deslizam com a garra NÃO podem ser estáticos
        if (t.name == "Trolley_Motor_Guindaste" || t.name == "Viga_Gantry_X" || t.name == "ZonaDeEntrega_Invisivel")
        {
            GameObjectUtility.SetStaticEditorFlags(t.gameObject, 0);
            return;
        }

        StaticEditorFlags flags = StaticEditorFlags.ContributeGI | 
                                  StaticEditorFlags.ReflectionProbeStatic | 
                                  StaticEditorFlags.BatchingStatic |
                                  StaticEditorFlags.OccludeeStatic |
                                  StaticEditorFlags.OccluderStatic;
        
        GameObjectUtility.SetStaticEditorFlags(t.gameObject, flags);

        for (int i = 0; i < t.childCount; i++)
        {
            SetStaticRecursively(t.GetChild(i));
        }
    }

    private static void ConfigureSceneLightingSettings()
    {
        try
        {
            LightingSettings settings = new LightingSettings();
            settings.name = "GarraManiaLightingSettings";
            settings.lightmapper = LightingSettings.Lightmapper.ProgressiveGPU;
            settings.lightmapResolution = 24;
            settings.lightmapMaxSize = 1024;
            settings.maxBounces = 2;
            settings.ao = true;
            settings.aoMaxDistance = 1.5f;

            Lightmapping.SetLightingSettingsForScene(EditorSceneManager.GetActiveScene(), settings);
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning("[GarraMania] Configuração programática de LightingSettings ignorada (usando padrão da cena): " + ex.Message);
        }
    }

    [MenuItem("GarraMania/2. Iniciar Bake da Iluminação (Lightmap + Probes)")]
    public static void BakeLighting()
    {
        Debug.Log("[GarraMania] Iniciando Bake da Iluminação da cena ativa...");
        Lightmapping.BakeAsync();
    }

    public static void ExecuteBatchSetupAndBake()
    {
        SetupSceneAndProbes();
        Lightmapping.Bake();
        Debug.Log("[GarraMania] Processamento batch concluído com sucesso!");
    }
}
