using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
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
                BuildVariant(destination, WebGLTextureSubtarget.DXT);
                string phoneOutput = destination + "-Android";
                // Unity's incremental WebGL exporter can move the previous output's Build
                // directory into the next export. Preserve desktop files across that step.
                string snapshot = Path.GetFullPath(destination + "-DesktopRuntime-" + Guid.NewGuid().ToString("N"));
                if (!snapshot.StartsWith(project, StringComparison.OrdinalIgnoreCase))
                    throw new BuildFailedException("Runtime backup must stay inside the project.");
                CopyFolder(Path.Combine(destination, "Build"), snapshot);
                try { BuildVariant(phoneOutput, WebGLTextureSubtarget.ETC2); }
                finally
                {
                    CopyFolder(snapshot, Path.Combine(destination, "Build"));
                    if (!Path.GetFullPath(snapshot).StartsWith(project, StringComparison.OrdinalIgnoreCase))
                        throw new BuildFailedException("Refusing to remove a runtime backup outside the project.");
                    Directory.Delete(snapshot, true);
                }
                ExportPhoneManifest(destination, phoneOutput);
                string html = File.ReadAllText(Path.Combine(destination, "index.html"));
                var runtime = Regex.Matches(html, "(?:loaderUrl|dataUrl|frameworkUrl|codeUrl):\\s*\"(Build/[^\"]+)\"");
                if (runtime.Count != 4 || runtime.Cast<Match>().Any(match => !File.Exists(Path.Combine(destination, match.Groups[1].Value))))
                    throw new BuildFailedException("Desktop runtime is incomplete after exporting the phone build.");
            }
            File.WriteAllText(Path.Combine(destination, ".nojekyll"), string.Empty);
            FingerprintTemplateAssets(destination);
            Debug.Log("Web demo ready at " + destination + ". Serve it over HTTP; index.html cannot run from file://.");
        }

        private static void BuildVariant(string destination, WebGLTextureSubtarget textures)
        {
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath }, locationPathName = destination,
                target = BuildTarget.WebGL, subtarget = (int)textures, options = BuildOptions.None
            });
            if (report.summary.result != BuildResult.Succeeded)
                throw new BuildFailedException("Web demo " + textures + " build failed: " + report.summary.result + "; errors: " + report.summary.totalErrors);
        }

        private static void CopyFolder(string source, string destination)
        {
            foreach (string file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
            {
                string target = Path.Combine(destination, Path.GetRelativePath(source, file));
                Directory.CreateDirectory(Path.GetDirectoryName(target));
                File.Copy(file, target, true);
            }
        }

        public static void FingerprintTemplateAssets(string destination)
        {
            string index = Path.Combine(destination, "index.html");
            string html = File.ReadAllText(index);
            using (var sha = System.Security.Cryptography.SHA256.Create())
                foreach (string asset in new[] { "demo.js", "touch-controls.js", "style.css" })
                {
                    string hash = BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(Path.Combine(destination, asset)))).Replace("-", "").Substring(0, 12).ToLowerInvariant();
                    html = html.Replace("\"" + asset + "\"", "\"" + asset + "?v=" + hash + "\"");
                }
            File.WriteAllText(index, html);
        }

        [Serializable] private sealed class PhoneConfig
        {
            public string dataUrl, frameworkUrl, codeUrl;
            public string streamingAssetsUrl = "StreamingAssets";
            public string companyName = "Roadside Haven", productName = "Roadside Haven", productVersion;
        }
        [Serializable] private sealed class PhoneManifest { public string loaderUrl; public PhoneConfig config; }

        private static void ExportPhoneManifest(string desktop, string phone)
        {
            string html = File.ReadAllText(Path.Combine(phone, "index.html"));
            string FileName(string key)
            {
                var match = Regex.Match(html, key + ":\\s*\"Build/([^\"]+)\"");
                if (!match.Success) throw new BuildFailedException("Phone export is missing " + key);
                return match.Groups[1].Value;
            }
            bool sharedRuntime = new[] { "loaderUrl", "frameworkUrl", "codeUrl" }.All(key => File.Exists(Path.Combine(desktop, "Build", FileName(key))));
            string prefix = sharedRuntime ? "Build/" : "Mobile/Build/";
            if (sharedRuntime)
                File.Copy(Path.Combine(phone, "Build", FileName("dataUrl")), Path.Combine(desktop, "Build", FileName("dataUrl")), true);
            else
                foreach (string file in Directory.GetFiles(phone, "*", SearchOption.AllDirectories))
                {
                    string target = Path.Combine(desktop, "Mobile", Path.GetRelativePath(phone, file));
                    Directory.CreateDirectory(Path.GetDirectoryName(target)); File.Copy(file, target, true);
                }
            var manifest = new PhoneManifest
            {
                loaderUrl = prefix + FileName("loaderUrl"),
                config = new PhoneConfig
                {
                    dataUrl = prefix + FileName("dataUrl"), frameworkUrl = prefix + FileName("frameworkUrl"), codeUrl = prefix + FileName("codeUrl"),
                    streamingAssetsUrl = sharedRuntime ? "StreamingAssets" : "Mobile/StreamingAssets", productVersion = PlayerSettings.bundleVersion
                }
            };
            File.WriteAllText(Path.Combine(desktop, "mobile-build.json"), JsonUtility.ToJson(manifest, true));
            Debug.Log("Android ETC2 export included; shared browser runtime: " + sharedRuntime);
        }
    }
}
