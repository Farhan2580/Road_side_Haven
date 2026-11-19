using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace RoadsideHaven
{
    /// <summary>
    /// Runtime "ultra" presentation layer: cinematic grading volume, camera anti-aliasing and the
    /// weather and grass systems. Attached automatically when the game scene loads, so the saved
    /// scene stays untouched. Lighter settings are used on mobile and in the browser build.
    /// </summary>
    public sealed class UltraGraphics : MonoBehaviour
    {
        public static bool HighEnd => !(Application.isMobilePlatform || MobileTouchInput.Enabled || Application.platform == RuntimePlatform.WebGLPlayer);
        /// <summary>Lets tests and the editor opt out of the automatic install.</summary>
        public static bool AutoInstall = true;
        private static bool hooked;

        private GameObject runtimeRoot;
        private Volume volume;
        private VolumeProfile profile;
        private ColorAdjustments colorAdjustments;
        private WhiteBalance whiteBalance;
        private Vignette vignette;
        private Bloom bloom;
        private WeatherSystem weather;
        private MotelAtmosphere atmosphere;
        private float nextCameraCheck;
        public Volume GradingVolume => volume;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (!hooked) { hooked = true; SceneManager.sceneLoaded += (_, __) => Install(); }
            Install();
        }

        public static UltraGraphics Install()
        {
            if (!AutoInstall || !Application.isPlaying) return null;
            var motel = Object.FindFirstObjectByType<MotelGameRoot>();
            if (motel == null) return null;
            var existing = motel.GetComponent<UltraGraphics>();
            if (existing != null) return existing;
            if (motel.GetComponent<WeatherSystem>() == null) motel.gameObject.AddComponent<WeatherSystem>();
            if (motel.GetComponent<GrassField>() == null) motel.gameObject.AddComponent<GrassField>();
            if (motel.GetComponent<WorldAudio>() == null) motel.gameObject.AddComponent<WorldAudio>();
            if (motel.GetComponent<SkyLife>() == null) motel.gameObject.AddComponent<SkyLife>();
            if (motel.GetComponent<VehicleDynamics>() == null) motel.gameObject.AddComponent<VehicleDynamics>();
            if (motel.GetComponent<VehicleFleet>() == null) motel.gameObject.AddComponent<VehicleFleet>();
            if (motel.GetComponent<TreeUpgrade>() == null) motel.gameObject.AddComponent<TreeUpgrade>();
            if (motel.GetComponent<StadiumUpgrade>() == null) motel.gameObject.AddComponent<StadiumUpgrade>();
            if (motel.GetComponent<FarhanMatchDay>() == null) motel.gameObject.AddComponent<FarhanMatchDay>();
            if (motel.GetComponent<HospitalUpgrade>() == null) motel.gameObject.AddComponent<HospitalUpgrade>();
            if (motel.GetComponent<AirportUpgrade>() == null) motel.gameObject.AddComponent<AirportUpgrade>();
            return motel.gameObject.AddComponent<UltraGraphics>();
        }

        private void Start()
        {
            weather = GetComponent<WeatherSystem>();
            atmosphere = GetComponent<MotelAtmosphere>();
            // Kept at the scene root (not under the motel) so world audits that walk the motel hierarchy are unaffected.
            runtimeRoot = new GameObject("Ultra Graphics Runtime");
            SceneManager.MoveGameObjectToScene(runtimeRoot, gameObject.scene);
            volume = runtimeRoot.AddComponent<Volume>();
            volume.isGlobal = true;
            // Above the base grading (10) but below dimension portals (25), which keep their own look.
            volume.priority = 15;
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.name = "Ultra cinematic grading";
            volume.sharedProfile = profile;

            var tone = profile.Add<Tonemapping>(true);
            tone.mode.Override(TonemappingMode.ACES);
            colorAdjustments = profile.Add<ColorAdjustments>(true);
            colorAdjustments.postExposure.Override(.1f);
            colorAdjustments.contrast.Override(14f);
            colorAdjustments.saturation.Override(8f);
            whiteBalance = profile.Add<WhiteBalance>(true);
            whiteBalance.temperature.Override(5f);
            whiteBalance.tint.Override(1f);
            bloom = profile.Add<Bloom>(true);
            bloom.threshold.Override(.95f);
            bloom.intensity.Override(.55f);
            bloom.scatter.Override(.72f);
            bloom.highQualityFiltering.Override(HighEnd);
            vignette = profile.Add<Vignette>(true);
            vignette.intensity.Override(.22f);
            vignette.smoothness.Override(.45f);
            var smh = profile.Add<ShadowsMidtonesHighlights>(true);
            smh.shadows.Override(new Vector4(.96f, .99f, 1.06f, 0f));
            smh.highlights.Override(new Vector4(1.04f, 1.01f, .96f, 0f));
            if (HighEnd)
            {
                var grain = profile.Add<FilmGrain>(true);
                grain.type.Override(FilmGrainLookup.Thin1);
                grain.intensity.Override(.12f);
                var aberration = profile.Add<ChromaticAberration>(true);
                aberration.intensity.Override(.04f);
                // No motion blur: it smears the whole frame whenever the camera turns or snaps.
            }
            if (HighEnd)
            {
                // Wet streets and glass need a live reflection of the procedural sky.
                RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;
                RenderSettings.reflectionIntensity = 1f;
            }
            ConfigureCamera();
        }

        private void ConfigureCamera()
        {
            var camera = Camera.main;
            if (camera == null) return;
            camera.allowHDR = true;
            if (camera.farClipPlane < 2200) camera.farClipPlane = 2200;
            var data = camera.GetUniversalAdditionalCameraData();
            if (data == null) return;
            data.renderPostProcessing = true;
            if (HighEnd)
            {
                data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
                data.antialiasingQuality = AntialiasingQuality.High;
            }
        }

        private void Update()
        {
            if (Time.unscaledTime >= nextCameraCheck) { nextCameraCheck = Time.unscaledTime + 1; ConfigureCamera(); }
            if (profile == null) return;
            float rain = weather == null ? 0 : weather.Rain;
            float cloud = weather == null ? 0 : weather.Cloud;
            float flash = weather == null ? 0 : weather.Lightning;
            float day = atmosphere == null ? 1 : atmosphere.Daylight;
            // Overcast and rain flatten contrast and pull colour toward a cool grey, as real rainy light does.
            colorAdjustments.saturation.value = Mathf.Lerp(8f, -22f, rain) - cloud * 4f;
            colorAdjustments.contrast.value = Mathf.Lerp(14f, 5f, Mathf.Max(cloud * .6f, rain));
            colorAdjustments.postExposure.value = Mathf.Lerp(.1f, -.12f, rain) + Mathf.Lerp(.25f, 0f, day) + flash * 1.6f;
            whiteBalance.temperature.value = Mathf.Lerp(5f, -12f, rain) + Mathf.Lerp(-6f, 0f, day);
            vignette.intensity.value = Mathf.Lerp(.22f, .3f, rain);
            bloom.intensity.value = Mathf.Lerp(.85f, .55f, day);
        }

        private void OnDestroy()
        {
            if (runtimeRoot != null) Destroy(runtimeRoot);
            if (profile != null) Destroy(profile);
        }
    }
}
