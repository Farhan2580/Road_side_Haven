using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

namespace RoadsideHaven
{
    /// <summary>Fictional airline paint scheme.</summary>
    public sealed class AirlineLivery
    {
        public string Name, Code; public Color Primary, Secondary, Tail, Belly;
        public AirlineLivery(string name, string code, Color primary, Color secondary, Color tail, Color? belly = null)
        { Name = name; Code = code; Primary = primary; Secondary = secondary; Tail = tail; Belly = belly ?? new Color(.78f, .8f, .82f); }
        public static readonly AirlineLivery Haven = new AirlineLivery("HAVEN AIR", "HV", new Color(.03f, .16f, .34f), new Color(.05f, .62f, .66f), new Color(.03f, .16f, .34f));
        public static readonly AirlineLivery Farhan = new AirlineLivery("FARHAN AIRWAYS", "FA", new Color(.04f, .42f, .22f), new Color(.92f, .7f, .12f), new Color(.04f, .42f, .22f));
        public static readonly AirlineLivery Skybridge = new AirlineLivery("SKYBRIDGE", "SB", new Color(.78f, .06f, .08f), new Color(.15f, .15f, .18f), new Color(.78f, .06f, .08f));
        public static readonly AirlineLivery Aurora = new AirlineLivery("AURORA JET", "AJ", new Color(.36f, .12f, .55f), new Color(.95f, .45f, .2f), new Color(.36f, .12f, .55f));
        public static readonly AirlineLivery Rescue = new AirlineLivery("AIR AMBULANCE", "HG", new Color(.82f, .06f, .05f), new Color(.98f, .8f, .1f), new Color(.82f, .06f, .05f), new Color(.9f, .9f, .9f));
        public static readonly AirlineLivery Tours = new AirlineLivery("HAVEN SKY TOURS", "ST", new Color(.05f, .1f, .14f), new Color(.1f, .62f, .9f), new Color(.1f, .62f, .9f));
    }

    /// <summary>
    /// Procedural aircraft: an airliner with a lofted, upswept fuselage painted with windows, cheat-line and doors,
    /// swept cambered wings with dihedral and winglets, lathe-turned turbofans on pylons, swept tailplane and fin,
    /// articulated landing gear and navigation lights; and a modern twin-engine helicopter.
    /// Coordinates: nose at +Z, wheels on the ground at Y = 0, fuselage axis at Y = 3.2 (airliner, scale 1).
    /// </summary>
    public static class AircraftArt
    {
        private static readonly Dictionary<string, Mesh> meshes = new Dictionary<string, Mesh>();
        private static readonly Dictionary<string, Material> materials = new Dictionary<string, Material>();
        private static Shader Lit => Shader.Find("Universal Render Pipeline/Lit");

