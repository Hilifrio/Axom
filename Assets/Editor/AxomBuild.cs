using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

// Build en ligne de commande : Unity -batchmode -quit -executeMethod AxomBuild.Windows -buildPath <dossier>
public static class AxomBuild
{
    [MenuItem("Axom/Build Windows")]
    public static void Windows()
    {
        string[] arguments = System.Environment.GetCommandLineArgs();
        int index = System.Array.IndexOf(arguments, "-buildPath");
        string folder = index >= 0 ? arguments[index + 1] : "Build/Windows";

        BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = new[] { "Assets/Scenes/SampleScene.unity" },
            locationPathName = folder + "/Axom.exe",
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None
        });

        Debug.Log($"[Build] {report.summary.result}, {report.summary.totalErrors} erreurs, {report.summary.totalSize / (1024 * 1024)} Mo");
        if (Application.isBatchMode && report.summary.result != BuildResult.Succeeded) EditorApplication.Exit(1);
    }
}
