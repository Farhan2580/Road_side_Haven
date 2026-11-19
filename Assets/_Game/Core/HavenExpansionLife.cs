using UnityEngine;
using UnityEngine.Rendering;

namespace RoadsideHaven
{
    /// <summary>Animation, people and distance culling for one of the new districts.</summary>
    public sealed class HavenExpansionLife : MonoBehaviour
    {
        [SerializeField] private Transform[] spinners, bobbers, swimmers, grazers, flags;
        [SerializeField] private Vector3 swimCentre;
        [SerializeField] private float swimRadiusX, swimRadiusZ;
        [SerializeField] private Rect grazeArea;
        [SerializeField] private int routeKind;   // 0 none, 1 lake loop, 2 running track
        private struct Visual { public MeshRenderer Renderer; public float Range; public ShadowCastingMode Shadows; }
        private Visual[] visuals;
        private Vector3[] grazeTargets;
        private Vector3[] bobBase;
        private TownCitizenAgent[] walkers;
        private Vector3[][] routes;
        private Camera view;
        private Vector3 areaCentre;
        private float refresh;
        public int WalkerCount => walkers == null ? 0 : walkers.Length;
        public Transform FirstSpinner => spinners != null && spinners.Length > 0 ? spinners[0] : null;
        public Transform FirstBobber => bobbers != null && bobbers.Length > 0 ? bobbers[0] : null;

        public void Configure(Transform[] spin, Transform[] bob, Transform[] swim, Vector3 centre, float rx, float rz, Transform[] graze, Rect area, Transform[] flagPoles, int route)
        { spinners = spin; bobbers = bob; swimmers = swim; swimCentre = centre; swimRadiusX = rx; swimRadiusZ = rz; grazers = graze; grazeArea = area; flags = flagPoles; routeKind = route; }

        private void Start()
        {
            view = Camera.main;
            bool mobile = !UltraGraphics.HighEnd;
            var renderers = GetComponentsInChildren<MeshRenderer>();
            visuals = new Visual[renderers.Length];
            var all = new Bounds(transform.position, Vector3.zero); bool any = false;
            foreach (var r in renderers) { if (!any) { all = r.bounds; any = true; } else all.Encapsulate(r.bounds); }
            areaCentre = all.center;
            for (int i = 0; i < renderers.Length; i++)
            {
                var r = renderers[i];
                float range = r.name.StartsWith("Expansion near batch") ? (mobile ? 45 : 80) : (mobile ? 260 : 520);
                if (r.GetComponent<TMPro.TMP_Text>() != null) range = 90;
                visuals[i] = new Visual { Renderer = r, Range = range * range, Shadows = r.shadowCastingMode };
            }
            bobBase = new Vector3[bobbers == null ? 0 : bobbers.Length];
            for (int i = 0; i < bobBase.Length; i++) if (bobbers[i] != null) bobBase[i] = bobbers[i].localPosition;
            grazeTargets = new Vector3[grazers == null ? 0 : grazers.Length];
            for (int i = 0; i < grazeTargets.Length; i++) if (grazers[i] != null) grazeTargets[i] = grazers[i].position;
            var source = routeKind == 1 ? HavenExpansionLayout.LakeLoop() : routeKind == 2 ? HavenExpansionLayout.TrackLoop() : null;
            if (source == null) return;
            int count = mobile ? 2 : 4; walkers = new TownCitizenAgent[count]; routes = new Vector3[count][];
            for (int i = 0; i < count; i++)
            {
                int points = source.Length - 1, offset = i * points / count;
                routes[i] = new Vector3[source.Length];
                for (int p = 0; p < source.Length; p++) routes[i][p] = source[(p + offset) % points];
                var go = new GameObject((routeKind == 2 ? "Track runner " : "Lakeside walker ") + i);
                go.transform.SetParent(transform, false);
                var person = go.AddComponent<TownCitizenAgent>();
                person.Initialize(null, null, false);
                person.SetJogging(routeKind == 2 || i == 1);
                person.Warp(routes[i][0]); person.Walk(routes[i]); walkers[i] = person;
                go.AddComponent<WorldNpc>().Configure((routeKind == 2 ? "sports-runner-" : "lake-walker-") + i, routeKind == 2 ? "Track runner" : "Park visitor");
            }
        }