        // ------------------------------------------------------------------ materials
        private static Material Mat(string key, Color c, float smooth = .6f, float metal = 0, Texture2D tex = null, Color? glow = null)
        {
            if (materials.TryGetValue(key, out var m) && m != null) return m;
            m = new Material(Lit) { name = key, color = c, enableInstancing = true }; m.SetFloat("_Smoothness", smooth); m.SetFloat("_Metallic", metal);
            if (tex != null) m.mainTexture = tex;
            if (glow.HasValue) { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", glow.Value); m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None; }
            materials[key] = m; return m;
        }
        public static Material Glass => Mat("Aircraft glass", new Color(.04f, .06f, .08f), .95f, .3f);
        public static Material Grey => Mat("Aircraft wing grey", new Color(.72f, .74f, .77f), .55f, .35f);
        public static Material Dark => Mat("Aircraft dark", new Color(.05f, .05f, .06f), .4f);
        public static Material Chrome => Mat("Aircraft chrome", new Color(.75f, .77f, .8f), .85f, .9f);
        public static Material Tyre => Mat("Aircraft tyre", new Color(.03f, .03f, .03f), .2f);
        public static Material Red => Mat("Nav light red", new Color(1, .1f, .1f), .5f, 0, null, new Color(4, .3f, .3f));
        public static Material Green => Mat("Nav light green", new Color(.1f, 1, .3f), .5f, 0, null, new Color(.3f, 4, .8f));
        public static Material Strobe => Mat("Strobe white", Color.white, .5f, 0, null, new Color(5, 5, 5));
        private static Material Paint(Color c) => Mat("Paint " + c, c, .7f, .1f);

        // ------------------------------------------------------------------ mesh helpers
        private struct Station { public float Z, Rx, Ry, Y; public Station(float z, float rx, float ry, float y) { Z = z; Rx = rx; Ry = ry; Y = y; } }
        /// <summary>Elliptical loft along Z with UV u = angle / 2π (0 = right side, .25 = top), v = along length.</summary>
        private static Mesh Loft(string key, Station[] st, int sides)
        {
            if (meshes.TryGetValue(key, out var cached) && cached != null) return cached;
            var v = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>();
            float z0 = st[0].Z, z1 = st[st.Length - 1].Z;
            for (int r = 0; r < st.Length; r++)
                for (int s = 0; s <= sides; s++)
                {
                    float a = s * Mathf.PI * 2 / sides; var p = st[r];
                    v.Add(new Vector3(Mathf.Cos(a) * p.Rx, p.Y + Mathf.Sin(a) * p.Ry, p.Z)); uv.Add(new Vector2(s / (float)sides, (p.Z - z0) / (z1 - z0)));
                    if (r > 0 && s > 0) { int i = r * (sides + 1) + s, j = i - (sides + 1); t.AddRange(new[] { j - 1, i - 1, i, j - 1, i, j }); }
                }
            var mesh = new Mesh { name = key }; mesh.SetVertices(v); mesh.SetUVs(0, uv); mesh.SetTriangles(t, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
            meshes[key] = mesh; return mesh;
        }
        /// <summary>Lathe around Z from (z, r) profile pairs.</summary>
        private static Mesh Lathe(string key, Vector2[] profile, int sides)
        {
            var st = new Station[profile.Length]; for (int i = 0; i < profile.Length; i++) st[i] = new Station(profile[i].x, profile[i].y, profile[i].y, 0);
            return Loft(key, st, sides);
        }
        /// <summary>Tapered, swept, cambered surface between a root and a tip section (wing, tailplane, fin).</summary>
        private static Mesh Surface(string key, Vector3 rootLE, float rootChord, float rootThick, Vector3 tipLE, float tipChord, float tipThick, bool vertical)
        {
            if (meshes.TryGetValue(key, out var cached) && cached != null) return cached;
            const int n = 9; var v = new List<Vector3>(); var t = new List<int>();
            // Airfoil: chordwise stations; thickness distribution of a NACA-like section, slight camber.
            float[] xs = { 0, .03f, .1f, .2f, .32f, .46f, .62f, .8f, 1 };
            Vector3 Section(Vector3 le, float chord, float thick, float x, float side)
            {
                float th = 5 * thick * (.2969f * Mathf.Sqrt(x) - .126f * x - .3516f * x * x + .2843f * x * x * x - .1036f * x * x * x * x);
                float camber = thick * .25f * Mathf.Sin(x * Mathf.PI);
                float off = camber + side * th * chord / Mathf.Max(.0001f, chord) ;
                return vertical ? le + new Vector3(side * th, 0, -x * chord) : le + new Vector3(0, off, -x * chord);
            }
            for (int s = 0; s < 2; s++) for (int side = 0; side < 2; side++) for (int i = 0; i < n; i++)
                    {
                        var le = s == 0 ? rootLE : tipLE; float chord = s == 0 ? rootChord : tipChord, thick = s == 0 ? rootThick : tipThick;
                        v.Add(Section(le, chord, thick, xs[i], side == 0 ? 1 : -1));
                    }
            int Idx(int s, int side, int i) => s * 2 * n + side * n + i;
            for (int i = 0; i < n - 1; i++)
            {
                // Upper and lower skins between root and tip; winding faces outwards on both.
                t.AddRange(new[] { Idx(0, 0, i), Idx(1, 0, i), Idx(1, 0, i + 1), Idx(0, 0, i), Idx(1, 0, i + 1), Idx(0, 0, i + 1) });
                t.AddRange(new[] { Idx(0, 1, i), Idx(1, 1, i + 1), Idx(1, 1, i), Idx(0, 1, i), Idx(0, 1, i + 1), Idx(1, 1, i + 1) });
                // Tip and root caps.
                t.AddRange(new[] { Idx(1, 0, i), Idx(1, 1, i), Idx(1, 1, i + 1), Idx(1, 0, i), Idx(1, 1, i + 1), Idx(1, 0, i + 1) });
                t.AddRange(new[] { Idx(0, 0, i), Idx(0, 1, i + 1), Idx(0, 1, i), Idx(0, 0, i), Idx(0, 0, i + 1), Idx(0, 1, i + 1) });
            }
            var mesh = new Mesh { name = key }; mesh.SetVertices(v); mesh.SetTriangles(t, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
            meshes[key] = mesh; return mesh;
        }
        private static Mesh Primitive(PrimitiveType type)
        {
            string key = "prim " + type; if (meshes.TryGetValue(key, out var m) && m != null) return m;
            var g = GameObject.CreatePrimitive(type); m = g.GetComponent<MeshFilter>().sharedMesh; Object.DestroyImmediate(g); meshes[key] = m; return m;
        }
        private static Transform Part(Transform p, string name, Mesh mesh, Material mat, Vector3 pos, Vector3? scale = null, Vector3? euler = null, bool shadows = true)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer)); go.transform.SetParent(p, false);
            go.transform.localPosition = pos; if (scale.HasValue) go.transform.localScale = scale.Value; if (euler.HasValue) go.transform.localRotation = Quaternion.Euler(euler.Value);
            go.GetComponent<MeshFilter>().sharedMesh = mesh; var r = go.GetComponent<MeshRenderer>(); r.sharedMaterial = mat; r.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off; return go.transform;
        }
        private static Transform Box(Transform p, string n, Vector3 pos, Vector3 size, Material m, Vector3? euler = null) => Part(p, n, Primitive(PrimitiveType.Cube), m, pos, size, euler);
        private static Transform Cyl(Transform p, string n, Vector3 pos, float radius, float length, Material m, Vector3 euler) => Part(p, n, Primitive(PrimitiveType.Cylinder), m, pos, new Vector3(radius * 2, length * .5f, radius * 2), euler);
        private static Transform Ball(Transform p, string n, Vector3 pos, Vector3 size, Material m, bool shadows = true) => Part(p, n, Primitive(PrimitiveType.Sphere), m, pos, size, null, shadows);

