using UnityEngine;

namespace RoadsideHaven
{
    /// <summary>Night fixture emission across the map, with a fixed nearest-light budget.</summary>
    public sealed class WorldNightLighting : MonoBehaviour
    {
        [Header("Scene references")]
        [SerializeField] private MotelAtmosphere atmosphere;
        [SerializeField] private IsometricCameraController mapCamera;
        [SerializeField] private Transform[] fixtures;
        [SerializeField] private Renderer[] glowingSurfaces;
        [Header("Performance and lighting")]
        [SerializeField, Range(0, 32)] private int lightBudget = 20;
        [SerializeField, Min(5)] private float activationRadius = 85f;
        [SerializeField, Min(.1f)] private float refreshSeconds = .25f;
        [SerializeField, Min(1)] private float lightRange = 24f;
        [SerializeField, Min(0)] private float intensity = 3.5f;
        [SerializeField, Min(0)] private float emission = 3f;
        [SerializeField] private Color warmLight = new Color(1f, .76f, .43f);
        private Light[] pool;
        private int[] nearest;
        private float[] distances;
        private Material[] originalMaterials;
        private Material[] runtimeMaterials;
        private bool initialized;
        private float refresh;
        private float previousNight = -1;


        public void Configure(MotelAtmosphere clock, IsometricCameraController camera, Transform[] points, Renderer[] surfaces)
        { atmosphere = clock; mapCamera = camera; fixtures = points; glowingSurfaces = surfaces; }

        private void Start() => Initialize();

        private void Initialize()
        {
            if (initialized) return;
            initialized = true;
            int budget = Mathf.Clamp(lightBudget, 0, 32);
            pool = new Light[budget]; nearest = new int[budget]; distances = new float[budget];
            for (int i = 0; i < budget; i++)
            {
                var point = new GameObject("Pooled night light " + i).AddComponent<Light>();
                point.transform.SetParent(transform, false); point.type = LightType.Point;
                point.shadows = LightShadows.None; point.range = lightRange; point.color = warmLight; point.enabled = false;
                pool[i] = point;
            }
            if (glowingSurfaces == null) glowingSurfaces = new Renderer[0];
            originalMaterials = new Material[glowingSurfaces.Length];
            runtimeMaterials = new Material[glowingSurfaces.Length];
            for (int i = 0; i < glowingSurfaces.Length; i++)
            {
                var surface = glowingSurfaces[i]; if (surface == null || surface.sharedMaterial == null) continue;
                originalMaterials[i] = surface.sharedMaterial;
                // Share one runtime material for every source material rather than creating one per window.
                for (int j = 0; j < i; j++)
                    if (originalMaterials[j] == originalMaterials[i]) { runtimeMaterials[i] = runtimeMaterials[j]; break; }
                if (runtimeMaterials[i] == null)
                {
                    runtimeMaterials[i] = new Material(originalMaterials[i]);
                    runtimeMaterials[i].EnableKeyword("_EMISSION");
                }
                surface.sharedMaterial = runtimeMaterials[i];
            }
        }

        private void OnEnable()
        {
            previousNight = -1; refresh = 0;
            if (initialized && glowingSurfaces != null)
                for (int i = 0; i < glowingSurfaces.Length; i++)
                    if (glowingSurfaces[i] != null && runtimeMaterials[i] != null) glowingSurfaces[i].sharedMaterial = runtimeMaterials[i];
        }

        private void Update()
        {
            if (!initialized) return;
            float hour = atmosphere == null ? 22 : atmosphere.Hour;
            float night = hour < 6 ? 1 : hour < 7 ? 7 - hour : hour < 17 ? 0 : hour < 19 ? (hour - 17) / 2 : 1;
            if (Mathf.Abs(night - previousNight) > .015f || night == 0 && previousNight != 0)
            {
                previousNight = night;
                for (int i = 0; i < runtimeMaterials.Length; i++)
                    if (runtimeMaterials[i] != null) runtimeMaterials[i].SetColor("_EmissionColor", warmLight * emission * night);
            }
            refresh -= Time.deltaTime;
            if (refresh > 0) return;
            refresh = Mathf.Max(.1f, refreshSeconds);
            for (int i = 0; i < pool.Length; i++) { nearest[i] = -1; distances[i] = float.MaxValue; }
            if (night > .01f && fixtures != null && mapCamera != null)
            {
                Vector3 focus = mapCamera.FocusPoint;
                for (int candidate = 0; candidate < fixtures.Length; candidate++)
                {
                    if (fixtures[candidate] == null) continue;
                    float d = (fixtures[candidate].position - focus).sqrMagnitude;
                    if (d > activationRadius * activationRadius) continue;
                    for (int slot = 0; slot < pool.Length; slot++)
                        if (d < distances[slot])
                        {
                            for (int shift = pool.Length - 1; shift > slot; shift--)
                            { distances[shift] = distances[shift - 1]; nearest[shift] = nearest[shift - 1]; }
                            distances[slot] = d; nearest[slot] = candidate; break;
                        }
                }
            }
            for (int i = 0; i < pool.Length; i++)
            {
                pool[i].enabled = nearest[i] >= 0;
                if (nearest[i] < 0) continue;
                pool[i].transform.position = fixtures[nearest[i]].position;
                pool[i].intensity = intensity * night;
            }
        }

        private void OnDisable()
        {
            if (pool != null) foreach (var point in pool) if (point != null) point.enabled = false;
            if (originalMaterials != null)
                for (int i = 0; i < originalMaterials.Length; i++)
                    if (glowingSurfaces[i] != null && originalMaterials[i] != null) glowingSurfaces[i].sharedMaterial = originalMaterials[i];
        }

        private void OnDestroy()
        {
            if (runtimeMaterials == null) return;
            for (int i = 0; i < runtimeMaterials.Length; i++)
            {
                if (runtimeMaterials[i] == null) continue;
                bool duplicate = false;
                for (int j = 0; j < i; j++) if (ReferenceEquals(runtimeMaterials[j], runtimeMaterials[i])) { duplicate = true; break; }
                if (!duplicate) Destroy(runtimeMaterials[i]);
            }
        }
    }
}
