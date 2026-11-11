using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.InputSystem;

namespace RoadsideHaven
{
    public enum WorldSeason { Summer, Monsoon, Autumn, LateAutumn, Winter, Spring }

    /// <summary>Six-season local calendar, solar lighting and procedural sky. Restores scene globals on disable.</summary>
    public sealed class MotelAtmosphere : MonoBehaviour
    {
        [Header("Clock and calendar")]
        [SerializeField, Min(30)] private float dayLengthSeconds = 480f;
        [SerializeField, Range(0, 24)] private float startingHour = 15f;
        [SerializeField] private bool runCycle = true;
        [SerializeField, Range(.1f,.95f), Tooltip("Real-time daylight share of a complete cycle. 0.8 means 80% day and 20% night.")] private float dayTimeFraction = .8f;
        [SerializeField] private WorldSeason startingSeason = WorldSeason.Summer;
        [SerializeField, Min(1)] private int daysPerSeason = 3;
        [Header("Lighting")]
        [SerializeField] private Light sun;
        [SerializeField, Min(0)] private float daylightIntensity = 1.35f;
        [SerializeField, Min(0)] private float moonlightIntensity = .14f;
        [SerializeField] private Color daylight = new Color(1f, .91f, .77f);
        [SerializeField] private Color twilight = new Color(1f, .55f, .31f);
        [SerializeField] private Color nightAmbient = new Color(.12f, .17f, .28f);
        [Header("Sky and haze")]
        [SerializeField, Tooltip("Procedural sky material; cloned at runtime, preserving the saved asset.")] private Material skyTemplate;
        [SerializeField] private bool seasonalFog = true;
        [SerializeField, Min(100)] private float clearVisibility = 650f;
        private Material runtimeSky, previousSky;
        private Light previousRenderSun;
        private Color previousAmbient, previousSunColor, previousFogColor;
        private AmbientMode previousMode;
        private FogMode previousFogMode;
        private float previousSunIntensity, previousFogStart, previousFogEnd;
        private bool previousFog;
        private Quaternion previousSunRotation;
        private float hour;
        private int elapsedDays;
        private bool captured;
        private bool started;
        private WorldSeason appliedSeason = (WorldSeason)(-1);
        private readonly List<Renderer> vegetation = new List<Renderer>();
        private readonly List<MaterialPropertyBlock> originalBlocks = new List<MaterialPropertyBlock>();
        private Camera worldCamera;
        private CameraClearFlags previousClearFlags;
        private readonly Dictionary<Light, float> fillLights = new Dictionary<Light, float>();
        public float Hour => hour;
        public int Day => elapsedDays + 1;
        public WorldSeason Season => (WorldSeason)(((int)startingSeason + elapsedDays / Mathf.Max(1, daysPerSeason)) % 6);

        public void Configure(Light directionalLight)
        {
            sun = directionalLight;
            if (sun != null && captured) CaptureSun();
        }

        public void ConfigureSky(Material template)
        {
            skyTemplate = template;
            if (!started || !Application.isPlaying) return;
            if (runtimeSky != null) Destroy(runtimeSky);
            runtimeSky = template == null ? null : new Material(template);
            RenderSettings.skybox = runtimeSky;
        }

        [ContextMenu("Preview/Sunset")]
        public void PreviewSunset() => hour = Season == WorldSeason.Summer ? 18.8f : Season == WorldSeason.Winter ? 16.8f : 17.8f;
        [ContextMenu("Preview/Night")]
        public void PreviewNight() => hour = 22f;
        [ContextMenu("Preview/Next Season")]
        public void PreviewNextSeason() => elapsedDays += Mathf.Max(1, daysPerSeason);

        private void CaptureSun()
        {
            previousSunColor = sun.color;
            previousSunIntensity = sun.intensity;
            previousSunRotation = sun.transform.rotation;
        }

        private void OnEnable()
        {
            previousAmbient = RenderSettings.ambientLight;
            previousMode = RenderSettings.ambientMode;
            previousSky = RenderSettings.skybox;
            previousRenderSun = RenderSettings.sun;
            previousFog = RenderSettings.fog;
            previousFogMode = RenderSettings.fogMode;
            previousFogColor = RenderSettings.fogColor;
            previousFogStart = RenderSettings.fogStartDistance;
            previousFogEnd = RenderSettings.fogEndDistance;
            if (sun != null) CaptureSun();
            captured = true;
            hour = startingHour;
            elapsedDays = 0;
            if (started) InitializeVisuals();
        }

        private void Start()
        {
            started = true;
            InitializeVisuals();
        }