        // ------------------------------------------------------------------ paint textures
        private static Texture2D FuselageTexture(AirlineLivery l, bool helicopter)
        {
            string key = (helicopter ? "heli " : "jet ") + l.Name; int w = helicopter ? 128 : 256, h = helicopter ? 256 : 640;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, true) { name = key, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear, anisoLevel = 8 };
            var px = new Color32[w * h]; Color white = new Color(.95f, .96f, .97f), glass = new Color(.06f, .08f, .1f), line = new Color(.62f, .65f, .68f);
            for (int y = 0; y < h; y++)
            {
                float vz = y / (float)(h - 1);
                for (int x = 0; x < w; x++)
                {
                    float a = x / (float)w * Mathf.PI * 2; float e = Mathf.Sin(a), side = Mathf.Cos(a); Color c = white;
                    if (!helicopter)
                    {
                        float z = Mathf.Lerp(-17.5f, 18f, vz);
                        if (e < -.55f) c = l.Belly;
                        if (e > -.2f && e < -.06f) c = l.Primary; else if (e > -.27f && e < -.22f) c = l.Secondary;
                        // Tail sweep of livery colour up the rear fuselage.
                        if (z < -9 && e > -.06f + (z + 9) * -.09f) c = Color.Lerp(c, l.Tail, Mathf.Clamp01((-9 - z) / 2));
                        bool windowRow = e > .38f && e < .5f && z > -11.6f && z < 12.4f;
                        float pitch = Mathf.Repeat((z + 11.6f) / .53f, 1);
                        bool door = (Mathf.Abs(z - 10.7f) < .55f || Mathf.Abs(z + 12.2f) < .5f || Mathf.Abs(z - .6f) < .35f) && e > -.12f && e < .62f;
                        if (windowRow && pitch < .5f && !door) c = glass;
                        if (door && (Mathf.Abs(Mathf.Abs(z - 10.7f) - .55f) < .03f || Mathf.Abs(Mathf.Abs(z + 12.2f) - .5f) < .03f || Mathf.Abs(Mathf.Abs(z - .6f) - .35f) < .03f || Mathf.Abs(e - .62f) < .012f)) c = line;
                        // Flight-deck windows.
                        if (z > 15.4f && z < 16.95f && e > .26f && e < .62f && Mathf.Abs(side) > .15f) { float k = (z - 15.4f) / 1.55f; if (e < .62f - k * .18f) c = glass; }
                        if (z > 16.4f && z < 17.15f && e > .5f && e < .8f) c = glass;
                    }
                    else
                    {
                        float z = Mathf.Lerp(-2.9f, 2.75f, vz);
                        c = l.Secondary.maxColorComponent > .9f && l == AirlineLivery.Rescue ? new Color(.95f, .95f, .95f) : white;
                        if (e < -.3f) c = l.Primary;
                        if (e > -.3f && e < -.12f) c = l.Secondary;
                        if (z > .75f && e > -.15f) c = glass;                                  // wrap-around canopy
                        if (z > -1.3f && z < .45f && e > .05f && e < .58f && Mathf.Abs(side) > .3f) c = glass; // cabin windows
                        if (Mathf.Abs(z + .45f) < .02f && e > -.3f && e < .62f) c = line;      // sliding door seam
                        if (e > .9f) c = l.Primary;
                    }
                    px[y * w + x] = c;
                }
            }
            tex.SetPixels32(px); tex.Apply(true); return tex;
        }
        private static Material FuselagePaint(AirlineLivery l, bool heli)
        {
            string key = (heli ? "Heli skin " : "Jet skin ") + l.Name; if (materials.TryGetValue(key, out var m) && m != null) return m;
            return Mat(key, Color.white, .78f, .15f, FuselageTexture(l, heli));
        }

