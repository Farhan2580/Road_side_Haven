using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;

namespace RoadsideHaven
{
    /// <summary>
    /// GPU-instanced 3D grass streamed in chunks around the camera. Clumps are only placed on
    /// open ground (never on roads, paving, buildings, water or painted dirt), grow taller in
    /// meadow patches, sway with the weather wind and part around the player and their vehicle.
    /// </summary>
    public sealed class GrassField : MonoBehaviour
    {
        private sealed class Chunk { public Matrix4x4[] Matrices; public int Count; public Bounds Bounds; }
        private struct Blocker { public Rect Area; public float Top; public bool Water; }

        public const float ChunkSize = 12f;
        private const int MaxPerChunk = 1023;
        private float spacing, radius, shadowRadius;
        private readonly Dictionary<Vector2Int, Chunk> chunks = new Dictionary<Vector2Int, Chunk>();
        private readonly List<Vector2Int> queue = new List<Vector2Int>();
        private readonly Dictionary<Vector2Int, List<Blocker>> blockers = new Dictionary<Vector2Int, List<Blocker>>();
        private readonly RaycastHit[] hits = new RaycastHit[16];
        private readonly Plane[] planes = new Plane[6];
        private readonly List<Matrix4x4> scratch = new List<Matrix4x4>(MaxPerChunk);
        private Mesh clump;
        private Material material;
        private Terrain terrain;
        private float[,,] alphamap;
        private MotelAtmosphere atmosphere;
        private OwnerSandboxController owner;
        private WorldDrivingController driving;
        private bool ready;
        private float nextQueue;
        public int ChunkCount => chunks.Count;
        public bool Ready => ready;
        public int InstanceCount { get { int n = 0; foreach (var c in chunks.Values) n += c.Count; return n; } }
        public IEnumerable<Vector3> InstancePositions()
        {
            foreach (var c in chunks.Values) for (int i = 0; i < c.Count; i++) yield return c.Matrices[i].GetColumn(3);
        }

        private void Start()
        {
            bool high = UltraGraphics.HighEnd;
            spacing = high ? .38f : .8f;
            radius = high ? 78f : 45f;
            shadowRadius = high ? 30f : 0f;
            var shader = Resources.Load<Shader>("UltraGrass");
            if (shader == null) shader = Shader.Find("RoadsideHaven/Ultra Grass");
            if (shader == null || !shader.isSupported) { enabled = false; return; }
            material = new Material(shader) { name = "Ultra grass", enableInstancing = true };
            clump = BuildClump();
            atmosphere = GetComponent<MotelAtmosphere>();
            owner = GetComponentInChildren<OwnerSandboxController>(true);
            driving = GetComponentInChildren<WorldDrivingController>(true);
            foreach (var t in Terrain.activeTerrains) if (t != null && t.gameObject.scene == gameObject.scene) { terrain = t; break; }
            if (terrain != null && terrain.terrainData != null && terrain.terrainData.alphamapLayers >= 2)
                alphamap = terrain.terrainData.GetAlphamaps(0, 0, terrain.terrainData.alphamapWidth, terrain.terrainData.alphamapHeight);
            CollectBlockers();
            Shader.SetGlobalVector("_UltraGrassFade", new Vector4(radius - 4f, 14f, 0, 0));
            ready = true;
        }

        // ---------- Placement rules ----------

        private static bool WaterName(string n) => n.Contains("river") || n.Contains("water") || n.Contains("lake") || n.Contains("pond") || n.Contains("stream") || n.Contains("sea") || n.Contains("ocean") || n.Contains("pool") || n.Contains("fountain") || n.Contains("canal");
        private static bool GrassName(string n) => n.Contains("lawn") || n.Contains("grass") || n.Contains("meadow") || n.Contains("turf");

