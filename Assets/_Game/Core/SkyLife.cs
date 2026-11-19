using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RoadsideHaven
{
    /// <summary>
    /// Life in the air and on the lawns: airliners crossing the sky, a circling helicopter, drifting
    /// hot-air balloons, bird and gull flocks, dogs on park paths, and wind-blown leaves and dust.
    /// </summary>
    public sealed class SkyLife : MonoBehaviour
    {
        private sealed class Plane { public Transform T; public Vector3 From, To; public float Start, Duration, IdleUntil; public AudioSource Audio; public Renderer Strobe; }
        private sealed class Bird { public Transform T, WingL, WingR; public Vector3 Offset; public float Phase; }
        private sealed class Flock { public Transform Root; public Vector3 Anchor; public float Radius, Altitude, Speed, Phase; public Bird[] Birds; public Vector3 Last; }
        private sealed class Balloon { public Transform T, Flame; public Vector3 Home; }
        private sealed class Dog { public Transform T, Tail; public Transform[] Legs; public Vector3[] Route; public int Next; public float Speed, BarkAt, GroundY; }

        private readonly List<Plane> planes = new List<Plane>();
        private readonly List<Flock> flocks = new List<Flock>();
        private readonly List<Balloon> balloons = new List<Balloon>();
        private readonly List<Dog> dogs = new List<Dog>();
        private readonly Dictionary<Color, Material> materials = new Dictionary<Color, Material>();
        private Transform helicopter, rotor, tailRotor;
        private AudioSource helicopterAudio;
        private ParticleSystem leaves, dust;
        private GameObject root;
        private MotelAtmosphere atmosphere;
        private Shader lit;
        public int PlaneCount => planes.Count;
        public int BirdCount { get { int n = 0; foreach (var f in flocks) n += f.Birds.Length; return n; } }
        public int BalloonCount => balloons.Count;
        public int DogCount => dogs.Count;
        public Transform Helicopter => helicopter;
        public Vector3 FirstBirdPosition => flocks.Count > 0 ? flocks[0].Birds[0].T.position : Vector3.zero;
        public ParticleSystem Leaves => leaves;

        private void Start()
        {
            atmosphere = GetComponent<MotelAtmosphere>();
            lit = Shader.Find("Universal Render Pipeline/Lit");
            root = new GameObject("Sky and Wildlife");
            SceneManager.MoveGameObjectToScene(root, gameObject.scene);
            var jet = ProceduralAudio.JetRumble(); var chop = ProceduralAudio.HelicopterChop();
            for (int i = 0; i < 3; i++) planes.Add(BuildPlane(i, jet));
            BuildHelicopter(chop);
            var balloonColours = new[] { (new Color(.86f, .16f, .12f), new Color(.98f, .82f, .18f)), (new Color(.12f, .36f, .78f), Color.white), (new Color(.18f, .62f, .30f), new Color(.98f, .55f, .10f)), (new Color(.55f, .18f, .62f), new Color(.98f, .86f, .30f)), (new Color(.95f, .45f, .12f), new Color(.10f, .55f, .70f)) };
            var homes = new[] { new Vector3(-120, 95, 300), new Vector3(-220, 120, -320), new Vector3(240, 110, -260), new Vector3(60, 140, 420), new Vector3(650, 105, 40) };
            for (int i = 0; i < homes.Length; i++) balloons.Add(BuildBalloon(homes[i], balloonColours[i].Item1, balloonColours[i].Item2));
            var flockSpots = new (Vector3 anchor, float radius, float altitude, bool gull)[]
            {
                (new Vector3(-90, 0, 340), 70, 26, false), (new Vector3(-200, 0, -330), 70, 22, false), (new Vector3(700, 0, 40), 90, 30, false),
                (new Vector3(-150, 0, -50), 45, 20, false), (new Vector3(590, 0, 400), 60, 24, false), (new Vector3(20, 0, 60), 60, 32, false),
                (new Vector3(-300, 0, -700), 120, 18, true), (new Vector3(150, 0, -720), 100, 22, true)
            };
            for (int i = 0; i < flockSpots.Length; i++) flocks.Add(BuildFlock(i, flockSpots[i].anchor, flockSpots[i].radius, flockSpots[i].altitude, flockSpots[i].gull));
            var dogColours = new[] { new Color(.55f, .36f, .18f), new Color(.86f, .70f, .40f), new Color(.08f, .07f, .07f), new Color(.92f, .90f, .86f), new Color(.40f, .30f, .22f) };
            var lake = HavenExpansionLayout.LakeLoop();
            for (int i = 0; i < 3; i++) dogs.Add(BuildDog(dogColours[i], lake, i * 8));
            for (int i = 0; i < 2; i++) dogs.Add(BuildDog(dogColours[3 + i], TransitSquareLayout.GardenWalk, i * 2));
            BuildWindParticles();
        }

        // ---------- Construction helpers ----------

        private Material Mat(Color c)
        {
            if (materials.TryGetValue(c, out var m)) return m;
            m = new Material(lit) { color = c, enableInstancing = true }; m.SetFloat("_Smoothness", .35f); materials[c] = m; return m;
        }

        private Transform Part(Transform parent, PrimitiveType type, Vector3 position, Vector3 scale, Color color, Vector3 euler = default)
        {
            var go = GameObject.CreatePrimitive(type);
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false); go.transform.localPosition = position; go.transform.localScale = scale; go.transform.localRotation = Quaternion.Euler(euler);
            var r = go.GetComponent<MeshRenderer>(); r.sharedMaterial = Mat(color); r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go.transform;
        }

        private Transform Group(Transform parent, string name) { var t = new GameObject(name).transform; t.SetParent(parent, false); return t; }

        private AudioSource Audio3D(Transform host, AudioClip clip, float volume, float min, float max)
        {
            var s = host.gameObject.AddComponent<AudioSource>(); s.clip = clip; s.loop = true; s.spatialBlend = 1; s.volume = volume; s.minDistance = min; s.maxDistance = max;
            s.rolloffMode = AudioRolloffMode.Linear; s.dopplerLevel = .6f; s.Play(); return s;
        }

        // ---------- Aircraft ----------

        private Plane BuildPlane(int index, AudioClip clip)
        {
            var t = Group(root.transform, "Airliner " + index);
            Color body = Color.white, accent = index == 0 ? new Color(.1f, .25f, .6f) : index == 1 ? new Color(.75f, .1f, .1f) : new Color(.1f, .5f, .35f);
            Part(t, PrimitiveType.Capsule, Vector3.zero, new Vector3(3.6f, 19, 3.6f), body, new Vector3(90, 0, 0));
            Part(t, PrimitiveType.Cube, new Vector3(0, -.3f, 1), new Vector3(34, .45f, 5.5f), body);
            Part(t, PrimitiveType.Cube, new Vector3(0, 3.2f, -16), new Vector3(.45f, 6, 4.5f), accent);
            Part(t, PrimitiveType.Cube, new Vector3(0, .6f, -16.5f), new Vector3(12, .35f, 3), body);
            Part(t, PrimitiveType.Cube, new Vector3(0, .5f, 0), new Vector3(3.7f, .5f, 30), accent);
            for (int s = -1; s <= 1; s += 2) Part(t, PrimitiveType.Cylinder, new Vector3(s * 7, -1.4f, 2.5f), new Vector3(1.8f, 2.4f, 1.8f), new Color(.6f, .62f, .65f), new Vector3(90, 0, 0));
            Part(t, PrimitiveType.Sphere, new Vector3(-17, -.3f, 1), Vector3.one * .7f, new Color(1, .1f, .1f));
            Part(t, PrimitiveType.Sphere, new Vector3(17, -.3f, 1), Vector3.one * .7f, new Color(.1f, 1, .2f));
            var strobe = Part(t, PrimitiveType.Sphere, new Vector3(0, 6.3f, -17), Vector3.one * .9f, new Color(1, 1, 1));
            var p = new Plane { T = t, Audio = Audio3D(t, clip, .55f, 60, 1400), Strobe = strobe.GetComponent<Renderer>() };
            p.Audio.pitch = .9f + index * .07f;
            p.IdleUntil = Time.time + index * 25f;
            t.gameObject.SetActive(false);
            return p;
        }

        private void NewRoute(Plane p)
        {
            var cam = Camera.main != null ? Camera.main.transform.position : Vector3.zero;
            float angle = Random.Range(0f, Mathf.PI * 2); var dir = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
            var centre = new Vector3(cam.x, 0, cam.z) + new Vector3(Random.Range(-450f, 450f), 0, Random.Range(-450f, 450f));
            float altitude = Random.Range(230f, 380f);
            p.From = centre - dir * 2000 + Vector3.up * altitude; p.To = centre + dir * 2000 + Vector3.up * (altitude + Random.Range(-40f, 60f));
            p.Start = Time.time; p.Duration = 4000f / Random.Range(70f, 90f);
            p.T.gameObject.SetActive(true);
        }

        private void BuildHelicopter(AudioClip clip)
        {
            helicopter = Group(root.transform, "City helicopter");
            Color paint = new Color(.95f, .62f, .1f), dark = new Color(.08f, .09f, .1f);
            Part(helicopter, PrimitiveType.Sphere, Vector3.zero, new Vector3(2.6f, 2.4f, 4.6f), paint);
            Part(helicopter, PrimitiveType.Sphere, new Vector3(0, .3f, 1.4f), new Vector3(2.2f, 1.6f, 2.2f), new Color(.15f, .3f, .4f));
            Part(helicopter, PrimitiveType.Cube, new Vector3(0, .4f, -4.5f), new Vector3(.45f, .45f, 6), paint);
            Part(helicopter, PrimitiveType.Cube, new Vector3(0, 1.3f, -7.3f), new Vector3(.2f, 1.8f, 1.1f), paint);
            for (int s = -1; s <= 1; s += 2) Part(helicopter, PrimitiveType.Cube, new Vector3(s * 1.1f, -1.5f, 0), new Vector3(.12f, .12f, 4), dark);
            rotor = Group(helicopter, "Main rotor"); rotor.localPosition = Vector3.up * 1.6f;
            Part(rotor, PrimitiveType.Cube, Vector3.zero, new Vector3(13, .06f, .45f), dark); Part(rotor, PrimitiveType.Cube, Vector3.zero, new Vector3(.45f, .06f, 13), dark);
            tailRotor = Group(helicopter, "Tail rotor"); tailRotor.localPosition = new Vector3(.3f, 1.3f, -7.3f);
            Part(tailRotor, PrimitiveType.Cube, Vector3.zero, new Vector3(.05f, 2.2f, .2f), dark);
            helicopterAudio = Audio3D(helicopter, clip, .7f, 15, 650);
        }

        private Balloon BuildBalloon(Vector3 home, Color a, Color b)
        {
            var t = Group(root.transform, "Hot air balloon"); t.position = home;
            var envelope = new GameObject("Balloon envelope", typeof(MeshFilter), typeof(MeshRenderer));
            envelope.transform.SetParent(t, false); envelope.transform.localPosition = Vector3.up * 11;
            envelope.GetComponent<MeshFilter>().sharedMesh = EnvelopeMesh();
            envelope.GetComponent<MeshRenderer>().sharedMaterials = new[] { Mat(a), Mat(b) };
            Part(t, PrimitiveType.Cube, Vector3.zero, new Vector3(1.6f, 1.1f, 1.6f), new Color(.45f, .3f, .15f));
            for (int x = -1; x <= 1; x += 2) for (int z = -1; z <= 1; z += 2)
                {
                    var rope = Part(t, PrimitiveType.Cube, new Vector3(x * 1.4f, 2.6f, z * 1.4f), new Vector3(.04f, 4.2f, .04f), new Color(.2f, .15f, .1f));
                    rope.localRotation = Quaternion.Euler(z * 9, 0, -x * 9);
                }
            var flame = Part(t, PrimitiveType.Sphere, Vector3.up * 1.6f, Vector3.one * .5f, new Color(1, .65f, .15f));
            return new Balloon { T = t, Flame = flame, Home = home };
        }

        private static Mesh EnvelopeMesh()
        {
            const int gores = 16, rings = 14; var v = new List<Vector3>(); var t0 = new List<int>(); var t1 = new List<int>();
            for (int g = 0; g < gores; g++)
                for (int r = 0; r < rings; r++)
                {
                    int start = v.Count;
                    for (int k = 0; k < 4; k++)
                    {
                        float a = (g + (k == 1 || k == 2 ? 1 : 0)) * Mathf.PI * 2 / gores, lat = (r + (k >= 2 ? 1 : 0)) * Mathf.PI / rings;
                        // Teardrop profile: full at the equator, tapering to the mouth.
                        float radius = Mathf.Sin(lat) * (lat > Mathf.PI * .5f ? Mathf.Lerp(1, .28f, (lat - Mathf.PI * .5f) / (Mathf.PI * .5f)) : 1) * 9;
                        v.Add(new Vector3(Mathf.Cos(a) * radius, Mathf.Cos(lat) * 10.5f, Mathf.Sin(a) * radius));
                    }
                    var list = g % 2 == 0 ? t0 : t1;
                    list.AddRange(new[] { start, start + 1, start + 2, start, start + 2, start + 3 });
                }
            var mesh = new Mesh { name = "Balloon envelope", subMeshCount = 2 }; mesh.SetVertices(v); mesh.SetTriangles(t0, 0); mesh.SetTriangles(t1, 1);
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            // Flip only if a sample face points inward (Unity front face normal = cross(b-a, c-a)).
            int sample = rings / 2 * 4; var n = Vector3.Cross(v[sample + 1] - v[sample], v[sample + 2] - v[sample]);
            if (Vector3.Dot(n, v[sample]) < 0)
            {
                for (int s = 0; s < 2; s++) { var tris = mesh.GetTriangles(s); for (int i = 0; i < tris.Length; i += 3) (tris[i + 1], tris[i + 2]) = (tris[i + 2], tris[i + 1]); mesh.SetTriangles(tris, s); }
                mesh.RecalculateNormals();
            }
            return mesh;
        }

        // ---------- Birds ----------

        private Flock BuildFlock(int index, Vector3 anchor, float radius, float altitude, bool gull)
        {
            var flockRoot = Group(root.transform, gull ? "Seagull flock" : "Bird flock");
            int count = gull ? 9 : Random.Range(10, 15);
            var birds = new Bird[count];
            Color body = gull ? new Color(.95f, .95f, .93f) : new Color(.18f, .15f, .13f), wing = gull ? new Color(.6f, .63f, .66f) : new Color(.12f, .10f, .09f);
            for (int i = 0; i < count; i++)
            {
                var b = Group(flockRoot, "Bird");
                float scale = gull ? 2.2f : 1.5f;
                Part(b, PrimitiveType.Sphere, Vector3.zero, new Vector3(.22f, .18f, .55f) * scale, body);
                var wl = Group(b, "Left wing"); var wr = Group(b, "Right wing");
                Part(wl, PrimitiveType.Cube, new Vector3(-.35f * scale, 0, 0), new Vector3(.7f, .03f, .26f) * scale, wing);
                Part(wr, PrimitiveType.Cube, new Vector3(.35f * scale, 0, 0), new Vector3(.7f, .03f, .26f) * scale, wing);
                birds[i] = new Bird { T = b, WingL = wl, WingR = wr, Offset = new Vector3(Random.Range(-6f, 6f), Random.Range(-2.5f, 2.5f), Random.Range(-6f, 6f)), Phase = Random.Range(0f, 10f) };
            }
            return new Flock { Root = flockRoot, Anchor = anchor, Radius = radius, Altitude = altitude, Speed = gull ? .07f : .11f, Phase = index * 1.7f, Birds = birds };
        }

        // ---------- Dogs ----------

        private Dog BuildDog(Color coat, Vector3[] route, int offset)
        {
            var t = Group(root.transform, "Park dog");
            Part(t, PrimitiveType.Cube, new Vector3(0, .42f, 0), new Vector3(.32f, .3f, .75f), coat);
            Part(t, PrimitiveType.Cube, new Vector3(0, .62f, .42f), new Vector3(.26f, .26f, .3f), coat);
            Part(t, PrimitiveType.Cube, new Vector3(0, .56f, .62f), new Vector3(.14f, .12f, .16f), coat * .8f);
            for (int s = -1; s <= 1; s += 2) Part(t, PrimitiveType.Cube, new Vector3(s * .1f, .8f, .38f), new Vector3(.06f, .12f, .05f), coat * .7f);
            var legs = new Transform[4]; int n = 0;
            for (int s = -1; s <= 1; s += 2) for (int e = -1; e <= 1; e += 2)
                { var leg = Group(t, "Dog leg"); leg.localPosition = new Vector3(s * .11f, .3f, e * .28f); Part(leg, PrimitiveType.Cube, new Vector3(0, -.15f, 0), new Vector3(.08f, .3f, .08f), coat * .85f); legs[n++] = leg; }
            var tail = Group(t, "Dog tail"); tail.localPosition = new Vector3(0, .52f, -.38f);
            Part(tail, PrimitiveType.Cube, new Vector3(0, .1f, -.08f), new Vector3(.05f, .05f, .25f), coat, new Vector3(-35, 0, 0));
            var copy = new Vector3[route.Length]; int points = route.Length - 1;
            for (int i = 0; i < route.Length; i++) copy[i] = route[(i + offset) % points] + new Vector3(.9f, 0, .9f);
            t.position = copy[0];
            return new Dog { T = t, Tail = tail, Legs = legs, Route = copy, Next = 1, Speed = Random.Range(1.3f, 2.2f), BarkAt = Time.time + Random.Range(8f, 25f), GroundY = copy[0].y };
        }

        // ---------- Wind particles ----------

        private void BuildWindParticles()
        {
            var shader = Resources.Load<Shader>("UltraParticle"); if (shader == null) shader = Shader.Find("RoadsideHaven/Ultra Wind Particle");
            if (shader == null) return;
            leaves = WindSystem("Wind blown leaves", shader, UltraGraphics.HighEnd ? 500 : 150, new ParticleSystem.MinMaxCurve(.12f, .24f), 7f);
            var col = leaves.main; col.startColor = new ParticleSystem.MinMaxGradient(new Color(.42f, .55f, .18f), new Color(.75f, .55f, .18f));
            var rot = leaves.rotationOverLifetime; rot.enabled = true; rot.z = new ParticleSystem.MinMaxCurve(-4f, 4f);
            dust = WindSystem("Wind dust", shader, UltraGraphics.HighEnd ? 700 : 200, new ParticleSystem.MinMaxCurve(.03f, .07f), 3.5f);
            var dc = dust.main; dc.startColor = new Color(.85f, .78f, .62f, .35f);
        }

        private ParticleSystem WindSystem(string name, Shader shader, int max, ParticleSystem.MinMaxCurve size, float life)
        {
            var go = new GameObject(name); go.transform.SetParent(root.transform, false);
            var ps = go.AddComponent<ParticleSystem>(); ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main; main.loop = true; main.startLifetime = life; main.startSpeed = 0; main.startSize = size; main.maxParticles = max;
            main.simulationSpace = ParticleSystemSimulationSpace.World; main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate; main.playOnAwake = false; main.gravityModifier = .02f;
            var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Box; shape.scale = new Vector3(60, 10, 60);
            var vel = ps.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(0f, 0f); vel.y = new ParticleSystem.MinMaxCurve(-.2f, .4f); vel.z = new ParticleSystem.MinMaxCurve(0f, 0f);
            var noise = ps.noise; noise.enabled = true; noise.strength = 1.4f; noise.frequency = .4f; noise.scrollSpeed = .5f;
            var fade = ps.colorOverLifetime; fade.enabled = true; var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) }, new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, .15f), new GradientAlphaKey(1, .8f), new GradientAlphaKey(0, 1) });
            fade.color = g;
            var r = go.GetComponent<ParticleSystemRenderer>(); r.sharedMaterial = new Material(shader); r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
            ps.Play();
            return ps;
        }

        // ---------- Update ----------

        private void Update()
        {
            float t = Time.time, dt = Time.deltaTime;
            var cam = Camera.main != null ? Camera.main.transform.position : Vector3.zero;
            float day = atmosphere == null ? 1 : atmosphere.Daylight;
            var weather = WeatherSystem.Active;
            float rain = weather == null ? 0 : weather.Rain, wind = weather == null ? .35f : weather.Wind;
            Vector3 windDir = weather == null ? new Vector3(.8f, 0, .6f) : weather.WindDirection;

            foreach (var p in planes)
            {
                if (!p.T.gameObject.activeSelf) { if (t >= p.IdleUntil) NewRoute(p); continue; }
                float k = (t - p.Start) / p.Duration;
                if (k >= 1) { p.T.gameObject.SetActive(false); p.IdleUntil = t + Random.Range(15f, 50f); continue; }
                p.T.position = Vector3.Lerp(p.From, p.To, k);
                p.T.rotation = Quaternion.LookRotation(p.To - p.From);
                p.Strobe.enabled = Mathf.Repeat(t, 1.2f) < .08f;
            }

            if (helicopter != null)
            {
                float a = t * .09f; var centre = new Vector3(150, 0, 60);
                var pos = centre + new Vector3(Mathf.Cos(a) * 330, 88 + Mathf.Sin(t * .3f) * 6, Mathf.Sin(a) * 260);
                var ahead = centre + new Vector3(Mathf.Cos(a + .05f) * 330, 88, Mathf.Sin(a + .05f) * 260);
                helicopter.position = pos;
                helicopter.rotation = Quaternion.LookRotation(ahead - pos) * Quaternion.Euler(6, 0, -12);
                rotor.localRotation = Quaternion.Euler(0, t * 1400, 0); tailRotor.localRotation = Quaternion.Euler(t * 2000, 0, 0);
            }

            foreach (var b in balloons)
            {
                bool show = rain < .5f && wind < 1f; if (b.T.gameObject.activeSelf != show) b.T.gameObject.SetActive(show);
                if (!show) continue;
                b.T.position += windDir * (1.2f + wind * 2.5f) * dt + Vector3.up * Mathf.Sin(t * .2f + b.Home.x) * .25f * dt;
                if ((b.T.position - b.Home).magnitude > 600) b.T.position = b.Home - windDir * 300;
                b.Flame.localScale = Vector3.one * (Mathf.Repeat(t * .25f + b.Home.z, 1) < .15f ? .9f + Mathf.Sin(t * 40) * .15f : .25f);
            }

            bool birdsOut = day > .25f && rain < .4f;
            foreach (var f in flocks)
            {
                bool near = (f.Anchor - new Vector3(cam.x, 0, cam.z)).sqrMagnitude < 500 * 500;
                bool show = birdsOut && near;
                if (f.Root.gameObject.activeSelf != show) f.Root.gameObject.SetActive(show);
                if (!show) continue;
                float a = t * f.Speed + f.Phase;
                var centre = f.Anchor + new Vector3(Mathf.Sin(a) * f.Radius, f.Altitude + Mathf.Sin(a * 2.3f) * 4, Mathf.Sin(a * 2f) * f.Radius * .6f);
                var heading = centre - f.Last; f.Last = centre;
                var face = heading.sqrMagnitude > 1e-6f ? Quaternion.LookRotation(heading) : Quaternion.identity;
                foreach (var b in f.Birds)
                {
                    var wobble = new Vector3(Mathf.Sin(t * .7f + b.Phase), Mathf.Sin(t * .9f + b.Phase * 2) * .5f, Mathf.Cos(t * .6f + b.Phase)) * 1.5f;
                    b.T.position = centre + face * (b.Offset + wobble);
                    b.T.rotation = face * Quaternion.Euler(0, 0, Mathf.Sin(t * .8f + b.Phase) * 15);
                    bool glide = Mathf.Sin(t * .45f + b.Phase) > .55f;
                    float flap = glide ? 6 : Mathf.Sin(t * 13 + b.Phase * 3) * 38;
                    b.WingL.localRotation = Quaternion.Euler(0, 0, flap); b.WingR.localRotation = Quaternion.Euler(0, 0, -flap);
                }
            }

            foreach (var d in dogs)
            {
                if ((d.T.position - cam).sqrMagnitude > 250 * 250) continue;
                var target = d.Route[d.Next]; var to = target - d.T.position; to.y = 0;
                if (to.magnitude < .5f) { d.Next = (d.Next + 1) % d.Route.Length; continue; }
                float trot = Mathf.Sin(t * .2f + d.BarkAt) > .6f ? 1.8f : 1;
                var step = to.normalized * d.Speed * trot * dt;
                var p = d.T.position + step;
                if (Physics.Raycast(p + Vector3.up * 2, Vector3.down, out var hit, 4, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) && hit.collider.attachedRigidbody == null) d.GroundY = Mathf.Lerp(d.GroundY, hit.point.y, .3f);
                p.y = d.GroundY; d.T.position = p;
                d.T.rotation = Quaternion.Slerp(d.T.rotation, Quaternion.LookRotation(to), dt * 6);
                float swing = Mathf.Sin(t * 9 * trot) * 32;
                for (int i = 0; i < 4; i++) d.Legs[i].localRotation = Quaternion.Euler((i == 0 || i == 3 ? swing : -swing), 0, 0);
                d.Tail.localRotation = Quaternion.Euler(0, Mathf.Sin(t * 14) * 35, 0);
                if (t >= d.BarkAt) { d.BarkAt = t + Random.Range(12f, 35f); WorldAudio.Active?.PlayWorld(WorldAudio.Active.BarkClip(d.Route.Length + (int)d.Speed), d.T.position + Vector3.up * .6f, .35f, Random.Range(.9f, 1.15f)); }
            }

            if (leaves != null)
            {
                // Spawn upwind of the camera so leaves drift through the view.
                var at = new Vector3(cam.x, cam.y + 2, cam.z) - windDir * 12;
                leaves.transform.position = at; dust.transform.position = at;
                var season = atmosphere == null ? WorldSeason.Summer : atmosphere.Season;
                bool autumn = season == WorldSeason.Autumn || season == WorldSeason.LateAutumn;
                var le = leaves.emission; le.rateOverTime = (2 + wind * wind * 55) * (autumn ? 2.5f : 1) * (1 - rain * .6f);
                var de = dust.emission; de.rateOverTime = (8 + wind * 90) * (1 - rain);
                float speed = 1.2f + wind * 8;
                foreach (var ps in new[] { leaves, dust })
                {
                    var v = ps.velocityOverLifetime;
                    v.x = new ParticleSystem.MinMaxCurve(windDir.x * speed * .6f, windDir.x * speed);
                    v.z = new ParticleSystem.MinMaxCurve(windDir.z * speed * .6f, windDir.z * speed);
                }
                var lc = leaves.main;
                lc.startColor = autumn ? new ParticleSystem.MinMaxGradient(new Color(.85f, .45f, .12f), new Color(.75f, .2f, .08f)) : new ParticleSystem.MinMaxGradient(new Color(.35f, .52f, .16f), new Color(.65f, .58f, .22f));
            }
        }

        private void OnDestroy()
        {
            if (root != null) Destroy(root);
            foreach (var m in materials.Values) if (m != null) Destroy(m);
        }
    }
}