        // ------------------------------------------------------------------ airliner
        public sealed class Airliner { public Transform Root, Gear, Beacon, Strobe; public Renderer[] Blink; }
        /// <summary>Builds a twin-engine airliner under <paramref name="parent"/>. Pass an existing gear transform to keep gear animation.</summary>
        public static Airliner BuildAirliner(Transform parent, AirlineLivery l, Transform gear = null)
        {
            var root = new GameObject("Airliner (" + l.Name + ")").transform; root.SetParent(parent, false);
            var fus = Loft("Airliner fuselage", new[]
            {
                new Station(-17.5f, .18f, .24f, 4.45f), new Station(-16.6f, .45f, .55f, 4.25f), new Station(-15f, .85f, .95f, 3.95f), new Station(-13f, 1.25f, 1.33f, 3.62f),
                new Station(-10.5f, 1.65f, 1.72f, 3.32f), new Station(-8.5f, 1.85f, 1.92f, 3.2f), new Station(9.5f, 1.85f, 1.92f, 3.2f), new Station(12f, 1.8f, 1.86f, 3.18f),
                new Station(14f, 1.62f, 1.66f, 3.12f), new Station(15.6f, 1.3f, 1.38f, 3.06f), new Station(16.8f, .9f, .98f, 3.0f), new Station(17.6f, .45f, .52f, 2.95f), new Station(18f, .05f, .06f, 2.92f)
            }, 40);
            Part(root, "Fuselage", fus, FuselagePaint(l, false), Vector3.zero);
            // Wing-body fairing.
            Ball(root, "Wing-body fairing", new Vector3(0, 1.85f, -1.2f), new Vector3(3.5f, 1.4f, 11f), Grey);
            foreach (int s in new[] { -1, 1 })
            {
                var wing = Part(root, "Main wing", Surface("Airliner wing", new Vector3(1.4f, 2.0f, 3.2f), 7.4f, .13f, new Vector3(17.2f, 3.35f, -6.4f), 1.7f, .1f, false), Grey, Vector3.zero, new Vector3(s, 1, 1));
                if (s < 0) FlipWinding(wing);
                var winglet = Part(root, "Winglet", Surface("Airliner winglet", new Vector3(17.15f, 3.35f, -6.35f), 1.6f, .08f, new Vector3(17.6f, 5.4f, -7.7f), .55f, .08f, true), Paint(l.Primary), Vector3.zero, new Vector3(s, 1, 1));
                if (s < 0) FlipWinding(winglet);
                // Flap track fairings.
                for (int k = 0; k < 3; k++) { float x = 4.5f + k * 3.6f; float z = Mathf.Lerp(-4.2f, -7.1f, (x - 1.4f) / 15.8f); Ball(root, "Flap track fairing", new Vector3(s * x, 2.1f + (x - 1.4f) * .085f, z), new Vector3(.25f, .3f, 1.9f), Grey); }
                // Turbofan on its pylon.
                var nac = Lathe("Turbofan nacelle", new[] { new Vector2(-2.7f, .5f), new Vector2(-2.2f, .72f), new Vector2(-1.1f, .98f), new Vector2(.4f, 1.08f), new Vector2(1.6f, 1.05f), new Vector2(2.15f, .98f), new Vector2(2.3f, .9f), new Vector2(2.25f, .82f) }, 28);
                var engine = new GameObject("Engine").transform; engine.SetParent(root, false); engine.localPosition = new Vector3(s * 5.7f, 1.75f, 2.6f);
                Part(engine, "Nacelle", nac, Paint(new Color(.93f, .94f, .95f)), Vector3.zero);
                Part(engine, "Intake lip", Lathe("Intake lip", new[] { new Vector2(2.0f, .8f), new Vector2(2.3f, .88f), new Vector2(2.32f, .7f) }, 28), Chrome, Vector3.zero);
                Cyl(engine, "Fan face", new Vector3(0, 0, 2.05f), .8f, .05f, Dark, new Vector3(90, 0, 0));
                Part(engine, "Spinner", Lathe("Spinner", new[] { new Vector2(1.95f, .26f), new Vector2(2.3f, .14f), new Vector2(2.45f, .01f) }, 16), Chrome, Vector3.zero);
                Part(engine, "Exhaust cone", Lathe("Exhaust cone", new[] { new Vector2(-2.6f, .42f), new Vector2(-3.3f, .18f), new Vector2(-3.55f, .02f) }, 16), Dark, Vector3.zero);
                Box(engine, "Engine stripe", new Vector3(s * 1.07f, 0, .6f), new Vector3(.02f, .5f, 1.4f), Paint(l.Secondary));
                Box(root, "Engine pylon", new Vector3(s * 5.7f, 2.45f, .3f), new Vector3(.32f, .7f, 4.2f), Grey, new Vector3(-6, 0, 0));
                // Horizontal stabiliser.
                var tp = Part(root, "Tailplane", Surface("Airliner tailplane", new Vector3(.5f, 3.95f, -12.8f), 3.8f, .1f, new Vector3(6.6f, 4.45f, -15.9f), 1.4f, .08f, false), Grey, Vector3.zero, new Vector3(s, 1, 1));
                if (s < 0) FlipWinding(tp);
            }
            var fin = Part(root, "Tail fin", Surface("Airliner fin", new Vector3(0, 4.7f, -10.4f), 6.6f, .12f, new Vector3(0, 11.2f, -15.4f), 2.6f, .1f, true), Paint(l.Tail), Vector3.zero);
            Box(root, "Fin flash", new Vector3(0, 8.4f, -14.2f), new Vector3(.32f, .5f, 3.3f), Paint(l.Secondary), new Vector3(0, 0, 0));
            foreach (int s in new[] { -1, 1 })
            {
                var code = Text(root, l.Code, new Vector3(s * .2f, 8.6f, -14.4f), s > 0 ? -90 : 90, new Vector2(3, 2), Color.white, 14);
                var name = Text(root, l.Name, new Vector3(s * 1.5f, 4.42f, 4f), 0, new Vector2(11, 1.2f), l.Primary, 9);
                var n = new Vector3(s * Mathf.Cos(38 * Mathf.Deg2Rad), Mathf.Sin(38 * Mathf.Deg2Rad), 0);
                name.transform.localRotation = Quaternion.LookRotation(-n, Vector3.up) * Quaternion.Euler(0, 0, 0);
                name.transform.localPosition = new Vector3(0, 3.2f, 3.5f) + Vector3.Scale(n, new Vector3(1.88f, 1.95f, 1));
            }
            // Lights.
            var blink = new List<Renderer>();
            Ball(root, "Nav light left (red)", new Vector3(-17.5f, 3.4f, -6.5f), Vector3.one * .25f, Red, false);
            Ball(root, "Nav light right (green)", new Vector3(17.5f, 3.4f, -6.5f), Vector3.one * .25f, Green, false);
            var beacon = Ball(root, "Anti-collision beacon", new Vector3(0, 5.15f, 1f), new Vector3(.3f, .14f, .3f), Red, false);
            var belly = Ball(root, "Belly beacon", new Vector3(0, 1.26f, 4f), new Vector3(.3f, .14f, .3f), Red, false);
            var strobe = Ball(root, "Tail strobe", new Vector3(0, 4.45f, -17.55f), Vector3.one * .2f, Strobe, false);
            blink.Add(beacon.GetComponent<Renderer>()); blink.Add(belly.GetComponent<Renderer>()); blink.Add(strobe.GetComponent<Renderer>());
            // Landing gear (nose and two four-wheel main bogies).
            if (gear == null) { gear = new GameObject("Landing gear").transform; gear.SetParent(root, false); }
            var g = new GameObject("Gear struts and wheels").transform; g.SetParent(gear, false);
            Cyl(g, "Nose gear strut", new Vector3(0, .95f, 14.2f), .1f, 1.5f, Chrome, Vector3.zero);
            foreach (int s in new[] { -1, 1 }) Cyl(g, "Nose wheel", new Vector3(s * .22f, .42f, 14.2f), .42f, .26f, Tyre, new Vector3(0, 0, 90));
            foreach (int s in new[] { -1, 1 })
            {
                Cyl(g, "Main gear strut", new Vector3(s * 2.55f, 1.15f, -1.6f), .15f, 1.8f, Chrome, Vector3.zero);
                Box(g, "Bogie beam", new Vector3(s * 2.55f, .6f, -1.6f), new Vector3(.18f, .16f, 1.8f), Chrome);
                foreach (float dz in new[] { -.7f, .7f }) foreach (int w in new[] { -1, 1 }) Cyl(g, "Main wheel", new Vector3(s * 2.55f + w * .38f, .58f, -1.6f + dz), .58f, .36f, Tyre, new Vector3(0, 0, 90));
            }
            return new Airliner { Root = root, Gear = gear, Beacon = beacon, Strobe = strobe, Blink = blink.ToArray() };
        }
        private static void FlipWinding(Transform part)
        {
            // Mirrored (negative scale) meshes render inside-out with back-face culling on URP Lit's default; use a mirrored copy instead.
            var f = part.GetComponent<MeshFilter>(); string key = f.sharedMesh.name + " (mirrored)";
            if (!meshes.TryGetValue(key, out var m) || m == null)
            {
                m = Object.Instantiate(f.sharedMesh); m.name = key; var v = m.vertices; for (int i = 0; i < v.Length; i++) v[i].x = -v[i].x; m.vertices = v;
                var t = m.triangles; for (int i = 0; i < t.Length; i += 3) { int k = t[i]; t[i] = t[i + 1]; t[i + 1] = k; } m.triangles = t; m.RecalculateNormals(); m.RecalculateBounds(); meshes[key] = m;
            }
            f.sharedMesh = m; part.localScale = Vector3.one;
        }
        private static TextMeshPro Text(Transform parent, string text, Vector3 pos, float yaw, Vector2 size, Color color, float max)
        {
            var t = new GameObject("Livery " + text).AddComponent<TextMeshPro>(); t.transform.SetParent(parent, false);
            t.transform.localPosition = pos; t.transform.localRotation = Quaternion.Euler(0, yaw, 0);
            if (TMP_Settings.defaultFontAsset != null) t.font = TMP_Settings.defaultFontAsset;
            t.text = text; t.color = color; t.alignment = TextAlignmentOptions.Center; t.textWrappingMode = TextWrappingModes.NoWrap; t.fontStyle = FontStyles.Bold | FontStyles.Italic;
            t.enableAutoSizing = true; t.fontSizeMin = .1f; t.fontSizeMax = max; t.rectTransform.sizeDelta = size;
            var m = t.fontMaterial; if (m.HasProperty("_CullMode")) m.SetFloat("_CullMode", (float)CullMode.Back); return t;
        }

