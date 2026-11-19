using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace RoadsideHaven.Editor
{
    /// <summary>
    /// Fills three large empty parcels with complete districts: Lakeside Park (lake, pier, boats,
    /// gazebo, picnic lawn, cafe), Haven Sports Park (stadium, track, floodlights, courts, clubhouse)
    /// and Haven Valley Farm (barn, silo, windmill, crop fields, orchard, cow paddock).
    /// </summary>
    public static class HavenExpansionBuilder
    {
        public const string Folder = "Assets/_Game/Generated/HavenExpansion";
        private static Material cream, ink, white, wood, darkWood, stone, asphalt, metal, glass, leaf, leafDark, bark, soil, gravel, barnRed, roofTile, wheat, corn, track, pitchA, pitchB, courtBlue, courtGreen, seatRed, seatBlue, seatYellow, water, ripple, warm, cowWhite, cowBlack, fruitOrange, fruitRed, net, sand, lineWhite, canvas, dirtLane;
        private static readonly List<Mesh> temporary = new List<Mesh>();
        private static readonly Dictionary<string, Mesh> cubes = new Dictionary<string, Mesh>();
        private static readonly List<Renderer> bulbs = new List<Renderer>();
        private static readonly List<Transform> moving = new List<Transform>();
        private static List<Transform> spinners, bobbers, swimmers, grazers, flags;
        private static Mesh crown, smallSphere;
        private static int meshIndex;
        private static List<Rect> roads;
        private static System.Random random;

        [MenuItem("Roadside Haven/Design/Build Lakeside, Sports and Farm Districts")]
        public static void BuildCurrent()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            var motel = UnityEngine.Object.FindFirstObjectByType<MotelGameRoot>(); if (motel == null) return;
            Directory.CreateDirectory("Backups/HavenExpansion");
            if (File.Exists(motel.gameObject.scene.path)) File.Copy(motel.gameObject.scene.path, "Backups/HavenExpansion/Before-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + ".unity", true);
            Build(motel);
            EditorSceneManager.MarkSceneDirty(motel.gameObject.scene); EditorSceneManager.SaveScene(motel.gameObject.scene); AssetDatabase.SaveAssets();
        }

        public static Transform[] Build(MotelGameRoot motel)
        {
            if (Application.isPlaying) throw new InvalidOperationException("Build the districts in Edit Mode.");
            var names = new[] { HavenExpansionLayout.Lakeside, HavenExpansionLayout.Sports, HavenExpansionLayout.Farm };
            if (names.All(n => motel.transform.Find(n) != null)) return names.Select(n => motel.transform.Find(n)).ToArray();
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/_Game/Generated", "HavenExpansion");
            temporary.Clear(); cubes.Clear(); bulbs.Clear(); moving.Clear(); meshIndex = 0; crown = smallSphere = null; random = new System.Random(2026);
            Materials();
            Physics.SyncTransforms();
            roads = motel.GetComponentsInChildren<MeshRenderer>().Where(r => r.gameObject.activeInHierarchy && TownPopulationController.RoadName(r.name)).Select(r => Rect.MinMaxRect(r.bounds.min.x, r.bounds.min.z, r.bounds.max.x, r.bounds.max.z)).ToList();
            foreach (var area in new[] { HavenExpansionLayout.LakesideArea, HavenExpansionLayout.SportsArea, HavenExpansionLayout.FarmArea })
                if (roads.Any(r => r.Overlaps(Shrink(area, 2)))) throw new InvalidOperationException("A new district parcel overlaps a public road: " + area);
            var result = new List<Transform>();
            if (motel.transform.Find(HavenExpansionLayout.Lakeside) == null) result.Add(District(motel, HavenExpansionLayout.Lakeside, Lakeside, 1, HavenExpansionLayout.LakeCentre, HavenExpansionLayout.LakeRadiusX, HavenExpansionLayout.LakeRadiusZ, default));
            if (motel.transform.Find(HavenExpansionLayout.Sports) == null) result.Add(District(motel, HavenExpansionLayout.Sports, SportsPark, 2, Vector3.zero, 0, 0, default));
            if (motel.transform.Find(HavenExpansionLayout.Farm) == null) result.Add(District(motel, HavenExpansionLayout.Farm, FarmDistrict, 0, Vector3.zero, 0, 0, HavenExpansionLayout.Paddock));
            RegisterLights(motel);
            foreach (var mesh in temporary) if (mesh != null && !AssetDatabase.Contains(mesh)) UnityEngine.Object.DestroyImmediate(mesh);
            temporary.Clear(); cubes.Clear(); crown = smallSphere = null; AssetDatabase.SaveAssets();
            return names.Select(n => motel.transform.Find(n)).ToArray();
        }

        private static Transform District(MotelGameRoot motel, string name, Action<Transform> build, int route, Vector3 swimCentre, float rx, float rz, Rect graze)
        {
            spinners = new List<Transform>(); bobbers = new List<Transform>(); swimmers = new List<Transform>(); grazers = new List<Transform>(); flags = new List<Transform>();
            var root = Group(motel.transform, name, Vector3.zero);
            root.gameObject.AddComponent<MotelDistrictArt>();
            build(root);
            root.gameObject.AddComponent<HavenExpansionLife>().Configure(spinners.ToArray(), bobbers.ToArray(), swimmers.ToArray(), swimCentre, rx, rz, grazers.ToArray(), graze, flags.ToArray(), route);
            Combine(root);
            foreach (var label in root.GetComponentsInChildren<TMP_Text>()) label.ForceMeshUpdate(true, true);
            return root;
        }

        // ================= Materials and primitives =================

        private static void Materials()
        {
            cream = Mat("Expansion warm plaster", new Color(.86f, .82f, .70f), "Stucco"); ink = Mat("Expansion graphite", new Color(.05f, .07f, .08f));
            white = Mat("Expansion painted white", new Color(.92f, .91f, .86f)); wood = Mat("Expansion cedar", new Color(.50f, .32f, .17f), "CedarGrain");
            darkWood = Mat("Expansion weathered timber", new Color(.30f, .21f, .13f), "CedarGrain"); stone = Mat("Expansion park paving", new Color(.72f, .70f, .63f), "Paving");
            asphalt = Mat("Expansion parking asphalt", new Color(.25f, .255f, .26f)); metal = Mat("Expansion galvanised metal", new Color(.55f, .58f, .58f)); metal.SetFloat("_Metallic", .6f); metal.SetFloat("_Smoothness", .55f);
            leaf = Mat("Expansion foliage", new Color(.22f, .40f, .14f)); leafDark = Mat("Expansion deep foliage", new Color(.14f, .30f, .12f)); bark = Mat("Expansion bark", new Color(.30f, .20f, .12f));
            soil = Mat("Expansion farm soil", new Color(.42f, .29f, .18f), "DirtPebbles"); gravel = Mat("Expansion yard gravel", new Color(.84f, .78f, .66f), "DirtPebbles"); dirtLane = Mat("Expansion dirt lane", new Color(.70f, .56f, .40f), "DirtPebbles");
            barnRed = Mat("Expansion barn red", new Color(.55f, .12f, .08f)); roofTile = Mat("Expansion roof slate", new Color(.24f, .22f, .22f));
            wheat = Mat("Expansion ripe wheat", new Color(.86f, .70f, .33f)); corn = Mat("Expansion maize leaves", new Color(.32f, .52f, .16f));
            track = Mat("Expansion running track", new Color(.66f, .27f, .18f)); pitchA = Mat("Expansion pitch light", new Color(.29f, .55f, .21f)); pitchB = Mat("Expansion pitch dark", new Color(.25f, .49f, .18f));
            courtBlue = Mat("Expansion court blue", new Color(.17f, .36f, .62f)); courtGreen = Mat("Expansion court green", new Color(.20f, .46f, .32f));
            seatRed = Mat("Expansion seat red", new Color(.70f, .12f, .10f)); seatBlue = Mat("Expansion seat blue", new Color(.12f, .25f, .62f)); seatYellow = Mat("Expansion seat yellow", new Color(.90f, .70f, .12f));
            warm = Mat("Expansion lamp diffuser", new Color(1, .82f, .45f)); cowWhite = Mat("Expansion cow white", new Color(.90f, .88f, .82f)); cowBlack = Mat("Expansion cow black", new Color(.08f, .07f, .07f));
            fruitOrange = Mat("Expansion orange fruit", new Color(.95f, .52f, .08f)); fruitRed = Mat("Expansion red apple", new Color(.78f, .10f, .08f)); sand = Mat("Expansion lake sand", new Color(.80f, .72f, .55f), "Sand");
            lineWhite = Mat("Expansion line paint", new Color(.96f, .96f, .93f)); canvas = Mat("Expansion umbrella canvas", new Color(.85f, .30f, .20f));
            glass = Transparent("Expansion glass", new Color(.20f, .45f, .50f, .25f)); net = Transparent("Expansion netting", new Color(.95f, .95f, .95f, .35f)); ripple = Transparent("Expansion water spray", new Color(.85f, .95f, 1f, .45f));
            water = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Generated/Lake/Lake water.mat");
            if (water == null) { water = Mat("Expansion lake water fallback", new Color(.10f, .38f, .48f)); water.SetFloat("_Smoothness", .9f); }
        }

        private static Material Mat(string name, Color color, string texture = null)
        {
            string path = Folder + "/" + name + ".mat"; var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, path); }
            m.color = color; m.SetFloat("_Smoothness", .2f); m.enableInstancing = true;
            if (texture != null)
            {
                bool district = texture == "Stucco" || texture == "CedarGrain"; string dir = district ? "Assets/_Game/Generated/MotelDistrict/" : "Assets/_Game/Art/Surfaces/";
                m.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(dir + texture + (district ? ".png" : "_Albedo.png"));
                var normal = AssetDatabase.LoadAssetAtPath<Texture2D>(dir + texture + (district ? "Normal.png" : "_Normal.png"));
                if (normal != null) { m.SetTexture("_BumpMap", normal); m.SetFloat("_BumpScale", .3f); m.EnableKeyword("_NORMALMAP"); }
            }
            EditorUtility.SetDirty(m); return m;
        }

        private static Material Transparent(string name, Color color)
        {
            var m = Mat(name, color); m.SetFloat("_Surface", 1); m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha); m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0); m.SetFloat("_Cull", 0); m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); m.renderQueue = 3000; m.SetShaderPassEnabled("ShadowCaster", false); return m;
        }

        private static Rect Shrink(Rect r, float d) => Rect.MinMaxRect(r.xMin + d, r.yMin + d, r.xMax - d, r.yMax - d);
        private static Transform Group(Transform parent, string name, Vector3 position, float yaw = 0)
        { var t = new GameObject(name).transform; t.SetParent(parent, false); t.localPosition = position; t.localRotation = Quaternion.Euler(0, yaw, 0); return t; }

        private static GameObject Box(Transform parent, string name, Vector3 p, Vector3 size, Material mat, bool solid = false)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer)); go.transform.SetParent(parent, false); go.transform.localPosition = p;
            go.GetComponent<MeshFilter>().sharedMesh = Cube(size); go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            if (solid) go.AddComponent<BoxCollider>().size = size; return go;
        }

        /// <summary>Flat slab whose top sits at <paramref name="top"/>, spanning a world rectangle.</summary>
        private static GameObject Slab(Transform parent, string name, Rect area, float top, Material mat, bool solid = true, float thickness = .12f)
            => Box(parent, name, new Vector3(area.center.x, top - thickness * .5f, area.center.y), new Vector3(area.width, thickness, area.height), mat, solid);

        private static Mesh Cube(Vector3 size)
        {
            string key = size.ToString("F3"); if (cubes.TryGetValue(key, out var mesh)) return mesh;
            var v = new List<Vector3>(); var uv = new List<Vector2>(); var tri = new List<int>(); var h = size * .5f;
            void Face(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
            {
                int n = v.Count; v.AddRange(new[] { a, b, c, d }); var normal = Vector3.Cross(b - a, d - a).normalized;
                foreach (var q in new[] { a, b, c, d }) uv.Add(Mathf.Abs(normal.y) > .5f ? new Vector2(q.x, q.z) : Mathf.Abs(normal.x) > .5f ? new Vector2(q.z, q.y) : new Vector2(q.x, q.y));
                tri.AddRange(new[] { n, n + 1, n + 2, n, n + 2, n + 3 });
            }
            Face(new Vector3(-h.x, -h.y, -h.z), new Vector3(-h.x, h.y, -h.z), new Vector3(h.x, h.y, -h.z), new Vector3(h.x, -h.y, -h.z));
            Face(new Vector3(h.x, -h.y, h.z), new Vector3(h.x, h.y, h.z), new Vector3(-h.x, h.y, h.z), new Vector3(-h.x, -h.y, h.z));
            Face(new Vector3(-h.x, -h.y, h.z), new Vector3(-h.x, h.y, h.z), new Vector3(-h.x, h.y, -h.z), new Vector3(-h.x, -h.y, -h.z));
            Face(new Vector3(h.x, -h.y, -h.z), new Vector3(h.x, h.y, -h.z), new Vector3(h.x, h.y, h.z), new Vector3(h.x, -h.y, h.z));
            Face(new Vector3(-h.x, h.y, -h.z), new Vector3(-h.x, h.y, h.z), new Vector3(h.x, h.y, h.z), new Vector3(h.x, h.y, -h.z));
            Face(new Vector3(-h.x, -h.y, h.z), new Vector3(-h.x, -h.y, -h.z), new Vector3(h.x, -h.y, -h.z), new Vector3(h.x, -h.y, h.z));
            mesh = new Mesh { name = "Expansion cuboid" }; mesh.SetVertices(v); mesh.SetUVs(0, uv); mesh.SetTriangles(tri, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
            temporary.Add(mesh); cubes[key] = mesh; return mesh;
        }

        private static GameObject Beam(Transform root, string name, Vector3 a, Vector3 b, float width, Material mat, bool solid = false)
        { var go = Box(root, name, (a + b) * .5f, new Vector3(width, (b - a).magnitude, width), mat, solid); go.transform.localRotation = Quaternion.FromToRotation(Vector3.up, b - a); return go; }

        private static Mesh Crown()
        {
            if (crown != null) return crown;
            const int lon = 12, lat = 8; var v = new List<Vector3>(); var t = new List<int>();
            for (int y = 0; y <= lat; y++) for (int x = 0; x <= lon; x++)
                {
                    float a = x * Mathf.PI * 2 / lon, b = y * Mathf.PI / lat;
                    float wobble = 1 + .06f * Mathf.Sin(a * 3 + b * 2);
                    v.Add(new Vector3(Mathf.Sin(b) * Mathf.Cos(a), Mathf.Cos(b), Mathf.Sin(b) * Mathf.Sin(a)) * .5f * wobble);
                    if (x < lon && y < lat) { int i = y * (lon + 1) + x; t.AddRange(new[] { i, i + 1, i + lon + 1, i + 1, i + lon + 2, i + lon + 1 }); }
                }
            crown = new Mesh { name = "Expansion crown sphere" }; crown.SetVertices(v); crown.SetTriangles(t, 0); crown.RecalculateNormals(); crown.RecalculateBounds(); temporary.Add(crown); return crown;
        }

        private static Mesh SmallSphere()
        {
            if (smallSphere != null) return smallSphere;
            float g = (1 + Mathf.Sqrt(5)) * .5f;
            var v = new[] { new Vector3(-1, g, 0), new Vector3(1, g, 0), new Vector3(-1, -g, 0), new Vector3(1, -g, 0), new Vector3(0, -1, g), new Vector3(0, 1, g), new Vector3(0, -1, -g), new Vector3(0, 1, -g), new Vector3(g, 0, -1), new Vector3(g, 0, 1), new Vector3(-g, 0, -1), new Vector3(-g, 0, 1) }.Select(p => p.normalized * .5f).ToArray();
            smallSphere = new Mesh { name = "Expansion low sphere", vertices = v, normals = v.Select(p => p.normalized).ToArray(), triangles = new[] { 0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11, 1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8, 3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9, 4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1 } };
            smallSphere.RecalculateBounds(); temporary.Add(smallSphere); return smallSphere;
        }

        private static GameObject Ball(Transform parent, string name, Vector3 p, Vector3 scale, Material mat, bool low = false)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer)); go.transform.SetParent(parent, false); go.transform.localPosition = p; go.transform.localScale = scale;
            go.GetComponent<MeshFilter>().sharedMesh = low ? SmallSphere() : Crown(); go.GetComponent<MeshRenderer>().sharedMaterial = mat; return go;
        }

        private static Transform MeshPart(Transform root, string name, Vector3 p, List<Vector3> vertices, List<int> triangles, Material mat, bool collider = false, List<Vector2> uvs = null, List<Color> colors = null)
        {
            var mesh = new Mesh { name = name, indexFormat = IndexFormat.UInt32 }; mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0);
            mesh.SetUVs(0, uvs ?? vertices.Select(q => new Vector2(q.x, q.z) * .25f).ToList()); if (colors != null) mesh.SetColors(colors);
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer)); go.transform.SetParent(root, false); go.transform.localPosition = p;
            if (collider) { mesh = Save(mesh, Folder + "/Collision" + (meshIndex++) + ".asset"); go.AddComponent<MeshCollider>().sharedMesh = mesh; }
            else temporary.Add(mesh);
            go.GetComponent<MeshFilter>().sharedMesh = mesh; go.GetComponent<MeshRenderer>().sharedMaterial = mat; return go.transform;
        }

        private static Transform Cylinder(Transform root, string name, Vector3 basePoint, float radiusBottom, float radiusTop, float height, int sides, Material mat, bool solid = false, bool capped = true)
        {
            var v = new List<Vector3>(); var t = new List<int>(); var uv = new List<Vector2>();
            for (int i = 0; i <= sides; i++)
            {
                float a = i * Mathf.PI * 2 / sides; var d = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                v.Add(d * radiusBottom); v.Add(d * radiusTop + Vector3.up * height); uv.Add(new Vector2(i / (float)sides * 4, 0)); uv.Add(new Vector2(i / (float)sides * 4, height * .25f));
                if (i < sides) { int k = i * 2; t.AddRange(new[] { k, k + 1, k + 2, k + 1, k + 3, k + 2 }); }
            }
            if (capped) { int c = v.Count; v.Add(Vector3.up * height); uv.Add(Vector2.zero); for (int i = 0; i < sides; i++) t.AddRange(new[] { c, i * 2 + 3, i * 2 + 1 }); }
            var part = MeshPart(root, name, basePoint, v, t, mat, false, uv);
            if (solid) { var box = part.gameObject.AddComponent<CapsuleCollider>(); box.radius = Mathf.Max(radiusBottom, radiusTop); box.height = height; box.center = Vector3.up * height * .5f; }
            return part;
        }

        private static Transform Ring(Transform root, string name, Vector3 p, float outer, float inner, float height, Material mat, int segments = 40)
        {
            var v = new List<Vector3>(); var t = new List<int>();
            for (int i = 0; i <= segments; i++)
            {
                float a = i * Mathf.PI * 2 / segments; var d = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                v.Add(d * outer + Vector3.up * height); v.Add(d * inner + Vector3.up * height);
                if (i < segments) { int k = i * 2; t.AddRange(new[] { k + 1, k + 3, k, k + 3, k + 2, k }); }
            }
            return MeshPart(root, name, p, v, t, mat);
        }

        private static Transform Label(Transform root, string text, Vector3 p, float width, float height, Color color, float yaw = 0)
        {
            var t = new GameObject(text.Split('\n')[0]).AddComponent<TextMeshPro>(); t.transform.SetParent(root, false); t.transform.localPosition = p; t.transform.localRotation = Quaternion.Euler(0, yaw, 0);
            t.font = TMP_Settings.GetFontAsset(); t.text = text; t.color = color; t.alignment = TextAlignmentOptions.Center; t.rectTransform.sizeDelta = new Vector2(width, height); t.raycastTarget = false;
            t.enableAutoSizing = true; t.fontSizeMin = 1; t.fontSizeMax = height * 6; t.overflowMode = TextOverflowModes.Ellipsis; t.ForceMeshUpdate(true, true); return t.transform;
        }

        private static void Bulb(Transform root, Vector3 p, float size = .3f)
        { var r = Box(root, "Expansion warm lamp glass", p, Vector3.one * size, warm).GetComponent<Renderer>(); bulbs.Add(r); }

        private static void LampPost(Transform root, Vector3 p, float height = 4.2f)
        {
            Box(root, "Park lamp post", p + Vector3.up * height * .5f, new Vector3(.14f, height, .14f), ink, true);
            Box(root, "Park lamp base", p + Vector3.up * .2f, new Vector3(.32f, .4f, .32f), ink);
            Bulb(root, p + Vector3.up * (height + .2f), .36f); Box(root, "Park lamp cap", p + Vector3.up * (height + .45f), new Vector3(.5f, .08f, .5f), ink);
        }

        private static void Tree(Transform root, Vector3 p, float size = 1, bool fruit = false)
        {
            var g = Group(root, fruit ? "Fruit tree" : "Park shade tree", p);
            float h = 3.2f * size;
            Box(g, "Tree trunk", Vector3.up * h * .5f, new Vector3(.36f * size, h, .36f * size), bark, true);
            var mat = random.NextDouble() < .4 ? leafDark : leaf;
            Ball(g, "Tree crown", Vector3.up * (h + 1.1f * size), new Vector3(3.6f, 2.8f, 3.6f) * size, mat);
            Ball(g, "Tree crown", new Vector3(.9f, h + .5f, .4f) * 1 + Vector3.zero, new Vector3(2.4f, 2f, 2.4f) * size, mat);
            Ball(g, "Tree crown", new Vector3(-.8f * size, h + .6f * size, -.6f * size), new Vector3(2.5f, 2.1f, 2.5f) * size, mat);
            if (fruit) { var detail = Group(g, "Close-up props", Vector3.zero); for (int i = 0; i < 9; i++) { float a = i * 2.39f; Ball(detail, "Hanging fruit", new Vector3(Mathf.Cos(a) * 1.5f * size, h + .6f + (i % 3) * .5f, Mathf.Sin(a) * 1.5f * size), Vector3.one * .22f, i % 2 == 0 ? fruitOrange : fruitRed, true); } }
        }

        private static void Bench(Transform root, Vector3 p, float yaw, float width = 2.4f)
        {
            var g = Group(root, "Timber public bench", p, yaw);
            for (int n = 0; n < 3; n++) Box(g, "Bench seat slat", new Vector3(0, .5f, -.2f + n * .17f), new Vector3(width, .07f, .13f), wood, n == 1);
            for (int n = 0; n < 2; n++) Box(g, "Bench back slat", new Vector3(0, .78f + n * .18f, .25f), new Vector3(width, .12f, .06f), wood, true);
            for (int s = -1; s <= 1; s += 2) Box(g, "Bench iron leg", new Vector3(s * width * .4f, .25f, 0), new Vector3(.08f, .5f, .5f), ink, true);
        }

        private static void Planter(Transform root, Vector3 p, Vector2 size)
        {
            Box(root, "Stone flower border", p + Vector3.up * .22f, new Vector3(size.x, .44f, size.y), cream, true);
            Box(root, "Flower bed soil", p + Vector3.up * .45f, new Vector3(size.x - .2f, .03f, size.y - .2f), soil);
            var detail = Group(root, "Close-up props", p);
            for (int i = 0; i < (int)(size.x * size.y * 2); i++)
            {
                float x = ((float)random.NextDouble() - .5f) * (size.x - .5f), z = ((float)random.NextDouble() - .5f) * (size.y - .5f);
                Ball(detail, "Garden flower", new Vector3(x, .6f, z), Vector3.one * .22f, i % 3 == 0 ? fruitRed : i % 3 == 1 ? seatYellow : white, true);
            }
            for (int i = 0; i < Mathf.Max(2, (int)size.x); i++) Ball(root, "Trimmed shrub", p + new Vector3(-size.x * .4f + i * size.x * .8f / Mathf.Max(1, (int)size.x - 1), .7f, 0), new Vector3(.8f, .6f, size.y * .7f), leafDark);
        }

        /// <summary>True when the footprint touches no existing (non-ground, non-road) collider.</summary>
        private static bool Clear(Rect area, float height = 2)
        {
            foreach (var c in Physics.OverlapBox(new Vector3(area.center.x, height * .5f + .2f, area.center.y), new Vector3(area.width * .5f, height * .5f, area.height * .5f)))
            {
                if (c.isTrigger || !c.enabled) continue; string n = c.name;
                if (GroundSurfaceStyle.SurfaceFor(n) == 0 || TownPopulationController.RoadName(n) || n.Contains("sidewalk") || n.Contains("curb") || n == "Continuous road physics") continue;
                return false;
            }
            return true;
        }

        /// <summary>Slides a driveway along the frontage until it finds a gap between street furniture.</summary>
        private static Rect FindGap(Rect start, Vector2 step, int tries = 12)
        {
            for (int i = 0; i < tries; i++) { var r = new Rect(start.position + step * i, start.size); if (Clear(r, 3)) return r; }
            return start;
        }

        // ================= Lakeside Park =================

        private static void Lakeside(Transform root)
        {
            var c = HavenExpansionLayout.LakeCentre;
            const int n = 96;
            // Water surface with depth in vertex colour (the lake shader shades deep water darker).
            var wv = new List<Vector3> { new Vector3(c.x, .03f, c.z) }; var wc = new List<Color> { new Color(1, 0, 0, 1) }; var wt = new List<int>(); var wu = new List<Vector2> { new Vector2(.5f, .5f) };
            const int rings = 10;
            for (int ring = 1; ring <= rings; ring++) for (int i = 0; i < n; i++)
                {
                    float k = ring / (float)rings; var p = HavenExpansionLayout.LakeOutline(i * Mathf.PI * 2 / n, k);
                    wv.Add(new Vector3(p.x, .03f, p.y)); wc.Add(new Color(1 - k, 0, 0, 1)); wu.Add(new Vector2((p.x - c.x) / 170 + .5f, (p.y - c.z) / 120 + .5f));
                }
            for (int i = 0; i < n; i++) wt.AddRange(new[] { 0, 1 + (i + 1) % n, 1 + i });
            for (int ring = 0; ring < rings - 1; ring++) for (int i = 0; i < n; i++) { int a = 1 + ring * n + i, b = 1 + ring * n + (i + 1) % n; wt.AddRange(new[] { a, b, a + n, b, b + n, a + n }); }
            MeshPart(root, "Park lake water surface", Vector3.zero, wv, wt, water, false, wu, wc);

            // Sandy shore band, then a low stone edge wall (gap at the pier), then the loop promenade.
            Band(root, "Lake sandy shore", 1.0f, 1.05f, .05f, sand, false);
            var shore = Group(root, "Lake stone edge", Vector3.zero);
            for (int i = 0; i < n; i++)
            {
                float a0 = i * Mathf.PI * 2 / n, a1 = (i + 1) * Mathf.PI * 2 / n; float mid = Mathf.Repeat((a0 + a1) * .5f + Mathf.PI, Mathf.PI * 2) - Mathf.PI;
                if (Mathf.Abs(mid) < .045f) continue; // pier opening
                var p0 = HavenExpansionLayout.LakeOutline(a0, 1.03f); var p1 = HavenExpansionLayout.LakeOutline(a1, 1.03f);
                var seg = Box(shore, "Lake edge stone wall", new Vector3((p0.x + p1.x) * .5f, .25f, (p0.y + p1.y) * .5f), new Vector3((p1 - p0).magnitude + .15f, .5f, .55f), stone, true);
                seg.transform.localRotation = Quaternion.Euler(0, -Mathf.Atan2(p1.y - p0.y, p1.x - p0.x) * Mathf.Rad2Deg, 0);
            }
            Band(root, "Lake loop promenade", 1.06f, 1.20f, .10f, stone, true);

            // Pier from the east shore into the lake, with a landing linking it to the promenade.
            var pier = Group(root, "Boating pier", Vector3.zero);
            float shoreX = HavenExpansionLayout.LakeOutline(0, 1.03f).x, pathX = HavenExpansionLayout.LakeOutline(0, 1.06f).x;
            Slab(pier, "Pier landing deck", Rect.MinMaxRect(shoreX - 1, c.z - 3, pathX + .5f, c.z + 3), .2f, wood);
            Slab(pier, "Pier timber deck", Rect.MinMaxRect(shoreX - 30, c.z - 1.6f, shoreX - .8f, c.z + 1.6f), .35f, wood, true, .18f);
            Slab(pier, "Pier end platform", Rect.MinMaxRect(shoreX - 38, c.z - 6, shoreX - 29, c.z + 6), .35f, wood, true, .18f);
            for (float x = shoreX - 37; x < shoreX - 1; x += 3) for (int s = -1; s <= 1; s += 2) Box(pier, "Pier post", new Vector3(x, -.1f, c.z + s * (x < shoreX - 29 ? 5.7f : 1.45f)), new Vector3(.22f, 1, .22f), darkWood);
            for (int s = -1; s <= 1; s += 2)
            {
                Box(pier, "Pier hand rail", new Vector3(shoreX - 15.5f, 1.25f, c.z + s * 1.55f), new Vector3(28, .08f, .08f), darkWood, true);
                Box(pier, "Platform hand rail", new Vector3(shoreX - 33.5f, 1.25f, c.z + s * 5.9f), new Vector3(9, .08f, .08f), darkWood, true);
                for (float x = shoreX - 29; x < shoreX - 1; x += 2.5f) Box(pier, "Pier rail baluster", new Vector3(x, .8f, c.z + s * 1.55f), new Vector3(.08f, .9f, .08f), darkWood);
            }
            Box(pier, "Platform end rail", new Vector3(shoreX - 37.9f, 1.25f, c.z), new Vector3(.08f, .08f, 12), darkWood, true);
            Bench(pier, new Vector3(shoreX - 34, .35f, c.z - 4.6f), 0); Bench(pier, new Vector3(shoreX - 34, .35f, c.z + 4.6f), 180);
            LampPost(pier, new Vector3(shoreX - 30, .35f, c.z + 5.4f), 3.6f); LampPost(pier, new Vector3(shoreX - 30, .35f, c.z - 5.4f), 3.6f);
            for (int i = 0; i < 3; i++) RowBoat(root, new Vector3(shoreX - 12 - i * 7, .02f, c.z + (i % 2 == 0 ? -4.5f : 4.5f)), i * 17);

            // Lake fountain and ducks.
            var fountain = Group(root, "Lake fountain jet", new Vector3(c.x, .03f, c.z));
            Cylinder(fountain, "Fountain nozzle", Vector3.zero, .6f, .4f, .3f, 10, metal);
            for (int i = 0; i < 6; i++)
            {
                float a = i * Mathf.PI / 3; var dir = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)); var pts = new List<Vector3>();
                for (int s = 0; s <= 12; s++) { float t = s / 12f; pts.Add(dir * t * 4.5f + Vector3.up * (.3f + 4.2f * Mathf.Sin(t * Mathf.PI) - t * .3f)); }
                Tube(fountain, "Fountain arc spray", pts, .06f, ripple);
            }
            Tube(fountain, "Fountain central jet", new List<Vector3> { Vector3.up * .3f, Vector3.up * 3, Vector3.up * 6.5f }, .12f, ripple);
            for (int i = 0; i < 7; i++) Duck(root, new Vector3(c.x + 30, .04f, c.z), i);

            // Gazebo on the north shore with a link path.
            float northZ = HavenExpansionLayout.LakeOutline(Mathf.PI / 2, 1.2f).y;
            Slab(root, "Gazebo link walk", Rect.MinMaxRect(c.x - 2, northZ - .5f, c.x + 2, northZ + 12), .105f, stone);
            Gazebo(root, new Vector3(c.x, 0, northZ + 18));

            // Picnic lawn to the west.
            float westX = HavenExpansionLayout.LakeOutline(Mathf.PI, 1.2f).x;
            Slab(root, "Picnic link walk", Rect.MinMaxRect(westX - 44, c.z - 2, westX + .5f, c.z + 2), .105f, stone);
            for (int i = 0; i < 6; i++) PicnicTable(root, new Vector3(westX - 20 - (i % 3) * 11, 0, c.z + (i < 3 ? 9 : -9)), i * 23);
            Planter(root, new Vector3(westX - 46, 0, c.z), new Vector2(3, 6));

            // Parking, driveway from the eastern avenue, entrance walk and cafe kiosk.
            var park = HavenExpansionLayout.LakeParking;
            var drive = FindGap(Rect.MinMaxRect(park.xMax - .5f, 338, 168.95f, 350), new Vector2(0, 6));
            Slab(root, "Lakeside car park", park, .08f, asphalt);
            Slab(root, "Lakeside access driveway", drive, .18f, asphalt, true, .2f);
            for (int i = 0; i < 9; i++) for (int s = 0; s < 2; s++) Box(root, "Parking bay line", new Vector3(park.xMin + 3 + i * 5, .085f, s == 0 ? park.yMin + 5 : park.yMax - 5), new Vector3(.12f, .01f, 10), lineWhite);
            
            Slab(root, "Lakeside entrance walk", Rect.MinMaxRect(HavenExpansionLayout.LakeOutline(-.18f, 1.18f).x, 321, park.xMin + .5f, 327), .105f, stone);
            Sign(root, new Vector3(park.xMax - 4, 0, drive.yMax + 3), "HAVEN LAKESIDE PARK", -90);
            Kiosk(root, new Vector3(60, 0, 306), "LAKE CAFE");
            Slab(root, "Cafe link walk", Rect.MinMaxRect(58, 310, 62, 321.5f), .1f, stone);

            // Lamps, benches and flower beds along the promenade; trees beyond it.
            for (int i = 0; i < 20; i++)
            {
                float a = i * Mathf.PI * 2 / 20 + .08f; var p = HavenExpansionLayout.LakeOutline(a, 1.215f);
                LampPost(root, new Vector3(p.x, .1f, p.y));
                var b = HavenExpansionLayout.LakeOutline(a + .16f, 1.205f);
                Bench(root, new Vector3(b.x, .1f, b.y), -Mathf.Atan2(c.z - b.y, c.x - b.x) * Mathf.Rad2Deg - 90);
            }
            for (int i = 0; i < 10; i++) { float a = i * Mathf.PI * 2 / 10 + .3f; var p = HavenExpansionLayout.LakeOutline(a, 1.3f); Planter(root, new Vector3(p.x, 0, p.y), new Vector2(3.5f, 1.6f)); }
            for (int i = 0; i < 28; i++)
            {
                float a = i * Mathf.PI * 2 / 28; if (Mathf.Abs(Mathf.DeltaAngle(a * Mathf.Rad2Deg, 0)) < 12 || Mathf.Abs(Mathf.DeltaAngle(a * Mathf.Rad2Deg, 90)) < 12 || Mathf.Abs(Mathf.DeltaAngle(a * Mathf.Rad2Deg, 180)) < 10 || Mathf.Abs(Mathf.DeltaAngle(a * Mathf.Rad2Deg, 345)) < 8) continue;
                var p = HavenExpansionLayout.LakeOutline(a, 1.42f); Tree(root, new Vector3(p.x, 0, p.y), .9f + (float)random.NextDouble() * .4f);
            }
            ScatterTrees(root, HavenExpansionLayout.LakesideArea, 46, p => HavenExpansionLayout.InLake(p, 1.55f) || HavenExpansionLayout.LakeParking.Contains(p) || Mathf.Abs(p.y - 324) < 6 && p.x > 0 || (p - new Vector2(60, 306)).magnitude < 14 || (p - new Vector2(c.x, northZ + 18)).magnitude < 14 || p.x < westX - 6 && p.x > westX - 60 && Mathf.Abs(p.y - c.z) < 18);
        }

        /// <summary>Flat ring between two scaled lake outlines.</summary>
        private static void Band(Transform root, string name, float inner, float outer, float top, Material mat, bool collider)
        {
            const int n = 96; var v = new List<Vector3>(); var t = new List<int>();
            for (int i = 0; i <= n; i++)
            {
                float a = i * Mathf.PI * 2 / n; var p0 = HavenExpansionLayout.LakeOutline(a, inner); var p1 = HavenExpansionLayout.LakeOutline(a, outer);
                v.Add(new Vector3(p0.x, top, p0.y)); v.Add(new Vector3(p1.x, top, p1.y));
                if (i < n) { int k = i * 2; t.AddRange(new[] { k, k + 2, k + 1, k + 2, k + 3, k + 1 }); }
            }
            MeshPart(root, name, Vector3.zero, v, t, mat, collider);
        }

        private static void Tube(Transform root, string name, List<Vector3> points, float radius, Material mat)
        {
            const int sides = 6; var v = new List<Vector3>(); var t = new List<int>();
            for (int i = 0; i < points.Count; i++)
            {
                var dir = (points[Mathf.Min(i + 1, points.Count - 1)] - points[Mathf.Max(0, i - 1)]).normalized;
                var right = Vector3.Cross(dir, Vector3.forward); if (right.sqrMagnitude < .01f) right = Vector3.right; right.Normalize(); var up = Vector3.Cross(right, dir).normalized;
                for (int s = 0; s <= sides; s++) { float a = s * Mathf.PI * 2 / sides; v.Add(points[i] + (Mathf.Cos(a) * right + Mathf.Sin(a) * up) * radius); if (i > 0 && s > 0) { int d = i * (sides + 1) + s, c = d - 1, b = d - sides - 1, e = b - 1; t.AddRange(new[] { e, c, b, c, d, b }); } }
            }
            MeshPart(root, name, Vector3.zero, v, t, mat);
        }

        private static void RowBoat(Transform root, Vector3 p, float yaw)
        {
            var boat = Group(root, "Rowing boat", p, yaw); bobbers.Add(boat); moving.Add(boat);
            Box(boat, "Boat hull floor", new Vector3(0, .12f, 0), new Vector3(1.3f, .14f, 3.6f), white);
            for (int s = -1; s <= 1; s += 2) Box(boat, "Boat hull side", new Vector3(s * .66f, .35f, 0), new Vector3(.1f, .5f, 3.6f), seatBlue);
            Box(boat, "Boat stern", new Vector3(0, .35f, -1.8f), new Vector3(1.4f, .5f, .1f), seatBlue); Box(boat, "Boat bow", new Vector3(0, .4f, 1.75f), new Vector3(.8f, .6f, .3f), seatBlue);
            Box(boat, "Boat seat", new Vector3(0, .42f, .2f), new Vector3(1.2f, .06f, .35f), wood);
            for (int s = -1; s <= 1; s += 2) Beam(boat, "Boat oar", new Vector3(s * .5f, .5f, .2f), new Vector3(s * 1.9f, .05f, .6f), .05f, wood);
        }

        private static void Duck(Transform root, Vector3 p, int i)
        {
            var duck = Group(root, "Lake duck " + i, p + new Vector3(i * 3, 0, i * 2)); swimmers.Add(duck); moving.Add(duck);
            Ball(duck, "Duck body", new Vector3(0, .13f, 0), new Vector3(.42f, .3f, .7f), i % 2 == 0 ? white : darkWood, true);
            Ball(duck, "Duck head", new Vector3(0, .36f, .3f), Vector3.one * .24f, i % 2 == 0 ? leafDark : darkWood, true);
            Ball(duck, "Duck beak", new Vector3(0, .32f, .47f), new Vector3(.12f, .06f, .18f), fruitOrange, true);
        }

        private static void Gazebo(Transform root, Vector3 p)
        {
            var g = Group(root, "Lakeside gazebo", p);
            Cylinder(g, "Gazebo floor", Vector3.zero, 6.5f, 6.5f, .4f, 8, stone);
            var floor = g.gameObject.AddComponent<BoxCollider>(); floor.center = Vector3.up * .2f; floor.size = new Vector3(12, .4f, 12);
            for (int i = 0; i < 8; i++) { float a = i * Mathf.PI / 4 + Mathf.PI / 8; Box(g, "Gazebo column", new Vector3(Mathf.Cos(a) * 5.8f, 2.1f, Mathf.Sin(a) * 5.8f), new Vector3(.3f, 3.4f, .3f), white, true); }
            Cylinder(g, "Gazebo roof", Vector3.up * 3.8f, 7.4f, .3f, 2.6f, 8, roofTile);
            Cylinder(g, "Gazebo fascia", Vector3.up * 3.6f, 7.2f, 7.2f, .3f, 8, white, false, false);
            for (int i = 0; i < 6; i++) { float a = i * Mathf.PI / 3; Bench(g, new Vector3(Mathf.Cos(a) * 4.3f, .4f, Mathf.Sin(a) * 4.3f), -a * Mathf.Rad2Deg - 90, 2.6f); }
            Bulb(g, Vector3.up * 3.4f, .45f);
        }

        private static void PicnicTable(Transform root, Vector3 p, float yaw)
        {
            var g = Group(root, "Picnic table", p, yaw);
            Box(g, "Picnic table top", new Vector3(0, .78f, 0), new Vector3(2.2f, .08f, .9f), wood, true);
            for (int s = -1; s <= 1; s += 2) { Box(g, "Picnic bench", new Vector3(0, .45f, s * .8f), new Vector3(2.2f, .07f, .32f), wood, true); Box(g, "Picnic frame", new Vector3(s * .9f, .38f, 0), new Vector3(.08f, .76f, 1.9f), darkWood); }
            Beam(g, "Umbrella pole", new Vector3(0, .8f, 0), new Vector3(0, 2.8f, 0), .06f, metal);
            Cylinder(g, "Picnic umbrella", Vector3.up * 2.4f, 1.6f, .05f, .5f, 8, canvas);
        }

        private static void Kiosk(Transform root, Vector3 p, string name)
        {
            var g = Group(root, name + " kiosk", p);
            Box(g, "Kiosk walls", new Vector3(0, 1.75f, 0), new Vector3(8, 3.5f, 5), cream, true);
            Box(g, "Kiosk roof", new Vector3(0, 3.65f, 0), new Vector3(9, .3f, 6), roofTile);
            Box(g, "Kiosk serving window", new Vector3(0, 1.7f, 2.52f), new Vector3(5, 1.2f, .05f), glass);
            Box(g, "Kiosk counter", new Vector3(0, 1.05f, 2.8f), new Vector3(5.4f, .1f, .6f), wood);
            
            Box(g, "Kiosk awning", new Vector3(0, 2.75f, 3.4f), new Vector3(6, .1f, 1.6f), canvas).transform.localRotation = Quaternion.Euler(-12, 0, 0);
            Box(g, "Kiosk sign board", new Vector3(0, 3.2f, 2.6f), new Vector3(5, .7f, .1f), ink);
            Label(g, name, new Vector3(0, 3.2f, 2.66f), 4.6f, .55f, white.color, 180);
            for (int i = -1; i <= 1; i += 2) { var t = Group(g, "Cafe table", new Vector3(i * 3, 0, 6)); Box(t, "Cafe table top", new Vector3(0, .75f, 0), new Vector3(1, .06f, 1), white, true); Box(t, "Cafe table leg", new Vector3(0, .37f, 0), new Vector3(.08f, .74f, .08f), metal); }
            Bulb(g, new Vector3(0, 2.5f, 2.7f));
        }

        private static void Sign(Transform root, Vector3 p, string text, float yaw)
        {
            var g = Group(root, "Entrance sign", p, yaw);
            for (int s = -1; s <= 1; s += 2) Box(g, "Sign stone pier", new Vector3(s * 2.6f, 1, 0), new Vector3(.6f, 2, .6f), stone, true);
            Box(g, "Sign board", new Vector3(0, 1.6f, 0), new Vector3(4.8f, 1, .2f), darkWood);
            Label(g, text, new Vector3(0, 1.6f, -.12f), 4.5f, .8f, white.color);
            Label(g, text, new Vector3(0, 1.6f, .12f), 4.5f, .8f, white.color, 180);
        }

        private static void ScatterTrees(Transform root, Rect area, int count, Func<Vector2, bool> excluded)
        {
            int placed = 0;
            for (int tries = 0; tries < count * 12 && placed < count; tries++)
            {
                var p = new Vector2(Mathf.Lerp(area.xMin + 8, area.xMax - 8, (float)random.NextDouble()), Mathf.Lerp(area.yMin + 8, area.yMax - 8, (float)random.NextDouble()));
                if (excluded(p) || !Clear(new Rect(p.x - 2.5f, p.y - 2.5f, 5, 5), 4)) continue;
                Tree(root, new Vector3(p.x, 0, p.y), .8f + (float)random.NextDouble() * .6f); placed++;
                Physics.SyncTransforms();
            }
        }

        // ================= Sports Park =================

        private static void SportsPark(Transform root)
        {
            var pc = HavenExpansionLayout.PitchCentre; float hx = HavenExpansionLayout.PitchHalfX, hz = HavenExpansionLayout.PitchHalfZ;
            var parking = HavenExpansionLayout.SportsParking;
            Slab(root, "Sports car park", parking, .08f, asphalt);
            for (int i = 0; i < 20; i++) for (int s = 0; s < 2; s++) Box(root, "Parking bay line", new Vector3(parking.xMin + 3 + i * 5.3f, .085f, s == 0 ? parking.yMin + 5 : parking.yMax - 5), new Vector3(.12f, .01f, 10), lineWhite);
            var drive = FindGap(Rect.MinMaxRect(84, parking.yMax - .5f, 96, -71.05f), new Vector2(6, 0));
            Slab(root, "Sports access driveway", Rect.MinMaxRect(drive.xMin, parking.yMax - .5f, drive.xMax, -74.2f), .08f, asphalt);
            Slab(root, "Sports driveway kerb ramp", Rect.MinMaxRect(drive.xMin, -74.2f, drive.xMax, -71.05f), .18f, asphalt, true, .2f);
            Sign(root, new Vector3(drive.xMax + 5, 0, -84), "HAVEN SPORTS PARK", 0);

            // Clubhouse.
            var club = Group(root, "Haven sports clubhouse", new Vector3(182, 0, -120));
            Box(club, "Clubhouse walls", new Vector3(0, 3.6f, 0), new Vector3(44, 7.2f, 22), cream, true);
            Box(club, "Clubhouse glazing band", new Vector3(0, 4.6f, -11.05f), new Vector3(40, 2.4f, .1f), glass);
            Box(club, "Clubhouse glazing band", new Vector3(0, 4.6f, 11.05f), new Vector3(40, 2.4f, .1f), glass);
            Box(club, "Clubhouse roof", new Vector3(0, 7.4f, 0), new Vector3(46, .4f, 24), roofTile);
            Box(club, "Clubhouse entrance canopy", new Vector3(-23, 3.4f, 0), new Vector3(4, .25f, 10), ink);
            Box(club, "Clubhouse doors", new Vector3(-22.05f, 1.3f, 0), new Vector3(.1f, 2.6f, 4), glass);
            Box(club, "Clubhouse sign band", new Vector3(0, 6.3f, 11.2f), new Vector3(24, 1.3f, .2f), seatBlue);
            Label(club, "HAVEN SPORTS CLUB", new Vector3(0, 6.3f, 11.35f), 23, 1.1f, white.color, 180);
            for (int i = 0; i < 3; i++) Flag(root, new Vector3(150 + i * 4, 0, -104), i == 0 ? seatRed : i == 1 ? seatBlue : seatYellow);
            Slab(root, "Clubhouse walk", Rect.MinMaxRect(parking.xMax - .5f, -122, 160, -118), .1f, stone);

            // Pitch with stripes, markings and goals.
            var pitch = Group(root, "Football stadium", Vector3.zero);
            StadiumBand(pitch, "Stadium infield surface", 0, HavenExpansionLayout.TrackInner + .1f, .07f, pitchB, true);
            for (int i = 0; i < 14; i++) Slab(pitch, "Football pitch stripe", Rect.MinMaxRect(pc.x - hx, pc.z - hz + i * hz * 2 / 14, pc.x + hx, pc.z - hz + (i + 1) * hz * 2 / 14), .075f, i % 2 == 0 ? pitchA : pitchB, false, .01f);
            void Line(Rect r) => Slab(pitch, "Pitch line marking", r, .082f, lineWhite, false, .01f);
            float w = .12f;
            Line(Rect.MinMaxRect(pc.x - hx, pc.z - hz, pc.x - hx + w, pc.z + hz)); Line(Rect.MinMaxRect(pc.x + hx - w, pc.z - hz, pc.x + hx, pc.z + hz));
            Line(Rect.MinMaxRect(pc.x - hx, pc.z - hz, pc.x + hx, pc.z - hz + w)); Line(Rect.MinMaxRect(pc.x - hx, pc.z + hz - w, pc.x + hx, pc.z + hz)); Line(Rect.MinMaxRect(pc.x - hx, pc.z - w * .5f, pc.x + hx, pc.z + w * .5f));
            Ring(pitch, "Centre circle marking", new Vector3(pc.x, .082f, pc.z), 9.15f, 9.03f, 0, lineWhite, 48);
            for (int s = -1; s <= 1; s += 2)
            {
                float gz = pc.z + s * hz;
                foreach (var (halfW, depth) in new[] { (20.15f, 16.5f), (9.16f, 5.5f) })
                {
                    float z0 = gz, z1 = gz - s * depth;
                    Line(Rect.MinMaxRect(pc.x - halfW, Mathf.Min(z0, z1), pc.x - halfW + w, Mathf.Max(z0, z1))); Line(Rect.MinMaxRect(pc.x + halfW - w, Mathf.Min(z0, z1), pc.x + halfW, Mathf.Max(z0, z1)));
                    Line(Rect.MinMaxRect(pc.x - halfW, Mathf.Min(z1, z1 + s * w), pc.x + halfW, Mathf.Max(z1, z1 + s * w)));
                }
                var goal = Group(pitch, "Football goal", new Vector3(pc.x, 0, gz), s > 0 ? 0 : 180);
                for (int side = -1; side <= 1; side += 2) Box(goal, "Goal post", new Vector3(side * 3.66f, 1.22f, 0), new Vector3(.12f, 2.44f, .12f), white, true);
                Box(goal, "Goal crossbar", new Vector3(0, 2.44f, 0), new Vector3(7.44f, .12f, .12f), white, true);
                Box(goal, "Goal net back", new Vector3(0, 1.22f, 2), new Vector3(7.3f, 2.44f, .03f), net); Box(goal, "Goal net roof", new Vector3(0, 2.44f, 1), new Vector3(7.3f, .03f, 2), net);
                for (int side = -1; side <= 1; side += 2) Box(goal, "Goal net side", new Vector3(side * 3.65f, 1.22f, 1), new Vector3(.03f, 2.44f, 2), net);
            }

            // Running track around the pitch with lane lines.
            StadiumBand(pitch, "Running track surface", HavenExpansionLayout.TrackInner, HavenExpansionLayout.TrackOuter, .085f, track, true);
            for (int lane = 1; lane < 6; lane++) { float r = HavenExpansionLayout.TrackInner + lane * (HavenExpansionLayout.TrackOuter - HavenExpansionLayout.TrackInner) / 6; StadiumBand(pitch, "Track lane line", r - .03f, r + .03f, .088f, lineWhite, false); }

            // Grandstands, floodlights and scoreboard.
            for (int s = -1; s <= 1; s += 2) Grandstand(root, new Vector3(pc.x + s * (HavenExpansionLayout.TrackOuter + 8), 0, pc.z), s);
            foreach (var corner in new[] { new Vector3(-1, 0, -1), new Vector3(1, 0, -1), new Vector3(-1, 0, 1), new Vector3(1, 0, 1) })
                Floodlight(root, new Vector3(pc.x + corner.x * (HavenExpansionLayout.TrackOuter + 4), 0, pc.z + corner.z * (hz + HavenExpansionLayout.TrackInner + 6)), corner);
            var board = Group(root, "Stadium scoreboard", new Vector3(pc.x, 0, pc.z + hz + HavenExpansionLayout.TrackOuter + 6));
            for (int s = -1; s <= 1; s += 2) Box(board, "Scoreboard leg", new Vector3(s * 6, 3, 0), new Vector3(.5f, 6, .5f), ink, true);
            Box(board, "Scoreboard panel", new Vector3(0, 7.5f, 0), new Vector3(15, 5, .5f), ink);
            Label(board, "HAVEN FC  2 - 1  VISITORS", new Vector3(0, 8.2f, -.3f), 14, 1.6f, seatYellow.color);
            Label(board, "SECOND HALF  67'", new Vector3(0, 6.4f, -.3f), 10, 1, white.color);

            // Courts with fences.
            for (int i = 0; i < 2; i++) BasketballCourt(root, new Vector3(314 + i * 36, 0, -119));
            for (int i = 0; i < 2; i++) TennisCourt(root, new Vector3(318, 0, -149 - i * 24));
            Slab(root, "Courts walk", Rect.MinMaxRect(204, -122, 296, -118), .1f, stone);
            for (int i = 0; i < 4; i++) Bench(root, new Vector3(300 + i * 18, 0, -134), 0);

            // Walks from the car park to the stadium.
            Slab(root, "Stadium approach walk", Rect.MinMaxRect(98, -252, 102, parking.yMin + .5f), .1f, stone);
            Slab(root, "Stadium approach walk", Rect.MinMaxRect(98, -252, pc.x - HavenExpansionLayout.TrackOuter - 15, -248), .1f, stone);
            for (int i = 0; i < 6; i++) LampPost(root, new Vector3(104, .1f, -160 - i * 18));
            ScatterTrees(root, HavenExpansionLayout.SportsArea, 40, p => p.x > pc.x - 70 && p.x < pc.x + 70 && p.y > pc.z - 110 && p.y < pc.z + 110 || p.x > 290 && p.y > -200 || HavenExpansionLayout.SportsParking.Contains(p) || p.x > 150 && p.x < 215 && p.y > -140 || Mathf.Abs(p.x - 100) < 6 || Mathf.Abs(p.y + 250) < 5 && p.x < 190 || p.y > -98);
        }

        private static void StadiumBand(Transform root, string name, float inner, float outer, float top, Material mat, bool collider)
        {
            var pc = HavenExpansionLayout.PitchCentre; float hz = HavenExpansionLayout.PitchHalfZ; var v = new List<Vector3>(); var t = new List<int>();
            const int arc = 24; var path = new List<(Vector3 dir, Vector3 centre)>();
            for (int i = 0; i <= arc; i++) { float a = Mathf.PI * i / arc; path.Add((new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)), pc + Vector3.forward * hz)); }
            for (int i = 0; i <= arc; i++) { float a = Mathf.PI + Mathf.PI * i / arc; path.Add((new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)), pc - Vector3.forward * hz)); }
            path.Add(path[0]);
            for (int i = 0; i < path.Count; i++)
            {
                v.Add(path[i].centre + path[i].dir * inner + Vector3.up * top); v.Add(path[i].centre + path[i].dir * outer + Vector3.up * top);
                if (i < path.Count - 1) { int k = i * 2; t.AddRange(new[] { k, k + 2, k + 1, k + 2, k + 3, k + 1 }); }
            }
            MeshPart(root, name, Vector3.zero, v, t, mat, collider);
        }

        private static void Grandstand(Transform root, Vector3 p, int side)
        {
            var g = Group(root, "Stadium grandstand", p, side > 0 ? 180 : 0);
            const int rows = 7; float length = 90;
            for (int r = 0; r < rows; r++)
            {
                Box(g, "Grandstand terrace step", new Vector3(-r * 1.4f, .35f + r * .55f, 0), new Vector3(1.4f, .7f + r * 1.1f, length), stone, true);
                var seats = r % 3 == 0 ? seatRed : r % 3 == 1 ? seatBlue : seatYellow;
                for (int k = 0; k < 2; k++) Box(g, "Grandstand seat row", new Vector3(-r * 1.4f + .1f, .7f + r * 1.1f + .17f, -length * .25f + k * length * .5f), new Vector3(.55f, .35f, length * .48f), seats);
            }
            Box(g, "Grandstand back wall", new Vector3(-rows * 1.4f, 4.5f, 0), new Vector3(.4f, 9, length), cream, true);
            for (int i = 0; i < 7; i++) Box(g, "Grandstand roof column", new Vector3(-rows * 1.4f + .4f, 6.5f, -length * .5f + 2 + i * (length - 4) / 6), new Vector3(.4f, 13, .4f), ink, true);
            Box(g, "Grandstand cantilever roof", new Vector3(-rows * .7f + .4f, 12.8f, 0), new Vector3(rows * 1.4f + 3, .35f, length + 2), metal).transform.localRotation = Quaternion.Euler(0, 0, -6);
            for (int i = 0; i < 5; i++) Bulb(g, new Vector3(-2, 12, -length * .4f + i * length * .2f), .5f);
        }

        private static void Floodlight(Transform root, Vector3 p, Vector3 corner)
        {
            var g = Group(root, "Stadium floodlight mast", p);
            Box(g, "Floodlight mast", Vector3.up * 12, new Vector3(.8f, 24, .8f), metal, true);
            var head = Group(g, "Floodlight head", Vector3.up * 24.5f, Mathf.Atan2(-corner.x, -corner.z) * Mathf.Rad2Deg);
            Box(head, "Floodlight frame", Vector3.zero, new Vector3(5, 2.6f, .4f), ink);
            for (int x = 0; x < 4; x++) for (int y = 0; y < 2; y++) Bulb(head, new Vector3(-1.8f + x * 1.2f, -.6f + y * 1.2f, .3f), .7f);
        }

        private static void Flag(Transform root, Vector3 p, Material color)
        {
            Box(root, "Flag pole", p + Vector3.up * 5, new Vector3(.12f, 10, .12f), metal, true);
            var flag = Group(root, "Waving flag", p + Vector3.up * 9.2f); flags.Add(flag); moving.Add(flag);
            Box(flag, "Flag cloth", new Vector3(1.1f, 0, 0), new Vector3(2.2f, 1.3f, .03f), color);
        }

        private static void Fence(Transform root, Rect r, float height, int gapSide)
        {
            var corners = new[] { new Vector3(r.xMin, 0, r.yMin), new Vector3(r.xMax, 0, r.yMin), new Vector3(r.xMax, 0, r.yMax), new Vector3(r.xMin, 0, r.yMax) };
            for (int side = 0; side < 4; side++)
            {
                var a = corners[side]; var b = corners[(side + 1) % 4]; var dir = b - a; float len = dir.magnitude; dir /= len;
                for (float d = 0; d <= len; d += 3) Box(root, "Fence post", a + dir * d + Vector3.up * height * .5f, new Vector3(.08f, height, .08f), ink);
                if (side == gapSide)
                {
                    float half = len * .5f - 1.6f;
                    foreach (var (s, e) in new[] { (0f, half), (len - half, len) }) Panel(root, a + dir * s, a + dir * e, height);
                }
                else Panel(root, a, b, height);
            }
        }

        private static void Panel(Transform root, Vector3 a, Vector3 b, float height)
        {
            var mid = (a + b) * .5f; float len = (b - a).magnitude; float yaw = -Mathf.Atan2(b.z - a.z, b.x - a.x) * Mathf.Rad2Deg;
            Box(root, "Chain link fence", mid + Vector3.up * height * .5f, new Vector3(len, height, .04f), net, true).transform.localRotation = Quaternion.Euler(0, yaw, 0);
            Box(root, "Fence top rail", mid + Vector3.up * height, new Vector3(len, .06f, .06f), ink).transform.localRotation = Quaternion.Euler(0, yaw, 0);
        }

        private static void BasketballCourt(Transform root, Vector3 p)
        {
            var g = Group(root, "Basketball court", Vector3.zero);
            var area = Rect.MinMaxRect(p.x - 16, p.z - 9.5f, p.x + 16, p.z + 9.5f);
            Slab(g, "Court surround surface", area, .08f, courtGreen); Slab(g, "Basketball court surface", Rect.MinMaxRect(p.x - 14, p.z - 7.5f, p.x + 14, p.z + 7.5f), .085f, courtBlue, false, .01f);
            Ring(g, "Court centre circle", new Vector3(p.x, .09f, p.z), 1.85f, 1.75f, 0, lineWhite, 32);
            Slab(g, "Court line marking", Rect.MinMaxRect(p.x - .05f, p.z - 7.5f, p.x + .05f, p.z + 7.5f), .09f, lineWhite, false, .01f);
            for (int s = -1; s <= 1; s += 2)
            {
                var hoop = Group(g, "Basketball hoop", new Vector3(p.x + s * 14.6f, 0, p.z), s > 0 ? -90 : 90);
                Box(hoop, "Hoop pole", new Vector3(0, 1.7f, 0), new Vector3(.18f, 3.4f, .18f), ink, true);
                Box(hoop, "Hoop arm", new Vector3(0, 3.3f, .5f), new Vector3(.12f, .12f, 1), ink);
                Box(hoop, "Hoop backboard", new Vector3(0, 3.5f, 1), new Vector3(1.8f, 1.05f, .05f), white);
                Ring(hoop, "Hoop rim", new Vector3(0, 3.05f, 1.3f), .25f, .22f, 0, fruitOrange, 16);
                Slab(g, "Court key marking", Rect.MinMaxRect(Mathf.Min(p.x + s * 14, p.x + s * 8.2f), p.z - 2.45f, Mathf.Max(p.x + s * 14, p.x + s * 8.2f), p.z + 2.45f), .088f, courtGreen, false, .01f);
            }
            Fence(g, area, 3.5f, 3);
            Bulb(g, new Vector3(p.x - 16, 4.2f, p.z - 9.5f)); Bulb(g, new Vector3(p.x + 16, 4.2f, p.z + 9.5f));
        }

        private static void TennisCourt(Transform root, Vector3 p)
        {
            var g = Group(root, "Tennis court", Vector3.zero);
            var area = Rect.MinMaxRect(p.x - 18, p.z - 9, p.x + 18, p.z + 9);
            Slab(g, "Tennis surround surface", area, .08f, courtGreen); Slab(g, "Tennis court surface", Rect.MinMaxRect(p.x - 11.9f, p.z - 5.5f, p.x + 11.9f, p.z + 5.5f), .085f, courtBlue, false, .01f);
            foreach (var z in new[] { -5.5f, -4.1f, 4.1f, 5.5f }) Slab(g, "Tennis line", Rect.MinMaxRect(p.x - 11.9f, p.z + z - .03f, p.x + 11.9f, p.z + z + .03f), .09f, lineWhite, false, .01f);
            foreach (var x in new[] { -11.9f, -6.4f, 6.4f, 11.9f }) Slab(g, "Tennis line", Rect.MinMaxRect(p.x + x - .03f, p.z - (Mathf.Abs(x) > 7 ? 5.5f : 4.1f), p.x + x + .03f, p.z + (Mathf.Abs(x) > 7 ? 5.5f : 4.1f)), .09f, lineWhite, false, .01f);
            for (int s = -1; s <= 1; s += 2) Box(g, "Tennis net post", new Vector3(p.x, .55f, p.z + s * 6.4f), new Vector3(.1f, 1.1f, .1f), ink, true);
            Box(g, "Tennis net", new Vector3(p.x, .5f, p.z), new Vector3(.03f, .95f, 12.8f), net);
            Box(g, "Tennis net tape", new Vector3(p.x, .98f, p.z), new Vector3(.05f, .06f, 12.8f), white);
            Fence(g, area, 3.5f, 0);
        }

        // ================= Valley Farm =================

        private static void FarmDistrict(Transform root)
        {
            var lane = FindGap(HavenExpansionLayout.FarmLane, new Vector2(0, 6));
            Slab(root, "Farm dirt lane", lane, .08f, dirtLane);
            Slab(root, "Farm lane kerb ramp", Rect.MinMaxRect(-338.95f, lane.yMin, lane.xMin + .5f, lane.yMax), .18f, dirtLane, true, .2f);
            var arch = Group(root, "Farm entrance arch", new Vector3(lane.xMin + 6, 0, lane.center.y));
            for (int s = -1; s <= 1; s += 2) Box(arch, "Arch timber post", new Vector3(0, 3, s * 5.5f), new Vector3(.45f, 6, .45f), darkWood, true);
            Box(arch, "Arch beam", new Vector3(0, 6.1f, 0), new Vector3(.5f, .5f, 11.6f), darkWood);
            Box(arch, "Arch name board", new Vector3(0, 5.2f, 0), new Vector3(.15f, 1, 7), wood);
            Label(arch, "HAVEN VALLEY FARM", new Vector3(-.1f, 5.2f, 0), 6.6f, .8f, white.color, 90);
            Label(arch, "HAVEN VALLEY FARM", new Vector3(.1f, 5.2f, 0), 6.6f, .8f, white.color, -90);
            for (float x = lane.xMin + 12; x < lane.xMax - 4; x += 3) for (int s = -1; s <= 1; s += 2) FencePost(root, new Vector3(x, 0, lane.center.y + s * 5.5f), x + 3 < lane.xMax - 4);
            for (int i = 0; i < 5; i++) LampPost(root, new Vector3(lane.xMin + 16 + i * 18, 0, lane.yMax + 1.2f), 3.4f);

            var yard = Rect.MinMaxRect(lane.xMax - .5f, -338, -172, -282);
            Slab(root, "Farm gravel yard", yard, .07f, gravel);
            Farmhouse(root, new Vector3(-244, 0, -272));
            Barn(root, new Vector3(-203, 0, -322));
            Cylinder(root, "Grain silo", new Vector3(-182, 0, -324), 3.6f, 3.6f, 15, 16, metal, true);
            var dome = Ball(root, "Silo dome", new Vector3(-182, 15, -324), new Vector3(7.4f, 4, 7.4f), metal);
            for (int i = 0; i < 5; i++) { var bale = Cylinder(root, "Round hay bale", new Vector3(-230 + i * 3.2f, .8f, -345), .8f, .8f, 1.2f, 12, wheat, false); bale.localRotation = Quaternion.Euler(0, 0, 90); bale.localPosition += new Vector3(.6f, 0, 0); var col = bale.gameObject.AddComponent<BoxCollider>(); col.center = new Vector3(0, .6f, 0); col.size = new Vector3(1.6f, 1.2f, 1.6f); }
            Tractor(root, new Vector3(-224, 0, -320));
            Windmill(root, HavenExpansionLayout.WindmillPosition);

            // Fields.
            var wheatField = Rect.MinMaxRect(-326, -414, -262, -340);
            Slab(root, "Farm soil bed", wheatField, .06f, soil, true, .08f);
            for (float z = wheatField.yMin + 1; z < wheatField.yMax - .5f; z += 1.25f) { Box(root, "Wheat crop row", new Vector3(wheatField.center.x, .5f, z), new Vector3(wheatField.width - 2, .9f, .75f), wheat); }
            Scarecrow(root, new Vector3(-294, 0, -377));
            var cornField = Rect.MinMaxRect(-252, -414, -186, -354);
            Slab(root, "Farm soil bed", cornField, .06f, soil, true, .08f);
            for (float z = cornField.yMin + 1; z < cornField.yMax - .5f; z += 1.6f) { Box(root, "Maize crop row", new Vector3(cornField.center.x, 1, z), new Vector3(cornField.width - 2, 1.9f, .6f), corn); Box(root, "Maize tassel row", new Vector3(cornField.center.x, 2.05f, z), new Vector3(cornField.width - 2, .2f, .3f), wheat); }
            var vegField = Rect.MinMaxRect(-178, -414, -122, -354);
            Slab(root, "Farm soil bed", vegField, .06f, soil, true, .08f);
            var crops = Group(root, "Close-up props", Vector3.zero);
            for (float z = vegField.yMin + 1.5f; z < vegField.yMax - 1; z += 2.2f) for (float x = vegField.xMin + 1.5f; x < vegField.xMax - 1; x += 1.3f) Ball(crops, "Cabbage plant", new Vector3(x, .3f, z), new Vector3(.75f, .5f, .75f), leaf, true);
            foreach (var f in new[] { wheatField, cornField, vegField }) FieldFence(root, f);

            // Orchard.
            for (int ix = 0; ix < 7; ix++) for (int iz = 0; iz < 5; iz++) Tree(root, new Vector3(-158 + ix * 14, 0, -322 + iz * 16), .75f, true);

            // Cow paddock.
            var pad = HavenExpansionLayout.Paddock;
            FieldFence(root, pad);
            Box(root, "Water trough", new Vector3(pad.xMin + 6, .4f, pad.yMax - 6), new Vector3(3, .8f, 1), metal, true);
            var shed = Group(root, "Paddock shelter", new Vector3(pad.xMax - 8, 0, pad.yMax - 7));
            for (int x = -1; x <= 1; x += 2) for (int z = -1; z <= 1; z += 2) Box(shed, "Shelter post", new Vector3(x * 4, 1.5f, z * 2.5f), new Vector3(.25f, 3, .25f), darkWood, true);
            Box(shed, "Shelter roof", new Vector3(0, 3.1f, 0), new Vector3(9, .2f, 6), roofTile).transform.localRotation = Quaternion.Euler(6, 0, 0);
            for (int i = 0; i < 5; i++) Cow(root, new Vector3(pad.xMin + 12 + i * 9, 0, pad.yMin + 15 + (i % 2) * 18), i);
            ScatterTrees(root, HavenExpansionLayout.FarmArea, 18, p => p.y < -334 && p.x > -330 || p.x > -170 && p.y > -338 && p.y < -232 || Mathf.Abs(p.y - lane.center.y) < 9 && p.x < lane.xMax + 2 || yard.Contains(p) || (p - new Vector2(HavenExpansionLayout.WindmillPosition.x, HavenExpansionLayout.WindmillPosition.z)).magnitude < 14 || p.x > -264 && p.x < -226 && p.y > -292 && p.y < -256 || p.y < -338 && p.y > -350 && p.x > -236 && p.x < -210);
        }

        private static void FencePost(Transform root, Vector3 p, bool rail)
        {
            Box(root, "Farm fence post", p + Vector3.up * .6f, new Vector3(.16f, 1.2f, .16f), darkWood);
            if (rail) for (int r = 0; r < 2; r++) Box(root, "Farm fence rail", p + new Vector3(1.5f, .45f + r * .5f, 0), new Vector3(3, .1f, .08f), darkWood, r == 1);
        }

        private static void FieldFence(Transform root, Rect r)
        {
            var corners = new[] { new Vector3(r.xMin, 0, r.yMin), new Vector3(r.xMax, 0, r.yMin), new Vector3(r.xMax, 0, r.yMax), new Vector3(r.xMin, 0, r.yMax) };
            for (int side = 0; side < 4; side++)
            {
                var a = corners[side]; var b = corners[(side + 1) % 4]; var mid = (a + b) * .5f; float len = (b - a).magnitude; float yaw = -Mathf.Atan2(b.z - a.z, b.x - a.x) * Mathf.Rad2Deg;
                for (int rr = 0; rr < 2; rr++)
                {
                    // Leave a gate opening in the middle of the side facing the farm yard.
                    if (side == 2) { float half = len * .5f - 2; foreach (var off in new[] { -1, 1 }) { var seg = Box(root, "Farm fence rail", mid + (b - a).normalized * off * (len * .5f - half * .5f) + Vector3.up * (.45f + rr * .5f), new Vector3(half, .1f, .08f), darkWood, rr == 1); seg.transform.localRotation = Quaternion.Euler(0, yaw, 0); } }
                    else Box(root, "Farm fence rail", mid + Vector3.up * (.45f + rr * .5f), new Vector3(len, .1f, .08f), darkWood, rr == 1).transform.localRotation = Quaternion.Euler(0, yaw, 0);
                }
                var dir = (b - a) / len; for (float d = 0; d <= len; d += 3.5f) Box(root, "Farm fence post", a + dir * d + Vector3.up * .6f, new Vector3(.16f, 1.2f, .16f), darkWood);
            }
        }

        private static void Gable(Transform root, string name, Vector3 centre, float width, float length, float rise, Material mat)
        {
            float hw = width * .5f + .5f, hl = length * .5f + .5f;
            var v = new List<Vector3> { new Vector3(-hw, 0, -hl), new Vector3(0, rise, -hl), new Vector3(hw, 0, -hl), new Vector3(-hw, 0, hl), new Vector3(0, rise, hl), new Vector3(hw, 0, hl) };
            var t = new List<int> { 0, 1, 4, 0, 4, 3, 1, 2, 5, 1, 5, 4, 0, 2, 1, 3, 4, 5, 0, 1, 4, 0, 4, 3 };
            // Separate vertices per face for crisp shading.
            var fv = new List<Vector3>(); var ft = new List<int>();
            var middle = new Vector3(0, rise * .35f, 0);
            for (int i = 0; i < 18; i += 3)
            {
                Vector3 a = v[t[i]], b = v[t[i + 1]], c = v[t[i + 2]];
                // Wind each face so its normal points away from the roof centre (Unity: normal = cross(b-a, c-a)).
                if (Vector3.Dot(Vector3.Cross(b - a, c - a), (a + b + c) / 3 - middle) < 0) { var swap = b; b = c; c = swap; }
                fv.Add(a); fv.Add(b); fv.Add(c); ft.AddRange(new[] { i, i + 1, i + 2 });
            }
            MeshPart(root, name, centre, fv, ft, mat);
        }

        private static void Farmhouse(Transform root, Vector3 p)
        {
            var g = Group(root, "Farmhouse", p);
            Box(g, "Farmhouse walls", new Vector3(0, 2.6f, 0), new Vector3(14, 5.2f, 10), cream, true);
            Gable(g, "Farmhouse roof", new Vector3(0, 5.2f, 0), 14, 10, 3.2f, barnRed);
            Box(g, "Farmhouse porch deck", new Vector3(0, .2f, -6.2f), new Vector3(10, .4f, 2.6f), wood, true);
            for (int s = -2; s <= 2; s++) Box(g, "Porch column", new Vector3(s * 2.4f, 1.6f, -7.3f), new Vector3(.2f, 2.8f, .2f), white);
            Box(g, "Porch roof", new Vector3(0, 3.1f, -6.4f), new Vector3(10.6f, .15f, 3), roofTile);
            Box(g, "Farmhouse door", new Vector3(0, 1.2f, -5.03f), new Vector3(1.2f, 2.3f, .06f), darkWood);
            for (int s = -1; s <= 1; s += 2) { Box(g, "Farmhouse window", new Vector3(s * 4, 2.6f, -5.03f), new Vector3(1.6f, 1.4f, .06f), glass); Box(g, "Window shutters", new Vector3(s * 4, 2.6f, -5.05f), new Vector3(2.4f, 1.5f, .04f), seatBlue); Box(g, "Farmhouse window", new Vector3(s * 3.5f, 2.6f, 5.03f), new Vector3(1.6f, 1.4f, .06f), glass); }
            Box(g, "Farmhouse chimney", new Vector3(4.5f, 7.5f, 2), new Vector3(1, 3, 1), stone);
            Bulb(g, new Vector3(1.2f, 2.5f, -5.2f));
            Box(g, "Farm mailbox", new Vector3(-6, 1.1f, -10), new Vector3(.5f, .4f, .7f), seatRed); Box(g, "Mailbox post", new Vector3(-6, .5f, -10), new Vector3(.1f, 1, .1f), darkWood);
        }

        private static void Barn(Transform root, Vector3 p)
        {
            var g = Group(root, "Red barn", p);
            Box(g, "Barn walls", new Vector3(0, 3.5f, 0), new Vector3(16, 7, 24), barnRed, true);
            Gable(g, "Barn roof", new Vector3(0, 7, 0), 16, 24, 5, roofTile);
            foreach (var z in new[] { -12.03f, 12.03f })
            {
                Box(g, "Barn door", new Vector3(0, 2.6f, z), new Vector3(6, 5.2f, .06f), barnRed);
                Box(g, "Barn door trim", new Vector3(0, 2.6f, z * 1.001f), new Vector3(6.3f, .25f, .05f), white).transform.localRotation = Quaternion.Euler(0, 0, 40);
                Box(g, "Barn door trim", new Vector3(0, 2.6f, z * 1.001f), new Vector3(6.3f, .25f, .05f), white).transform.localRotation = Quaternion.Euler(0, 0, -40);
                Box(g, "Barn door frame", new Vector3(0, 5.3f, z * 1.001f), new Vector3(6.4f, .25f, .05f), white);
                Box(g, "Barn loft window", new Vector3(0, 8.3f, z * 1.001f), new Vector3(2, 1.6f, .05f), white);
            }
            Bulb(g, new Vector3(0, 5.8f, -12.3f));
        }

        private static void Tractor(Transform root, Vector3 p)
        {
            var g = Group(root, "Farm tractor", p, 25);
            Box(g, "Tractor body", new Vector3(0, 1.3f, .6f), new Vector3(1.3f, 1, 2.6f), corn, true);
            Box(g, "Tractor cab", new Vector3(0, 2.4f, -.6f), new Vector3(1.5f, 1.6f, 1.4f), glass); Box(g, "Tractor cab roof", new Vector3(0, 3.25f, -.6f), new Vector3(1.6f, .1f, 1.5f), corn);
            Box(g, "Tractor exhaust", new Vector3(.4f, 2.6f, 1.4f), new Vector3(.1f, 1.2f, .1f), ink);
            foreach (var (z, r, w) in new[] { (-1f, .95f, .55f), (1.5f, .55f, .35f) }) for (int s = -1; s <= 1; s += 2) { var wheel = Cylinder(g, "Tractor tyre", new Vector3(s * (.75f + w * .5f), r, z), r, r, w, 14, ink); wheel.localRotation = Quaternion.Euler(0, 0, s * 90); wheel.localPosition = new Vector3(s * .75f, r, z); }
        }

        private static void Windmill(Transform root, Vector3 p)
        {
            var g = Group(root, "Farm windmill", p);
            Cylinder(g, "Windmill tower", Vector3.zero, 3.4f, 2.2f, 12, 8, white, true);
            Cylinder(g, "Windmill cap", Vector3.up * 12, 2.6f, .2f, 2.4f, 8, barnRed);
            Box(g, "Windmill door", new Vector3(0, 1.1f, -3.25f), new Vector3(1.2f, 2.2f, .1f), darkWood);
            var hub = Group(g, "Windmill rotor", new Vector3(0, 11.6f, -3.1f)); spinners.Add(hub); moving.Add(hub);
            Ball(hub, "Rotor hub", Vector3.zero, Vector3.one * .7f, darkWood, true);
            for (int i = 0; i < 4; i++)
            {
                var arm = Group(hub, "Windmill sail arm", Vector3.zero); arm.localRotation = Quaternion.Euler(0, 0, i * 90);
                Box(arm, "Sail spar", new Vector3(0, 4.2f, -.1f), new Vector3(.22f, 8.4f, .15f), darkWood);
                Box(arm, "Sail lattice", new Vector3(.75f, 4.8f, -.12f), new Vector3(1.3f, 6.4f, .04f), cream);
            }
        }

        private static void Scarecrow(Transform root, Vector3 p)
        {
            var g = Group(root, "Field scarecrow", p);
            Box(g, "Scarecrow pole", Vector3.up * 1.2f, new Vector3(.1f, 2.4f, .1f), darkWood); Box(g, "Scarecrow arms", Vector3.up * 1.8f, new Vector3(1.8f, .08f, .08f), darkWood);
            Box(g, "Scarecrow shirt", Vector3.up * 1.6f, new Vector3(.8f, .8f, .3f), seatBlue); Ball(g, "Scarecrow head", Vector3.up * 2.3f, Vector3.one * .45f, wheat, true);
            Cylinder(g, "Scarecrow hat", Vector3.up * 2.45f, .45f, .2f, .35f, 10, darkWood);
        }

        private static void Cow(Transform root, Vector3 p, int i)
        {
            var cow = Group(root, "Grazing cow " + i, p, i * 70f); grazers.Add(cow); moving.Add(cow);
            var body = cow.gameObject.AddComponent<BoxCollider>(); body.center = new Vector3(0, 1, 0); body.size = new Vector3(.9f, 1.4f, 2.2f);
            var rb = cow.gameObject.AddComponent<Rigidbody>(); rb.isKinematic = true;
            Box(cow, "Cow body", new Vector3(0, 1.15f, 0), new Vector3(.9f, .85f, 1.9f), i % 2 == 0 ? cowWhite : darkWood);
            Box(cow, "Cow patch", new Vector3(.46f, 1.2f, .2f), new Vector3(.02f, .5f, .7f), cowBlack); Box(cow, "Cow patch", new Vector3(-.46f, 1.1f, -.4f), new Vector3(.02f, .45f, .6f), cowBlack);
            Box(cow, "Cow head", new Vector3(0, 1.3f, 1.2f), new Vector3(.5f, .55f, .6f), i % 2 == 0 ? cowWhite : darkWood); Box(cow, "Cow muzzle", new Vector3(0, 1.15f, 1.52f), new Vector3(.42f, .3f, .2f), fruitRed);
            for (int s = -1; s <= 1; s += 2) { Box(cow, "Cow horn", new Vector3(s * .28f, 1.65f, 1.15f), new Vector3(.25f, .07f, .07f), white); for (int e = -1; e <= 1; e += 2) Box(cow, "Cow leg", new Vector3(s * .3f, .37f, e * .7f), new Vector3(.18f, .74f, .18f), i % 2 == 0 ? cowWhite : darkWood); }
            Box(cow, "Cow tail", new Vector3(0, .95f, -1f), new Vector3(.06f, .8f, .06f), cowBlack);
        }

        // ================= Batching and lights =================

        private static bool Moving(Transform t) => moving.Any(m => m != null && t.IsChildOf(m));
        private static bool Near(Transform t, Transform root) { for (var p = t; p != null && p != root; p = p.parent) if (p.name == "Close-up props") return true; return false; }

        private static Mesh Save(Mesh mesh, string path)
        {
            var old = AssetDatabase.LoadAssetAtPath<Mesh>(path); if (old == null) { AssetDatabase.CreateAsset(mesh, path); return mesh; }
            old.Clear(); EditorUtility.CopySerialized(mesh, old); old.UploadMeshData(false); EditorUtility.SetDirty(old); UnityEngine.Object.DestroyImmediate(mesh); return old;
        }

        private static void Combine(Transform root)
        {
            var filters = root.GetComponentsInChildren<MeshFilter>().Where(f => f.GetComponent<TMP_Text>() == null && f.GetComponent<MeshRenderer>() != null && !Moving(f.transform) && !bulbs.Contains(f.GetComponent<Renderer>())).ToArray();
            // Chunk the district into 80 m cells so distant parts can be culled independently.
            foreach (var group in filters.GroupBy(f => new { Material = f.GetComponent<MeshRenderer>().sharedMaterial, Detail = Near(f.transform, root), Cell = new Vector2Int(Mathf.FloorToInt(f.GetComponent<MeshRenderer>().bounds.center.x / 80), Mathf.FloorToInt(f.GetComponent<MeshRenderer>().bounds.center.z / 80)) }))
            {
                var mesh = new Mesh { name = "Expansion " + group.Key.Material.name, indexFormat = IndexFormat.UInt32 };
                mesh.CombineMeshes(group.Select(f => new CombineInstance { mesh = f.sharedMesh, transform = root.worldToLocalMatrix * f.transform.localToWorldMatrix }).ToArray(), true, true);
                mesh.RecalculateBounds(); var saved = Save(mesh, Folder + "/" + root.name.Split(' ')[0] + "Batch" + (meshIndex++) + ".asset");
                var go = new GameObject((group.Key.Detail ? "Expansion near batch " : "Expansion static batch ") + group.Key.Material.name, typeof(MeshFilter), typeof(MeshRenderer));
                go.transform.SetParent(root, false); go.GetComponent<MeshFilter>().sharedMesh = saved; go.GetComponent<MeshRenderer>().sharedMaterial = group.Key.Material;
                foreach (var f in group) { var original = f.gameObject; UnityEngine.Object.DestroyImmediate(f.GetComponent<MeshRenderer>()); UnityEngine.Object.DestroyImmediate(f); if (original.transform.childCount == 0 && original.GetComponents<Component>().Length == 1) UnityEngine.Object.DestroyImmediate(original); }
            }
            foreach (var f in root.GetComponentsInChildren<MeshFilter>()) if (f.sharedMesh != null && !AssetDatabase.Contains(f.sharedMesh)) f.sharedMesh = Save(UnityEngine.Object.Instantiate(f.sharedMesh), Folder + "/Fixture" + (meshIndex++) + ".asset");
        }

        private static void RegisterLights(MotelGameRoot motel)
        {
            var lighting = motel.GetComponentInChildren<WorldNightLighting>(); if (lighting == null) return;
            var so = new SerializedObject(lighting); var fixtures = so.FindProperty("fixtures"); var surfaces = so.FindProperty("glowingSurfaces"); int a = fixtures.arraySize, b = surfaces.arraySize;
            fixtures.arraySize += bulbs.Count; surfaces.arraySize += bulbs.Count;
            for (int n = 0; n < bulbs.Count; n++) { fixtures.GetArrayElementAtIndex(a + n).objectReferenceValue = bulbs[n].transform; surfaces.GetArrayElementAtIndex(b + n).objectReferenceValue = bulbs[n]; }
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
