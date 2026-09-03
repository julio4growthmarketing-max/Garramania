using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class WebGLBuilder
{
    [MenuItem("GarraMania/Recarregar Scripts")]
    public static void ReloadScripts()
    {
        AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
        EditorUtility.RequestScriptReload();
        Debug.Log("[GarraMania] Scripts e Assets recarregados com sucesso!");
    }

    public static void Build() => BuildWebGLMobile();

    [MenuItem("GarraMania/Gerar Build WebGL Mobile")]
    public static void BuildWebGLMobile()
    {
        Debug.Log("[GarraMania] Executando otimização para iOS e Mobile...");
        MobileOptimizationTool.OptimizeForMobileAndIOS();

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
            PostProcessIndexHtml(buildPath);
            Debug.Log($"[GarraMania] Build WebGL gerado com sucesso em '{buildPath}'! Total: {summary.totalSize / (1024f * 1024f):F1} MB");
        }
        else if (summary.result == BuildResult.Failed)
        {
            Debug.LogError("[GarraMania] Falha ao gerar Build WebGL!");
        }
    }

    private static void PostProcessIndexHtml(string buildPath)
    {
        string indexPath = System.IO.Path.Combine(buildPath, "index.html");
        if (!System.IO.File.Exists(indexPath)) return;

        string html = System.IO.File.ReadAllText(indexPath);

        // 1. Ajuste de Viewport com viewport-fit=cover
        if (html.Contains("width=device-width, height=device-height"))
        {
            html = html.Replace(
                "meta.content = 'width=device-width, height=device-height, initial-scale=1.0, user-scalable=no, shrink-to-fit=yes';",
                "meta.content = 'width=device-width, initial-scale=1.0, maximum-scale=1.0, user-scalable=no, viewport-fit=cover';"
            );
        }

        // 2. Trava de DPR para 1.5x (evita estouro de memória da GPU no Safari/Chrome mobile)
        if (!html.Contains("config.devicePixelRatio = Math.min"))
        {
            html = html.Replace(
                "canvas.className = \"unity-mobile\";",
                "canvas.className = \"unity-mobile\";\n        config.devicePixelRatio = Math.min(window.devicePixelRatio || 1, 1.5);"
            );
        }

        // 3. Desbloqueio do AudioContext no Safari iOS no primeiro toque
        if (!html.Contains("webkitAudioContext"))
        {
            string audioUnlock = @"
      window.addEventListener('touchstart', function unlockAudio() {
        if (typeof webkitAudioContext !== 'undefined') {
          try {
            var ctx = new webkitAudioContext();
            if (ctx.state === 'suspended') ctx.resume();
          } catch(e) {}
        }
      }, { once: true });
";
            html = html.Replace("document.querySelector(\"#unity-loading-bar\").style.display = \"block\";", audioUnlock + "\n      document.querySelector(\"#unity-loading-bar\").style.display = \"block\";");
        }

        System.IO.File.WriteAllText(indexPath, html);
        Debug.Log("[WebGLBuilder] index.html pós-processado com sucesso para mobile e iOS!");
    }
}
