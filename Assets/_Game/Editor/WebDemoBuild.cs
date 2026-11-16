using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace RoadsideHaven.Editor
{
    /// <summary>Builds the saved game scene for static hosting, including GitHub Pages.</summary>
    public static class WebDemoBuild
    {
        public const string ScenePath = "Assets/_Game/Generated/RoadsideHavenPrototype.unity";
        public const string DefaultOutput = "Builds/WebGL";

        [MenuItem("Roadside Haven/Web Demo/Configure Browser Build")]
        public static void Configure()
        {
            // These settings belong to WebGL; the PC build and its graphics profile stay intact.
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            PlayerSettings.WebGL.decompressionFallback = true;
            PlayerSettings.WebGL.threadsSupport = false;
            PlayerSettings.WebGL.nameFilesAsHashes = true;
            PlayerSettings.WebGL.dataCaching = true;
            PlayerSettings.WebGL.initialMemorySize = 256;
            PlayerSettings.WebGL.maximumMemorySize = 2048;
            PlayerSettings.WebGL.memoryGrowthMode = WebGLMemoryGrowthMode.Geometric;
            PlayerSettings.WebGL.template = "PROJECT:RoadsideHaven";
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.WebGL, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.WebGL, ManagedStrippingLevel.Low);
            WebDemoBuildQuality.ConfigureProfile();
            AssetDatabase.SaveAssets();
            Debug.Log("Web demo configured: gzip fallback, single thread, hashed assets and browser template.");
        }

        [MenuItem("Roadside Haven/Web Demo/Build Browser Demo")]
        public static void BuildLocal()
        {
            Build(DefaultOutput);
            EditorUtility.RevealInFinder(Path.GetFullPath(Path.Combine(DefaultOutput, "index.html")));
        }

        public static void BuildFromCommandLine()
        {
            string[] args = Environment.GetCommandLineArgs();
            int outputArgument = Array.IndexOf(args, "-webDemoOutput");
            if (outputArgument >= 0 && (outputArgument + 1 >= args.Length || args[outputArgument + 1].StartsWith("-")))
                throw new BuildFailedException("-webDemoOutput requires an output directory.");
            Build(outputArgument >= 0 ? args[outputArgument + 1] : DefaultOutput);
        }

        private static void Build(string output)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new BuildFailedException("Stop Play Mode before building the browser demo.");
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.WebGL, BuildTarget.WebGL))
                throw new BuildFailedException("Web Build Support is missing. In Unity Hub, add Web Build Support to Unity " + Application.unityVersion + ".");
            if (!File.Exists(ScenePath))
                throw new BuildFailedException("Save the game scene before building: " + ScenePath);
            if (!File.Exists("Assets/WebGLTemplates/RoadsideHaven/index.html"))
                throw new BuildFailedException("The Roadside Haven browser template is missing.");

            Configure();
            string destination = Path.GetFullPath(output);
            string project = Path.GetFullPath(".") + Path.DirectorySeparatorChar;
            if (!destination.StartsWith(project, StringComparison.OrdinalIgnoreCase) ||
                new[] { "Assets", "Packages", "ProjectSettings", "Library", ".git" }.Any(folder =>
                    destination.Equals(Path.GetFullPath(folder), StringComparison.OrdinalIgnoreCase) ||
                    destination.StartsWith(Path.GetFullPath(folder) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)))
                throw new BuildFailedException("Write the demo to a dedicated build folder inside the project, for example Builds/WebGL.");

            using (new WebDemoBuildQuality.Scope(WebDemoBuildQuality.ConfigureProfile()))
            {
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { ScenePath },
                    locationPathName = destination,
                    target = BuildTarget.WebGL,
                    options = BuildOptions.None
                });
                if (report.summary.result != BuildResult.Succeeded)
                    throw new BuildFailedException("Web demo build failed: " + report.summary.result + "; errors: " + report.summary.totalErrors);
            }
            File.WriteAllText(Path.Combine(destination, ".nojekyll"), string.Empty);
            Debug.Log("Web demo ready at " + destination + ". Serve it over HTTP; index.html cannot run from file://.");
        }
    }
}
