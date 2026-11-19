using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace RoadsideHaven
{
    public enum TreeSpecies { Broadleaf, Blossom, Pine, Palm }

    /// <summary>
    /// Procedural, photographic-style trees: tapered and curved bark trunks with root flare, branching
    /// limbs, hundreds of alpha-cut leaf cards with crown-shaped normals, layered pine whorls, and palms
    /// with ringed trunks, folded drooping fronds, dead fronds and coconuts. Every texture (leaf clusters,
    /// blossom, needles, fronds and bark) is painted at start-up, so the trees need no imported assets.
    /// Meshes are shared per species / variant / LOD; submesh 0 is bark, submesh 1 foliage.
    /// </summary>
    public static class TreeArt
    {
        public const int Variants = 4;
        private static readonly Dictionary<int, Mesh> meshes = new Dictionary<int, Mesh>();
        private static Material bark, palmBark, leaves, needles, fronds;
        private static Texture2D leafAtlas, needleTex, frondTex, barkTex, palmBarkTex;

        /// <summary>Height of the generated mesh before scaling (metres).</summary>
        public static float CanonicalHeight(TreeSpecies s) => s == TreeSpecies.Pine ? 9.5f : s == TreeSpecies.Palm ? 7.6f : s == TreeSpecies.Blossom ? 6.6f : 8.4f;

        public static Texture2D LeafAtlas { get { EnsureTextures(); return leafAtlas; } }

        public static Material[] Materials(TreeSpecies s)
        {
            EnsureMaterials();
            switch (s)
            {
                case TreeSpecies.Pine: return new[] { bark, needles };
                case TreeSpecies.Palm: return new[] { palmBark, fronds };
                default: return new[] { bark, leaves };
            }
        }

        public static Mesh Get(TreeSpecies s, int variant, int lod)
        {
            int key = ((int)s * 16 + variant % Variants) * 4 + lod;
            if (meshes.TryGetValue(key, out var m) && m != null) return m;
            var b = new Builder(new System.Random(1009 * (int)s + 31 * variant + 7), lod);
            switch (s)
            {
                case TreeSpecies.Pine: b.Pine(variant); break;
                case TreeSpecies.Palm: b.Palm(variant); break;
                default: b.Broadleaf(variant, s == TreeSpecies.Blossom); break;
            }
            m = b.Mesh(s + " " + variant + " LOD" + lod);
            meshes[key] = m;
            return m;
        }

        // ---------------------------------------------------------------- materials & textures

        private static void EnsureMaterials()
        {
            if (bark != null) return;
            EnsureTextures();
            var shader = Resources.Load<Shader>("FoliageLit");
            Material Make(string name, Texture2D tex, bool leaf, bool seasonal, float cutoff, float translucency, float wind)
            {
                Material m;
                if (shader != null && shader.isSupported)
                {
                    m = new Material(shader) { name = name };
                    m.SetFloat("_Leaf", leaf ? 1 : 0); m.SetFloat("_Seasonal", seasonal ? 1 : 0); m.SetFloat("_Translucency", translucency); m.SetFloat("_WindScale", wind);
                }
                else
                {
                    m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
                    if (leaf) { m.SetFloat("_AlphaClip", 1); m.EnableKeyword("_ALPHATEST_ON"); m.SetFloat("_Cull", 0); }
                }
                m.SetTexture("_BaseMap", tex); m.SetFloat("_Cutoff", cutoff); m.enableInstancing = true;
                if (leaf) m.renderQueue = (int)RenderQueue.AlphaTest;
                return m;
            }
            bark = Make("Tree bark", barkTex, false, false, 0, 0, 1);
            palmBark = Make("Palm bark", palmBarkTex, false, false, 0, 0, 1);
            leaves = Make("Broadleaf canopy", leafAtlas, true, true, .42f, .6f, 1);
            needles = Make("Pine needles", needleTex, true, false, .4f, .35f, .7f);
            fronds = Make("Palm fronds", frondTex, true, false, .38f, .5f, 1.3f);
        }

        private static void EnsureTextures()
        {
            if (leafAtlas != null) return;
            leafAtlas = LeafClusterAtlas();
            needleTex = NeedleCard();
            frondTex = FrondCard();
            barkTex = BarkTexture(false);
            palmBarkTex = BarkTexture(true);
        }

        private static Texture2D NewTexture(string name, int w, int h, bool alpha)
        {
            return new Texture2D(w, h, alpha ? TextureFormat.RGBA32 : TextureFormat.RGB24, true) { name = name, wrapMode = alpha ? TextureWrapMode.Clamp : TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear, anisoLevel = 4 };
        }

        /// <summary>Uploads the pixels; for cut-out textures keeps the canopy dense in the smaller mip levels.</summary>
        private static void Finish(Texture2D t, Color32[] px, bool alpha)
        {
            t.SetPixels32(px); t.Apply(true, false);
            if (alpha)
            {
                for (int m = 1; m < t.mipmapCount; m++)
                {
                    var mip = t.GetPixels32(m); float boost = 1 + .32f * m;
                    for (int i = 0; i < mip.Length; i++) mip[i].a = (byte)Mathf.Min(255, mip[i].a * boost);
                    t.SetPixels32(mip, m);
                }
                t.Apply(false, true);
            }
            else t.Apply(false, true);
        }

        private static Color32[] Background(int count, Color c) { var px = new Color32[count]; Color32 b = c; b.a = 0; for (int i = 0; i < count; i++) px[i] = b; return px; }

        private static void Blend(Color32[] px, int w, int x, int y, Color c, float a)
        {
            int i = y * w + x; var d = px[i]; float k = Mathf.Clamp01(a);
            byte Ch(byte from, float to) => (byte)Mathf.Clamp(Mathf.Lerp(from, to * 255, k), 0, 255);
            px[i] = new Color32(Ch(d.r, c.r), Ch(d.g, c.g), Ch(d.b, c.b), (byte)Mathf.Max(d.a, k * 255));
        }

        private static void Line(Color32[] px, int w, int h, Vector2 a, Vector2 b, float width, Color c, Rect clip)
        {
            int x0 = Mathf.Max((int)clip.xMin, Mathf.FloorToInt(Mathf.Min(a.x, b.x) - width - 1)), x1 = Mathf.Min((int)clip.xMax - 1, Mathf.CeilToInt(Mathf.Max(a.x, b.x) + width + 1));
            int y0 = Mathf.Max((int)clip.yMin, Mathf.FloorToInt(Mathf.Min(a.y, b.y) - width - 1)), y1 = Mathf.Min((int)clip.yMax - 1, Mathf.CeilToInt(Mathf.Max(a.y, b.y) + width + 1));
            Vector2 ab = b - a; float len2 = Mathf.Max(ab.sqrMagnitude, .0001f);
            for (int y = y0; y <= y1; y++) for (int x = x0; x <= x1; x++)
            {
                var p = new Vector2(x + .5f, y + .5f); float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / len2);
                float d = Vector2.Distance(p, a + ab * t); float wt = width * Mathf.Lerp(1, .45f, t);
                if (d < wt + .7f) Blend(px, w, x, y, c, Mathf.Clamp01(wt + .7f - d));
            }
        }

        /// <summary>One leaf: pointed ellipse with a darker midrib, lighter on one half.</summary>
        private static void Leaf(Color32[] px, int w, Vector2 baseP, float angle, float length, float width, Color c, Rect clip, float roundness)
        {
            Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)), side = new Vector2(-dir.y, dir.x);
            Vector2 tip = baseP + dir * length;
            float ext = length + width;
            int x0 = Mathf.Max((int)clip.xMin, (int)(Mathf.Min(baseP.x, tip.x) - width - 2)), x1 = Mathf.Min((int)clip.xMax - 1, (int)(Mathf.Max(baseP.x, tip.x) + width + 2));
            int y0 = Mathf.Max((int)clip.yMin, (int)(Mathf.Min(baseP.y, tip.y) - width - 2)), y1 = Mathf.Min((int)clip.yMax - 1, (int)(Mathf.Max(baseP.y, tip.y) + width + 2));
            for (int y = y0; y <= y1; y++) for (int x = x0; x <= x1; x++)
            {
                var p = new Vector2(x + .5f, y + .5f) - baseP; float u = Vector2.Dot(p, dir) / length, v = Vector2.Dot(p, side);
                if (u < 0 || u > 1) continue;
                float half = width * Mathf.Pow(Mathf.Sin(Mathf.PI * Mathf.Pow(u, roundness)), .75f);
                float edge = half - Mathf.Abs(v); if (edge < -.6f) continue;
                float shade = (v > 0 ? 1.07f : .93f) * Mathf.Lerp(.86f, 1.06f, u) * (Mathf.Abs(v) < .9f ? .8f : 1) * (edge < 1.2f ? .88f : 1);
                // Fine lateral veins.
                float vein = Mathf.Abs(Mathf.Sin((u * length - Mathf.Abs(v) * .9f) * .55f));
                if (vein < .12f && Mathf.Abs(v) > 1) shade *= .93f;
                Blend(px, w, x, y, c * shade, Mathf.Clamp01(edge + .6f));
            }
            _ = ext;
        }

        private static Texture2D LeafClusterAtlas()
        {
            const int S = 512, C = 256; var px = Background(S * S, new Color(.24f, .38f, .13f));
            var r = new System.Random(77);
            float F(float a, float b) => a + (float)r.NextDouble() * (b - a);
            for (int cell = 0; cell < 4; cell++)
            {
                var clip = new Rect((cell % 2) * C + 3, (cell / 2) * C + 3, C - 6, C - 6);
                Vector2 center = clip.center; bool blossom = cell == 3;
                // Twigs radiating from the lower middle of the card.
                Vector2 root = new Vector2(center.x, clip.yMin + 22);
                int twigs = 5; var tips = new List<Vector2>();
                for (int t = 0; t < twigs; t++)
                {
                    float a = Mathf.Lerp(.35f, Mathf.PI - .35f, (t + F(-.2f, .2f)) / (twigs - 1));
                    var tip = root + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * F(80, 120);
                    Line(px, S, S, root, tip, 2.4f, new Color(.27f, .2f, .13f), clip); tips.Add(tip);
                }
                int count = blossom ? 26 : cell == 2 ? 70 : 46;
                for (int i = 0; i < count; i++)
                {
                    var tip = tips[r.Next(tips.Count)]; float along = F(.25f, 1.05f);
                    Vector2 at = Vector2.Lerp(root, tip, along) + new Vector2(F(-10, 10), F(-10, 10));
                    float ang = Mathf.Atan2(at.y - root.y, at.x - root.x) + F(-1.1f, 1.1f);
                    Color g = Color.Lerp(new Color(.2f, .36f, .1f), new Color(.38f, .55f, .17f), F(0, 1)) * F(.82f, 1.12f);
                    if (cell == 0) Leaf(px, S, at, ang, F(26, 36), F(9, 13), g, clip, 1);
                    else if (cell == 1) Leaf(px, S, at, ang, F(32, 44), F(7, 9), g * .92f, clip, .8f);
                    else if (cell == 2) Leaf(px, S, at, ang, F(15, 22), F(5, 7.5f), Color.Lerp(g, new Color(.42f, .5f, .16f), .3f), clip, 1.1f);
                    else Leaf(px, S, at, ang, F(20, 28), F(7, 9), g * .9f, clip, 1);
                }
                if (blossom)
                {
                    // Jacaranda-style blossom clusters over a little foliage.
                    for (int i = 0; i < 70; i++)
                    {
                        var tip = tips[r.Next(tips.Count)]; Vector2 at = Vector2.Lerp(root, tip, F(.45f, 1.05f)) + new Vector2(F(-16, 16), F(-16, 16));
                        Color petal = Color.Lerp(new Color(.62f, .42f, .82f), new Color(.86f, .55f, .78f), F(0, 1)) * F(.85f, 1.1f);
                        for (int k = 0; k < 5; k++) Leaf(px, S, at, k * 1.2566f + F(0, 1), F(7, 10), F(3.5f, 4.5f), petal, clip, 1);
                        Line(px, S, S, at, at + Vector2.one * .5f, 1.6f, new Color(.95f, .9f, .6f), clip);
                    }
                }
            }
            var atlas = NewTexture("Leaf cluster atlas", S, S, true); Finish(atlas, px, true); return atlas;
        }

        private static Texture2D NeedleCard()
        {
            const int S = 256; var px = Background(S * S, new Color(.1f, .24f, .15f)); var r = new System.Random(5); var clip = new Rect(2, 2, S - 4, S - 4);
            float F(float a, float b) => a + (float)r.NextDouble() * (b - a);
            var twig = new Color(.25f, .18f, .12f);
            // A fir branchlet: main twig up the middle, side twigs, dense needles.
            Line(px, S, S, new Vector2(128, 4), new Vector2(128, 250), 2.6f, twig, clip);
            var shoots = new List<(Vector2 a, Vector2 b)> { (new Vector2(128, 4), new Vector2(128, 250)) };
            for (int i = 0; i < 9; i++)
            {
                float y = 20 + i * 25; float side = i % 2 == 0 ? 1 : -1; var a = new Vector2(128, y); var b = a + new Vector2(side * F(60, 105), F(40, 70));
                Line(px, S, S, a, b, 1.6f, twig, clip); shoots.Add((a, b));
            }
            foreach (var s in shoots)
            {
                Vector2 d = (s.b - s.a); float len = d.magnitude; d /= len; Vector2 n = new Vector2(-d.y, d.x);
                for (float t = 0; t < len; t += 2.2f)
                    for (int k = -1; k <= 1; k += 2)
                    {
                        Vector2 p = s.a + d * t; Vector2 dir = (d * .55f + n * k * F(.7f, 1)).normalized;
                        Color c = Color.Lerp(new Color(.1f, .26f, .15f), new Color(.24f, .42f, .24f), F(0, 1)) * Mathf.Lerp(1.08f, .85f, t / len);
                        Line(px, S, S, p, p + dir * F(14, 22) * Mathf.Lerp(1, .6f, t / len), 1.1f, c, clip);
                    }
            }
            var tex = NewTexture("Needle card", S, S, true); Finish(tex, px, true); return tex;
        }

        private static Texture2D FrondCard()
        {
            const int W = 128, H = 512; var px = Background(W * H, new Color(.3f, .44f, .16f)); var r = new System.Random(11); var clip = new Rect(1, 1, W - 2, H - 2);
            float F(float a, float b) => a + (float)r.NextDouble() * (b - a);
            for (float y = 18; y < H - 6; y += 7.5f)
            {
                float t = y / H; float len = 58 * Mathf.Sin(Mathf.PI * Mathf.Pow(t, .8f)) * (1 - .25f * t) + 6;
                for (int k = -1; k <= 1; k += 2)
                {
                    if (r.NextDouble() < .06) continue; // the odd torn leaflet
                    Vector2 a = new Vector2(64 + k * 2, y), b = a + new Vector2(k * len * .93f, len * .38f + F(-3, 3));
                    Color c = Color.Lerp(new Color(.2f, .36f, .12f), new Color(.42f, .55f, .2f), F(0, 1)) * Mathf.Lerp(.95f, 1.08f, t);
                    if (t > .9f) c = Color.Lerp(c, new Color(.55f, .5f, .25f), .4f);
                    Line(px, W, H, a, b, 3.2f * Mathf.Lerp(1, .6f, t), c, clip);
                    Line(px, W, H, a + (b - a) * .1f, b, .8f, c * 1.18f, clip);
                }
            }
            Line(px, W, H, new Vector2(64, 0), new Vector2(64, H - 2), 2.8f, new Color(.52f, .5f, .26f), clip);
            var tex = NewTexture("Palm frond card", W, H, true); Finish(tex, px, true); return tex;
        }

        private static float Tile(float x, float y, float w, float h, float sx, float sy, float ox)
        {
            float N(float a, float b) => Mathf.PerlinNoise(ox + a * sx, ox * .7f + b * sy);
            return (N(x, y) * (w - x) * (h - y) + N(x - w, y) * x * (h - y) + N(x, y - h) * (w - x) * y + N(x - w, y - h) * x * y) / (w * h);
        }

        private static Texture2D BarkTexture(bool palm)
        {
            const int S = 256; var px = new Color32[S * S];
            for (int y = 0; y < S; y++) for (int x = 0; x < S; x++)
            {
                float v;
                Color c;
                if (!palm)
                {
                    float warp = Tile(x, y, S, S, .03f, .012f, 3) * 6;
                    float ridge = Mathf.Abs(Mathf.Sin((x / (float)S * Mathf.PI * 2) * 9 + warp));
                    float fine = Tile(x, y, S, S, .12f, .05f, 17);
                    v = Mathf.Pow(ridge, .6f) * .75f + fine * .35f;
                    c = Color.Lerp(new Color(.13f, .095f, .07f), new Color(.46f, .4f, .33f), v);
                }
                else
                {
                    float ring = Mathf.Repeat(y / 14f + Tile(x, y, S, S, .05f, .05f, 9) * .6f, 1);
                    float groove = Mathf.SmoothStep(0, 1, Mathf.Abs(ring - .5f) * 2);
                    float fibre = Tile(x, y, S, S, .2f, .03f, 23);
                    v = (1 - groove * .55f) * (.75f + fibre * .4f);
                    c = Color.Lerp(new Color(.24f, .2f, .16f), new Color(.58f, .52f, .43f), v);
                }
                px[y * S + x] = c;
            }
            var t = NewTexture(palm ? "Palm bark" : "Tree bark", S, S, false); Finish(t, px, false); return t;
        }

        // ---------------------------------------------------------------- geometry

        private sealed class Builder
        {
            readonly List<Vector3> v = new List<Vector3>(), n = new List<Vector3>();
            readonly List<Vector2> uv = new List<Vector2>();
            readonly List<Color> col = new List<Color>();
            readonly List<int> barkTris = new List<int>(), leafTris = new List<int>();
            readonly System.Random r; readonly int lod;
            public Builder(System.Random random, int lod) { r = random; this.lod = lod; }
            float F(float a, float b) => a + (float)r.NextDouble() * (b - a);
            Vector3 Jitter(float s) => new Vector3(F(-s, s), F(-s, s), F(-s, s));

            public Mesh Mesh(string name)
            {
                var m = new Mesh { name = name };
                if (v.Count > 65000) m.indexFormat = IndexFormat.UInt32;
                m.SetVertices(v); m.SetNormals(n); m.SetUVs(0, uv); m.SetColors(col);
                m.subMeshCount = 2; m.SetTriangles(barkTris, 0); m.SetTriangles(leafTris, 1);
                m.RecalculateBounds(); m.UploadMeshData(true); return m;
            }

            /// <summary>Tapered tube along a path, parallel-transported frames, bark UVs in metres.</summary>
            void Tube(IList<Vector3> path, IList<float> radius, float flex0, float flex1, int sides, Color tint, float uRepeat = 1, float vScale = .9f, float lumpy = .12f)
            {
                if (lod > 0) sides = Mathf.Max(5, sides / 2);
                Vector3 up = Mathf.Abs(Vector3.Dot((path[1] - path[0]).normalized, Vector3.up)) > .95f ? Vector3.right : Vector3.up;
                Vector3 prevT = (path[1] - path[0]).normalized; Vector3 side = Vector3.Cross(prevT, up).normalized;
                float length = 0; int start = v.Count;
                for (int i = 0; i < path.Count; i++)
                {
                    Vector3 t = i < path.Count - 1 ? (path[i + 1] - path[i]).normalized : (path[i] - path[i - 1]).normalized;
                    if (i > 0) { length += Vector3.Distance(path[i], path[i - 1]); var q = Quaternion.FromToRotation(prevT, t); side = q * side; }
                    prevT = t; Vector3 other = Vector3.Cross(t, side).normalized;
                    float f = Mathf.Lerp(flex0, flex1, i / (float)(path.Count - 1));
                    for (int s = 0; s <= sides; s++)
                    {
                        float a = s * Mathf.PI * 2 / sides; Vector3 dir = side * Mathf.Cos(a) + other * Mathf.Sin(a);
                        float bump = 1 + lumpy * (Mathf.PerlinNoise(a * 1.3f + i * .7f, length * .9f) - .5f);
                        v.Add(path[i] + dir * radius[i] * bump); n.Add(dir);
                        uv.Add(new Vector2(s / (float)sides * uRepeat, length * vScale));
                        float ao = Mathf.Lerp(.78f, 1, Mathf.Clamp01(path[i].y / 2.5f));
                        col.Add(new Color(tint.r * ao, tint.g * ao, tint.b * ao, f));
                    }
                }
                int ring = sides + 1;
                for (int i = 0; i < path.Count - 1; i++) for (int s = 0; s < sides; s++)
                {
                    int a = start + i * ring + s, b = a + ring;
                    barkTris.Add(a); barkTris.Add(a + 1); barkTris.Add(b); barkTris.Add(a + 1); barkTris.Add(b + 1); barkTris.Add(b);
                }
            }

            /// <summary>Leaf card: a quad with UVs from an atlas cell; normals bent towards the crown shape.</summary>
            void Card(Vector3 center, Vector3 right, Vector3 upDir, Vector2 size, Rect cell, Vector3 crown, float flex, Color tint, float normalBend = .7f)
            {
                int s = v.Count; Vector3 face = Vector3.Cross(right, upDir).normalized;
                Vector3[] corners = { center - right * size.x * .5f - upDir * size.y * .5f, center + right * size.x * .5f - upDir * size.y * .5f, center + right * size.x * .5f + upDir * size.y * .5f, center - right * size.x * .5f + upDir * size.y * .5f };
                Vector2[] uvs = { new Vector2(cell.xMin, cell.yMin), new Vector2(cell.xMax, cell.yMin), new Vector2(cell.xMax, cell.yMax), new Vector2(cell.xMin, cell.yMax) };
                for (int i = 0; i < 4; i++)
                {
                    Vector3 radial = (corners[i] - crown); radial = radial.sqrMagnitude > .001f ? radial.normalized : Vector3.up;
                    if (Vector3.Dot(face, radial) < 0) face = -face;
                    v.Add(corners[i]); n.Add(Vector3.Slerp(face, radial, normalBend).normalized); uv.Add(uvs[i]);
                    float edge = i >= 2 ? 1 : .82f;
                    col.Add(new Color(tint.r, tint.g, tint.b, Mathf.Clamp01(flex * edge)));
                }
                leafTris.Add(s); leafTris.Add(s + 2); leafTris.Add(s + 1); leafTris.Add(s); leafTris.Add(s + 3); leafTris.Add(s + 2);
            }

            /// <summary>A puff of foliage: several crossing cards around a point.</summary>
            void Cluster(Vector3 c, float size, Vector3 crown, Vector3 crownRadii, float flex, bool blossom)
            {
                int cards = lod == 0 ? 3 : 2; float sz = lod == 0 ? size : size * 1.45f;
                Vector3 rel = Vector3.Scale(c - crown, new Vector3(1 / crownRadii.x, 1 / crownRadii.y, 1 / crownRadii.z));
                float outer = Mathf.Clamp01(rel.magnitude);
                float ao = Mathf.Lerp(.5f, 1.05f, outer) * Mathf.Lerp(.72f, 1.05f, Mathf.Clamp01(rel.y * .5f + .55f));
                float hue = F(.86f, 1.12f);
                for (int k = 0; k < cards; k++)
                {
                    var q = Quaternion.Euler(F(-35, 35), F(0, 360), F(-25, 25));
                    Vector3 right = q * Vector3.right, upDir = q * Vector3.up;
                    int cellIndex = blossom ? (r.NextDouble() < .7 ? 3 : r.Next(2)) : r.Next(3);
                    var cell = new Rect((cellIndex % 2) * .5f + .004f, (cellIndex / 2) * .5f + .004f, .492f, .492f);
                    Color tint = new Color(ao * hue * F(.94f, 1.04f), ao * hue, ao * F(.9f, 1.05f));
                    Card(c + Jitter(size * .12f), right, upDir, new Vector2(sz, sz) * F(.85f, 1.15f), cell, crown, flex, tint);
                }
            }

            // ----- broadleaf / blossom

            public void Broadleaf(int variant, bool blossom)
            {
                float H = blossom ? 6.6f : 8.4f, split = H * F(.3f, .38f), lean = F(-.25f, .25f);
                Vector3 crown = new Vector3(lean * .6f, H * .66f, 0); Vector3 radii = new Vector3(H * F(.36f, .42f), H * .29f, H * F(.36f, .42f));
                Color barkTint = blossom ? new Color(.9f, .86f, .82f) : Color.white;
                // Trunk with root flare.
                var path = new List<Vector3>(); var rad = new List<float>(); float baseR = H * .033f;
                for (int i = 0; i <= 7; i++)
                {
                    float t = i / 7f; path.Add(new Vector3(lean * t * t * 1.2f + Mathf.Sin(t * 3 + variant) * .06f, t * split * 1.1f, Mathf.Cos(t * 2.3f + variant) * .05f));
                    rad.Add(baseR * (1 + 1.1f * Mathf.Pow(1 - t, 6)) * Mathf.Lerp(1, .78f, t));
                }
                Tube(path, rad, 0, .08f, 12, barkTint, 2, .9f, .2f);
                Vector3 fork = path[path.Count - 1];
                int limbs = r.Next(4, 6); var tips = new List<(Vector3 p, Vector3 d)>();
                for (int l = 0; l < limbs; l++)
                {
                    float yaw = l * 360f / limbs + F(-22, 22) + variant * 17, tilt = F(30, 52);
                    Vector3 dir = Quaternion.Euler(0, yaw, 0) * (Quaternion.Euler(0, 0, -tilt) * Vector3.up);
                    float len = H * F(.3f, .4f);
                    var lp = new List<Vector3>(); var lr = new List<float>();
                    Vector3 p = fork + Vector3.down * F(0, split * .15f);
                    for (int i = 0; i <= 5; i++)
                    {
                        float t = i / 5f; lp.Add(p + dir * len * t + Vector3.up * len * .25f * t * t + Jitter(.05f)); lr.Add(baseR * .62f * Mathf.Lerp(1, .32f, t));
                    }
                    Tube(lp, lr, .08f, .45f, 8, barkTint, 1, .9f, .1f);
                    tips.Add((lp[lp.Count - 1], (lp[lp.Count - 1] - lp[lp.Count - 2]).normalized));
                    // Secondary branches.
                    int twigs = lod == 0 ? 3 : 1;
                    for (int b = 0; b < twigs; b++)
                    {
                        float t = F(.45f, .9f); Vector3 sp = Vector3.Lerp(lp[0], lp[lp.Count - 1], t);
                        Vector3 sd = (dir + Quaternion.Euler(0, F(-80, 80), 0) * Vector3.Cross(dir, Vector3.up).normalized * F(.5f, 1) + Vector3.up * F(.2f, .7f)).normalized;
                        float sl = len * F(.4f, .62f);
                        var bp = new List<Vector3> { sp, sp + sd * sl * .5f + Vector3.up * .1f, sp + sd * sl + Vector3.up * .25f };
                        Tube(bp, new List<float> { baseR * .3f, baseR * .2f, baseR * .1f }, .45f, .7f, 6, barkTint, 1, .9f, .05f);
                        tips.Add((bp[2], sd));
                    }
                }
                // Foliage: clusters at branch tips and across the crown shell.
                float clusterSize = H * (blossom ? .19f : .2f);
                foreach (var tip in tips) for (int k = 0; k < (lod == 0 ? 3 : 1); k++) Cluster(tip.p + tip.d * F(.1f, .6f) + Jitter(.5f), clusterSize, crown, radii, .8f, blossom);
                int fill = lod == 0 ? 95 : 34;
                for (int i = 0; i < fill; i++)
                {
                    Vector3 dir = Random3(); float shell = Mathf.Pow(F(.35f, 1f), .5f);
                    Vector3 p = crown + Vector3.Scale(dir, radii) * shell; if (p.y < split + .6f) p.y = split + .6f + F(0, .8f);
                    // Irregular, lobed crown silhouette rather than a sphere.
                    p += dir * Mathf.Sin(dir.x * 5 + variant) * Mathf.Cos(dir.z * 4) * .5f;
                    Cluster(p, clusterSize * F(.85f, 1.2f), crown, radii, Mathf.Lerp(.6f, 1, shell), blossom);
                }
            }

            Vector3 Random3() { Vector3 d; do d = new Vector3(F(-1, 1), F(-.75f, 1), F(-1, 1)); while (d.sqrMagnitude > 1 || d.sqrMagnitude < .05f); return d.normalized; }

            // ----- pine

            public void Pine(int variant)
            {
                float H = 9.5f, baseR = .24f;
                var path = new List<Vector3>(); var rad = new List<float>();
                for (int i = 0; i <= 8; i++) { float t = i / 8f; path.Add(new Vector3(Mathf.Sin(t * 2 + variant) * .05f, t * H, 0)); rad.Add(baseR * (1 + .8f * Mathf.Pow(1 - t, 8)) * Mathf.Lerp(1, .12f, t)); }
                Tube(path, rad, 0, .35f, 10, new Color(.85f, .78f, .74f), 2, .9f, .22f);
                var cell = new Rect(.01f, .01f, .98f, .98f);
                float firstWhorl = F(1.6f, 2.1f), step = lod == 0 ? .5f : .85f; int w = 0;
                for (float y = firstWhorl; y < H - .25f; y += step * F(.85f, 1.15f), w++)
                {
                    float t = (y - firstWhorl) / (H - firstWhorl);
                    float len = Mathf.Lerp(2.7f, .35f, Mathf.Pow(t, .85f)) * F(.85f, 1.1f);
                    int count = lod == 0 ? r.Next(5, 8) : 4;
                    float ao = Mathf.Lerp(.62f, 1.05f, t);
                    for (int b = 0; b < count; b++)
                    {
                        float yaw = b * 360f / count + w * 37 + F(-15, 15);
                        Vector3 outDir = Quaternion.Euler(0, yaw, 0) * Vector3.forward;
                        Vector3 a = new Vector3(path[0].x, y, 0), mid = a + outDir * len * .5f + Vector3.down * len * .05f, end = a + outDir * len + Vector3.down * len * F(.15f, .32f);
                        if (lod == 0) Tube(new List<Vector3> { a, mid, end }, new List<float> { .045f * (1 - t * .6f), .03f, .012f }, .3f, .75f, 5, new Color(.8f, .74f, .7f), 1, .9f, 0);
                        Vector3 crownC = new Vector3(0, y, 0);
                        Vector3 along = (end - a).normalized, across = Vector3.Cross(Vector3.up, along).normalized;
                        Color tint = new Color(ao * F(.92f, 1.05f), ao, ao * F(.92f, 1.06f));
                        // A flat spray along the branch and a tilted one crossing it.
                        Card(Vector3.Lerp(a, end, .58f) + Vector3.up * .05f, across, along, new Vector2(len * .75f + .35f, len * 1.02f), cell, crownC + Vector3.down * .8f, .85f, tint, .55f);
                        Card(Vector3.Lerp(a, end, .6f), (across * .4f + Vector3.up).normalized, along, new Vector2(len * .55f + .2f, len), cell, crownC + Vector3.down * .8f, .85f, tint * .95f, .55f);
                        if (lod == 0) Card(Vector3.Lerp(a, end, .45f) + Vector3.down * .08f, (across * -.5f + Vector3.up).normalized, along, new Vector2(len * .5f + .25f, len * .85f), cell, crownC + Vector3.down * .8f, .8f, tint * 1.05f, .55f);
                    }
                }
                // Leader at the top.
                Card(new Vector3(path[8].x, H - .2f, 0), Vector3.right, Vector3.up, new Vector2(.7f, .9f), cell, new Vector3(0, H - 1, 0), .9f, Color.white, .5f);
                Card(new Vector3(path[8].x, H - .2f, 0), Vector3.forward, Vector3.up, new Vector2(.7f, .9f), cell, new Vector3(0, H - 1, 0), .9f, Color.white, .5f);
            }

            // ----- palm

            public void Palm(int variant)
            {
                float H = 7f; float lean = variant == 0 ? 0 : F(.3f, 1.1f); float yawLean = F(0, 360);
                Vector3 leanDir = Quaternion.Euler(0, yawLean, 0) * Vector3.forward;
                var path = new List<Vector3>(); var rad = new List<float>(); int segs = lod == 0 ? 14 : 7;
                for (int i = 0; i <= segs; i++)
                {
                    float t = i / (float)segs; path.Add(leanDir * lean * t * t + new Vector3(0, t * H, 0));
                    rad.Add((.215f + .07f * Mathf.Pow(1 - t, 5)) * Mathf.Lerp(1, .93f, t));
                }
                Tube(path, rad, 0, .18f, 12, Color.white, 2, 1.6f, .06f);
                Vector3 top = path[segs]; Vector3 axis = (path[segs] - path[segs - 1]).normalized;
                // Crown shaft: a short green-brown bulge where the fronds emerge.
                Tube(new List<Vector3> { top - axis * .05f, top + axis * .35f, top + axis * .6f }, new List<float> { .23f, .25f, .12f }, .18f, .25f, 10, new Color(.62f, .7f, .45f), 1, 1.2f, .04f);
                Vector3 crown = top + axis * .45f;
                int live = lod == 0 ? 16 : 9; var frondCell = new Rect(0, 0, 1, 1);
                for (int f = 0; f < live; f++)
                {
                    float yaw = f * 137.5f + variant * 23; float rise = Mathf.Lerp(55, -28, (f % 4) / 3f + F(-.1f, .1f)) + (f < 4 ? 15 : 0);
                    Frond(crown, yaw, rise, F(2.9f, 3.6f), F(.55f, .7f), Color.white * F(.88f, 1.06f), false);
                }
                if (lod == 0) for (int d = 0; d < 4; d++) Frond(crown + Vector3.down * .2f, d * 90 + F(0, 60), -72, F(1.8f, 2.4f), .4f, new Color(.62f, .48f, .3f), true);
                // Coconuts.
                if (variant % 2 == 0)
                    for (int c = 0; c < (lod == 0 ? 6 : 3); c++)
                    {
                        float a = c * 60 + F(-15, 15); Vector3 p = crown + Quaternion.Euler(0, a, 0) * Vector3.forward * .26f + Vector3.down * F(.2f, .4f);
                        Ball(p, .13f, new Color(.42f, .38f, .2f));
                    }
                _ = frondCell;
            }

            void Frond(Vector3 start, float yaw, float riseDeg, float length, float width, Color tint, bool dead)
            {
                int segs = lod == 0 ? 10 : 5;
                Vector3 dir = Quaternion.Euler(0, yaw, 0) * Vector3.forward; Vector3 side = Vector3.Cross(Vector3.up, dir).normalized;
                Vector3 v0 = (dir * Mathf.Cos(riseDeg * Mathf.Deg2Rad) + Vector3.up * Mathf.Sin(riseDeg * Mathf.Deg2Rad)).normalized;
                float droop = dead ? .15f : .75f;
                int s = v.Count;
                for (int i = 0; i <= segs; i++)
                {
                    float t = i / (float)segs;
                    Vector3 spine = start + v0 * length * t + Vector3.down * droop * length * t * t;
                    Vector3 tangent = (v0 * length + Vector3.down * 2 * droop * length * t).normalized;
                    Vector3 across = Vector3.Cross(tangent, Vector3.Cross(side, tangent)).sqrMagnitude > 0 ? side : side;
                    Vector3 normal = Vector3.Cross(across, tangent).normalized; if (normal.y < 0) normal = -normal;
                    float w = width * (dead ? .5f : 1) * Mathf.Sin(Mathf.PI * Mathf.Pow(Mathf.Max(t, .04f), .75f));
                    // V-folded leaf: edges hang lower than the midrib.
                    Vector3 fold = -normal * w * .32f;
                    float flex = Mathf.Lerp(.25f, 1, t);
                    Color c = new Color(tint.r, tint.g, tint.b, flex);
                    v.Add(spine - across * w + fold); n.Add((normal - across * .4f).normalized); uv.Add(new Vector2(0, t)); col.Add(c * new Color(.86f, .86f, .86f, 1));
                    v.Add(spine); n.Add(normal); uv.Add(new Vector2(.5f, t)); col.Add(c);
                    v.Add(spine + across * w + fold); n.Add((normal + across * .4f).normalized); uv.Add(new Vector2(1, t)); col.Add(c * new Color(.86f, .86f, .86f, 1));
                }
                for (int i = 0; i < segs; i++)
                    for (int k = 0; k < 2; k++)
                    {
                        int a = s + i * 3 + k, b = a + 3;
                        leafTris.Add(a); leafTris.Add(b); leafTris.Add(a + 1); leafTris.Add(a + 1); leafTris.Add(b); leafTris.Add(b + 1);
                    }
            }

            void Ball(Vector3 c, float radius, Color tint)
            {
                int rings = lod == 0 ? 6 : 4, segs = lod == 0 ? 8 : 5, s = v.Count;
                for (int y = 0; y <= rings; y++) for (int x = 0; x <= segs; x++)
                {
                    float lat = Mathf.PI * y / rings, lon = Mathf.PI * 2 * x / segs; var d = new Vector3(Mathf.Sin(lat) * Mathf.Cos(lon), Mathf.Cos(lat), Mathf.Sin(lat) * Mathf.Sin(lon));
                    v.Add(c + d * radius); n.Add(d); uv.Add(new Vector2(x / (float)segs, y / (float)rings * .3f)); col.Add(new Color(tint.r, tint.g, tint.b, .2f));
                }
                for (int y = 0; y < rings; y++) for (int x = 0; x < segs; x++)
                {
                    int a = s + y * (segs + 1) + x, b = a + segs + 1;
                    barkTris.Add(a); barkTris.Add(a + 1); barkTris.Add(b); barkTris.Add(a + 1); barkTris.Add(b + 1); barkTris.Add(b);
                }
            }
        }
    }
}