        private void InitializeVisuals()
        {
            vegetation.Clear();
            originalBlocks.Clear();
            fillLights.Clear();
            appliedSeason = (WorldSeason)(-1);
            // Start runs after the Editor installer has finished composing the world.
            if (skyTemplate != null)
            {
                runtimeSky = new Material(skyTemplate);
                RenderSettings.skybox = runtimeSky;
            }
            var cameras = Object.FindObjectsByType<IsometricCameraController>(FindObjectsSortMode.None);
            foreach (var controller in cameras)
                if (controller.gameObject.scene == gameObject.scene) { worldCamera = controller.GetComponent<Camera>(); break; }
            if (worldCamera != null)
            {
                previousClearFlags = worldCamera.clearFlags;
                worldCamera.clearFlags = CameraClearFlags.Skybox;
            }
            foreach (var root in gameObject.scene.GetRootGameObjects())
                foreach (var light in root.GetComponentsInChildren<Light>())
                    if (light != sun && light.type == LightType.Directional) fillLights[light] = light.intensity;
            foreach (var renderer in GetComponentsInChildren<Renderer>(true))
            {
                string name = renderer.name;
                if (!name.Contains("Tree crown") && name != "Plant" && name != "Crop row" &&
                    name != "World foundation" && name != "District ground" && name != "Ground") continue;
                var block = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(block);
                vegetation.Add(renderer);
                originalBlocks.Add(block);
            }
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.f6Key.wasPressedThisFrame) PreviewSunset();
                if (keyboard.f7Key.wasPressedThisFrame) PreviewNight();
                if (keyboard.f8Key.wasPressedThisFrame) PreviewNextSeason();
            }
            if (runCycle)
            {
                float seasonalDayHours = Season == WorldSeason.Summer ? 14f : Season == WorldSeason.Winter ? 10f : 12f;
                hour = DayNightClock.Advance(hour, Time.deltaTime, dayLengthSeconds, seasonalDayHours, dayTimeFraction, out int passedDays);
                elapsedDays += passedDays;

            }
            float daylightHours = Season == WorldSeason.Summer ? 14f : Season == WorldSeason.Winter ? 10f : 12f;
            float sunrise = 12f - daylightHours * .5f;
            float solarPhase = hour >= sunrise && hour <= sunrise + daylightHours
                ? (hour - sunrise) / daylightHours * Mathf.PI
                : Mathf.PI + Mathf.Repeat(hour - sunrise - daylightHours, 24f) / (24f - daylightHours) * Mathf.PI;
            float elevation = Mathf.Sin(solarPhase);
            float day = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-.12f, .28f, elevation));
            float cloud = Season == WorldSeason.Monsoon ? .65f : Season == WorldSeason.Winter ? .28f : .10f;
            Vector3 sunDirection = new Vector3(Mathf.Cos(solarPhase), elevation, .22f).normalized;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = Color.Lerp(nightAmbient, new Color(.62f, .68f, .73f), day);
            foreach (var fill in fillLights)
                if (fill.Key != null) fill.Key.intensity = fill.Value * Mathf.Lerp(.12f, 1f, day);
            if (sun != null)
            {
                RenderSettings.sun = sun;
                sun.intensity = elevation >= 0 ? daylightIntensity * Mathf.Clamp01(elevation) * (1f - cloud * .45f) : moonlightIntensity;
                sun.color = elevation < 0 ? new Color(.55f, .68f, 1f) : Color.Lerp(twilight, daylight, day);
                sun.transform.rotation = Quaternion.LookRotation(elevation >= 0 ? -sunDirection : sunDirection);
            }
            if (runtimeSky != null)
            {
                runtimeSky.SetVector("_SunDirection", sunDirection);
                runtimeSky.SetVector("_MoonDirection", -sunDirection);
                runtimeSky.SetFloat("_Daylight", day);
                runtimeSky.SetFloat("_Cloudiness", cloud);
                runtimeSky.SetFloat("_SkyTime", Time.time * .002f);
            }
            if (seasonalFog)
            {
                RenderSettings.fog = true;
                RenderSettings.fogMode = FogMode.Linear;
                float cameraOffset = worldCamera != null && worldCamera.orthographic
                    ? Mathf.Max(0, worldCamera.transform.position.y / Mathf.Max(.05f, -worldCamera.transform.forward.y)) : 0;
                float overview = worldCamera != null && worldCamera.orthographic ? worldCamera.orthographicSize : 0;
                RenderSettings.fogStartDistance = Mathf.Max(0, cameraOffset - overview) + clearVisibility * .55f;
                RenderSettings.fogEndDistance = cameraOffset + overview * 2f + clearVisibility * (Season == WorldSeason.Winter ? .75f : 1.4f);
                RenderSettings.fogColor = Color.Lerp(new Color(.06f, .09f, .17f), new Color(.59f, .68f, .74f), day);
            }
            if (appliedSeason != Season) ApplySeason();
        }

        private void ApplySeason()
        {
            appliedSeason = Season;
            Color tint = Season == WorldSeason.Monsoon ? new Color(.15f, .42f, .21f) :
                Season == WorldSeason.Autumn ? new Color(.57f, .43f, .20f) :
                Season == WorldSeason.LateAutumn ? new Color(.59f, .51f, .30f) :
                Season == WorldSeason.Winter ? new Color(.53f, .59f, .47f) :
                Season == WorldSeason.Spring ? new Color(.36f, .63f, .27f) : new Color(.36f, .49f, .22f);
            for (int i = 0; i < vegetation.Count; i++)
            {
                if (vegetation[i] == null) continue;
                var block = new MaterialPropertyBlock();
                vegetation[i].GetPropertyBlock(block);
                block.SetColor("_BaseColor", tint);
                vegetation[i].SetPropertyBlock(block);
            }
        }

        private void OnDisable()
        {
            if (!captured) return;
            RenderSettings.ambientLight = previousAmbient;
            RenderSettings.ambientMode = previousMode;
            RenderSettings.skybox = previousSky;
            RenderSettings.sun = previousRenderSun;
            RenderSettings.fog = previousFog;
            RenderSettings.fogMode = previousFogMode;
            RenderSettings.fogColor = previousFogColor;
            RenderSettings.fogStartDistance = previousFogStart;
            RenderSettings.fogEndDistance = previousFogEnd;
            if (worldCamera != null) worldCamera.clearFlags = previousClearFlags;
            foreach (var fill in fillLights) if (fill.Key != null) fill.Key.intensity = fill.Value;
            for (int i = 0; i < vegetation.Count; i++)
                if (vegetation[i] != null) vegetation[i].SetPropertyBlock(originalBlocks[i]);
            if (sun != null)
            {
                sun.color = previousSunColor;
                sun.intensity = previousSunIntensity;
                sun.transform.rotation = previousSunRotation;
            }
            if (runtimeSky != null) Destroy(runtimeSky);
        }
    }
}