        // ------------------------------------------------------------------ helicopter
        public sealed class Helicopter { public Transform Root, Rotor, Tail; public Renderer[] Blink; }
        /// <summary>Modern twin-engine helicopter with skids. Rotor/tail transforms may be supplied to reuse existing animation.</summary>
        public static Helicopter BuildHelicopter(Transform parent, AirlineLivery l, Transform rotor = null, Transform tail = null)
        {
            var root = new GameObject("Helicopter (" + l.Name + ")").transform; root.SetParent(parent, false);
            var body = Loft("Helicopter fuselage", new[]
            {
                new Station(-2.9f, .28f, .3f, 2.05f), new Station(-2.45f, .6f, .62f, 1.9f), new Station(-1.6f, .82f, .92f, 1.72f), new Station(-.4f, .88f, 1.0f, 1.64f),
                new Station(.7f, .84f, .97f, 1.6f), new Station(1.5f, .74f, .86f, 1.52f), new Station(2.15f, .52f, .62f, 1.4f), new Station(2.55f, .26f, .32f, 1.3f), new Station(2.75f, .04f, .05f, 1.26f)
            }, 32);
            Part(root, "Fuselage", body, FuselagePaint(l, true), Vector3.zero);
            Ball(root, "Engine cowling", new Vector3(0, 2.55f, -.9f), new Vector3(1.1f, .6f, 2.6f), Paint(l.Primary));
            Box(root, "Engine exhausts", new Vector3(0, 2.68f, -2.2f), new Vector3(.9f, .22f, .35f), Dark);
            var boom = Lathe("Helicopter tail boom", new[] { new Vector2(-7.3f, .14f), new Vector2(-5f, .2f), new Vector2(-2.6f, .32f) }, 16);
            Part(root, "Tail boom", boom, Paint(l.Primary), new Vector3(0, 2.15f, 0), null, new Vector3(-3, 0, 0));
            Part(root, "Vertical fin", Surface("Heli fin", new Vector3(0, 2.0f, -6.6f), 1.2f, .12f, new Vector3(0, 3.6f, -7.6f), .7f, .1f, true), Paint(l.Secondary), Vector3.zero);
            Box(root, "Horizontal stabiliser", new Vector3(0, 2.38f, -6.2f), new Vector3(2.2f, .06f, .55f), Paint(l.Primary));
            foreach (int s in new[] { -1, 1 })
            {
                Cyl(root, "Skid", new Vector3(s * 1.05f, .1f, .1f), .06f, 3.9f, Chrome, new Vector3(90, 0, 0));
                Cyl(root, "Skid toe", new Vector3(s * 1.05f, .24f, 2.12f), .06f, .45f, Chrome, new Vector3(55, 0, 0));
                foreach (float z in new[] { -.9f, .9f }) Cyl(root, "Skid strut", new Vector3(s * .82f, .55f, z), .05f, .95f, Chrome, new Vector3(0, 0, s * 24));
            }
            Cyl(root, "Rotor mast", new Vector3(0, 3.0f, -.3f), .1f, .5f, Chrome, Vector3.zero);
            if (rotor == null) { rotor = new GameObject("Main rotor").transform; rotor.SetParent(root, false); }
            rotor.localPosition = new Vector3(0, 3.28f, -.3f);
            Cyl(rotor, "Rotor hub", Vector3.zero, .28f, .22f, Dark, Vector3.zero);
            for (int i = 0; i < 4; i++) { var blade = new GameObject("Blade").transform; blade.SetParent(rotor, false); blade.localRotation = Quaternion.Euler(0, i * 90, 0); Box(blade, "Rotor blade", new Vector3(0, 0, 2.75f), new Vector3(.32f, .05f, 5.2f), Dark, new Vector3(0, 0, 3)); Box(blade, "Blade tip", new Vector3(0, 0, 5.25f), new Vector3(.33f, .055f, .25f), Paint(new Color(.95f, .8f, .1f))); }
            if (tail == null) { tail = new GameObject("Tail rotor").transform; tail.SetParent(root, false); }
            tail.localPosition = new Vector3(.22f, 2.75f, -7.25f);
            for (int i = 0; i < 3; i++) { var b = new GameObject("Tail blade").transform; b.SetParent(tail, false); b.localRotation = Quaternion.Euler(i * 120, 0, 0); Box(b, "Tail blade", new Vector3(0, .5f, 0), new Vector3(.03f, 1.0f, .14f), Dark); }
            var blink = new List<Renderer>();
            blink.Add(Ball(root, "Beacon", new Vector3(0, 2.92f, -1.9f), new Vector3(.2f, .1f, .2f), Red, false).GetComponent<Renderer>());
            Ball(root, "Nav light left", new Vector3(-1.12f, 2.38f, -6.2f), Vector3.one * .12f, Red, false);
            Ball(root, "Nav light right", new Vector3(1.12f, 2.38f, -6.2f), Vector3.one * .12f, Green, false);
            blink.Add(Ball(root, "Tail strobe", new Vector3(0, 3.62f, -7.65f), Vector3.one * .1f, Strobe, false).GetComponent<Renderer>());
            Ball(root, "Search light", new Vector3(0, .72f, 1.9f), Vector3.one * .22f, Chrome);
            foreach (int s in new[] { -1, 1 }) Text(root, l.Name, new Vector3(s * .9f, 1.35f, -1.6f), s > 0 ? -90 : 90, new Vector2(2.4f, .3f), l == AirlineLivery.Rescue ? l.Primary : Color.white, 3);
            return new Helicopter { Root = root, Rotor = rotor, Tail = tail, Blink = blink.ToArray() };
        }

        /// <summary>Hides every renderer under <paramref name="t"/> except those inside <paramref name="keep"/>.</summary>
        public static void HideOld(Transform t, Transform keep)
        {
            foreach (var r in t.GetComponentsInChildren<Renderer>(true)) if (keep == null || !r.transform.IsChildOf(keep)) r.enabled = false;
            var lod = t.GetComponent<LODGroup>(); if (lod != null) lod.enabled = false;
        }
    }
}