        private void CollectBlockers()
        {
            foreach (var root in gameObject.scene.GetRootGameObjects())
                foreach (var r in root.GetComponentsInChildren<MeshRenderer>())
                {
                    if (!r.enabled || r.GetComponentInParent<Rigidbody>() != null) continue;
                    var b = r.bounds;
                    string n = r.name.ToLowerInvariant();
                    bool water = WaterName(n) || (r.sharedMaterial != null && WaterName(r.sharedMaterial.name.ToLowerInvariant()));
                    float area = b.size.x * b.size.z;
                    if (!water)
                    {
                        // Only flat overlays (paint, aprons, dirt paths, patios) that a ray might miss because they have no collider.
                        if (b.size.y > 1.2f || area < .4f || area > 2500f) continue;
                        if (GroundSurfaceStyle.SurfaceFor(r.name) == 0 || GrassName(n) || (r.sharedMaterial != null && GrassName(r.sharedMaterial.name.ToLowerInvariant()))) continue;
                        if (r.GetComponentInParent<BiomePlant>() != null) continue;
                    }
                    var blocker = new Blocker { Area = Rect.MinMaxRect(b.min.x - .2f, b.min.z - .2f, b.max.x + .2f, b.max.z + .2f), Top = b.max.y, Water = water };
                    int x0 = Mathf.FloorToInt(blocker.Area.xMin / 16f), x1 = Mathf.FloorToInt(blocker.Area.xMax / 16f);
                    int z0 = Mathf.FloorToInt(blocker.Area.yMin / 16f), z1 = Mathf.FloorToInt(blocker.Area.yMax / 16f);
                    if ((x1 - x0 + 1) * (z1 - z0 + 1) > 400) continue;
                    for (int x = x0; x <= x1; x++) for (int z = z0; z <= z1; z++)
                    {
                        var key = new Vector2Int(x, z);
                        if (!blockers.TryGetValue(key, out var list)) blockers[key] = list = new List<Blocker>();
                        list.Add(blocker);
                    }
                }
        }

        private bool Blocked(Vector3 p)
        {
            if (!blockers.TryGetValue(new Vector2Int(Mathf.FloorToInt(p.x / 16f), Mathf.FloorToInt(p.z / 16f)), out var list)) return false;
            var xz = new Vector2(p.x, p.z);
            foreach (var b in list)
                if (b.Area.Contains(xz) && (b.Water || (b.Top > p.y - .03f && b.Top < p.y + 1.2f))) return true;
            return false;
        }

        private static bool IgnoredHit(Collider c) => c.isTrigger || c.attachedRigidbody != null || c.GetComponentInParent<CharacterController>() != null || c.GetComponentInParent<NavMeshAgent>() != null;

        private static bool GroundCollider(Collider c)
        {
            if (c is TerrainCollider) return true;
            if (GroundSurfaceStyle.SurfaceFor(c.name) == 0) return true;
            string n = c.name.ToLowerInvariant();
            if (n.Contains("playground") || n.Contains("underground")) return false;
            return GrassName(n) || n.Contains("foundation") || n.Contains("ground");
        }

        /// <summary>True and the grass root height when a clump may grow at this point.</summary>
        public bool TryGround(float x, float z, out float height)
        {
            height = 0;
            int count = Physics.RaycastNonAlloc(new Vector3(x, 60f, z), Vector3.down, hits, 80f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue; Collider top = null; float topY = 0;
            for (int i = 0; i < count; i++)
            {
                if (IgnoredHit(hits[i].collider)) continue;
                if (hits[i].distance < best) { best = hits[i].distance; top = hits[i].collider; topY = hits[i].point.y; }
            }
            if (top == null || !GroundCollider(top)) return false;
            height = topY;
            if (terrain != null)
            {
                var tp = terrain.transform.position; var size = terrain.terrainData.size;
                float u = (x - tp.x) / size.x, v = (z - tp.z) / size.z;
                if (u >= 0 && u <= 1 && v >= 0 && v <= 1)
                {
                    height = Mathf.Max(height, terrain.SampleHeight(new Vector3(x, 0, z)) + tp.y);
                    if (alphamap != null)
                    {
                        int ax = Mathf.Clamp((int)(u * (alphamap.GetLength(1) - 1)), 0, alphamap.GetLength(1) - 1);
                        int az = Mathf.Clamp((int)(v * (alphamap.GetLength(0) - 1)), 0, alphamap.GetLength(0) - 1);
                        // Layers 0 and 1 are the two grass layers; dirt and gravel stay bare.
                        if (alphamap[az, ax, 0] + alphamap[az, ax, 1] < .5f) return false;
                    }
                }
            }
            return !Blocked(new Vector3(x, height, z));
        }

        private Chunk Generate(Vector2Int key)
        {
            scratch.Clear();
            float ox = key.x * ChunkSize, oz = key.y * ChunkSize;
            var random = new System.Random(key.x * 73856093 ^ key.y * 19349663);
            int cells = Mathf.FloorToInt(ChunkSize / spacing);
            float minY = float.MaxValue, maxY = float.MinValue;
            for (int ix = 0; ix < cells && scratch.Count < MaxPerChunk; ix++)
                for (int iz = 0; iz < cells && scratch.Count < MaxPerChunk; iz++)
                {
                    float x = ox + (ix + (float)random.NextDouble()) * spacing, z = oz + (iz + (float)random.NextDouble()) * spacing;
                    if (!TryGround(x, z, out float y)) continue;
                    float meadow = Mathf.PerlinNoise(x * .021f + 31.7f, z * .021f + 7.3f);
                    float tall = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.52f, .75f, meadow));
                    float h = Mathf.Lerp(.42f, 1.3f, tall) * (.75f + (float)random.NextDouble() * .5f);
                    float w = .85f + (float)random.NextDouble() * .4f;
                    scratch.Add(Matrix4x4.TRS(new Vector3(x, y - .02f, z), Quaternion.Euler(0, (float)random.NextDouble() * 360f, 0), new Vector3(w, h, w)));
                    minY = Mathf.Min(minY, y); maxY = Mathf.Max(maxY, y);
                }
            // Shuffle so drawing the first N instances thins the chunk evenly for distant LOD.
            for (int i = scratch.Count - 1; i > 0; i--) { int j = random.Next(i + 1); (scratch[i], scratch[j]) = (scratch[j], scratch[i]); }
            var chunk = new Chunk { Matrices = scratch.ToArray(), Count = scratch.Count };
            if (chunk.Count > 0) chunk.Bounds = new Bounds(new Vector3(ox + ChunkSize * .5f, (minY + maxY) * .5f + .8f, oz + ChunkSize * .5f), new Vector3(ChunkSize + 3f, maxY - minY + 3.5f, ChunkSize + 3f));
            return chunk;
        }

