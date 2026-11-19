using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace RoadsideHaven
{
    /// <summary>
    /// Replaces the scene's primitive trees (sphere crowns on cylinder or box trunks, and the merged
    /// palm-frond meshes of the districts) with <see cref="TreeArt"/> trees at the same spots and
    /// sizes, and drives their wind from the weather and their canopy colour from the season.
    /// The original objects and colliders stay in place; only their renderers are hidden.
    /// </summary>
    public sealed class TreeUpgrade : MonoBehaviour
    {
        public sealed class Planted { public GameObject Tree; public TreeSpecies Species; public Vector3 Base; public float Height; }
        private readonly List<Planted> trees = new List<Planted>();
        private readonly List<Renderer> hidden = new List<Renderer>();
        private readonly List<LODGroup> disabledLods = new List<LODGroup>();
        private GameObject root;
        private MotelAtmosphere atmosphere;
        public IReadOnlyList<Planted> Trees => trees;
        public IReadOnlyList<Renderer> Hidden => hidden;
        public int PalmsFromFoliage { get; private set; }
        public bool Done { get; private set; }
        private static readonly int WindId = Shader.PropertyToID("_TreeWind"), SeasonId = Shader.PropertyToID("_TreeSeason");

        private IEnumerator Start()
        {
            for (int i = 0; i < 5; i++) yield return null;
            atmosphere = GetComponent<MotelAtmosphere>();
            root = new GameObject("Realistic trees");
            SceneManager.MoveGameObjectToScene(root, gameObject.scene);
            UpgradeAvenueTrees();
            UpgradeTrunkAndCrownTrees();
            UpgradeMergedPalms();
            UpgradeMergedCrowns();
            UpgradeLeftoverCrowns();
            Done = true;
        }

        private static int Seed(Vector3 p) { unchecked { return ((int)(p.x * 73.1f) * 397 ^ (int)(p.z * 51.7f)) & int.MaxValue; } }

        private static Mesh empty;
        /// <summary>An empty mesh: hiding by swapping meshes survives district scripts that re-enable renderers by distance.</summary>
        public static Mesh HiddenMesh => empty != null ? empty : (empty = new Mesh { name = "Hidden by upgrade" });
        private readonly List<(MeshFilter filter, Mesh original)> emptied = new List<(MeshFilter, Mesh)>();
        private void Hide(Renderer r)
        {
            if (r == null) return;
            var f = r.GetComponent<MeshFilter>(); if (f != null && f.sharedMesh != null && f.sharedMesh != HiddenMesh) { emptied.Add((f, f.sharedMesh)); f.sharedMesh = HiddenMesh; }
            if (r.enabled) { r.enabled = false; hidden.Add(r); }
        }

        private void HideGroup(Transform t)
        {
            foreach (var r in t.GetComponentsInChildren<Renderer>()) Hide(r);
            var lod = t.GetComponent<LODGroup>(); if (lod != null && lod.enabled) { lod.enabled = false; disabledLods.Add(lod); }
        }

        // 1. The avenue trees of the complete streets: one group per tree.
        private void UpgradeAvenueTrees()
        {
            foreach (var t in GetComponentsInChildren<Transform>())
            {
                if (t.name != "Avenue oak" && t.name != "Avenue pine" && t.name != "Avenue palm") continue;
                var renderers = t.GetComponentsInChildren<Renderer>(); if (renderers.Length == 0) continue;
                var b = renderers[0].bounds; foreach (var r in renderers) b.Encapsulate(r.bounds);
                Vector3 basePoint = new Vector3(t.position.x, b.min.y, t.position.z);
                var species = t.name == "Avenue pine" ? TreeSpecies.Pine : t.name == "Avenue palm" ? TreeSpecies.Palm : TreeSpecies.Broadleaf;
                int seed = Seed(basePoint);
                // Make the street pines and oaks a little taller and fuller than the old placeholders.
                float height = (b.max.y - basePoint.y) * (species == TreeSpecies.Palm ? 1.12f : 1.25f);
                HideGroup(t);
                Plant(species, basePoint, height, seed, species == TreeSpecies.Palm ? 1 + seed % 3 : seed);
            }
        }

        // 2. Loose trunk + crown pieces that share a parent (districts, airport, beach, downtown).
        private void UpgradeTrunkAndCrownTrees()
        {
            var used = new HashSet<Renderer>();
            foreach (var trunk in GetComponentsInChildren<MeshRenderer>())
            {
                string n = trunk.name.ToLowerInvariant();
                if (!trunk.enabled || !n.Contains("trunk") || n.Contains("palm") || used.Contains(trunk) || trunk.transform.parent == null) continue;
                if (trunk.GetComponentInParent<TreeUpgrade>() != this) continue;
                var tb = trunk.bounds; if (tb.size.y < 1 || tb.size.x > 1.2f) continue;
                var crowns = new List<Renderer>();
                foreach (Transform sibling in trunk.transform.parent)
                {
                    var r = sibling.GetComponent<MeshRenderer>(); if (r == null || !r.enabled || used.Contains(r)) continue;
                    string sn = sibling.name.ToLowerInvariant(); if (!sn.Contains("crown") || sn.Contains("landmark")) continue;
                    var cb = r.bounds; float reach = Mathf.Max(1.7f, Mathf.Max(cb.extents.x, cb.extents.z) * .8f);
                    if (new Vector2(cb.center.x - tb.center.x, cb.center.z - tb.center.z).magnitude <= reach && cb.min.y > tb.min.y + .5f && cb.min.y < tb.max.y + 1.5f) crowns.Add(r);
                }
                if (crowns.Count == 0)
                {
                    // A duplicate trunk standing inside a tree that is already replaced, or a bare trunk: replace too.
                    bool inside = false; foreach (var t in trees) if (new Vector2(t.Base.x - tb.center.x, t.Base.z - tb.center.z).sqrMagnitude < 1.5f) { inside = true; break; }
                    if (inside) { used.Add(trunk); Hide(trunk); }
                    else if (n.Contains("tree trunk") && tb.size.y > 1.5f) { used.Add(trunk); Hide(trunk); var bp = new Vector3(tb.center.x, tb.min.y, tb.center.z); Plant(TreeSpecies.Broadleaf, bp, tb.size.y * 2.6f, Seed(bp), Seed(bp)); }
                    continue;
                }
                // Branches, blossom and fruit that belonged to the old tree.
                foreach (Transform sibling in trunk.transform.parent)
                {
                    var r = sibling.GetComponent<MeshRenderer>(); if (r == null || !r.enabled || used.Contains(r)) continue;
                    string sn = sibling.name.ToLowerInvariant(); if (!(sn.Contains("branch") || sn.Contains("blossom") || sn.Contains("fruit")) || sn.Contains("palm")) continue;
                    var cb = r.bounds; if (new Vector2(cb.center.x - tb.center.x, cb.center.z - tb.center.z).magnitude < 2.6f && cb.min.y > tb.min.y + .3f) { used.Add(r); Hide(r); }
                }
                float top = tb.max.y; foreach (var c in crowns) top = Mathf.Max(top, c.bounds.max.y);
                Vector3 basePoint = new Vector3(tb.center.x, tb.min.y, tb.center.z);
                used.Add(trunk); Hide(trunk); foreach (var c in crowns) { used.Add(c); Hide(c); }
                int seed = Seed(basePoint); float height = (top - basePoint.y) * 1.22f;
                var species = height < 6.2f && seed % 3 == 0 ? TreeSpecies.Blossom : TreeSpecies.Broadleaf;
                Plant(species, basePoint, height, seed, seed);
            }
        }

        // Crowns whose trunk was elsewhere or shared: hide inside replaced trees, otherwise grow a tree beneath.
        private void UpgradeLeftoverCrowns()
        {
            foreach (var r in GetComponentsInChildren<MeshRenderer>())
            {
                string n = r.name.ToLowerInvariant(); if (!r.enabled || !n.Contains("tree crown")) continue;
                var cb = r.bounds; bool inside = false;
                foreach (var t in trees) if (new Vector2(t.Base.x - cb.center.x, t.Base.z - cb.center.z).sqrMagnitude < 2.6f * 2.6f && cb.max.y < t.Base.y + t.Height * 1.3f) { inside = true; break; }
                Hide(r); if (inside) continue;
                float ground = Ground(cb.center); var bp = new Vector3(cb.center.x, ground, cb.center.z);
                if (cb.max.y - ground > 2.5f) Plant(TreeSpecies.Broadleaf, bp, (cb.max.y - ground) * 1.15f, Seed(bp), Seed(bp));
            }
        }

        // 3. Palm fronds, usually merged into one mesh per district together with shrubs of the same material:
        // find each raised palm crown, cut only its triangles out, and grow a realistic palm in its place.
        private readonly List<(MeshFilter filter, Mesh original)> stripped = new List<(MeshFilter, Mesh)>();
        private void UpgradeMergedPalms()
        {
            var palms = new List<Vector3>();
            foreach (var r in GetComponentsInChildren<MeshRenderer>())
            {
                string n = r.name.ToLowerInvariant();
                if (!r.enabled || !(n.Contains("palm foliage") || n.Contains("palm frond"))) continue;
                var filter = r.GetComponent<MeshFilter>(); if (filter == null || filter.sharedMesh == null || !filter.sharedMesh.isReadable) continue;
                var world = World(filter); var remove = new bool[world.Length];
                var crowns = FindCrowns(world, remove);
                if (crowns.Count == 0) continue;
                Strip(r, filter, remove);
                foreach (var (crown, ground) in crowns)
                {
                    var basePoint = new Vector3(crown.x, ground, crown.z); int seed = Seed(basePoint);
                    // Straight trunks: an old merged trunk stays hidden inside the new one.
                    Plant(TreeSpecies.Palm, basePoint, crown.y - ground + .35f, seed, 0);
                    palms.Add(basePoint); PalmsFromFoliage++;
                }
            }
            if (palms.Count == 0) return;
            // Loose palm trunks, fruit and leaf tips that belonged to the replaced palms.
            foreach (var r in GetComponentsInChildren<MeshRenderer>())
            {
                if (!r.enabled) continue; string n = r.name.ToLowerInvariant();
                if (n.Contains("palm leaf tips"))
                {
                    var filter = r.GetComponent<MeshFilter>(); if (filter == null || filter.sharedMesh == null || !filter.sharedMesh.isReadable) continue;
                    var world = World(filter); var remove = new bool[world.Length]; bool any = false;
                    for (int i = 0; i < world.Length; i++) foreach (var p in palms) if (world[i].y > p.y + 2 && new Vector2(world[i].x - p.x, world[i].z - p.z).sqrMagnitude < 3.8f * 3.8f) { remove[i] = any = true; break; }
                    if (any) Strip(r, filter, remove);
                    continue;
                }
                if (!(n.Contains("palm trunk") || n.Contains("palm fruit"))) continue;
                var c = r.bounds.center; if (r.bounds.size.x > 3) continue;
                foreach (var p in palms) if (new Vector2(c.x - p.x, c.z - p.z).sqrMagnitude < 2.5f * 2.5f) { Hide(r); break; }
            }
        }

        // 4. Ball-shaped crowns merged into per-material batches (parks, farm, heritage gardens, transit square).
        private static readonly string[] crownBatches = { "expansion foliage", "expansion deep foliage", "fruit tree leaves", "deep garden leaves", "transit foliage" };
        private void UpgradeMergedCrowns()
        {
            var planted = new List<(Vector3 basePoint, float bottom)>(); var scopes = new HashSet<Transform>();
            foreach (var r in GetComponentsInChildren<MeshRenderer>())
            {
                string n = r.name.ToLowerInvariant(); if (!r.enabled) continue;
                bool match = false; foreach (var b in crownBatches) if (n.Contains(b)) { match = true; break; }
                if (!match) continue;
                var filter = r.GetComponent<MeshFilter>(); if (filter == null || filter.sharedMesh == null || !filter.sharedMesh.isReadable) continue;
                var world = World(filter); var remove = new bool[world.Length];
                var crowns = FindCrowns(world, remove, 1.5f, 2.6f);
                if (crowns.Count == 0) continue;
                Strip(r, filter, remove); scopes.Add(r.transform.parent);
                foreach (var (crown, ground) in crowns)
                {
                    var basePoint = new Vector3(crown.x, ground, crown.z); int seed = Seed(basePoint); float height = (crown.y - ground) * 1.18f;
                    Plant(height < 5.8f && seed % 3 == 0 ? TreeSpecies.Blossom : TreeSpecies.Broadleaf, basePoint, height, seed, seed);
                    planted.Add((basePoint, ground + (crown.y - ground) * .55f));
                }
            }
            // Cut the old box trunks out of the bark / timber batches next to the replaced crowns.
            foreach (var scope in scopes)
                foreach (var r in scope.GetComponentsInChildren<MeshRenderer>())
                {
                    string n = r.name.ToLowerInvariant(); if (!r.enabled || !(n.Contains("bark") || n.Contains("timber"))) continue;
                    var filter = r.GetComponent<MeshFilter>(); if (filter == null || filter.sharedMesh == null || !filter.sharedMesh.isReadable) continue;
                    var world = World(filter); var remove = new bool[world.Length]; bool any = false;
                    for (int i = 0; i < world.Length; i++)
                        foreach (var (bp, bottom) in planted)
                            if (world[i].y < bottom && world[i].y > bp.y - .3f && new Vector2(world[i].x - bp.x, world[i].z - bp.z).sqrMagnitude < .42f * .42f) { remove[i] = any = true; break; }
                    if (any) Strip(r, filter, remove);
                }
        }

        private static Vector3[] World(MeshFilter filter)
        {
            var vertices = filter.sharedMesh.vertices; var m = filter.transform.localToWorldMatrix;
            for (int i = 0; i < vertices.Length; i++) vertices[i] = m.MultiplyPoint3x4(vertices[i]);
            return vertices;
        }

        /// <summary>Removes the flagged triangles from the renderer's mesh (on a copy), hiding it if nothing is left.</summary>
        private void Strip(MeshRenderer r, MeshFilter filter, bool[] remove)
        {
            var source = filter.sharedMesh; var copy = Instantiate(source); copy.name = source.name + " without palms"; int kept = 0;
            for (int s = 0; s < source.subMeshCount; s++)
            {
                var tris = source.GetTriangles(s); var keep = new List<int>(tris.Length);
                for (int t = 0; t < tris.Length; t += 3) if (!remove[tris[t]] && !remove[tris[t + 1]] && !remove[tris[t + 2]]) { keep.Add(tris[t]); keep.Add(tris[t + 1]); keep.Add(tris[t + 2]); }
                copy.SetTriangles(keep, s); kept += keep.Count;
            }
            if (kept == 0) { Destroy(copy); Hide(r); return; }
            stripped.Add((filter, source)); filter.sharedMesh = copy;
        }

        /// <summary>
        /// Finds palm crowns among foliage vertices: connected 1 m cells, then mean-shift on vertex density
        /// (fronds converge on the crown centre) to separate neighbouring palms. Only clusters held high
        /// above the ground count as palms; their vertices are flagged for removal. Shrubs are left alone.
        /// </summary>
        private static List<(Vector3 crown, float ground)> FindCrowns(Vector3[] world, bool[] remove, float raised = 2.2f, float merge = 1.6f)
        {
            var result = new List<(Vector3, float)>();
            var cells = new Dictionary<Vector2Int, List<int>>();
            for (int i = 0; i < world.Length; i++)
            {
                var key = new Vector2Int(Mathf.FloorToInt(world[i].x), Mathf.FloorToInt(world[i].z));
                if (!cells.TryGetValue(key, out var list)) cells[key] = list = new List<int>(); list.Add(i);
            }
            var seen = new HashSet<Vector2Int>(); var stack = new Stack<Vector2Int>();
            foreach (var start in cells.Keys)
            {
                if (!seen.Add(start)) continue;
                var members = new List<int>(); var componentCells = new List<Vector2Int>(); stack.Push(start);
                while (stack.Count > 0)
                {
                    var c = stack.Pop(); members.AddRange(cells[c]); componentCells.Add(c);
                    for (int dx = -1; dx <= 1; dx++) for (int dz = -1; dz <= 1; dz++)
                    {
                        var k = new Vector2Int(c.x + dx, c.y + dz); if (cells.ContainsKey(k) && seen.Add(k)) stack.Push(k);
                    }
                }
                if (members.Count < 12) continue;
                int step = Mathf.Max(1, members.Count / 2500); var sample = new List<Vector2>();
                for (int i = 0; i < members.Count; i += step) sample.Add(new Vector2(world[members[i]].x, world[members[i]].z));
                // Mean-shift from seeds spread over the component.
                var modes = new List<Vector2>();
                foreach (var cell in componentCells)
                {
                    if ((cell.x & 1) != 0 || (cell.y & 1) != 0) continue;
                    Vector2 m = new Vector2(cell.x + .5f, cell.y + .5f);
                    for (int it = 0; it < 18; it++)
                    {
                        Vector2 sum = Vector2.zero; int count = 0;
                        foreach (var s in sample) if ((s - m).sqrMagnitude < 2.2f * 2.2f) { sum += s; count++; }
                        if (count == 0) break; var next = sum / count; if ((next - m).sqrMagnitude < .0004f) { m = next; break; } m = next;
                    }
                    bool merged = false; foreach (var e in modes) if ((e - m).sqrMagnitude < merge * merge) { merged = true; break; }
                    if (!merged) modes.Add(m);
                }
                if (modes.Count == 0) continue;
                // Classify each mode: crown height where the fronds meet, the surface beneath, and the crown shape.
                var owner = new int[members.Count]; var top = new float[modes.Count];
                for (int k = 0; k < modes.Count; k++) top[k] = float.MinValue;
                for (int i = 0; i < members.Count; i++)
                {
                    var p = world[members[i]]; var xz = new Vector2(p.x, p.z); int best = -1; float bestD = 4.5f * 4.5f;
                    for (int k = 0; k < modes.Count; k++) { float d = (modes[k] - xz).sqrMagnitude; if (d < bestD) { bestD = d; best = k; } }
                    owner[i] = best; if (best >= 0 && bestD < .9f * .9f) top[best] = Mathf.Max(top[best], p.y);
                }
                var palm = new bool[modes.Count]; var ground = new float[modes.Count];
                for (int k = 0; k < modes.Count; k++)
                {
                    if (top[k] == float.MinValue) continue;
                    ground[k] = Ground(new Vector3(modes[k].x, top[k] - .3f, modes[k].y));
                    float height = top[k] - ground[k]; if (height < 3f || height > 15f) continue;
                    // Only the foliage held up high belongs to the crown (shrubs in a planter below stay).
                    int count = 0; float sxx = 0, szz = 0, sxz = 0, radial = 0;
                    for (int i = 0; i < members.Count; i++)
                    {
                        if (owner[i] != k) continue; var p = world[members[i]]; if (p.y < ground[k] + raised) continue;
                        float dx = p.x - modes[k].x, dz = p.z - modes[k].y; sxx += dx * dx; szz += dz * dz; sxz += dx * dz; radial += Mathf.Sqrt(dx * dx + dz * dz); count++;
                    }
                    if (count < 20) continue;
                    sxx /= count; szz /= count; sxz /= count; radial /= count;
                    float tr = sxx + szz, det = sxx * szz - sxz * sxz, disc = Mathf.Sqrt(Mathf.Max(0, tr * tr * .25f - det));
                    float major = tr * .5f + disc, minor = tr * .5f - disc;
                    // A palm crown is round and radial, not a strip of hedge or roof planting.
                    palm[k] = major > 0 && minor / major > .3f && radial > .6f && radial < 3.6f;
                    if (palm[k]) result.Add((new Vector3(modes[k].x, top[k], modes[k].y), ground[k]));
                }
                for (int i = 0; i < members.Count; i++) { int k = owner[i]; if (k >= 0 && palm[k] && world[members[i]].y > ground[k] + raised) remove[members[i]] = true; }
            }            return result;
        }
        /// <summary>The nearest upward-facing surface below a point (ground, plaza or planter).</summary>
        private static float Ground(Vector3 from)
        {
            float best = float.MinValue;
            foreach (var hit in Physics.RaycastAll(from, Vector3.down, 40, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                {
                    var size = hit.collider.bounds.size; bool pole = size.x < .9f && size.z < .9f && size.y > 1; // old trunks and lamp posts
                    if (hit.normal.y > .7f && hit.point.y > best && !pole && hit.collider.GetComponentInParent<Rigidbody>() == null) best = hit.point.y;
                }
            return best > float.MinValue ? best : from.y - 6;
        }
        private GameObject Plant(TreeSpecies species, Vector3 basePoint, float height, int seed, int variant)
        {
            variant = Mathf.Abs(variant) % TreeArt.Variants;
            var go = new GameObject("Realistic " + species.ToString().ToLowerInvariant() + " tree");
            go.transform.SetParent(root.transform, false);
            go.transform.SetPositionAndRotation(basePoint, Quaternion.Euler(0, seed % 360, 0));
            float scale = Mathf.Clamp(height / TreeArt.CanonicalHeight(species), .45f, 2.2f) * (1 + (seed % 7 - 3) * .025f);
            go.transform.localScale = Vector3.one * scale;
            var materials = TreeArt.Materials(species);
            var lods = new LOD[2];
            for (int l = 0; l < 2; l++)
            {
                var part = new GameObject("LOD" + l); part.transform.SetParent(go.transform, false);
                part.AddComponent<MeshFilter>().sharedMesh = TreeArt.Get(species, variant, l);
                var r = part.AddComponent<MeshRenderer>(); r.sharedMaterials = materials;
                r.shadowCastingMode = ShadowCastingMode.On; r.receiveShadows = true;
                lods[l] = new LOD(l == 0 ? .14f : .006f, new Renderer[] { r });
            }
            var group = go.AddComponent<LODGroup>(); group.fadeMode = LODFadeMode.None; group.SetLODs(lods); group.RecalculateBounds();
            trees.Add(new Planted { Tree = go, Species = species, Base = basePoint, Height = height });
            return go;
        }

        /// <summary>Removes planted trees standing inside an area (cleared for a new road or pitch).</summary>
        public int RemoveTreesIn(Rect area)
        {
            int removed = 0;
            for (int i = trees.Count - 1; i >= 0; i--)
            {
                var t = trees[i]; float reach = t.Height * .3f;
                var expanded = Rect.MinMaxRect(area.xMin - reach, area.yMin - reach, area.xMax + reach, area.yMax + reach);
                if (!expanded.Contains(new Vector2(t.Base.x, t.Base.z))) continue;
                if (t.Tree != null) Destroy(t.Tree); trees.RemoveAt(i); removed++;
            }
            return removed;
        }

        private void Update()
        {
            var weather = WeatherSystem.Active;
            Vector3 dir = weather != null ? weather.WindDirection : new Vector3(.8f, 0, .6f);
            float wind = weather != null ? weather.Wind : .3f;
            Shader.SetGlobalVector(WindId, new Vector4(dir.x, dir.z, Mathf.Lerp(.35f, 2.4f, Mathf.InverseLerp(0, 1.2f, wind)), Mathf.InverseLerp(.4f, 1.2f, wind)));
            Color tint = Color.white;
            if (atmosphere != null)
                switch (atmosphere.Season)
                {
                    case WorldSeason.Autumn: tint = new Color(1.25f, .82f, .38f); break;
                    case WorldSeason.LateAutumn: tint = new Color(1.1f, .62f, .32f); break;
                    case WorldSeason.Winter: tint = new Color(.8f, .86f, .74f); break;
                    case WorldSeason.Spring: tint = new Color(.98f, 1.08f, .86f); break;
                    case WorldSeason.Monsoon: tint = new Color(.88f, 1.02f, .9f); break;
                }
            tint.a = 1; Shader.SetGlobalColor(SeasonId, tint);
        }

        private void OnDestroy()
        {
            if (root != null) Destroy(root);
            foreach (var (f, original) in emptied) if (f != null) f.sharedMesh = original;
            foreach (var r in hidden) if (r != null) r.enabled = true;
            foreach (var l in disabledLods) if (l != null) l.enabled = true;
            foreach (var (filter, original) in stripped) if (filter != null) { var copy = filter.sharedMesh; filter.sharedMesh = original; if (copy != null && copy != original) Destroy(copy); }
        }
    }
}
