using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace RoadsideHaven
{
    /// <summary>
    /// Rebuilds the Haven Sports Park stadium as a modern enclosed arena: a continuous seating bowl
    /// round the track with crowds in the seats, upper tiers and glass hospitality boxes on the long
    /// sides, a white ring roof with floodlights under its edge (lit at night), LED screens, pitch-side
    /// LED boards, a striped hybrid-grass pitch with full FIFA markings and netted goals, and a finned
    /// façade with corner entrances. Adds a two-lane access road from the sports car park to a stadium
    /// plaza, and a fenced, floodlit artificial practice pitch with players training on it.
    /// </summary>
    public sealed class StadiumUpgrade : MonoBehaviour
    {
        // Layout (world space). The pitch, track and car park come from HavenExpansionLayout.
        public static Vector3 Centre => HavenExpansionLayout.PitchCentre;
        public const float Straight = HavenExpansionLayout.PitchHalfZ;         // half length of the straights
        public const float LowerFront = HavenExpansionLayout.TrackOuter + 4;  // first seat row
        public const int LowerRows = 20, UpperRows = 14;
        public const float RowDepth = .8f, RowRise = .45f, UpperFront = 58, UpperFirst = 13, RoofInner = 44, RoofOuter = 70, RoofLow = 30, RoofHigh = 33.5f;
        public const float Concourse = LowerFront + LowerRows * RowDepth + .6f, OuterWall = 69.1f;
        public static readonly Rect RoadNorthSouth = Rect.MinMaxRect(108, -266, 116, -150.3f);
        public static readonly Rect RoadEastWest = Rect.MinMaxRect(108, -266, 141, -258);
        public static readonly Rect Plaza = Rect.MinMaxRect(140, -308, 166, -192);
        public static readonly Rect PracticeFence = Rect.MinMaxRect(38, -286, 88, -214);
        public static readonly Rect PracticePitch = Rect.MinMaxRect(44, -280, 82, -220);
        public static readonly Rect PracticePath = Rect.MinMaxRect(88, -252, 98.5f, -248);

        public bool Done { get; private set; }
        public Transform Root { get; private set; }
        public Transform Stadium { get; private set; }
        public Transform Practice { get; private set; }
        public int StrippedTriangles { get; private set; }
        public IReadOnlyList<Light> Floodlights => floodlights;
        public IReadOnlyList<TownCitizenAgent> Players => players;

        private readonly List<Light> floodlights = new List<Light>();
        private readonly List<TownCitizenAgent> players = new List<TownCitizenAgent>();
        private readonly List<Object> owned = new List<Object>();
        private readonly List<(MeshFilter filter, Mesh original)> stripped = new List<(MeshFilter, Mesh)>();
        private readonly List<Behaviour> disabled = new List<Behaviour>();
        private readonly List<Collider> disabledColliders = new List<Collider>();
        private readonly List<Renderer> hidden = new List<Renderer>();
        private MotelAtmosphere atmosphere;
        private Material mediaMaterial, ribbonMaterial, concrete, seats, roof, grass, turf, lineWhite, net, fence, steel, glass, asphalt, paving, kerb, screen, ledWhite, ledBlue, ledWarm, cone, darkPanel;
        private readonly Material[] adColours = new Material[4];
        private float nextPlayerThink;
        private bool lit;

        private IEnumerator Start()
        {
            for (int i = 0; i < 6; i++) yield return null;
            var trees = GetComponent<TreeUpgrade>();
            float until = Time.realtimeSinceStartup + 10;
            while (trees != null && !trees.Done && Time.realtimeSinceStartup < until) yield return null;
            atmosphere = GetComponent<MotelAtmosphere>();
            var sports = transform.Find(HavenExpansionLayout.Sports);
            if (sports == null) { Done = true; yield break; }
            Root = new GameObject("Farhan Stadium and Practice Turf").transform;
            SceneManager.MoveGameObjectToScene(Root.gameObject, gameObject.scene);
            CreateMaterials();
            ClearOldStadium(sports);
            if (trees != null) trees.RemoveTreesIn(Rect.MinMaxRect(Centre.x - 72, Centre.z - 125, Centre.x + 72, Centre.z + 125));
            foreach (var area in new[] { RoadNorthSouth, RoadEastWest, Plaza, PracticeFence, PracticePath }) ClearArea(sports, area, trees);
            BuildArena();
            BuildRoadAndPlaza(sports);
            BuildPractice();
            var grassField = GetComponent<GrassField>();
            if (grassField != null) foreach (var area in new[] { RoadNorthSouth, RoadEastWest, Plaza, PracticeFence, PracticePath, Rect.MinMaxRect(Centre.x - 72, Centre.z - 125, Centre.x + 72, Centre.z + 125) }) grassField.Invalidate(area);
            if (WeatherSystem.Active != null) WeatherSystem.Active.CollectWetSurfaces();
            Done = true;
        }

        // ================================================================ old stadium

        /// <summary>Distance from the oval's centre line (the segment between the two arc centres).</summary>
        public static float OvalDistance(Vector3 world)
        {
            float x = world.x - Centre.x, z = world.z - Centre.z; float dz = Mathf.Max(0, Mathf.Abs(z) - Straight);
            return Mathf.Sqrt(x * x + dz * dz);
        }

        private static bool OldStandRegion(Vector3 p) { float d = OvalDistance(p); return d > HavenExpansionLayout.TrackOuter + .3f && d < 68; }
        private static bool OldGoalXZ(Vector3 p) => Mathf.Abs(p.x - Centre.x) < 5 && Mathf.Abs(Mathf.Abs(p.z - Centre.z) - Straight) < 3.2f;
        private static bool OldGoal(Vector3 p) => Mathf.Abs(p.x - Centre.x) < 5 && Mathf.Abs(Mathf.Abs(p.z - Centre.z) - Straight) < 3.2f && p.y > .12f;

        private void ClearOldStadium(Transform sports)
        {
            // Old grandstands, floodlight masts, scoreboard and goals live in per-material batches: cut them out.
            foreach (var r in sports.GetComponentsInChildren<MeshRenderer>())
            {
                if (!r.enabled) continue; var filter = r.GetComponent<MeshFilter>(); if (filter == null || filter.sharedMesh == null) continue;
                var b = r.bounds; if (OvalDistance(b.center) > 140 && b.size.x < 200) continue;
                if (!filter.sharedMesh.isReadable) { if (OldStandRegion(b.center) && b.size.x < 30 && b.size.y > .3f) Hide(r); continue; }
                Strip(r, filter, (a, c, d) => (OldStandRegion(a) && OldStandRegion(c) && OldStandRegion(d) || OldGoalXZ(a) && OldGoalXZ(c) && OldGoalXZ(d)) && Mathf.Max(a.y, Mathf.Max(c.y, d.y)) > .15f);
            }
            foreach (var t in sports.GetComponentsInChildren<TMP_Text>()) if (OldStandRegion(t.transform.position) && t.GetComponent<Renderer>() != null && t.GetComponent<Renderer>().enabled) Hide(t.GetComponent<Renderer>());
            foreach (var l in sports.GetComponentsInChildren<Light>()) if (l.enabled && OldStandRegion(l.transform.position)) { l.enabled = false; disabled.Add(l); }
            foreach (var c in sports.GetComponentsInChildren<Collider>())
            {
                if (!c.enabled) continue; var b = c.bounds;
                float dist = OvalDistance(b.center);
                if (b.size.y > .5f && (OldStandRegion(b.center) || OldGoal(b.center) || dist > 45.3f && dist < 72.5f && b.size.x < 5 && b.size.z < 5)) { c.enabled = false; disabledColliders.Add(c); }
            }
            foreach (var r in sports.GetComponentsInChildren<MeshRenderer>())
                if (r.enabled && r.GetComponent<MeshFilter>()?.sharedMesh != null && !r.GetComponent<MeshFilter>().sharedMesh.isReadable && (OldStandRegion(r.bounds.center) || OldGoal(r.bounds.center)) && r.bounds.size.y > .3f) Hide(r);
        }

        private void ClearArea(Transform sports, Rect area, TreeUpgrade trees)
        {
            if (trees != null) trees.RemoveTreesIn(area);
            var centre = new Vector3(area.center.x, 4, area.center.y);
            foreach (var c in Physics.OverlapBox(centre, new Vector3(area.width * .5f, 4.5f, area.height * .5f), Quaternion.identity, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                if (!c.enabled || c.transform.IsChildOf(Root) || c.attachedRigidbody != null || c is TerrainCollider) continue;
                var b = c.bounds; string n = c.name.ToLowerInvariant();
                if (GroundSurfaceStyle.SurfaceFor(c.name) >= 0 || n.Contains("ground") || n.Contains("foundation") || n.Contains("parking") || n.Contains("walk") || n.Contains("car park")) continue;
                if (b.size.y < .4f || b.size.x > 5 || b.size.z > 5) continue;
                c.enabled = false; disabledColliders.Add(c);
                foreach (var r in c.GetComponentsInChildren<Renderer>()) Hide(r);
            }
            Rect inside = area, around = Rect.MinMaxRect(area.xMin - 2.5f, area.yMin - 2.5f, area.xMax + 2.5f, area.yMax + 2.5f);
            foreach (var r in sports.GetComponentsInChildren<MeshRenderer>())
            {
                if (!r.enabled) continue; var filter = r.GetComponent<MeshFilter>(); if (filter == null || filter.sharedMesh == null || !filter.sharedMesh.isReadable) continue;
                var b = r.bounds; if (!Rect.MinMaxRect(b.min.x, b.min.z, b.max.x, b.max.z).Overlaps(area)) continue;
                string mat = r.sharedMaterial != null ? r.sharedMaterial.name.ToLowerInvariant() : "";
                bool paint = mat.Contains("line");
                // Painted lines fully inside; raised objects (old trees, shrubs) removed whole when any part reaches the area.
                if (paint) Strip(r, filter, (a, c, d) => In(inside, a) && In(inside, c) && In(inside, d));
                else if (!n2(r)) StripObjects(r, filter, ob => ob.max.y > .3f && ob.size.y > .35f && ob.size.x < 12 && ob.size.z < 12 && Rect.MinMaxRect(ob.min.x, ob.min.z, ob.max.x, ob.max.z).Overlaps(around));
            }
        }

        private static bool In(Rect r, Vector3 p) => r.Contains(new Vector2(p.x, p.z));
        /// <summary>Large buildings and the stadium itself are never trimmed by an area clearance.</summary>
        private static bool n2(MeshRenderer r) { var b = r.bounds; string n = r.name.ToLowerInvariant(); return n.Contains("clubhouse") || n.Contains("court") || b.size.y > 12; }

        private void Strip(MeshRenderer r, MeshFilter filter, System.Func<Vector3, Vector3, Vector3, bool> remove)
        {
            var source = filter.sharedMesh; var vertices = source.vertices; var m = filter.transform.localToWorldMatrix;
            for (int i = 0; i < vertices.Length; i++) vertices[i] = m.MultiplyPoint3x4(vertices[i]);
            Mesh copy = null; int kept = 0, removed = 0;
            for (int s = 0; s < source.subMeshCount; s++)
            {
                var tris = source.GetTriangles(s); var keep = new List<int>(tris.Length);
                for (int t = 0; t < tris.Length; t += 3)
                {
                    if (remove(vertices[tris[t]], vertices[tris[t + 1]], vertices[tris[t + 2]])) { removed++; continue; }
                    keep.Add(tris[t]); keep.Add(tris[t + 1]); keep.Add(tris[t + 2]);
                }
                if (removed > 0 && copy == null) { copy = Instantiate(source); copy.name = source.name + " (stadium rebuilt)"; for (int p = 0; p < s; p++) copy.SetTriangles(source.GetTriangles(p), p); }
                if (copy != null) copy.SetTriangles(keep, s);
                kept += keep.Count;
            }
            if (copy == null) return;
            StrippedTriangles += removed;
            if (kept == 0) { Destroy(copy); Hide(r); return; }
            stripped.Add((filter, source)); filter.sharedMesh = copy; owned.Add(copy);
        }

        /// <summary>Removes whole original objects from a merged batch: connected pieces whose bounds match.</summary>
        private void StripObjects(MeshRenderer r, MeshFilter filter, System.Func<Bounds, bool> remove)
        {
            var source = filter.sharedMesh; var vertices = source.vertices; var m = filter.transform.localToWorldMatrix;
            for (int i = 0; i < vertices.Length; i++) vertices[i] = m.MultiplyPoint3x4(vertices[i]);
            var parent = new int[vertices.Length]; for (int i = 0; i < parent.Length; i++) parent[i] = i;
            int Find(int x) { while (parent[x] != x) { parent[x] = parent[parent[x]]; x = parent[x]; } return x; }
            var all = new List<int[]>();
            for (int s = 0; s < source.subMeshCount; s++) { var tris = source.GetTriangles(s); all.Add(tris); for (int t = 0; t < tris.Length; t += 3) { int a = Find(tris[t]); parent[Find(tris[t + 1])] = a; parent[Find(tris[t + 2])] = a; } }
            var bounds = new Dictionary<int, Bounds>();
            foreach (var tris in all) foreach (int v in tris) { int root = Find(v); if (bounds.TryGetValue(root, out var b)) { b.Encapsulate(vertices[v]); bounds[root] = b; } else bounds[root] = new Bounds(vertices[v], Vector3.zero); }
            var doomed = new HashSet<int>(); foreach (var kv in bounds) if (remove(kv.Value)) doomed.Add(kv.Key);
            if (doomed.Count == 0) return;
            var copy = Instantiate(source); copy.name = source.name + " (stadium rebuilt)"; int kept = 0, removed = 0;
            for (int s = 0; s < all.Count; s++)
            {
                var tris = all[s]; var keep = new List<int>(tris.Length);
                for (int t = 0; t < tris.Length; t += 3) { if (doomed.Contains(Find(tris[t]))) { removed++; continue; } keep.Add(tris[t]); keep.Add(tris[t + 1]); keep.Add(tris[t + 2]); }
                copy.SetTriangles(keep, s); kept += keep.Count;
            }
            StrippedTriangles += removed;
            if (kept == 0) { Destroy(copy); Hide(r); return; }
            if (!stripped.Exists(e => e.filter == filter)) stripped.Add((filter, source)); filter.sharedMesh = copy; owned.Add(copy);
        }

        private readonly List<(MeshFilter filter, Mesh original)> emptied = new List<(MeshFilter, Mesh)>();
        private void Hide(Renderer r)
        {
            if (r == null) return;
            var f = r.GetComponent<MeshFilter>(); if (f != null && f.sharedMesh != null && f.sharedMesh != TreeUpgrade.HiddenMesh) { emptied.Add((f, f.sharedMesh)); f.sharedMesh = TreeUpgrade.HiddenMesh; }
            if (r.enabled) { r.enabled = false; hidden.Add(r); }
        }

        // ================================================================ materials & textures

        private Material Surface(string name, Texture2D tex, float cutoff = 0)
        {
            var shader = Resources.Load<Shader>("FoliageLit"); Material m;
            if (shader != null && shader.isSupported)
            {
                m = new Material(shader) { name = name }; m.SetFloat("_Leaf", 0); m.SetFloat("_Seasonal", 0); m.SetFloat("_WindScale", 0); m.SetFloat("_Translucency", 0);
            }
            else { m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name }; m.SetFloat("_Cull", 0); if (cutoff > 0) { m.SetFloat("_AlphaClip", 1); m.EnableKeyword("_ALPHATEST_ON"); } }
            m.SetTexture("_BaseMap", tex); m.SetFloat("_Cutoff", cutoff); m.enableInstancing = true;
            if (cutoff > 0) m.renderQueue = (int)RenderQueue.AlphaTest;
            owned.Add(m); owned.Add(tex); return m;
        }

        private Material Lit(string name, Color color, float metallic, float smoothness, Color emission = default)
        {
            var m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name, color = color };
            m.SetFloat("_Metallic", metallic); m.SetFloat("_Smoothness", smoothness); m.SetFloat("_Cull", 0); m.enableInstancing = true;
            if (emission.maxColorComponent > 0) { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", emission); m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None; }
            owned.Add(m); return m;
        }

        private static Texture2D Tex(string name, int w, int h, System.Func<int, int, Color> paint, bool alpha = false, bool clamp = false)
        {
            var t = new Texture2D(w, h, alpha ? TextureFormat.RGBA32 : TextureFormat.RGB24, true) { name = name, wrapMode = clamp ? TextureWrapMode.Clamp : TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear, anisoLevel = 8 };
            var px = new Color[w * h]; for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) px[y * w + x] = paint(x, y);
            t.SetPixels(px); t.Apply(true, true); return t;
        }

        private static float Noise(float x, float y, float s, float o) => Mathf.PerlinNoise(x * s + o, y * s + o * .7f);
        private static float TileNoise(int x, int y, int w, int h, float s, float o)
        {
            return (Noise(x, y, s, o) * (w - x) * (h - y) + Noise(x - w, y, s, o) * x * (h - y) + Noise(x, y - h, s, o) * (w - x) * y + Noise(x - w, y - h, s, o) * x * y) / (w * h);
        }

        private void CreateMaterials()
        {
            concrete = Surface("Arena precast concrete", Tex("Concrete", 256, 256, (x, y) => { float n = TileNoise(x, y, 256, 256, .05f, 3) * .6f + TileNoise(x, y, 256, 256, .25f, 9) * .4f; return Color.Lerp(new Color(.62f, .62f, .6f), new Color(.8f, .8f, .78f), n); }));
            roof = Surface("Arena roof membrane", Tex("Membrane", 256, 256, (x, y) => { float seam = (x % 64 < 2) ? .86f : 1; float n = TileNoise(x, y, 256, 256, .04f, 5) * .06f; return new Color(.95f, .96f, .97f) * (seam - n); }));
            var seatPalette = new[] { new Color(.1f, .25f, .7f), new Color(.85f, .85f, .88f), new Color(.75f, .12f, .12f), new Color(.95f, .78f, .15f), new Color(.1f, .1f, .12f), new Color(.15f, .55f, .3f) };
            var rnd = new System.Random(4);
            var seatPixels = new Color[512 * 256];
            for (int i = 0; i < seatPixels.Length; i++) seatPixels[i] = new Color(.5f, .5f, .5f, 0);
            for (int s = 0; s < 8; s++)
            {
                int x0 = s * 64;
                // Lower half: empty folding seats (tinted per section by vertex colour).
                for (int y = 6; y < 110; y++) for (int x = x0 + 8; x < x0 + 56; x++) { bool back = y > 34; bool round = !(back && (x < x0 + 12 || x > x0 + 52) && y > 100); if (round) seatPixels[y * 512 + x] = new Color(.92f, .92f, .92f, 1) * (back ? 1 : .8f); }
                // Upper half: fans in the seats.
                for (int k = 0; k < 1; k++)
                {
                    bool occupied = rnd.NextDouble() < .82;
                    if (!occupied) { for (int y = 134; y < 238; y++) for (int x = x0 + 8; x < x0 + 56; x++) seatPixels[y * 512 + x] = new Color(.2f, .32f, .75f, 1); continue; }
                    Color shirt = rnd.NextDouble() < .55 ? new Color(.12f, .3f, .82f) : seatPalette[rnd.Next(seatPalette.Length)];
                    Color skin = Color.Lerp(new Color(.38f, .24f, .16f), new Color(.95f, .78f, .62f), (float)rnd.NextDouble());
                    Color hair = Color.Lerp(new Color(.05f, .04f, .03f), new Color(.55f, .4f, .2f), (float)rnd.NextDouble() * .6f);
                    int cx = x0 + 32 + rnd.Next(-5, 6), shoulders = 150 + rnd.Next(30), headR = 13 + rnd.Next(4);
                    bool armsUp = rnd.NextDouble() < .2;
                    for (int y = 128; y < 256; y++) for (int x = x0 + 2; x < x0 + 62; x++)
                    {
                        float dx = x - cx;
                        bool torso = y < shoulders + 10 && Mathf.Abs(dx) < 22 - Mathf.Max(0, y - shoulders) * .8f;
                        bool head = (dx * dx + (y - shoulders - 22) * (y - shoulders - 22)) < headR * headR;
                        bool hairCap = head && y > shoulders + 22 + headR * .2f;
                        bool arm = armsUp && Mathf.Abs(Mathf.Abs(dx) - 20) < 4 && y > shoulders && y < shoulders + 60;
                        if (torso) seatPixels[y * 512 + x] = shirt * (dx > 0 ? .9f : 1);
                        if (arm) seatPixels[y * 512 + x] = skin * .95f;
                        if (head) seatPixels[y * 512 + x] = hairCap ? hair : skin;
                    }
                }
            }
            var seatTex = new Texture2D(512, 256, TextureFormat.RGBA32, true) { name = "Stadium seats", wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear, anisoLevel = 8 };
            seatTex.SetPixels(seatPixels); seatTex.Apply(true, true);
            seats = Surface("Arena seats and crowd", seatTex, .5f);
            grass = Surface("Arena hybrid grass", Tex("Pitch grass", 256, 256, (x, y) => { float n = TileNoise(x, y, 256, 256, .09f, 2) * .5f + TileNoise(x, y, 256, 256, .5f, 7) * .5f; float blade = Mathf.Abs(Mathf.Sin((x * .9f + Noise(x, y, .2f, 1) * 9) * 1.7f)); return Color.Lerp(new Color(.17f, .42f, .12f), new Color(.3f, .6f, .19f), n * .7f + blade * .3f); }));
            turf = Surface("Practice synthetic pitch", Tex("Synthetic", 128, 128, (x, y) => { float n = TileNoise(x, y, 128, 128, .6f, 4); float fibre = ((x + y * 3) % 5 == 0) ? .9f : 1; return Color.Lerp(new Color(.2f, .52f, .18f), new Color(.27f, .63f, .22f), n) * fibre; }));
            lineWhite = Surface("Pitch line paint", Tex("Line", 8, 8, (x, y) => new Color(.96f, .97f, .95f)));
            net = Surface("Goal net", Tex("Net", 64, 64, (x, y) => (x % 16 < 2 || y % 16 < 2) ? new Color(.97f, .97f, .97f, 1) : new Color(1, 1, 1, 0), true), .5f);
            fence = Surface("Chain-link fence", Tex("Chain link", 64, 64, (x, y) => { int a = (x + y) % 32, b = (x - y + 64) % 32; return (a < 2 || b < 2) ? new Color(.55f, .58f, .6f, 1) : new Color(.5f, .5f, .5f, 0); }, true), .5f);
            asphalt = Surface("Stadium road asphalt", Tex("Asphalt", 256, 256, (x, y) => { float n = TileNoise(x, y, 256, 256, .08f, 11) * .5f + TileNoise(x, y, 256, 256, .7f, 13) * .5f; float speck = Noise(x, y, 1.9f, 17) > .78f ? 1.25f : 1; return Color.Lerp(new Color(.15f, .155f, .16f), new Color(.27f, .27f, .28f), n) * speck; }));
            paving = Surface("Stadium plaza paving", Tex("Plaza stone", 256, 256, (x, y) => { bool joint = x % 64 < 2 || y % 32 < 2; float n = TileNoise(x, y, 256, 256, .1f, 21); return joint ? new Color(.45f, .44f, .42f) : Color.Lerp(new Color(.68f, .66f, .62f), new Color(.8f, .78f, .74f), n); }));
            kerb = Lit("Granite kerb", new Color(.62f, .62f, .6f), 0, .3f);
            steel = Lit("Arena steel", new Color(.72f, .74f, .76f), .85f, .6f);
            glass = Lit("Hospitality glass", new Color(.08f, .14f, .18f), .3f, .95f, new Color(.08f, .07f, .05f));
            screen = Lit("LED screen", new Color(.02f, .02f, .03f), 0, .9f);
            darkPanel = Lit("Screen housing", new Color(.08f, .09f, .1f), .5f, .5f);
            ledWhite = Lit("Floodlight LEDs", new Color(.95f, .95f, .9f), 0, .5f, new Color(2.2f, 2.2f, 2f));
            ledBlue = Lit("Facade LED band", new Color(.1f, .3f, .9f), 0, .5f, new Color(.25f, .7f, 2.4f));
            ledWarm = Lit("Lamp head", new Color(1, .95f, .8f), 0, .5f, new Color(2.4f, 2.1f, 1.5f));
            cone = Lit("Training cone", new Color(1, .42f, .08f), 0, .4f);
            var mediaTex = Tex("Media facade", 256, 64, (x, y) =>
            {
                float t = x / 256f; Color band = Color.Lerp(Color.Lerp(new Color(.1f, .35f, 1f), new Color(.1f, .9f, 1f), Mathf.PingPong(t * 3, 1)), new Color(.9f, .2f, .8f), Mathf.Clamp01(Mathf.Sin(t * Mathf.PI * 2 + y * .05f)) * .6f);
                bool dot = x % 4 == 0 || y % 4 == 0; return dot ? band * .25f : band;
            });
            mediaMaterial = Lit("LED media facade", Color.white, 0, .4f, Color.white * 1.8f); mediaMaterial.SetTexture("_BaseMap", mediaTex); mediaMaterial.SetTexture("_EmissionMap", mediaTex); owned.Add(mediaTex);
            var ribbonTex = Tex("LED ribbon", 512, 32, (x, y) =>
            {
                int block = x / 128; Color[] palette = { new Color(.1f, .35f, 1f), new Color(1f, .75f, .1f), new Color(.9f, .15f, .2f), new Color(.1f, .8f, .4f) };
                int lx = x % 128; bool letter = y > 9 && y < 23 && lx > 14 && lx < 114 && (lx / 7) % 2 == 0 && ((lx * 7 + y * 3 + block) % 5 != 0);
                return letter ? Color.white : palette[block] * (x % 3 == 0 || y % 3 == 0 ? .6f : 1);
            });
            ribbonMaterial = Lit("LED ribbon board", Color.white, 0, .4f, Color.white * 1.6f); ribbonMaterial.SetTexture("_BaseMap", ribbonTex); ribbonMaterial.SetTexture("_EmissionMap", ribbonTex); owned.Add(ribbonTex);
            Color[] ads = { new Color(.1f, .35f, 1.6f), new Color(1.6f, .2f, .15f), new Color(.15f, 1.2f, .45f), new Color(1.6f, 1.1f, .1f) };
            for (int i = 0; i < 4; i++) adColours[i] = Lit("LED board " + i, ads[i] * .4f, 0, .6f, ads[i]);
        }

        // ================================================================ mesh building

        private sealed class Builder
        {
            public readonly List<Vector3> V = new List<Vector3>(), N = new List<Vector3>(); public readonly List<Vector2> UV = new List<Vector2>(); public readonly List<Color> C = new List<Color>(); public readonly List<int> T = new List<int>();
            public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color col, Vector2 ua, Vector2 ub, Vector2 uc, Vector2 ud)
            {
                int s = V.Count; var n = Vector3.Cross(b - a, d - a); n = n.sqrMagnitude > 1e-8f ? n.normalized : Vector3.up;
                V.Add(a); V.Add(b); V.Add(c); V.Add(d); for (int i = 0; i < 4; i++) { N.Add(n); C.Add(col); }
                UV.Add(ua); UV.Add(ub); UV.Add(uc); UV.Add(ud); T.Add(s); T.Add(s + 1); T.Add(s + 2); T.Add(s); T.Add(s + 2); T.Add(s + 3);
            }
            /// <summary>Quad with UVs in metres (planar on its own axes).</summary>
            public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color col, float uvScale = .5f)
            {
                float w = Vector3.Distance(a, b), h = Vector3.Distance(a, d);
                Quad(a, b, c, d, col, Vector2.zero, new Vector2(w * uvScale, 0), new Vector2(w * uvScale, h * uvScale), new Vector2(0, h * uvScale));
            }
            public void Box(Vector3 centre, Vector3 size, Quaternion rot, Color col, float uvScale = .5f)
            {
                Vector3 P(int i, int j, int k) => centre + rot * new Vector3((i - .5f) * size.x, (j - .5f) * size.y, (k - .5f) * size.z);
                Quad(P(1, 0, 0), P(1, 1, 0), P(1, 1, 1), P(1, 0, 1), col, uvScale); Quad(P(0, 0, 0), P(0, 0, 1), P(0, 1, 1), P(0, 1, 0), col, uvScale);
                Quad(P(0, 1, 0), P(0, 1, 1), P(1, 1, 1), P(1, 1, 0), col, uvScale); Quad(P(0, 0, 0), P(1, 0, 0), P(1, 0, 1), P(0, 0, 1), col, uvScale);
                Quad(P(0, 0, 1), P(1, 0, 1), P(1, 1, 1), P(0, 1, 1), col, uvScale); Quad(P(0, 0, 0), P(0, 1, 0), P(1, 1, 0), P(1, 0, 0), col, uvScale);
            }
            public void Cylinder(Vector3 a, Vector3 b, float r, Color col, int sides = 10)
            {
                Vector3 axis = (b - a).normalized, side = Vector3.Cross(axis, Mathf.Abs(axis.y) > .9f ? Vector3.right : Vector3.up).normalized, other = Vector3.Cross(axis, side);
                for (int i = 0; i < sides; i++)
                {
                    float a0 = i * Mathf.PI * 2 / sides, a1 = (i + 1) * Mathf.PI * 2 / sides;
                    Vector3 d0 = side * Mathf.Cos(a0) + other * Mathf.Sin(a0), d1 = side * Mathf.Cos(a1) + other * Mathf.Sin(a1);
                    int s = V.Count; V.Add(a + d0 * r); V.Add(a + d1 * r); V.Add(b + d1 * r); V.Add(b + d0 * r);
                    N.Add(d0); N.Add(d1); N.Add(d1); N.Add(d0); for (int k = 0; k < 4; k++) C.Add(col);
                    UV.Add(new Vector2(0, 0)); UV.Add(new Vector2(1, 0)); UV.Add(new Vector2(1, 1)); UV.Add(new Vector2(0, 1));
                    T.Add(s); T.Add(s + 3); T.Add(s + 2); T.Add(s); T.Add(s + 2); T.Add(s + 1);
                }
            }
            public Mesh Build(string name)
            {
                var m = new Mesh { name = name, indexFormat = V.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
                m.SetVertices(V); m.SetNormals(N); m.SetUVs(0, UV); m.SetColors(C); m.SetTriangles(T, 0); m.RecalculateBounds(); return m;
            }
        }

        private GameObject Part(Transform parent, string name, Builder b, Material mat, bool collider = false, ShadowCastingMode shadows = ShadowCastingMode.On)
        {
            var go = new GameObject(name); go.transform.SetParent(parent, false);
            var mesh = b.Build(name); owned.Add(mesh);
            go.AddComponent<MeshFilter>().sharedMesh = mesh; var r = go.AddComponent<MeshRenderer>(); r.sharedMaterial = mat; r.shadowCastingMode = shadows;
            if (collider) go.AddComponent<MeshCollider>().sharedMesh = mesh;
            return go;
        }

        private TextMeshPro Text(Transform parent, string text, Vector3 position, float yaw, float size, Color color, float width)
        {
            var t = new GameObject(text).AddComponent<TextMeshPro>(); t.transform.SetParent(parent, false);
            t.transform.SetPositionAndRotation(position, Quaternion.Euler(0, yaw, 0));
            if (TMP_Settings.defaultFontAsset != null) t.font = TMP_Settings.defaultFontAsset;
            t.text = text; t.color = color; t.alignment = TextAlignmentOptions.Center; t.textWrappingMode = TextWrappingModes.NoWrap;
            t.enableAutoSizing = true; t.fontSizeMin = .5f; t.fontSizeMax = size; t.fontSize = size;
            t.rectTransform.sizeDelta = new Vector2(width, size * .14f); t.fontStyle = FontStyles.Bold;
            return t;
        }

        // ================================================================ arena

        /// <summary>Samples round the oval at a given distance: position, outward normal, distance along.</summary>
        private static List<(Vector3 p, Vector3 n, float s)> Ring(float d, float straightStep = 3f, int arcSteps = 36)
        {
            var list = new List<(Vector3, Vector3, float)>(); var c = Centre; float s = 0; Vector3 last = Vector3.zero; bool first = true;
            void Add(Vector3 p, Vector3 n) { if (!first) s += Vector3.Distance(last, p); list.Add((p, n, s)); last = p; first = false; }
            for (int i = 0; i <= arcSteps; i++) { float a = Mathf.PI * i / arcSteps; var n = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)); Add(c + Vector3.forward * Straight + n * d, n); }
            for (float z = Straight - straightStep; z > -Straight + .01f; z -= straightStep) Add(c + new Vector3(-d, 0, z), Vector3.left);
            for (int i = 0; i <= arcSteps; i++) { float a = Mathf.PI + Mathf.PI * i / arcSteps; var n = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)); Add(c - Vector3.forward * Straight + n * d, n); }
            for (float z = -Straight + straightStep; z < Straight - .01f; z += straightStep) Add(c + new Vector3(d, 0, z), Vector3.right);
            Add(list[0].Item1, list[0].Item2);
            return list;
        }

        /// <summary>The four corner entrances, where the straights meet the curved ends.</summary>
        public static bool InGate(Vector3 p) => Mathf.Abs(Mathf.Abs(p.z - Centre.z) - Straight) < 3.4f;
        private static bool OnStraight(Vector3 p) => Mathf.Abs(p.z - Centre.z) <= Straight + .01f;

        // ----- public hooks for the match-day director
        public MeshRenderer SeatRenderer { get; private set; }
        public bool FullHouse { get; private set; }
        public IReadOnlyList<MeshRenderer> ScreenPanels => screenPanels;
        public IReadOnlyList<TextMeshPro> ScreenHeadlines => screenHeadlines;
        public IReadOnlyList<TextMeshPro> ScreenDetails => screenDetails;
        public IReadOnlyList<Vector3> GatePoints => gatePoints;
        public static readonly Vector3[] Kiosks = { new Vector3(149.6f, 0, -232), new Vector3(149.6f, 0, -268) };
        public Material ScreenIdleMaterial => screen;
        private readonly List<MeshRenderer> screenPanels = new List<MeshRenderer>();
        private readonly List<TextMeshPro> screenHeadlines = new List<TextMeshPro>(), screenDetails = new List<TextMeshPro>();
        private readonly List<Vector3> gatePoints = new List<Vector3>();
        private readonly List<(BoxCollider barrier, Transform left, Transform right, float openUntil)> gates = new List<(BoxCollider, Transform, Transform, float)>();
        private Mesh crowdSeats, quietSeats;
        private MeshRenderer media, ribbon;

        /// <summary>Fills the seats with fans (match days) or shows mostly empty seats.</summary>
        public void SetCrowd(bool full)
        {
            if (SeatRenderer == null || FullHouse == full) return;
            FullHouse = full; SeatRenderer.GetComponent<MeshFilter>().sharedMesh = full ? crowdSeats : quietSeats;
        }
        /// <summary>Tints the LED media façade (club colours, goal flashes).</summary>
        public void SetMediaMood(Color emission) { if (mediaMaterial != null) mediaMaterial.SetColor("_EmissionColor", emission); }
        /// <summary>Slides a gate open for a while (ticket scanned); it closes again by itself.</summary>
        public void OpenGate(int index, float seconds)
        {
            if (index < 0 || index >= gates.Count) return; var g = gates[index]; g.openUntil = Time.time + seconds; gates[index] = g;
        }
        public bool GateOpen(int index) => index >= 0 && index < gates.Count && !gates[index].barrier.enabled;
        public int NearestGate(Vector3 p, float range)
        {
            int best = -1; float bestD = range * range;
            for (int i = 0; i < gatePoints.Count; i++) { float d = (new Vector2(gatePoints[i].x - p.x, gatePoints[i].z - p.z)).sqrMagnitude; if (d < bestD) { bestD = d; best = i; } }
            return best;
        }

        private void BuildArena()
        {
            Stadium = new GameObject("Farhan Stadium").transform; Stadium.SetParent(Root, false);
            var structure = new Builder(); var seatsFull = new Builder(); var seatsQuiet = new Builder(); var roofMesh = new Builder(); var steelMesh = new Builder(); var glassMesh = new Builder();
            var leds = new Builder(); var halo = new Builder(); var mediaMesh = new Builder(); var ribbonMesh = new Builder();
            Color white = Color.white, shade = new Color(.82f, .82f, .82f), dark = new Color(.55f, .55f, .56f);
            void SeatStrip(Vector3 a, Vector3 b, float h, float s0, float s1, int section, int row, bool end)
            {
                // Match day: a sell-out crowd; other days: empty seats in club colours with the odd visitor.
                bool crowdFull = (section * 7 + row / 5) % 11 != 0, crowdQuiet = (section * 13 + row * 3) % 37 == 0;
                Color club = end ? new Color(.85f, .15f, .15f) : new Color(.15f, .35f, .85f);
                foreach (var (b2, crowd) in new[] { (seatsFull, crowdFull), (seatsQuiet, crowdQuiet) })
                {
                    float v0 = crowd ? .5f : 0, v1 = crowd ? 1 : .5f; Color tint = crowd ? Color.white : club;
                    b2.Quad(a + Vector3.up * h, b + Vector3.up * h, b + Vector3.up * (h + .55f), a + Vector3.up * (h + .55f), tint, new Vector2(s0 / 4, v0), new Vector2(s1 / 4, v0), new Vector2(s1 / 4, v1), new Vector2(s0 / 4, v1));
                }
            }
            void Tier(float front, int rows, float depth, float rise, float firstHeight, int rowOffset)
            {
                for (int r = 0; r < rows; r++)
                {
                    float d0 = front + r * depth, d1 = d0 + depth, h = firstHeight + r * rise, below = r == 0 ? firstHeight - 1.2f : h - rise;
                    var fr = Ring(d0); var bk = Ring(d1);
                    for (int i = 0; i < fr.Count - 1; i++)
                    {
                        if (InGate(fr[i].p) || InGate(fr[i + 1].p)) continue;
                        Vector3 a = fr[i].p, b = fr[i + 1].p, a2 = bk[i].p, b2 = bk[i + 1].p;
                        structure.Quad(a + Vector3.up * h, a2 + Vector3.up * h, b2 + Vector3.up * h, b + Vector3.up * h, shade);
                        structure.Quad(a + Vector3.up * below, a + Vector3.up * h, b + Vector3.up * h, b + Vector3.up * below, white);
                        SeatStrip(Vector3.Lerp(a, a2, .45f), Vector3.Lerp(b, b2, .45f), h, fr[i].s, fr[i + 1].s, Mathf.FloorToInt(fr[i].s / 20f), r + rowOffset, Mathf.Abs(a.z - Centre.z) > Straight);
                    }
                }
            }
            // --- Lower tier all the way round, steeper and deeper.
            Tier(LowerFront, LowerRows, RowDepth, RowRise, 1.2f, 0);
            float lowerTop = 1.2f + (LowerRows - 1) * RowRise;
            var pw = Ring(LowerFront - .05f);
            for (int i = 0; i < pw.Count - 1; i++) { if (InGate(pw[i].p) || InGate(pw[i + 1].p)) continue; structure.Quad(pw[i + 1].p, pw[i + 1].p + Vector3.up * 1.2f, pw[i].p + Vector3.up * 1.2f, pw[i].p, dark); }
            // Concourse and the glass hospitality ring between the tiers.
            var cIn = Ring(LowerFront + LowerRows * RowDepth); var cOut = Ring(Concourse);
            for (int i = 0; i < cIn.Count - 1; i++)
            {
                if (InGate(cIn[i].p) || InGate(cIn[i + 1].p)) continue;
                structure.Quad(cIn[i].p + Vector3.up * lowerTop, cOut[i].p + Vector3.up * lowerTop, cOut[i + 1].p + Vector3.up * lowerTop, cIn[i + 1].p + Vector3.up * lowerTop, shade);
                glassMesh.Quad(cOut[i + 1].p + Vector3.up * lowerTop, cOut[i + 1].p + Vector3.up * (UpperFirst - .55f), cOut[i].p + Vector3.up * (UpperFirst - .55f), cOut[i].p + Vector3.up * lowerTop, white);
                if (i % 3 == 0) steelMesh.Box(cOut[i].p + Vector3.up * (lowerTop + UpperFirst - .55f) * .5f, new Vector3(.12f, UpperFirst - .55f - lowerTop, .12f), Quaternion.identity, white);
            }
            // --- Upper tier, also all the way round, cantilevered over the back of the lower tier.
            Tier(UpperFront, UpperRows, .78f, .58f, UpperFirst, LowerRows);
            float upperTop = UpperFirst + (UpperRows - 1) * .58f;
            var uf = Ring(UpperFront - .05f); var ub = Ring(OuterWall); var utop = Ring(UpperFront + UpperRows * .78f);
            for (int i = 0; i < uf.Count - 1; i++)
            {
                if (InGate(uf[i].p) || InGate(uf[i + 1].p)) continue;
                // LED ribbon board along the front of the upper tier.
                ribbonMesh.Quad(uf[i + 1].p + Vector3.up * (UpperFirst - 1.25f), uf[i + 1].p + Vector3.up * (UpperFirst - .2f), uf[i].p + Vector3.up * (UpperFirst - .2f), uf[i].p + Vector3.up * (UpperFirst - 1.25f), white, new Vector2(uf[i + 1].s / 6, 0), new Vector2(uf[i + 1].s / 6, 1), new Vector2(uf[i].s / 6, 1), new Vector2(uf[i].s / 6, 0));
                structure.Quad(uf[i].p + Vector3.up * (UpperFirst - 1.3f), uf[i + 1].p + Vector3.up * (UpperFirst - 1.3f), ub[i + 1].p + Vector3.up * (upperTop - .6f), ub[i].p + Vector3.up * (upperTop - .6f), dark);   // soffit
                structure.Quad(ub[i].p + Vector3.up * upperTop, ub[i + 1].p + Vector3.up * upperTop, utop[i + 1].p + Vector3.up * upperTop, utop[i].p + Vector3.up * upperTop, shade);
                // Outer wall with the LED media façade.
                structure.Quad(ub[i + 1].p, ub[i + 1].p + Vector3.up * (upperTop + 1.2f), ub[i].p + Vector3.up * (upperTop + 1.2f), ub[i].p, white);
                Vector3 o0 = ub[i].p + ub[i].n * .06f, o1 = ub[i + 1].p + ub[i + 1].n * .06f;
                mediaMesh.Quad(o1 + Vector3.up * 13.5f, o1 + Vector3.up * 19.5f, o0 + Vector3.up * 19.5f, o0 + Vector3.up * 13.5f, white, new Vector2(ub[i + 1].s / 24, 0), new Vector2(ub[i + 1].s / 24, 1), new Vector2(ub[i].s / 24, 1), new Vector2(ub[i].s / 24, 0));
            }
            // --- Ring roof, 34 m high, floodlights under the leading edge and an LED halo round the rim.
            var rin = Ring(RoofInner, 4, 40); var rout = Ring(RoofOuter, 4, 40);
            for (int i = 0; i < rin.Count - 1; i++)
            {
                Vector3 a = rin[i].p + Vector3.up * RoofLow, b = rin[i + 1].p + Vector3.up * RoofLow, a2 = rout[i].p + Vector3.up * RoofHigh, b2 = rout[i + 1].p + Vector3.up * RoofHigh;
                roofMesh.Quad(a, a2, b2, b, white, new Vector2(rin[i].s / 4, 0), new Vector2(rin[i].s / 4, 6), new Vector2(rin[i + 1].s / 4, 6), new Vector2(rin[i + 1].s / 4, 0));
                roofMesh.Quad(a + Vector3.down * 1.4f, b + Vector3.down * 1.4f, b, a, shade);
                roofMesh.Quad(a2, a2 + Vector3.down * 1.8f, b2 + Vector3.down * 1.8f, b2, shade);
                roofMesh.Quad(a + Vector3.down * 1.4f, a2 + Vector3.down * 1.8f, b2 + Vector3.down * 1.8f, b + Vector3.down * 1.4f, new Color(.7f, .72f, .75f));
                halo.Quad(a2 + rout[i].n * .05f + Vector3.down * 1.3f, a2 + rout[i].n * .05f + Vector3.down * .5f, b2 + rout[i + 1].n * .05f + Vector3.down * .5f, b2 + rout[i + 1].n * .05f + Vector3.down * 1.3f, white);
                halo.Quad(a + Vector3.down * .9f - rin[i].n * .05f, b + Vector3.down * .9f - rin[i + 1].n * .05f, b + Vector3.down * .5f - rin[i + 1].n * .05f, a + Vector3.down * .5f - rin[i].n * .05f, white);
                steelMesh.Box((a + b) * .5f + Vector3.down * 1.8f - rin[i].n * .3f, new Vector3(.55f, .55f, Vector3.Distance(a, b) + .05f), Quaternion.LookRotation(b - a), white);
                if (i % 2 == 0) leds.Box(a + Vector3.down * 2.3f - rin[i].n * .5f, new Vector3(1.8f, .8f, .25f), Quaternion.LookRotation(-rin[i].n) * Quaternion.Euler(30, 0, 0), white);
            }
            for (int i = 0; i < rout.Count - 1; i += 3)
            {
                if (InGate(rout[i].p)) continue;
                var p = rout[i].p; steelMesh.Cylinder(p + rout[i].n * .2f, p + rout[i].n * .2f + Vector3.up * (RoofHigh - .2f), .55f, white);
                steelMesh.Cylinder(p + rout[i].n * .2f + Vector3.up * 12, p - rout[i].n * 7 + Vector3.up * (RoofHigh - .7f), .25f, white);
            }
            var fins = Ring(RoofOuter + .9f, 3, 60);
            for (int i = 0; i < fins.Count - 1; i++)
            {
                if (InGate(fins[i].p)) continue;
                steelMesh.Box(fins[i].p + Vector3.up * RoofHigh * .5f, new Vector3(.18f, RoofHigh, 1.1f), Quaternion.LookRotation(Vector3.Cross(Vector3.up, fins[i].n)), white);
            }
            Part(Stadium, "Stadium seating bowl", structure, concrete, true);
            var seatsObj = Part(Stadium, "Stadium seats and fans", seatsQuiet, seats);
            crowdSeats = seatsFull.Build("Stadium seats - match day"); owned.Add(crowdSeats); quietSeats = seatsObj.GetComponent<MeshFilter>().sharedMesh;
            SeatRenderer = seatsObj.GetComponent<MeshRenderer>();
            Part(Stadium, "Stadium ring roof", roofMesh, roof);
            Part(Stadium, "Stadium steelwork", steelMesh, steel);
            Part(Stadium, "Stadium hospitality glazing", glassMesh, glass);
            Part(Stadium, "Stadium floodlight units", leds, ledWhite, false, ShadowCastingMode.Off);
            Part(Stadium, "Stadium roof halo", halo, ledBlue, false, ShadowCastingMode.Off);
            media = Part(Stadium, "Stadium media facade", mediaMesh, mediaMaterial, false, ShadowCastingMode.Off).GetComponent<MeshRenderer>();
            ribbon = Part(Stadium, "Stadium LED ribbon", ribbonMesh, ribbonMaterial, false, ShadowCastingMode.Off).GetComponent<MeshRenderer>();
            // Name on all four sides, in front of the fins.
            foreach (var (dir, yaw) in new[] { (Vector3.left, 90f), (Vector3.right, -90f) })
                OneSided(Text(Stadium, "FARHAN STADIUM", Centre + dir * (RoofOuter + 1.7f) + Vector3.up * 26.5f, yaw, 80, new Color(.35f, .7f, 1f), 90));
            foreach (var (dir, yaw) in new[] { (Vector3.forward, 180f), (Vector3.back, 0f) })
                OneSided(Text(Stadium, "FARHAN STADIUM", Centre + dir * (Straight + RoofOuter + 1.7f) + Vector3.up * 26.5f, yaw, 70, new Color(.35f, .7f, 1f), 70));
            // Floodlights for evening and night matches.
            foreach (var (x, z) in new[] { (-1f, -.6f), (-1f, 0f), (-1f, .6f), (1f, -.6f), (1f, 0f), (1f, .6f), (0f, 1f), (0f, -1f) })
            {
                var pos = x != 0 ? Centre + new Vector3(x * (RoofInner - 1), RoofLow - 2, z * Straight) : Centre + new Vector3(0, RoofLow - 2, z * (Straight + RoofInner - 1));
                var go = new GameObject("Stadium floodlight"); go.transform.SetParent(Stadium, false); go.transform.position = pos; go.transform.LookAt(Centre + new Vector3(x * 12, 0, z * 18));
                var l = go.AddComponent<Light>(); l.type = LightType.Spot; l.range = 190; l.spotAngle = 80; l.innerSpotAngle = 50; l.intensity = 8; l.color = new Color(1, .97f, .92f); l.shadows = LightShadows.None; l.enabled = false;
                floodlights.Add(l);
            }
            BuildGates();
            BuildPitch();
            BuildScreens();
        }

        /// <summary>Façade lettering reads only from outside; from the stands it would show mirrored.</summary>
        private void OneSided(TextMeshPro t) { var m = t.fontMaterial; if (m.HasProperty("_CullMode")) m.SetFloat("_CullMode", (float)CullMode.Back); owned.Add(m); }

        private void BuildGates()
        {
            // A sliding glass gate in each corner entrance: closed unless a ticket is scanned.
            foreach (int sx in new[] { -1, 1 }) foreach (int sz in new[] { -1, 1 })
            {
                var p = Centre + new Vector3(sx * (OuterWall + .2f), 0, sz * Straight); gatePoints.Add(p);
                var root = new GameObject("Stadium gate " + (char)('A' + gatePoints.Count - 1)); root.transform.SetParent(Stadium, false); root.transform.position = p;
                var box = root.AddComponent<BoxCollider>(); box.size = new Vector3(.4f, 3.4f, 6.9f); box.center = Vector3.up * 1.7f;
                Transform Panel(float side)
                {
                    var b = new Builder(); b.Box(Vector3.zero, new Vector3(.08f, 3, 3.4f), Quaternion.identity, Color.white);
                    var go = Part(root.transform, "Gate panel", b, glass); go.transform.localPosition = new Vector3(0, 1.6f, side * 1.72f);
                    var frame = new Builder(); frame.Box(new Vector3(0, 1.5f, 0), new Vector3(.12f, .1f, 3.4f), Quaternion.identity, Color.white); frame.Box(new Vector3(0, -1.5f, 0), new Vector3(.12f, .1f, 3.4f), Quaternion.identity, Color.white);
                    Part(go.transform, "Gate frame", frame, steel);
                    return go.transform;
                }
                var left = Panel(-1); var right = Panel(1);
                var head = new Builder(); head.Box(new Vector3(0, 3.6f, 0), new Vector3(.5f, .5f, 7.2f), Quaternion.identity, Color.white);
                Part(root.transform, "Gate header", head, darkPanel, true);
                Text(root.transform, "GATE " + (char)('A' + gatePoints.Count - 1), p + new Vector3(sx * .27f, 3.6f, 0), sx < 0 ? 90 : -90, 4, Color.white, 6);
                gates.Add((box, left, right, 0));
            }
        }

        private void BuildScreens()
        {
            // Two giant screens hung under the roof at each end, showing the live broadcast on match days.
            for (int s = -1; s <= 1; s += 2)
            {
                var pos = Centre + new Vector3(0, 0, s * (Straight + RoofInner + 2.5f));
                var housing = new Builder(); housing.Box(pos + Vector3.up * 23, new Vector3(32, 11.5f, 1.2f), Quaternion.identity, Color.white);
                for (int k = -1; k <= 1; k += 2) steelMesh(housing, pos + new Vector3(k * 12, 28.8f, 0), pos + new Vector3(k * 12, RoofLow, 0));
                Part(Stadium, "Big screen housing", housing, darkPanel);
                var face = new Builder(); float fz = pos.z - s * .62f;
                face.Quad(new Vector3(pos.x - 15.4f, 17.6f, fz), new Vector3(pos.x + 15.4f, 17.6f, fz), new Vector3(pos.x + 15.4f, 28.4f, fz), new Vector3(pos.x - 15.4f, 28.4f, fz), Color.white, new Vector2(s > 0 ? 0 : 1, 0), new Vector2(s > 0 ? 1 : 0, 0), new Vector2(s > 0 ? 1 : 0, 1), new Vector2(s > 0 ? 0 : 1, 1));
                screenPanels.Add(Part(Stadium, "Big screen panel", face, screen, false, ShadowCastingMode.Off).GetComponent<MeshRenderer>());
                float yaw = s > 0 ? 0 : 180; Vector3 front = new Vector3(pos.x, 0, fz - s * .05f);
                screenHeadlines.Add(Text(Stadium, "FARHAN STADIUM", front + Vector3.up * 24.6f, yaw, 40, new Color(1, .85f, .2f), 30));
                screenDetails.Add(Text(Stadium, "WELCOME", front + Vector3.up * 20.2f, yaw, 28, Color.white, 30));
            }
        }

        private static void steelMesh(Builder b, Vector3 from, Vector3 to) => b.Cylinder(from, to, .12f, Color.white, 6);
        private void BuildPitch()
        {
            var c = Centre; float hx = HavenExpansionLayout.PitchHalfX, hz = Straight, inner = HavenExpansionLayout.TrackInner + .05f;
            // Hybrid grass over the whole infield, mown in alternating stripes.
            var g = new Builder(); const float top = .1f;
            Color light = new Color(1.08f, 1.1f, 1.02f), darkStripe = new Color(.8f, .86f, .78f);
            float HalfWidth(float z) { float over = Mathf.Abs(z) - hz; return over <= 0 ? inner : Mathf.Sqrt(Mathf.Max(0, inner * inner - over * over)); }
            var bands = new List<(float z0, float z1, Color col)>();
            for (int i = 0; i < 14; i++) bands.Add((-hz + i * hz * 2 / 14, -hz + (i + 1) * hz * 2 / 14, i % 2 == 0 ? light : darkStripe));
            for (float z = hz; z < hz + inner - .01f; z += 1.5f) { bands.Add((z, Mathf.Min(hz + inner, z + 1.5f), darkStripe)); bands.Add((-Mathf.Min(hz + inner, z + 1.5f), -z, darkStripe)); }
            foreach (var (z0, z1, col) in bands)
            {
                int columns = 24;
                for (int k = 0; k < columns; k++)
                {
                    float t0 = -1 + 2f * k / columns, t1 = -1 + 2f * (k + 1) / columns;
                    Vector3 a = new Vector3(c.x + t0 * HalfWidth(z0), top, c.z + z0), b = new Vector3(c.x + t1 * HalfWidth(z0), top, c.z + z0), d = new Vector3(c.x + t0 * HalfWidth(z1), top, c.z + z1), e = new Vector3(c.x + t1 * HalfWidth(z1), top, c.z + z1);
                    g.Quad(a, d, e, b, col, new Vector2(a.x, a.z) / 1.6f, new Vector2(d.x, d.z) / 1.6f, new Vector2(e.x, e.z) / 1.6f, new Vector2(b.x, b.z) / 1.6f);
                }
            }
            var pitchObj = Part(Stadium, "Stadium hybrid pitch surface", g, grass, false);
            pitchObj.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            // FIFA markings.
            var lines = new Builder(); const float lw = .12f, ly = .115f;
            void Rect(float x0, float z0, float x1, float z1)
            {
                Seg(new Vector3(x0, ly, z0), new Vector3(x1, ly, z0)); Seg(new Vector3(x1, ly, z0), new Vector3(x1, ly, z1)); Seg(new Vector3(x1, ly, z1), new Vector3(x0, ly, z1)); Seg(new Vector3(x0, ly, z1), new Vector3(x0, ly, z0));
            }
            void Seg(Vector3 a, Vector3 b)
            {
                a += new Vector3(c.x, 0, c.z); b += new Vector3(c.x, 0, c.z); var dir = (b - a).normalized; var side = Vector3.Cross(Vector3.up, dir) * lw * .5f;
                lines.Quad(a - side - dir * lw * .5f, a + side - dir * lw * .5f, b + side + dir * lw * .5f, b - side + dir * lw * .5f, Color.white);
            }
            void Arc(Vector3 centre, float r, float a0, float a1, System.Func<Vector3, bool> keep = null)
            {
                int steps = Mathf.Max(6, Mathf.CeilToInt(Mathf.Abs(a1 - a0) * r * 1.5f));
                for (int i = 0; i < steps; i++)
                {
                    float u0 = Mathf.Lerp(a0, a1, i / (float)steps), u1 = Mathf.Lerp(a0, a1, (i + 1) / (float)steps);
                    Vector3 p0 = centre + new Vector3(Mathf.Cos(u0), 0, Mathf.Sin(u0)) * r, p1 = centre + new Vector3(Mathf.Cos(u1), 0, Mathf.Sin(u1)) * r;
                    if (keep != null && (!keep(p0) || !keep(p1))) continue;
                    var n0 = new Vector3(Mathf.Cos(u0), 0, Mathf.Sin(u0)) * lw * .5f; var n1 = new Vector3(Mathf.Cos(u1), 0, Mathf.Sin(u1)) * lw * .5f;
                    var o = new Vector3(c.x, ly, c.z);
                    lines.Quad(o + p0 - n0, o + p1 - n1, o + p1 + n1, o + p0 + n0, Color.white);
                }
            }
            Rect(-hx, -hz, hx, hz); Seg(new Vector3(-hx, ly, 0), new Vector3(hx, ly, 0));
            Arc(Vector3.zero, 9.15f, 0, Mathf.PI * 2); Arc(Vector3.zero, .15f, 0, Mathf.PI * 2); Arc(Vector3.zero, .08f, 0, Mathf.PI * 2);
            for (int s = -1; s <= 1; s += 2)
            {
                float gz = s * hz;
                Rect(-20.15f, gz - s * 16.5f, 20.15f, gz); Rect(-9.16f, gz - s * 5.5f, 9.16f, gz);
                var spot = new Vector3(0, 0, gz - s * 11); Arc(spot, .14f, 0, Mathf.PI * 2); Arc(spot, .07f, 0, Mathf.PI * 2);
                float boxEdge = gz - s * 16.5f; Arc(spot, 9.15f, 0, Mathf.PI * 2, p => s > 0 ? p.z < boxEdge - .02f : p.z > boxEdge + .02f);
                for (int side = -1; side <= 1; side += 2) Arc(new Vector3(side * hx, 0, gz), 1, side > 0 ? (s > 0 ? Mathf.PI : Mathf.PI * .5f) : (s > 0 ? Mathf.PI * 1.5f : 0), side > 0 ? (s > 0 ? Mathf.PI * 1.5f : Mathf.PI) : (s > 0 ? Mathf.PI * 2 : Mathf.PI * .5f));
            }
            Part(Stadium, "Stadium pitch markings", lines, lineWhite, false, ShadowCastingMode.Off);
            // Goals with round posts and nets, corner flags.
            var posts = new Builder(); var nets = new Builder(); var flags = new Builder();
            for (int s = -1; s <= 1; s += 2)
            {
                float gz = c.z + s * hz; float back = s * 2.2f;
                for (int side = -1; side <= 1; side += 2) posts.Cylinder(new Vector3(c.x + side * 3.72f, .1f, gz), new Vector3(c.x + side * 3.72f, 2.5f, gz), .06f, Color.white);
                posts.Cylinder(new Vector3(c.x - 3.78f, 2.5f, gz), new Vector3(c.x + 3.78f, 2.5f, gz), .06f, Color.white);
                for (int side = -1; side <= 1; side += 2) posts.Cylinder(new Vector3(c.x + side * 3.72f, 2.5f, gz), new Vector3(c.x + side * 3.72f, .1f, gz + back), .025f, Color.white);
                Vector3 bl = new Vector3(c.x - 3.72f, .1f, gz + back), br = new Vector3(c.x + 3.72f, .1f, gz + back), tl = new Vector3(c.x - 3.72f, 2.5f, gz), tr = new Vector3(c.x + 3.72f, 2.5f, gz);
                Vector3 bl0 = new Vector3(c.x - 3.72f, .1f, gz), br0 = new Vector3(c.x + 3.72f, .1f, gz);
                nets.Quad(bl, br, tr, tl, Color.white, 2f); nets.Quad(bl0, bl, tl, tl, Color.white, 2f); nets.Quad(br, br0, tr, tr, Color.white, 2f);
                for (int side = -1; side <= 1; side += 2)
                {
                    var p = new Vector3(c.x + side * hx, .1f, gz); flags.Cylinder(p, p + Vector3.up * 1.6f, .025f, Color.white, 6);
                    flags.Quad(p + Vector3.up * 1.6f, p + Vector3.up * 1.6f + new Vector3(0, 0, .45f), p + Vector3.up * 1.25f + new Vector3(0, 0, .45f), p + Vector3.up * 1.25f, new Color(1, .85f, .1f));
                }
                var col = new GameObject("Goal frame collider"); col.transform.SetParent(Stadium, false); col.transform.position = new Vector3(c.x, 1.3f, gz + back * .5f);
                var box = col.AddComponent<BoxCollider>(); box.size = new Vector3(7.6f, 2.5f, .1f); box.center = new Vector3(0, 0, -back * .5f);
            }
            Part(Stadium, "Stadium goal frames", posts, steel);
            Part(Stadium, "Stadium goal nets", nets, net, false, ShadowCastingMode.Off);
            Part(Stadium, "Stadium corner flags", flags, lineWhite);
            // Pitch-side LED advertising boards.
            string[] slogans = { "HAVEN MOTEL", "PIT STOP SHOP", "FARHAN FC", "CITY FAST FOOD", "LUMA", "ROADSIDE HAVEN", "ASTER MARKET", "HAVEN AIRWAYS" };
            int adIndex = 0;
            void Board(Vector3 a, Vector3 b, float yaw)
            {
                var m = new Builder(); var dir = (b - a).normalized; var nrm = Quaternion.Euler(0, yaw, 0) * Vector3.back;
                m.Box((a + b) * .5f + Vector3.up * .55f, new Vector3(Vector3.Distance(a, b), .9f, .25f), Quaternion.LookRotation(Vector3.Cross(dir, Vector3.up)), Color.white);
                var go = Part(Stadium, "Pitch-side LED board", m, adColours[adIndex % 4], true, ShadowCastingMode.Off);
                Text(go.transform, slogans[adIndex % slogans.Length], (a + b) * .5f + Vector3.up * .55f + nrm * .14f, yaw, 6, Color.white, Vector3.Distance(a, b));
                adIndex++;
            }
            for (int s = -1; s <= 1; s += 2)
            {
                float x = c.x + s * (hx + 2.4f);
                for (float z = -hz + 2; z < hz - 2; z += 13) Board(new Vector3(x, .1f, c.z + z), new Vector3(x, .1f, c.z + Mathf.Min(hz - 2, z + 12.5f)), s < 0 ? -90 : 90);
                float bz = c.z + s * (hz + 4.5f);
                for (float xx = -24; xx < 24; xx += 12.5f) Board(new Vector3(c.x + xx, .1f, bz), new Vector3(c.x + xx + 12, .1f, bz), s < 0 ? 180 : 0);
            }
            // Team dugouts on the west touchline.
            var dug = new Builder(); var dugGlass = new Builder();
            foreach (float z in new[] { -9f, 9f })
            {
                var p = new Vector3(c.x - HavenExpansionLayout.TrackOuter - 2.2f, .1f, c.z + z);
                dug.Box(p + new Vector3(-.9f, .25f, 0), new Vector3(1.4f, .5f, 6), Quaternion.identity, new Color(.12f, .3f, .8f));
                dug.Box(p + new Vector3(-1.6f, 1.15f, 0), new Vector3(.12f, 2.1f, 6.2f), Quaternion.identity, Color.white);
                dugGlass.Quad(p + new Vector3(-1.6f, 2.2f, -3.1f), p + new Vector3(-1.6f, 2.2f, 3.1f), p + new Vector3(.4f, 2f, 3.1f), p + new Vector3(.4f, 2f, -3.1f), Color.white);
                dugGlass.Quad(p + new Vector3(-1.6f, .1f, -3.1f), p + new Vector3(-1.6f, 2.2f, -3.1f), p + new Vector3(.4f, 2f, -3.1f), p + new Vector3(.4f, .1f, -3.1f), Color.white);
                dugGlass.Quad(p + new Vector3(-1.6f, .1f, 3.1f), p + new Vector3(.4f, .1f, 3.1f), p + new Vector3(.4f, 2f, 3.1f), p + new Vector3(-1.6f, 2.2f, 3.1f), Color.white);
            }
            Part(Stadium, "Team dugouts", dug, concrete, true);
            Part(Stadium, "Dugout canopies", dugGlass, glass);
        }

        // ================================================================ road and plaza

        private void BuildRoadAndPlaza(Transform sports)
        {
            var road = new GameObject("Stadium access road").transform; road.SetParent(Root, false);
            var surface = new Builder(); const float top = .11f;
            Slab(surface, Rect.MinMaxRect(108, -258, 116, RoadNorthSouth.yMax), top, .35f, .35f, 0, .35f);   // north-south run from the car park
            Slab(surface, Rect.MinMaxRect(108, -266, 116, -258), top, .35f, 0, .35f, 0);                  // the bend
            Slab(surface, Rect.MinMaxRect(116, -266, RoadEastWest.xMax, -258), top, 0, .35f, .35f, .35f);    // east-west run to the plaza
            Part(road, "Stadium access road asphalt", surface, asphalt, true);
            // Markings: centre dashes, edge lines and a zebra crossing where the walk crosses.
            var paint = new Builder(); float py = top + .013f;
            void Stripe(float x0, float z0, float x1, float z1) => paint.Quad(new Vector3(x0, py, z0), new Vector3(x0, py, z1), new Vector3(x1, py, z1), new Vector3(x1, py, z0), Color.white);
            for (float z = -152; z > -256; z -= 6) if (z > -247 || z < -253) Stripe(111.9f, z - 3, 112.1f, z);
            for (float x = 118; x < 139; x += 6) Stripe(x, -262.1f, x + 3, -261.9f);
            Stripe(108.35f, -258, 108.5f, -151); Stripe(115.5f, -247, 115.65f, -151); Stripe(115.5f, -258, 115.65f, -253);
            Stripe(116, -258.5f, 140, -258.35f); Stripe(108.35f, -265.65f, 140, -265.5f);
            for (float x = 108.6f; x < 115.6f; x += 1) Stripe(x, -252.6f, x + .5f, -247.4f);
            Part(road, "Stadium road markings", paint, lineWhite, false, ShadowCastingMode.Off);
            // Kerbs (visual only, so cars never snag), streetlights and signs.
            var kerbs = new Builder();
            kerbs.Box(new Vector3(116.1f, .08f, (-151 + -246.5f) * .5f), new Vector3(.2f, .16f, 95.5f), Quaternion.identity, Color.white);
            kerbs.Box(new Vector3(116.1f, .08f, (-253.5f + -258) * .5f), new Vector3(.2f, .16f, 4.5f), Quaternion.identity, Color.white);
            kerbs.Box(new Vector3(107.9f, .08f, (-151 + -246.5f) * .5f), new Vector3(.2f, .16f, 95.5f), Quaternion.identity, Color.white);
            kerbs.Box(new Vector3(107.9f, .08f, (-253.5f + -266.2f) * .5f), new Vector3(.2f, .16f, 12.7f), Quaternion.identity, Color.white);
            kerbs.Box(new Vector3(128, .08f, -257.9f), new Vector3(24, .16f, .2f), Quaternion.identity, Color.white);
            kerbs.Box(new Vector3(124, .08f, -266.1f), new Vector3(32, .16f, .2f), Quaternion.identity, Color.white);
            Part(road, "Stadium road kerbs", kerbs, kerb);
            var poles = new Builder(); var heads = new Builder();
            for (float z = -160; z > -250; z -= 22) Streetlight(poles, heads, new Vector3(117.6f, 0, z), Vector3.left);
            for (float x = 124; x < 140; x += 14) Streetlight(poles, heads, new Vector3(x, 0, -267.6f), Vector3.forward);
            Part(road, "Stadium road streetlight poles", poles, steel, true);
            Part(road, "Stadium road streetlight lamps", heads, ledWarm, false, ShadowCastingMode.Off);
            var signPost = new Builder(); signPost.Box(new Vector3(118.5f, 1.4f, -156), new Vector3(.12f, 2.8f, .12f), Quaternion.identity, Color.white); signPost.Box(new Vector3(118.5f, 2.6f, -156), new Vector3(.08f, 1, 2.6f), Quaternion.identity, Color.white);
            Part(road, "Stadium direction sign", signPost, darkPanel, true);
            Text(road, "FARHAN STADIUM  >", new Vector3(118.43f, 2.62f, -156), 90, 5, Color.white, 2.5f);
            Text(road, "FARHAN STADIUM  >", new Vector3(118.57f, 2.62f, -156), -90, 5, Color.white, 2.5f);
            // Plaza in front of the arena with ticket kiosks, bollards and turnstile gates at the entrances.
            var plaza = new GameObject("Stadium plaza").transform; plaza.SetParent(Root, false);
            var pave = new Builder(); Slab(pave, Plaza, .118f, .3f, .3f, .3f, .3f);
            foreach (float zGate in new[] { Centre.z - Straight, Centre.z + Straight }) Slab(pave, Rect.MinMaxRect(Plaza.xMax - .3f, zGate - 3.4f, Centre.x - LowerFront + 1, zGate + 3.4f), .128f, .05f, .2f, .2f, .2f);
            Part(plaza, "Stadium plaza paving", pave, paving, true);
            var kiosks = new Builder(); var kioskGlass = new Builder(); var bollards = new Builder();
            foreach (float z in new[] { -232f, -268f })
            {
                kiosks.Box(new Vector3(147, 1.5f, z), new Vector3(4, 3, 3), Quaternion.identity, new Color(.15f, .35f, .85f));
                kiosks.Box(new Vector3(147, 3.15f, z), new Vector3(4.6f, .3f, 3.6f), Quaternion.identity, Color.white);
                kioskGlass.Quad(new Vector3(149.02f, 1, z - 1.3f), new Vector3(149.02f, 1, z + 1.3f), new Vector3(149.02f, 2.4f, z + 1.3f), new Vector3(149.02f, 2.4f, z - 1.3f), Color.white);
                Text(plaza, "TICKETS", new Vector3(149.1f, 2.75f, z), -90, 5, Color.white, 4);
            }
            for (float z = -300; z < -198; z += 4) if (z < -270 || z > -230) bollards.Cylinder(new Vector3(141.2f, .1f, z), new Vector3(141.2f, 1, z), .12f, Color.white, 8);
            foreach (float zGate in new[] { Centre.z - Straight, Centre.z + Straight })
                for (int k = -2; k <= 2; k++) kiosks.Box(new Vector3(Centre.x - OuterWall - 3.2f, .58f, zGate + k * 1.3f), new Vector3(1.1f, 1, .18f), Quaternion.identity, new Color(.75f, .76f, .78f));
            Part(plaza, "Ticket kiosks and turnstiles", kiosks, concrete, true);
            Part(plaza, "Kiosk windows", kioskGlass, glass);
            Part(plaza, "Plaza bollards", bollards, steel, true);
            _ = sports;
        }

        /// <summary>Flat slab whose edges slope to the ground (per side: west, east, south, north; 0 = continues into a neighbour).</summary>
        private static void Slab(Builder b, Rect r, float h, float west, float east, float south, float north)
        {
            Vector3 A(float x, float z, float y) => new Vector3(x, y, z);
            float x0 = r.xMin, x1 = r.xMax, z0 = r.yMin, z1 = r.yMax, ix0 = x0 + west, ix1 = x1 - east, iz0 = z0 + south, iz1 = z1 - north;
            b.Quad(A(ix0, iz0, h), A(ix0, iz1, h), A(ix1, iz1, h), A(ix1, iz0, h), Color.white, new Vector2(ix0, iz0) / 4, new Vector2(ix0, iz1) / 4, new Vector2(ix1, iz1) / 4, new Vector2(ix1, iz0) / 4);
            if (west > 0) b.Quad(A(x0, iz0, .01f), A(x0, iz1, .01f), A(ix0, iz1, h), A(ix0, iz0, h), Color.white);
            if (east > 0) b.Quad(A(ix1, iz0, h), A(ix1, iz1, h), A(x1, iz1, .01f), A(x1, iz0, .01f), Color.white);
            if (south > 0) b.Quad(A(ix0, z0, .01f), A(ix0, iz0, h), A(ix1, iz0, h), A(ix1, z0, .01f), Color.white);
            if (north > 0) b.Quad(A(ix0, iz1, h), A(ix0, z1, .01f), A(ix1, z1, .01f), A(ix1, iz1, h), Color.white);
        }

        private static void Streetlight(Builder poles, Builder heads, Vector3 p, Vector3 arm)
        {
            poles.Cylinder(p, p + Vector3.up * 8, .11f, Color.white, 8);
            poles.Cylinder(p + Vector3.up * 7.9f, p + Vector3.up * 8.1f + arm * 2.2f, .06f, Color.white, 6);
            heads.Box(p + Vector3.up * 8f + arm * 2.4f, new Vector3(.35f, .12f, .7f), Quaternion.LookRotation(arm), Color.white);
        }

        // ================================================================ practice pitch

        private void BuildPractice()
        {
            Practice = new GameObject("Haven practice turf").transform; Practice.SetParent(Root, false);
            var f = PracticeFence; var p = PracticePitch;
            var surface = new Builder(); const float top = .1f;
            Color lightStripe = new Color(1.05f, 1.06f, 1f), darkStripe = new Color(.86f, .9f, .84f);
            int bands = 10; float bandW = f.height / bands;
            for (int i = 0; i < bands; i++)
            {
                float z0 = f.yMin + i * bandW, z1 = z0 + bandW;
                surface.Quad(new Vector3(f.xMin, top, z0), new Vector3(f.xMin, top, z1), new Vector3(f.xMax, top, z1), new Vector3(f.xMax, top, z0), i % 2 == 0 ? lightStripe : darkStripe, new Vector2(f.xMin, z0) / 1.2f, new Vector2(f.xMin, z1) / 1.2f, new Vector2(f.xMax, z1) / 1.2f, new Vector2(f.xMax, z0) / 1.2f);
            }
            // Bevelled edge so players and the camera never trip on it.
            surface.Quad(new Vector3(f.xMin - .3f, .01f, f.yMin - .3f), new Vector3(f.xMin, top, f.yMin), new Vector3(f.xMax, top, f.yMin), new Vector3(f.xMax + .3f, .01f, f.yMin - .3f), darkStripe);
            surface.Quad(new Vector3(f.xMin, top, f.yMax), new Vector3(f.xMin - .3f, .01f, f.yMax + .3f), new Vector3(f.xMax + .3f, .01f, f.yMax + .3f), new Vector3(f.xMax, top, f.yMax), darkStripe);
            surface.Quad(new Vector3(f.xMin - .3f, .01f, f.yMin - .3f), new Vector3(f.xMin - .3f, .01f, f.yMax + .3f), new Vector3(f.xMin, top, f.yMax), new Vector3(f.xMin, top, f.yMin), darkStripe);
            surface.Quad(new Vector3(f.xMax, top, f.yMin), new Vector3(f.xMax, top, f.yMax), new Vector3(f.xMax + .3f, .01f, f.yMax + .3f), new Vector3(f.xMax + .3f, .01f, f.yMin - .3f), darkStripe);
            Part(Practice, "Practice synthetic pitch surface", surface, turf, true).GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            // Markings.
            var lines = new Builder(); float ly = top + .012f, lw = .1f;
            void Seg(float x0, float z0, float x1, float z1)
            {
                var a = new Vector3(x0, ly, z0); var b = new Vector3(x1, ly, z1); var dir = (b - a).normalized; var s = Vector3.Cross(Vector3.up, dir) * lw * .5f;
                lines.Quad(a - s - dir * lw * .5f, a + s - dir * lw * .5f, b + s + dir * lw * .5f, b - s + dir * lw * .5f, Color.white);
            }
            void Box(float x0, float z0, float x1, float z1) { Seg(x0, z0, x1, z0); Seg(x1, z0, x1, z1); Seg(x1, z1, x0, z1); Seg(x0, z1, x0, z0); }
            void Circle(Vector2 c, float r, float a0 = 0, float a1 = Mathf.PI * 2)
            {
                int steps = Mathf.CeilToInt(Mathf.Abs(a1 - a0) * r * 2) + 4;
                for (int i = 0; i < steps; i++) { float u0 = Mathf.Lerp(a0, a1, i / (float)steps), u1 = Mathf.Lerp(a0, a1, (i + 1) / (float)steps); Seg(c.x + Mathf.Cos(u0) * r, c.y + Mathf.Sin(u0) * r, c.x + Mathf.Cos(u1) * r, c.y + Mathf.Sin(u1) * r); }
            }
            Box(p.xMin, p.yMin, p.xMax, p.yMax); Seg(p.xMin, p.center.y, p.xMax, p.center.y); Circle(p.center, 6); Circle(p.center, .12f);
            for (int s = -1; s <= 1; s += 2)
            {
                float gz = s > 0 ? p.yMax : p.yMin;
                Box(p.center.x - 10, gz - s * 10, p.center.x + 10, gz); Box(p.center.x - 4.5f, gz - s * 4, p.center.x + 4.5f, gz);
                Circle(new Vector2(p.center.x, gz - s * 8), .12f);
            }
            Part(Practice, "Practice pitch markings", lines, lineWhite, false, ShadowCastingMode.Off);
            // Small goals.
            var posts = new Builder(); var nets = new Builder();
            for (int s = -1; s <= 1; s += 2)
            {
                float gz = s > 0 ? p.yMax : p.yMin; float back = s * 1.5f; float cx = p.center.x;
                for (int side = -1; side <= 1; side += 2) posts.Cylinder(new Vector3(cx + side * 2.5f, top, gz), new Vector3(cx + side * 2.5f, 2.1f, gz), .05f, Color.white);
                posts.Cylinder(new Vector3(cx - 2.55f, 2.1f, gz), new Vector3(cx + 2.55f, 2.1f, gz), .05f, Color.white);
                nets.Quad(new Vector3(cx - 2.5f, top, gz + back), new Vector3(cx + 2.5f, top, gz + back), new Vector3(cx + 2.5f, 2.1f, gz), new Vector3(cx - 2.5f, 2.1f, gz), Color.white, 2);
                nets.Quad(new Vector3(cx - 2.5f, top, gz), new Vector3(cx - 2.5f, top, gz + back), new Vector3(cx - 2.5f, 2.1f, gz), new Vector3(cx - 2.5f, 2.1f, gz), Color.white, 2);
                nets.Quad(new Vector3(cx + 2.5f, top, gz + back), new Vector3(cx + 2.5f, top, gz), new Vector3(cx + 2.5f, 2.1f, gz), new Vector3(cx + 2.5f, 2.1f, gz), Color.white, 2);
                var col = new GameObject("Practice goal collider"); col.transform.SetParent(Practice, false); col.transform.position = new Vector3(cx, 1.1f, gz + back * .5f); col.AddComponent<BoxCollider>().size = new Vector3(5.1f, 2.1f, Mathf.Abs(back));
            }
            Part(Practice, "Practice goal frames", posts, steel);
            Part(Practice, "Practice goal nets", nets, net, false, ShadowCastingMode.Off);
            // Chain-link fence with a gate towards the walk, posts and rails.
            var mesh = new Builder(); var frame = new Builder(); const float fh = 4;
            void FenceRun(Vector3 a, Vector3 b)
            {
                mesh.Quad(a + Vector3.up * .05f, b + Vector3.up * .05f, b + Vector3.up * fh, a + Vector3.up * fh, Color.white, 3.5f);
                frame.Cylinder(a + Vector3.up * fh, b + Vector3.up * fh, .04f, Color.white, 6);
                float len = Vector3.Distance(a, b); int n = Mathf.Max(1, Mathf.RoundToInt(len / 3));
                for (int i = 0; i <= n; i++) { var q = Vector3.Lerp(a, b, i / (float)n); frame.Cylinder(q, q + Vector3.up * (fh + .05f), .05f, Color.white, 6); }
            }
            Vector3 V(float x, float z) => new Vector3(x, top, z);
            FenceRun(V(f.xMin, f.yMin), V(f.xMax, f.yMin)); FenceRun(V(f.xMin, f.yMax), V(f.xMax, f.yMax)); FenceRun(V(f.xMin, f.yMin), V(f.xMin, f.yMax));
            FenceRun(V(f.xMax, f.yMin), V(f.xMax, PracticePath.yMin)); FenceRun(V(f.xMax, PracticePath.yMax), V(f.xMax, f.yMax));
            var fenceObj = Part(Practice, "Practice chain-link fence", mesh, fence, false);
            Part(Practice, "Practice fence posts", frame, steel);
            foreach (var (a, b) in new[] { (V(f.xMin, f.yMin), V(f.xMax, f.yMin)), (V(f.xMin, f.yMax), V(f.xMax, f.yMax)), (V(f.xMin, f.yMin), V(f.xMin, f.yMax)), (V(f.xMax, f.yMin), V(f.xMax, PracticePath.yMin)), (V(f.xMax, PracticePath.yMax), V(f.xMax, f.yMax)) })
            {
                var col = new GameObject("Practice fence collider"); col.transform.SetParent(Practice, false); col.transform.position = (a + b) * .5f + Vector3.up * fh * .5f;
                col.transform.rotation = Quaternion.LookRotation(b - a); col.AddComponent<BoxCollider>().size = new Vector3(.1f, fh, Vector3.Distance(a, b));
            }
            _ = fenceObj;
            // Path to the stadium walk, floodlights, bench shelter, training cones and a sign.
            var path = new Builder(); Slab(path, PracticePath, .118f, 0, .2f, .2f, .2f);
            Part(Practice, "Practice path paving", path, paving, true);
            var masts = new Builder(); var lamps = new Builder();
            foreach (var corner in new[] { new Vector2(f.xMin - 1.5f, f.yMin - 1.5f), new Vector2(f.xMax + 1.5f, f.yMin - 1.5f), new Vector2(f.xMin - 1.5f, f.yMax + 1.5f), new Vector2(f.xMax + 1.5f, f.yMax + 1.5f) })
            {
                var b = new Vector3(corner.x, 0, corner.y); masts.Cylinder(b, b + Vector3.up * 14, .2f, Color.white, 8);
                var toward = (new Vector3(p.center.x, 0, p.center.y) - b); toward.y = 0; toward.Normalize();
                lamps.Box(b + Vector3.up * 14.2f + toward * .4f, new Vector3(2.4f, .9f, .25f), Quaternion.LookRotation(toward) * Quaternion.Euler(35, 0, 0), Color.white);
                var go = new GameObject("Practice floodlight"); go.transform.SetParent(Practice, false); go.transform.position = b + Vector3.up * 14; go.transform.LookAt(new Vector3(p.center.x, 0, p.center.y) - toward * 8);
                var l = go.AddComponent<Light>(); l.type = LightType.Spot; l.range = 75; l.spotAngle = 85; l.intensity = 4; l.color = new Color(1, .97f, .92f); l.shadows = LightShadows.None; l.enabled = false; floodlights.Add(l);
            }
            Part(Practice, "Practice floodlight masts", masts, steel, true);
            Part(Practice, "Practice floodlight lamps", lamps, ledWhite, false, ShadowCastingMode.Off);
            var bench = new Builder(); var benchRoof = new Builder();
            var bp = new Vector3(f.xMin + 3, top, p.center.y + 8);
            bench.Box(bp + new Vector3(0, .25f, 0), new Vector3(1.2f, .5f, 7), Quaternion.identity, new Color(.15f, .35f, .85f)); bench.Box(bp + new Vector3(-.75f, 1.1f, 0), new Vector3(.1f, 2.2f, 7.2f), Quaternion.identity, Color.white);
            benchRoof.Quad(bp + new Vector3(-.8f, 2.25f, -3.6f), bp + new Vector3(-.8f, 2.25f, 3.6f), bp + new Vector3(1, 2.05f, 3.6f), bp + new Vector3(1, 2.05f, -3.6f), Color.white);
            Part(Practice, "Practice team bench", bench, concrete, true); Part(Practice, "Practice bench canopy", benchRoof, glass);
            var cones = new Builder(); var rnd = new System.Random(9);
            for (int i = 0; i < 10; i++) { var cp = new Vector3(p.xMin + 6 + i * 2.6f, top, p.center.y - 12 + (i % 2) * 1.5f); ConeMesh(cones, cp); }
            for (int i = 0; i < 6; i++) { var cp = new Vector3(p.xMin + 5 + (float)rnd.NextDouble() * 28, top, p.yMin + 6 + (float)rnd.NextDouble() * 8); ConeMesh(cones, cp); }
            Part(Practice, "Training cones", cones, cone);
            var signBoard = new Builder(); signBoard.Box(new Vector3(f.xMax + .5f, 2.2f, PracticePath.yMax + 2.6f), new Vector3(.15f, 1.4f, 4.6f), Quaternion.identity, Color.white);
            signBoard.Box(new Vector3(f.xMax + .5f, .9f, PracticePath.yMax + .6f), new Vector3(.1f, 1.8f, .1f), Quaternion.identity, Color.white); signBoard.Box(new Vector3(f.xMax + .5f, .9f, PracticePath.yMax + 4.6f), new Vector3(.1f, 1.8f, .1f), Quaternion.identity, Color.white);
            Part(Practice, "Practice sign", signBoard, darkPanel, true);
            Text(Practice, "FARHAN FC  PRACTICE PITCH", new Vector3(f.xMax + .6f, 2.25f, PracticePath.yMax + 2.6f), -90, 4, Color.white, 4.4f);
            // Players training.
            for (int i = 0; i < 6; i++)
            {
                var go = new GameObject("Footballer " + i); go.transform.SetParent(Practice, false);
                var agent = go.AddComponent<TownCitizenAgent>(); agent.InitializeAs(i % 3 == 0 ? HumanAge.Teen : HumanAge.YoungAdult);
                agent.Warp(new Vector3(p.xMin + 6 + i * 5, top, p.center.y + (i % 2 == 0 ? 6 : -6)));
                agent.SetJogging(true); players.Add(agent);
            }
        }

        private static void ConeMesh(Builder b, Vector3 p)
        {
            const int sides = 8;
            for (int i = 0; i < sides; i++)
            {
                float a0 = i * Mathf.PI * 2 / sides, a1 = (i + 1) * Mathf.PI * 2 / sides;
                Vector3 d0 = new Vector3(Mathf.Cos(a0), 0, Mathf.Sin(a0)), d1 = new Vector3(Mathf.Cos(a1), 0, Mathf.Sin(a1));
                b.Quad(p + d0 * .14f, p + Vector3.up * .3f + d0 * .02f, p + Vector3.up * .3f + d1 * .02f, p + d1 * .14f, Color.white);
            }
        }

        // ================================================================ runtime

        private void Update()
        {
            if (!Done || Root == null) return;
            float hour = atmosphere != null ? atmosphere.Hour : 12;
            bool night = hour < 6.4f || hour > 18.2f || (WeatherSystem.Active != null && WeatherSystem.Active.Rain > .6f);
            if (night != lit) { lit = night; foreach (var l in floodlights) if (l != null) l.enabled = night; ledWhite.SetColor("_EmissionColor", night ? new Color(6, 6, 5.5f) : new Color(1.4f, 1.4f, 1.3f)); }
            // Gates slide open while a scanned ticket is valid, then close.
            for (int i = 0; i < gates.Count; i++)
            {
                var g = gates[i]; bool open = Time.time < g.openUntil; g.barrier.enabled = !open;
                float slide = Mathf.MoveTowards(Mathf.Abs(g.left.localPosition.z) - 1.72f, open ? 3.3f : 0, Time.deltaTime * 2.5f);
                g.left.localPosition = new Vector3(0, 1.6f, -1.72f - slide); g.right.localPosition = new Vector3(0, 1.6f, 1.72f + slide);
            }
            // Media façade and ribbon boards scroll continuously.
            if (mediaMaterial != null) mediaMaterial.SetTextureOffset("_BaseMap", new Vector2(Time.time * .015f, 0));
            if (ribbonMaterial != null) ribbonMaterial.SetTextureOffset("_BaseMap", new Vector2(-Time.time * .05f, 0));            // Training drills: jog to a new spot on the practice pitch whenever a player gets there.
            if (Time.time >= nextPlayerThink)
            {
                nextPlayerThink = Time.time + .5f; var p = PracticePitch;
                for (int i = 0; i < players.Count; i++)
                {
                    var a = players[i]; if (a == null || !a.Arrived || a.Partner != null) continue;
                    float t = Mathf.Repeat(Time.time * .37f + i * 2.3f, 1);
                    var target = new Vector3(Mathf.Lerp(p.xMin + 3, p.xMax - 3, Mathf.PerlinNoise(i * 3.1f, Time.time * .05f)), .1f, Mathf.Lerp(p.yMin + 4, p.yMax - 4, t));
                    a.Walk(new[] { target });
                }
            }
        }

        private void OnDestroy()
        {
            foreach (var (filter, original) in stripped) if (filter != null) filter.sharedMesh = original;
            foreach (var (f, original) in emptied) if (f != null) f.sharedMesh = original;
            foreach (var r in hidden) if (r != null) r.enabled = true;
            foreach (var b in disabled) if (b != null) b.enabled = true;
            foreach (var c in disabledColliders) if (c != null) c.enabled = true;
            if (Root != null) Destroy(Root.gameObject);
            foreach (var o in owned) if (o != null) Destroy(o);
        }
    }
}
