using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Wimme.EditorTools
{
    /// <summary>
    /// Headless Windows standalone build for ML-Agents training.
    /// Invoke via Unity command line:
    /// "C:\Program Files\Unity\Hub\Editor\6000.3.9f1\Editor\Unity.exe"
    ///     -batchmode -quit -nographics
    ///     -projectPath C:\VR
    ///     -executeMethod Wimme.EditorTools.HeadlessBuild.BuildTraining
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

            // Exit code is what Unity returns to the calling shell.
            EditorApplication.Exit(s.result == BuildResult.Succeeded ? 0 : 1);
        }
    }
}
