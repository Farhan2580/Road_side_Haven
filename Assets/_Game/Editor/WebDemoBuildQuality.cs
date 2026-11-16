using System;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace RoadsideHaven.Editor
{
    /// <summary>Full-resolution browser rendering without changing the PC or mobile presets.</summary>
    public static class WebDemoBuildQuality
    {
        public const string ProfilePath = "Assets/Settings/Web_RPAsset.asset";

        public static UniversalRenderPipelineAsset ConfigureProfile()
        {
            var profile = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(ProfilePath);
            if (profile == null)
            {
                if (!AssetDatabase.CopyAsset("Assets/Settings/Mobile_RPAsset.asset", ProfilePath))
                    throw new BuildFailedException("Could not create the browser rendering profile.");
                profile = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(ProfilePath);
            }
            profile.name = "Web_RPAsset";
            profile.renderScale = 1f;
            profile.msaaSampleCount = 2;
            profile.upscalingFilter = UpscalingFilterSelection.Auto;
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            return profile;
        }

        public sealed class Scope : IDisposable
        {
            private readonly int previousLevel;
            private readonly RenderPipelineAsset[] pipelines;
            private readonly AnisotropicFiltering[] filters;

            public Scope(UniversalRenderPipelineAsset profile)
            {
                previousLevel = QualitySettings.GetQualityLevel();
                pipelines = new RenderPipelineAsset[QualitySettings.names.Length];
                filters = new AnisotropicFiltering[pipelines.Length];
                for (int i = 0; i < pipelines.Length; i++)
                {
                    QualitySettings.SetQualityLevel(i, false);
                    pipelines[i] = QualitySettings.renderPipeline;
                    filters[i] = QualitySettings.anisotropicFiltering;
                    QualitySettings.renderPipeline = profile;
                    QualitySettings.anisotropicFiltering = AnisotropicFiltering.ForceEnable;
                }
                QualitySettings.SetQualityLevel(previousLevel, false);
                Debug.Log("Browser quality: native render scale, 2x MSAA and anisotropic texture filtering.");
            }

            public void Dispose()
            {
                for (int i = 0; i < pipelines.Length; i++)
                {
                    QualitySettings.SetQualityLevel(i, false);
                    QualitySettings.renderPipeline = pipelines[i];
                    QualitySettings.anisotropicFiltering = filters[i];
                }
                QualitySettings.SetQualityLevel(previousLevel, false);
                AssetDatabase.SaveAssets();
            }
        }
    }

    public sealed class WebDemoCameraProcessor : IProcessSceneWithReport
    {
        public int callbackOrder => 100;
        public void OnProcessScene(Scene scene, BuildReport report)
        {
            if (report == null || report.summary.platform != BuildTarget.WebGL) return;
            foreach (var root in scene.GetRootGameObjects())
                foreach (var camera in root.GetComponentsInChildren<Camera>(true))
                {
                    camera.allowMSAA = true;
                    camera.allowDynamicResolution = false;
                    var data = camera.GetComponent<UniversalAdditionalCameraData>();
                    if (data != null) data.antialiasing = AntialiasingMode.None;
                }
        }
    }
}
