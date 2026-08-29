using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class WebGLBuilder
{
    [MenuItem("GarraMania/Gerar Build WebGL Mobile")]
    public static void BuildWebGLMobile()
    {
        Debug.Log("[GarraMania] Iniciando compilação do Build WebGL Mobile...");
        string[] scenes = new string[] { "Assets/Scenes/SampleScene.unity" };
        string buildPath = "Build_Web";

        BuildPlayerOptions buildPlayerOptions = new BuildPlayerOptions();
        buildPlayerOptions.scenes = scenes;
        buildPlayerOptions.locationPathName = buildPath;
        buildPlayerOptions.target = BuildTarget.WebGL;
        buildPlayerOptions.options = BuildOptions.None;

        BuildReport report = BuildPipeline.BuildPlayer(buildPlayerOptions);
        BuildSummary summary = report.summary;

        if (summary.result == BuildResult.Succeeded)
        {
            Debug.Log($"[GarraMania] Build WebGL gerado com sucesso em '{buildPath}'! Total: {summary.totalSize / (1024f * 1024f):F1} MB");
        }
        else if (summary.result == BuildResult.Failed)
        {
            Debug.LogError("[GarraMania] Falha ao gerar Build WebGL!");
        }
    }
}
