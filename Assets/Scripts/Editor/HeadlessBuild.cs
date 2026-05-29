using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Wimme.EditorTools
{
    /// <summary>
    /// CLI-entrypoint voor een headless Windows standalone-build van de
    /// trainings-scène. Exit-code 0 bij succes, 1 bij fout, zodat de aanroepende
    /// shell het resultaat kan controleren.
    ///
    /// <code>
    /// Unity.exe -batchmode -quit -nographics ^
    ///     -projectPath C:\VR ^
    ///     -executeMethod Wimme.EditorTools.HeadlessBuild.BuildTraining
    /// </code>
    /// </summary>
    public static class HeadlessBuild
    {
        public static void BuildTraining()
        {
            string outDir = Path.Combine(Application.dataPath, "..", "Builds");
            Directory.CreateDirectory(outDir);
            string outPath = Path.Combine(outDir, "VR_project.exe");

            var opts = new BuildPlayerOptions
            {
                scenes = new[] { "Assets/Scenes/kean_scene_Training2.unity" },
                locationPathName = outPath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None,
            };

            BuildReport report = BuildPipeline.BuildPlayer(opts);
            BuildSummary s = report.summary;
            Debug.Log($"[HeadlessBuild] result={s.result} totalSize={s.totalSize} duration={s.totalTime} errors={s.totalErrors}");

            EditorApplication.Exit(s.result == BuildResult.Succeeded ? 0 : 1);
        }
    }
}