        // ---------- Streaming and drawing ----------

        /// <summary>Synchronously builds every chunk around a point (used by tests and tooling).</summary>
        public void GenerateAround(Vector3 p, float range)
        {
            if (!ready) return;
            int r = Mathf.CeilToInt(range / ChunkSize);
            var c = new Vector2Int(Mathf.FloorToInt(p.x / ChunkSize), Mathf.FloorToInt(p.z / ChunkSize));
            for (int x = -r; x <= r; x++) for (int z = -r; z <= r; z++) { var k = new Vector2Int(c.x + x, c.y + z); if (!chunks.ContainsKey(k)) chunks[k] = Generate(k); }
        }

        /// <summary>Drops the chunks over an area so they regrow around newly built roads or surfaces.</summary>
        public void Invalidate(Rect area)
        {
            var stale = new List<Vector2Int>();
            foreach (var key in chunks.Keys)
                if (new Rect(key.x * ChunkSize, key.y * ChunkSize, ChunkSize, ChunkSize).Overlaps(area)) stale.Add(key);
            foreach (var key in stale) chunks.Remove(key);
        }

        private void Update()
        {
            if (!ready) return;
            var camera = Camera.main;
            if (camera == null) return;
            Vector3 eye = camera.transform.position;
            var centre = new Vector2Int(Mathf.FloorToInt(eye.x / ChunkSize), Mathf.FloorToInt(eye.z / ChunkSize));
            int reach = Mathf.CeilToInt(radius / ChunkSize);
            if (Time.unscaledTime >= nextQueue)
            {
                nextQueue = Time.unscaledTime + .25f;
                queue.Clear();
                for (int x = -reach; x <= reach; x++) for (int z = -reach; z <= reach; z++)
                {
                    var k = new Vector2Int(centre.x + x, centre.y + z);
                    if (!chunks.ContainsKey(k) && (x * x + z * z) * ChunkSize * ChunkSize <= (radius + ChunkSize) * (radius + ChunkSize)) queue.Add(k);
                }
                queue.Sort((a, b) => (a - centre).sqrMagnitude.CompareTo((b - centre).sqrMagnitude));
                // Forget chunks well outside the radius to bound memory on the 2 km map.
                var drop = new List<Vector2Int>();
                foreach (var k in chunks.Keys) if (Mathf.Abs(k.x - centre.x) > reach + 3 || Mathf.Abs(k.y - centre.y) > reach + 3) drop.Add(k);
                foreach (var k in drop) chunks.Remove(k);
            }
            int budget = UltraGraphics.HighEnd ? 3 : 1;
            while (budget-- > 0 && queue.Count > 0) { var k = queue[0]; queue.RemoveAt(0); if (!chunks.ContainsKey(k)) chunks[k] = Generate(k); }

            UpdateGlobals();
            GeometryUtility.CalculateFrustumPlanes(camera, planes);
            foreach (var pair in chunks)
            {
                var chunk = pair.Value;
                if (chunk.Count == 0) continue;
                Vector3 c = chunk.Bounds.center;
                float d = new Vector2(c.x - eye.x, c.z - eye.z).magnitude;
                if (d > radius + ChunkSize || !GeometryUtility.TestPlanesAABB(planes, chunk.Bounds)) continue;
                float density = d < 28f ? 1f : d < 50f ? .4f : .15f;
                int n = Mathf.Max(1, Mathf.RoundToInt(chunk.Count * density));
                var rp = new RenderParams(material)
                {
                    worldBounds = chunk.Bounds,
                    shadowCastingMode = d < shadowRadius ? ShadowCastingMode.On : ShadowCastingMode.Off,
                    receiveShadows = true,
                    layer = gameObject.layer
                };
                Graphics.RenderMeshInstanced(rp, clump, 0, chunk.Matrices, n);
            }
        }

