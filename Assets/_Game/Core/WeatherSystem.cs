using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace RoadsideHaven
{
    public enum WeatherKind { Clear, Cloudy, Rain, Storm }

    /// <summary>
    /// Season-weighted weather: clouds, rain with ground splashes, wet reflective streets, wind,
    /// lightning and procedurally generated rain and thunder audio. F11 cycles the weather.
    /// </summary>
    public sealed class WeatherSystem : MonoBehaviour
    {
        public static WeatherSystem Active { get; private set; }
        public WeatherKind Target { get; private set; } = WeatherKind.Clear;
        public float Cloud { get; private set; } = .08f;
        public float Rain { get; private set; }
        public float Wetness { get; private set; }
        public float Wind { get; private set; } = .3f;
        public float Lightning { get; private set; }
        public Vector3 WindDirection { get; private set; } = new Vector3(.8f, 0, .6f).normalized;
        public bool AutomaticChanges = true;
        public int WetSurfaceCount => wetRenderers.Count;
        public ParticleSystem RainParticles => rain;

        private static readonly float[] CloudFor = { .08f, .55f, .86f, 1f };
        private static readonly float[] RainFor = { 0f, 0f, .7f, 1f };
        private static readonly float[] WindFor = { .3f, .5f, .75f, 1.15f };
        private static readonly int WetnessId = Shader.PropertyToID("_UltraWetness");
        private static readonly int WindId = Shader.PropertyToID("_UltraWind");
        private static readonly int DaylightId = Shader.PropertyToID("_UltraDaylight");
        private static readonly int SmoothnessId = Shader.PropertyToID("_Smoothness");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        private MotelAtmosphere atmosphere;
        private GameObject fxRoot;
        private ParticleSystem rain, splash;
        private Light flashLight;
        private AudioSource rainAudio, thunderAudio;
        private AudioClip thunderClip;
        private float nextChange, nextFlash = 8, flashTime = -10, thunderAt = -1, thunderVolume;
        private float appliedWetness = -1, nextWetApply, nextReflection;
        private readonly List<Renderer> wetRenderers = new List<Renderer>();
        private readonly List<float> drySmoothness = new List<float>();
        private readonly List<Color> dryColor = new List<Color>();
        private MaterialPropertyBlock block;

        private void Awake() { Active = this; }

        private void Start()
        {
            atmosphere = GetComponent<MotelAtmosphere>();
            block = new MaterialPropertyBlock();
            nextChange = Time.time + Random.Range(90f, 180f);
            fxRoot = new GameObject("Ultra Weather Effects");
            SceneManager.MoveGameObjectToScene(fxRoot, gameObject.scene);
            BuildRain();
            BuildFlashLight();
            BuildAudio();
            CollectWetSurfaces();
        }

        public void SetWeather(WeatherKind kind, bool immediate = false)
        {
            Target = kind;
            nextChange = Time.time + Random.Range(150f, 300f);
            if (!immediate) return;
            Cloud = CloudFor[(int)kind];
            Rain = RainFor[(int)kind];
            Wind = WindFor[(int)kind];
            if (Rain > .1f) Wetness = Mathf.Max(Wetness, .85f);
        }

        private WeatherKind PickNext()
        {
            var season = atmosphere == null ? WorldSeason.Summer : atmosphere.Season;
            // Weights for Clear, Cloudy, Rain, Storm.
            float[] w = season == WorldSeason.Monsoon ? new[] { 12f, 23f, 45f, 20f }
                : season == WorldSeason.Winter ? new[] { 45f, 40f, 12f, 3f }
                : season == WorldSeason.Spring ? new[] { 45f, 30f, 20f, 5f }
                : season == WorldSeason.Summer ? new[] { 55f, 28f, 12f, 5f }
                : new[] { 38f, 37f, 20f, 5f };
            float roll = Random.value * (w[0] + w[1] + w[2] + w[3]);
            for (int i = 0; i < 4; i++) { if (roll < w[i]) return (WeatherKind)i; roll -= w[i]; }
            return WeatherKind.Clear;
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.f11Key.wasPressedThisFrame) SetWeather((WeatherKind)(((int)Target + 1) % 4));
            if (AutomaticChanges && Time.time >= nextChange) SetWeather(PickNext());

            float dt = Time.deltaTime;
            int t = (int)Target;
            Cloud = Mathf.MoveTowards(Cloud, CloudFor[t], dt / 18f);
            // Rain only builds once the sky has clouded over, and eases off before the clouds clear.
            float rainGoal = Cloud > .7f ? RainFor[t] : 0f;
            Rain = Mathf.MoveTowards(Rain, rainGoal, dt / 10f);
            Wind = Mathf.MoveTowards(Wind, WindFor[t] + Rain * .15f, dt / 8f);
            Wetness = Rain > .08f ? Mathf.Min(1, Wetness + dt / 30f * Rain) : Mathf.Max(0, Wetness - dt / 140f);
            float yaw = Mathf.Sin(Time.time * .013f) * 50f + 35f;
            WindDirection = Quaternion.Euler(0, yaw, 0) * Vector3.forward;

            UpdateLightning(dt);
            Shader.SetGlobalFloat(WetnessId, Wetness);
            Shader.SetGlobalVector(WindId, new Vector4(WindDirection.x, WindDirection.z, Wind, Mathf.Lerp(1.1f, 2.4f, Mathf.InverseLerp(.3f, 1.15f, Wind))));
            Shader.SetGlobalFloat(DaylightId, atmosphere == null ? 1 : Mathf.Max(atmosphere.Daylight, Lightning));
            UpdateRainFx();
            if (Time.time >= nextWetApply) { nextWetApply = Time.time + .4f; ApplyWetness(Mathf.Abs(Wetness - appliedWetness) < .015f && Time.frameCount % 5 != 0); }
            if (UltraGraphics.HighEnd && Time.time >= nextReflection)
            {
                // Re-render the sky into the reflection/ambient probe so wet roads mirror the current sky.
                nextReflection = Time.time + 3f;
                DynamicGI.UpdateEnvironment();
            }
        }

        private void UpdateLightning(float dt)
        {
            if (Target == WeatherKind.Storm && Rain > .6f && Time.time >= nextFlash)
            {
                nextFlash = Time.time + Random.Range(6f, 18f);
                flashTime = Time.time;
                float distance = Random.Range(.4f, 3.2f);
                thunderAt = Time.time + distance;
                thunderVolume = Mathf.Lerp(1f, .35f, distance / 3.2f);
                if (flashLight != null) flashLight.transform.rotation = Quaternion.Euler(Random.Range(35f, 70f), Random.Range(0f, 360f), 0);
            }
            float since = Time.time - flashTime;
            // Two quick strobes then a fade, like a real strike.
            Lightning = since < 0 || since > .7f ? 0 : Mathf.Max(0, (since < .08f ? 1f : since < .14f ? .25f : since < .22f ? .85f : Mathf.Lerp(.6f, 0, (since - .22f) / .48f)));
            if (flashLight != null) { flashLight.intensity = Lightning * 2.6f; flashLight.enabled = Lightning > .01f; }
            if (thunderAt > 0 && Time.time >= thunderAt)
            {
                thunderAt = -1;
                if (thunderAudio != null && thunderClip != null) thunderAudio.PlayOneShot(thunderClip, thunderVolume);
            }
        }

        private void UpdateRainFx()
        {
            var camera = Camera.main;
            if (camera != null && fxRoot != null)
            {
                Vector3 p = camera.transform.position + camera.transform.forward * 8f;
                fxRoot.transform.position = new Vector3(p.x, camera.transform.position.y + 16f, p.z);
            }
            if (rain != null)
            {
                var emission = rain.emission;
                emission.rateOverTime = Rain * (UltraGraphics.HighEnd ? 9000f : 2500f);
                var velocity = rain.velocityOverLifetime;
                float vx = WindDirection.x * Wind * 5f, vz = WindDirection.z * Wind * 5f;
                velocity.x = new ParticleSystem.MinMaxCurve(vx * .85f, vx);
                velocity.z = new ParticleSystem.MinMaxCurve(vz * .85f, vz);
                if (Rain > .001f && !rain.isPlaying) rain.Play();
            }
            if (rainAudio != null)
            {
                rainAudio.volume = Mathf.Clamp01(Rain) * .55f * WorldAudio.OutdoorLevel;
                if (Rain > .01f && !rainAudio.isPlaying) rainAudio.Play();
                else if (Rain <= .01f && rainAudio.isPlaying) rainAudio.Stop();
            }
        }

        // ---------- Wet streets ----------

        private static bool WetCandidate(Renderer r)
        {
            if (r.GetComponentInParent<Rigidbody>() != null) return false;
            int kind = GroundSurfaceStyle.SurfaceFor(r.name);
            if (kind == 1 || kind == 5) return true;
            var m = r.sharedMaterial;
            if (m == null || !m.HasProperty(SmoothnessId) || !m.HasProperty(BaseColorId)) return false;
            string n = m.name.ToLowerInvariant();
            return n.Contains("asphalt") || n.Contains("road") || n.Contains("paving") || n.Contains("sidewalk") || n.Contains("concrete")
                || n.Contains("limestone") || n.Contains("curb") || n.Contains("tarmac") || n.Contains("driveway") || n.Contains("parking")
                || n.Contains("promenade") || n.Contains("plaza") || n.Contains("cobble");
        }

        public void CollectWetSurfaces()
        {
            wetRenderers.Clear(); drySmoothness.Clear(); dryColor.Clear();
            foreach (var root in gameObject.scene.GetRootGameObjects())
                foreach (var r in root.GetComponentsInChildren<MeshRenderer>())
                {
                    if (!r.enabled || !WetCandidate(r)) continue;
                    var m = r.sharedMaterial;
                    if (m == null || !m.HasProperty(SmoothnessId) || !m.HasProperty(BaseColorId)) continue;
                    // The dry look may come from an existing property block (ground styling), not the material.
                    r.GetPropertyBlock(block);
                    wetRenderers.Add(r);
                    drySmoothness.Add(block.HasFloat(SmoothnessId) ? block.GetFloat(SmoothnessId) : m.GetFloat(SmoothnessId));
                    dryColor.Add(block.HasColor(BaseColorId) ? block.GetColor(BaseColorId) : m.GetColor(BaseColorId));
                    block.Clear();
                }
            appliedWetness = -1;
        }

        public float SmoothnessOf(int index)
        {
            wetRenderers[index].GetPropertyBlock(block);
            return block.isEmpty ? drySmoothness[index] : block.GetFloat(SmoothnessId);
        }

        private void ApplyWetness(bool skip)
        {
            if (skip && appliedWetness >= 0) return;
            appliedWetness = Wetness;
            float w = Mathf.SmoothStep(0, 1, Wetness);
            for (int i = 0; i < wetRenderers.Count; i++)
            {
                var r = wetRenderers[i];
                if (r == null) continue;
                // Merge with any existing block (ground tiling, seasonal tint) instead of replacing it.
                r.GetPropertyBlock(block);
                if (w <= .001f && block.isEmpty) continue;
                block.SetFloat(SmoothnessId, Mathf.Lerp(drySmoothness[i], .86f, w));
                var color = dryColor[i];
                block.SetColor(BaseColorId, new Color(color.r * Mathf.Lerp(1, .58f, w), color.g * Mathf.Lerp(1, .58f, w), color.b * Mathf.Lerp(1, .6f, w), color.a));
                r.SetPropertyBlock(block);
                block.Clear();
            }
        }

        // ---------- Effects construction ----------

        private static Material RainMaterial(bool splashShape)
        {
            var shader = Resources.Load<Shader>("UltraRain");
            if (shader == null) shader = Shader.Find("RoadsideHaven/Ultra Rain");
            var m = new Material(shader) { name = splashShape ? "Ultra rain splash" : "Ultra rain streak" };
            m.EnableKeyword(splashShape ? "_SHAPE_SPLASH" : "_SHAPE_STREAK");
            m.DisableKeyword(splashShape ? "_SHAPE_STREAK" : "_SHAPE_SPLASH");
            m.SetColor("_Tint", splashShape ? new Color(.8f, .84f, .9f, .45f) : new Color(.72f, .78f, .86f, .5f));
            return m;
        }

        private void BuildRain()
        {
            bool high = UltraGraphics.HighEnd;
            var rainObject = new GameObject("Rain");
            rainObject.transform.SetParent(fxRoot.transform, false);
            rain = rainObject.AddComponent<ParticleSystem>();
            rain.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = rain.main;
            main.loop = true;
            main.duration = 5;
            main.startLifetime = 1.3f;
            main.startSpeed = 0;
            main.startSize = new ParticleSystem.MinMaxCurve(.018f, .03f);
            main.startColor = new Color(1, 1, 1, .9f);
            main.maxParticles = high ? 14000 : 3500;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.playOnAwake = false;
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
            var emission = rain.emission;
            emission.rateOverTime = 0;
            var shape = rain.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(70, 1, 70);
            var velocity = rain.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(0f, 0f);
            velocity.y = new ParticleSystem.MinMaxCurve(-26f, -21f);
            velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);
            var renderer = rainObject.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.velocityScale = .045f;
            renderer.lengthScale = 1.5f;
            renderer.sharedMaterial = RainMaterial(false);
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            var splashObject = new GameObject("Rain splashes");
            splashObject.transform.SetParent(fxRoot.transform, false);
            splash = splashObject.AddComponent<ParticleSystem>();
            splash.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var sm = splash.main;
            sm.startLifetime = .24f;
            sm.startSpeed = 0;
            sm.startSize = new ParticleSystem.MinMaxCurve(.14f, .32f);
            sm.maxParticles = high ? 4000 : 800;
            sm.simulationSpace = ParticleSystemSimulationSpace.World;
            sm.playOnAwake = false;
            sm.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
            var se = splash.emission;
            se.enabled = false;
            var size = splash.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1, AnimationCurve.Linear(0, .3f, 1, 1.25f));
            var fade = splash.colorOverLifetime;
            fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) }, new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(0, 1) });
            fade.color = gradient;
            var splashRenderer = splashObject.GetComponent<ParticleSystemRenderer>();
            splashRenderer.renderMode = ParticleSystemRenderMode.HorizontalBillboard;
            splashRenderer.sharedMaterial = RainMaterial(true);
            splashRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            // Drops stop on roofs, awnings and the ground, and splash where they land.
            var collision = rain.collision;
            collision.enabled = true;
            collision.type = ParticleSystemCollisionType.World;
            collision.mode = ParticleSystemCollisionMode.Collision3D;
            collision.quality = high ? ParticleSystemCollisionQuality.Medium : ParticleSystemCollisionQuality.Low;
            collision.lifetimeLoss = 1;
            collision.bounce = 0;
            collision.radiusScale = .1f;
            collision.enableDynamicColliders = false;
            var sub = rain.subEmitters;
            sub.enabled = true;
            sub.AddSubEmitter(splash, ParticleSystemSubEmitterType.Collision, ParticleSystemSubEmitterProperties.InheritNothing, high ? .35f : .15f);
        }

        private void BuildFlashLight()
        {
            var go = new GameObject("Lightning flash");
            go.transform.SetParent(fxRoot.transform, false);
            flashLight = go.AddComponent<Light>();
            flashLight.type = LightType.Directional;
            flashLight.color = new Color(.74f, .82f, 1f);
            flashLight.shadows = LightShadows.None;
            flashLight.intensity = 0;
            flashLight.enabled = false;
        }

        private void BuildAudio()
        {
            rainAudio = fxRoot.AddComponent<AudioSource>();
            rainAudio.clip = RainLoop();
            rainAudio.loop = true;
            rainAudio.spatialBlend = 0;
            rainAudio.volume = 0;
            rainAudio.playOnAwake = false;
            thunderAudio = fxRoot.AddComponent<AudioSource>();
            thunderAudio.spatialBlend = 0;
            thunderAudio.playOnAwake = false;
            thunderClip = Thunder();
        }

        private static AudioClip RainLoop()
        {
            const int rate = 44100; int length = rate * 3;
            var data = new float[length];
            var rng = new System.Random(7);
            float low = 0, high = 0;
            for (int i = 0; i < length; i++)
            {
                float white = (float)rng.NextDouble() * 2 - 1;
                low += (white - low) * .08f;      // body of the downpour
                high += (white - high) * .55f;    // hiss
                float drop = rng.NextDouble() < .0016 ? (float)rng.NextDouble() * .9f : 0;
                data[i] = low * .9f + (high - low) * .25f + drop;
            }
            // Cross-fade the ends so the loop has no click.
            int fade = rate / 10;
            for (int i = 0; i < fade; i++) { float k = i / (float)fade; data[i] = data[i] * k + data[length - fade + i] * (1 - k); }
            var clip = AudioClip.Create("Procedural rain", length - fade, 1, rate, false);
            var trimmed = new float[length - fade];
            System.Array.Copy(data, trimmed, trimmed.Length);
            clip.SetData(trimmed, 0);
            return clip;
        }

        private static AudioClip Thunder()
        {
            const int rate = 44100; int length = rate * 5;
            var data = new float[length];
            var rng = new System.Random(11);
            float brown = 0, rumble = 0;
            for (int i = 0; i < length; i++)
            {
                float t = i / (float)rate;
                brown = Mathf.Clamp(brown + ((float)rng.NextDouble() * 2 - 1) * .06f, -1, 1);
                rumble += (brown - rumble) * .02f;
                float crack = t < .18f ? ((float)rng.NextDouble() * 2 - 1) * (1 - t / .18f) * .7f : 0;
                float envelope = Mathf.Exp(-t * .9f) * (.6f + .4f * Mathf.Sin(t * 7.3f) * Mathf.Sin(t * 2.1f));
                data[i] = Mathf.Clamp(rumble * 3.2f * envelope + crack, -1, 1);
            }
            var clip = AudioClip.Create("Procedural thunder", length, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private void OnDestroy()
        {
            if (Active == this) Active = null;
            Shader.SetGlobalFloat(WetnessId, 0);
            if (block != null)
                for (int i = 0; i < wetRenderers.Count; i++) if (wetRenderers[i] != null) { wetRenderers[i].GetPropertyBlock(block); block.SetFloat(SmoothnessId, drySmoothness[i]); block.SetColor(BaseColorId, dryColor[i]); wetRenderers[i].SetPropertyBlock(block); block.Clear(); }
            if (fxRoot != null) Destroy(fxRoot);
        }
    }
}