        private void Update()
        {
            if (Time.unscaledTime >= refresh)
            {
                refresh = Time.unscaledTime + .4f;
                if (view == null) view = Camera.main;
                if (view != null && visuals != null)
                    foreach (var v in visuals)
                    {
                        if (v.Renderer == null) continue;
                        float d = v.Renderer.bounds.SqrDistance(view.transform.position);
                        v.Renderer.enabled = d < v.Range;
                        v.Renderer.shadowCastingMode = d < 90 * 90 ? v.Shadows : ShadowCastingMode.Off;
                    }
            }
            // Nobody can see the animation from the far side of the map.
            if (view != null && (view.transform.position - areaCentre).sqrMagnitude > 600 * 600) return;
            float t = Time.time;
            float wind = WeatherSystem.Active == null ? .4f : WeatherSystem.Active.Wind;
            if (spinners != null) foreach (var s in spinners) if (s != null) s.localRotation = Quaternion.Euler(0, 0, t * (25 + wind * 40));
            if (bobbers != null) for (int i = 0; i < bobbers.Length; i++) if (bobbers[i] != null)
                    { bobbers[i].localPosition = bobBase[i] + Vector3.up * Mathf.Sin(t * 1.3f + i) * .05f; bobbers[i].localRotation = Quaternion.Euler(Mathf.Sin(t * 1.1f + i) * 2.5f, i * 47, Mathf.Sin(t * .9f + i * 2) * 3f); }
            if (swimmers != null) for (int i = 0; i < swimmers.Length; i++) if (swimmers[i] != null)
                    {
                        float a = t * .05f + i * 1.05f, k = .45f + (i % 3) * .12f;
                        var p = new Vector3(swimCentre.x + Mathf.Cos(a) * swimRadiusX * k, swimmers[i].position.y, swimCentre.z + Mathf.Sin(a) * swimRadiusZ * k);
                        swimmers[i].rotation = Quaternion.LookRotation(new Vector3(-Mathf.Sin(a), 0, Mathf.Cos(a)));
                        swimmers[i].position = p;
                    }
            if (grazers != null) for (int i = 0; i < grazers.Length; i++)
                {
                    var g = grazers[i]; if (g == null) continue;
                    var to = grazeTargets[i] - g.position; to.y = 0;
                    if (to.magnitude < .4f) grazeTargets[i] = new Vector3(Random.Range(grazeArea.xMin + 4, grazeArea.xMax - 4), g.position.y, Random.Range(grazeArea.yMin + 4, grazeArea.yMax - 4));
                    else { g.rotation = Quaternion.RotateTowards(g.rotation, Quaternion.LookRotation(to), 30 * Time.deltaTime); g.position += g.forward * .45f * Time.deltaTime * Mathf.Clamp01(Vector3.Dot(g.forward, to.normalized)); }
                }
            if (flags != null) for (int i = 0; i < flags.Length; i++) if (flags[i] != null) flags[i].localRotation = Quaternion.Euler(0, Mathf.Sin(t * (1.5f + wind * 3) + i) * (6 + wind * 10), 0);
            if (walkers != null) for (int i = 0; i < walkers.Length; i++) if (walkers[i] != null && walkers[i].Arrived) walkers[i].Walk(routes[i]);
        }

        private void OnDisable() { if (visuals == null) return; foreach (var v in visuals) if (v.Renderer != null) { v.Renderer.enabled = true; v.Renderer.shadowCastingMode = v.Shadows; } }
    }
}