        private void UpdateGlobals()
        {
            var season = atmosphere == null ? WorldSeason.Summer : atmosphere.Season;
            Color tint = season == WorldSeason.Monsoon ? new Color(.82f, 1.08f, .82f) : season == WorldSeason.Autumn ? new Color(1.18f, .95f, .58f)
                : season == WorldSeason.LateAutumn ? new Color(1.1f, .88f, .6f) : season == WorldSeason.Winter ? new Color(.9f, .95f, .84f)
                : season == WorldSeason.Spring ? new Color(.92f, 1.1f, .86f) : Color.white;
            Shader.SetGlobalColor("_UltraSeasonTint", tint);
            Vector4 far = new Vector4(0, -1000, 0, 0);
            Shader.SetGlobalVector("_UltraPush0", owner != null && owner.isActiveAndEnabled && owner.IsWalking ? (Vector4)owner.transform.position + new Vector4(0, 0, 0, .9f) : far);
            var vehicle = driving == null ? null : driving.ActiveVehicleTransform;
            Shader.SetGlobalVector("_UltraPush1", vehicle != null ? (Vector4)vehicle.position + new Vector4(0, 0, 0, 2.6f) : far);
            if (WeatherSystem.Active == null) Shader.SetGlobalVector("_UltraWind", new Vector4(.8f, .6f, .35f, 1.2f));
        }

        private static Mesh BuildClump()
        {
            const int blades = 10, rows = 4;
            var random = new System.Random(5);
            var vertices = new List<Vector3>(); var normals = new List<Vector3>(); var colors = new List<Color>(); var uvs = new List<Vector2>(); var triangles = new List<int>();
            for (int b = 0; b < blades; b++)
            {
                float angle = (float)random.NextDouble() * Mathf.PI * 2f, offset = Mathf.Sqrt((float)random.NextDouble()) * .26f;
                var root = new Vector3(Mathf.Cos(angle) * offset, 0, Mathf.Sin(angle) * offset);
                float facing = (float)random.NextDouble() * Mathf.PI * 2f;
                var side = new Vector3(Mathf.Cos(facing), 0, Mathf.Sin(facing));
                var normal = Vector3.Cross(side, Vector3.up).normalized;
                float height = .45f + (float)random.NextDouble() * .55f, width = .05f + (float)random.NextDouble() * .03f;
                var lean = new Vector3((float)random.NextDouble() - .5f, 0, (float)random.NextDouble() - .5f) * .35f;
                float tone = (float)random.NextDouble();
                int start = vertices.Count;
                for (int r = 0; r <= rows; r++)
                {
                    float t = r / (float)rows;
                    // Curved blade: lean grows quadratically, width tapers to a point.
                    var centre = root + Vector3.up * (height * t) + lean * (t * t);
                    float half = width * (1 - t * .92f) * .5f;
                    vertices.Add(centre - side * half); vertices.Add(centre + side * half);
                    normals.Add(normal); normals.Add(normal);
                    colors.Add(new Color(tone, 0, 0, 1)); colors.Add(new Color(tone, 0, 0, 1));
                    uvs.Add(new Vector2(0, height * t)); uvs.Add(new Vector2(1, height * t));
                }
                for (int r = 0; r < rows; r++)
                {
                    int i = start + r * 2;
                    triangles.AddRange(new[] { i, i + 2, i + 1, i + 1, i + 2, i + 3 });
                }
            }
            var mesh = new Mesh { name = "Ultra grass clump" };
            mesh.SetVertices(vertices); mesh.SetNormals(normals); mesh.SetColors(colors); mesh.SetUVs(0, uvs); mesh.SetTriangles(triangles, 0);
            mesh.bounds = new Bounds(new Vector3(0, .6f, 0), new Vector3(1.6f, 1.4f, 1.6f));
            return mesh;
        }

        private void OnDestroy()
        {
            if (material != null) Destroy(material);
            if (clump != null) Destroy(clump);
        }
    }
}
