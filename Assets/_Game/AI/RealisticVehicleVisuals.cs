using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace RoadsideHaven
{
    public enum VehicleBodyType { Sedan, Hatchback, SUV, Van, Pickup, Coupe, CityBus, Motorbike }

    /// <summary>
    /// Original procedural vehicle designs built from smooth lofted bodies: real wheel arches,
    /// glass greenhouse with painted roof, LED lamps, alloy wheels with brakes, mirrors and trim.
    /// Wheels spin and steer, brake lights glow, and a rider appears on a driven motorbike.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public sealed class RealisticVehicleVisuals : MonoBehaviour
    {
        [SerializeField] private VehicleBodyType type;
        [SerializeField] private int paintSeed;
        private struct Spec { public float Length, Width, WheelBase, WheelRadius, TyreWidth, Ride, Belt, Roof, HoodZ, WindBaseZ, WindTopZ, RoofEndZ, RearBaseZ, NoseY, TailY, FrontOverhang; }
        private static readonly Dictionary<string, Mesh> meshCache = new Dictionary<string, Mesh>();
        private static readonly Dictionary<string, Material> materialCache = new Dictionary<string, Material>();
        private static readonly Color[] Paints =
        {
            new Color(.93f, .93f, .91f), new Color(.03f, .03f, .035f), new Color(.62f, .64f, .66f), new Color(.24f, .26f, .28f), new Color(.06f, .13f, .32f),
            new Color(.62f, .05f, .05f), new Color(.08f, .22f, .15f), new Color(.70f, .63f, .50f), new Color(.32f, .05f, .09f), new Color(.30f, .52f, .72f)
        };
        private Rigidbody body;
        private Transform shell, rider;
        private readonly List<Transform> wheels = new List<Transform>(), steering = new List<Transform>();
        private Renderer brakeLights;
        private MaterialPropertyBlock block;
        private Vector3 lastPosition; private float lastYaw, lastSpeed, wheelAngle;
        private WorldDrivingController driving;
        public VehicleBodyType Type => type;
        public bool IsBike => type == VehicleBodyType.Motorbike;
        public bool Built => shell != null;
        public int WheelCount => wheels.Count;
        public Transform Shell => shell;
        public bool RiderVisible => rider != null && rider.gameObject.activeSelf;

        public void Configure(VehicleBodyType bodyType, int seed) { type = bodyType; paintSeed = seed; }

        /// <summary>Gives a traffic or parked car one of the everyday designs (with the odd sports car).</summary>
        public static void ForTraffic(GameObject vehicle, int index)
        {
            if (index % 9 == 4) { vehicle.AddComponent<ModernCarVisuals>().Configure(index); return; }
            VehicleBodyType[] mix = { VehicleBodyType.Sedan, VehicleBodyType.Hatchback, VehicleBodyType.SUV, VehicleBodyType.Sedan, VehicleBodyType.Van, VehicleBodyType.Pickup, VehicleBodyType.SUV, VehicleBodyType.Hatchback, VehicleBodyType.Coupe };
            vehicle.AddComponent<RealisticVehicleVisuals>().Configure(mix[Mathf.Abs(index) % mix.Length], index * 7 + 3);
        }

        public static Vector3 ColliderSize(VehicleBodyType t)
        {
            switch (t)
            {
                case VehicleBodyType.SUV: return new Vector3(2.0f, 1.75f, 4.7f);
                case VehicleBodyType.Van: return new Vector3(2.0f, 2.1f, 4.9f);
                case VehicleBodyType.Pickup: return new Vector3(2.0f, 1.8f, 5.2f);
                case VehicleBodyType.CityBus: return new Vector3(2.55f, 3.1f, 11.8f);
                case VehicleBodyType.Motorbike: return new Vector3(.8f, 1.3f, 2.15f);
                case VehicleBodyType.Coupe: return new Vector3(1.95f, 1.3f, 4.6f);
                default: return new Vector3(1.9f, 1.45f, 4.6f);
            }
        }

        private static Spec SpecFor(VehicleBodyType t)
        {
            switch (t)
            {
                case VehicleBodyType.Hatchback: return new Spec { Length = 4.1f, Width = 1.8f, WheelBase = 2.6f, WheelRadius = .33f, TyreWidth = .22f, Ride = .22f, Belt = .96f, Roof = 1.48f, HoodZ = 1.15f, WindBaseZ = .95f, WindTopZ = .15f, RoofEndZ = -1.35f, RearBaseZ = -1.85f, NoseY = .62f, TailY = .9f };
                case VehicleBodyType.SUV: return new Spec { Length = 4.7f, Width = 1.95f, WheelBase = 2.85f, WheelRadius = .4f, TyreWidth = .26f, Ride = .34f, Belt = 1.14f, Roof = 1.75f, HoodZ = 1.35f, WindBaseZ = 1.12f, WindTopZ = .3f, RoofEndZ = -1.95f, RearBaseZ = -2.2f, NoseY = .92f, TailY = 1.08f };
                case VehicleBodyType.Van: return new Spec { Length = 4.9f, Width = 1.98f, WheelBase = 3.0f, WheelRadius = .36f, TyreWidth = .24f, Ride = .28f, Belt = 1.12f, Roof = 2.05f, HoodZ = 1.85f, WindBaseZ = 1.75f, WindTopZ = 1.05f, RoofEndZ = -2.35f, RearBaseZ = -2.42f, NoseY = .88f, TailY = 1.1f };
                case VehicleBodyType.Pickup: return new Spec { Length = 5.2f, Width = 1.98f, WheelBase = 3.2f, WheelRadius = .4f, TyreWidth = .27f, Ride = .38f, Belt = 1.16f, Roof = 1.82f, HoodZ = 1.55f, WindBaseZ = 1.3f, WindTopZ = .55f, RoofEndZ = -.55f, RearBaseZ = -.7f, NoseY = 1.0f, TailY = 1.1f };
                case VehicleBodyType.Coupe: return new Spec { Length = 4.55f, Width = 1.9f, WheelBase = 2.7f, WheelRadius = .35f, TyreWidth = .26f, Ride = .18f, Belt = .86f, Roof = 1.28f, HoodZ = 1.05f, WindBaseZ = .85f, WindTopZ = -.05f, RoofEndZ = -.95f, RearBaseZ = -1.95f, NoseY = .52f, TailY = .8f };
                default: return new Spec { Length = 4.6f, Width = 1.85f, WheelBase = 2.8f, WheelRadius = .34f, TyreWidth = .23f, Ride = .2f, Belt = .92f, Roof = 1.42f, HoodZ = 1.2f, WindBaseZ = 1.0f, WindTopZ = .2f, RoofEndZ = -.95f, RearBaseZ = -1.7f, NoseY = .6f, TailY = .9f };
            }
        }

        private void Start()
        {
            body = GetComponent<Rigidbody>();
            block = new MaterialPropertyBlock();
            driving = GetComponentInParent<MotelGameRoot>()?.GetComponentInChildren<WorldDrivingController>(true);
            // Line-up vehicles live outside the motel hierarchy.
            if (driving == null) driving = Object.FindFirstObjectByType<WorldDrivingController>();
            foreach (var r in GetComponentsInChildren<Renderer>()) r.enabled = false;
            foreach (var c in GetComponentsInChildren<Collider>()) if (c.transform != transform && c.attachedRigidbody == body) c.enabled = false;
            foreach (Transform child in transform) if (child.name == "Vehicle mesh") child.name = "Legacy vehicle mesh";
            var size = ColliderSize(type);
            var box = GetComponent<BoxCollider>(); if (box == null) box = gameObject.AddComponent<BoxCollider>();
            if (GetComponent<TownVehicleAgent>() == null || type == VehicleBodyType.CityBus) { box.size = size; box.center = Vector3.up * size.y * .5f; }
            shell = new GameObject("Vehicle mesh").transform; shell.SetParent(transform, false);
            float sign = GetComponent<EnterableVehicle>()?.ForwardSign ?? 1; if (sign < 0) shell.localRotation = Quaternion.Euler(0, 180, 0);
            var paint = Paint(Paints[Mathf.Abs(paintSeed) % Paints.Length]);
            if (type == VehicleBodyType.CityBus) BuildBus(paint);
            else if (type == VehicleBodyType.Motorbike) BuildBike(paint);
            else BuildCar(SpecFor(type), paint);
            foreach (var r in shell.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = r.name.StartsWith("LED") ? ShadowCastingMode.Off : ShadowCastingMode.On;
            lastPosition = transform.position; lastYaw = transform.eulerAngles.y;
        }

        // ================= Materials =================

        private static Material Cached(string key, System.Func<Material> make) { if (!materialCache.TryGetValue(key, out var m) || m == null) { m = make(); materialCache[key] = m; } return m; }
        private static Shader Lit => Shader.Find("Universal Render Pipeline/Lit");
        private static Material Paint(Color c) => Cached("paint" + c, () => { var m = new Material(Lit) { color = c, enableInstancing = true, name = "Car paint" }; m.SetFloat("_Metallic", .55f); m.SetFloat("_Smoothness", .86f); return m; });
        private static Material Simple(string key, Color c, float metal, float smooth) => Cached(key, () => { var m = new Material(Lit) { color = c, enableInstancing = true, name = key }; m.SetFloat("_Metallic", metal); m.SetFloat("_Smoothness", smooth); return m; });
        private static Material Glow(string key, Color c, float intensity) => Cached(key, () => { var m = new Material(Lit) { color = c * .6f, enableInstancing = true, name = key }; m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", c * intensity); m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None; return m; });
        private static Material Glass => Cached("glass", () => { var m = new Material(Lit) { color = new Color(.03f, .05f, .07f), name = "Car glass" }; m.SetFloat("_Metallic", .2f); m.SetFloat("_Smoothness", .97f); return m; });
        private static Material Black => Simple("trim black", new Color(.025f, .027f, .03f), 0, .55f);
        private static Material Chrome => Simple("chrome", new Color(.8f, .82f, .85f), 1, .92f);
        private static Material Rubber => Simple("rubber", new Color(.025f, .025f, .028f), 0, .2f);
        private static Material Alloy => Simple("alloy", new Color(.66f, .68f, .7f), .9f, .75f);
        private static Material DarkAlloy => Simple("dark alloy", new Color(.12f, .13f, .14f), .8f, .6f);

        // ================= Mesh helpers =================

        private Transform Part(string name, Mesh mesh, Material[] mats, Vector3 p, Transform parent = null)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer)); go.transform.SetParent(parent == null ? shell : parent, false); go.transform.localPosition = p;
            go.GetComponent<MeshFilter>().sharedMesh = mesh; go.GetComponent<MeshRenderer>().sharedMaterials = mats; return go.transform;
        }

        private Transform Box(string name, Vector3 p, Vector3 size, Material mat, Transform parent = null, Vector3 euler = default)
        {
            var t = Part(name, CubeMesh(), new[] { mat }, p, parent); t.localScale = size; t.localRotation = Quaternion.Euler(euler); return t;
        }

        private static Mesh CubeMesh()
        {
            if (meshCache.TryGetValue("cube", out var m) && m != null) return m;
            var temp = GameObject.CreatePrimitive(PrimitiveType.Cube); m = temp.GetComponent<MeshFilter>().sharedMesh; Destroy(temp); meshCache["cube"] = m; return m;
        }

        private static Mesh SphereMesh()
        {
            if (meshCache.TryGetValue("sphere", out var m) && m != null) return m;
            var temp = GameObject.CreatePrimitive(PrimitiveType.Sphere); m = temp.GetComponent<MeshFilter>().sharedMesh; Destroy(temp); meshCache["sphere"] = m; return m;
        }

        private static Mesh CylinderMesh()
        {
            if (meshCache.TryGetValue("cylinder", out var m) && m != null) return m;
            var temp = GameObject.CreatePrimitive(PrimitiveType.Cylinder); m = temp.GetComponent<MeshFilter>().sharedMesh; Destroy(temp); meshCache["cylinder"] = m; return m;
        }

        private static float Smooth(float[] zs, float[] ys, float z)
        {
            if (z <= zs[0]) return ys[0]; if (z >= zs[zs.Length - 1]) return ys[ys.Length - 1];
            int i = 0; while (i < zs.Length - 2 && z > zs[i + 1]) i++;
            float t = Mathf.InverseLerp(zs[i], zs[i + 1], z); t = t * t * (3 - 2 * t);
            return Mathf.Lerp(ys[i], ys[i + 1], t);
        }

        /// <summary>
        /// Lofts a closed body: at each station a rounded-box section from bottom(z) to top(z) and
        /// half-width(z). Triangles above <paramref name="splitY"/> go to the second sub-mesh.
        /// </summary>
        private static Mesh Loft(string key, float zMin, float zMax, int stations, System.Func<float, float> halfWidth, System.Func<float, float> bottom, System.Func<float, float> top, float squareness, float tumble, float splitY = float.MaxValue)
        {
            if (meshCache.TryGetValue(key, out var cached) && cached != null) return cached;
            const int ring = 28; var v = new List<Vector3>(); var t0 = new List<int>(); var t1 = new List<int>();
            for (int s = 0; s <= stations; s++)
            {
                float u = s / (float)stations; float z = Mathf.Lerp(zMin, zMax, u);
                float w = halfWidth(z), y0 = bottom(z), y1 = Mathf.Max(top(z), y0 + .01f), mid = (y0 + y1) * .5f, h = (y1 - y0) * .5f;
                for (int k = 0; k < ring; k++)
                {
                    float a = k * Mathf.PI * 2 / ring, c = Mathf.Cos(a), sn = Mathf.Sin(a);
                    float x = Mathf.Sign(c) * Mathf.Pow(Mathf.Abs(c), 2f / squareness) * w;
                    float y = mid + Mathf.Sign(sn) * Mathf.Pow(Mathf.Abs(sn), 2f / squareness) * h;
                    x *= Mathf.Lerp(1, 1 - tumble, Mathf.Clamp01((y - mid) / Mathf.Max(h, .01f)));
                    v.Add(new Vector3(x, y, z));
                }
            }
            for (int s = 0; s < stations; s++) for (int k = 0; k < ring; k++)
                {
                    int a = s * ring + k, b = s * ring + (k + 1) % ring, c = a + ring, d = b + ring;
                    float avg = (v[a].y + v[b].y + v[c].y + v[d].y) * .25f;
                    var list = avg > splitY ? t1 : t0;
                    list.AddRange(new[] { a, c, b, b, c, d });
                }
            // End caps (fans) so a blunt nose or tail is closed.
            foreach (int s in new[] { 0, stations })
            {
                int centre = v.Count; float z = Mathf.Lerp(zMin, zMax, s / (float)stations); v.Add(new Vector3(0, (bottom(z) + top(z)) * .5f, z));
                for (int k = 0; k < ring; k++) { int a = s * ring + k, b = s * ring + (k + 1) % ring; if (s == 0) t0.AddRange(new[] { centre, a, b }); else t0.AddRange(new[] { centre, b, a }); }
            }
            var mesh = new Mesh { name = key, subMeshCount = 2 }; mesh.SetVertices(v); mesh.SetTriangles(t0, 0); mesh.SetTriangles(t1, 1);
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            // Make sure faces point outward (Unity front face normal = cross(b-a, c-a)).
            // Probe the triangle on the +X side at the middle station: (a, c, b) as emitted above.
            int pa = stations / 2 * ring, pb = pa + 1, pc = pa + ring;
            var n = Vector3.Cross(v[pc] - v[pa], v[pb] - v[pa]);
            if (n.x < 0)
            {
                for (int sm = 0; sm < 2; sm++) { var tr = mesh.GetTriangles(sm); for (int i = 0; i < tr.Length; i += 3) (tr[i + 1], tr[i + 2]) = (tr[i + 2], tr[i + 1]); mesh.SetTriangles(tr, sm); }
                mesh.RecalculateNormals();
            }
            meshCache[key] = mesh; return mesh;
        }

        private static Mesh WheelMesh(float radius, float width, string style)
        {
            string key = "wheel" + radius.ToString("F2") + width.ToString("F2") + style;
            if (meshCache.TryGetValue(key, out var cached) && cached != null) return cached;
            var v = new List<Vector3>(); var tyre = new List<int>(); var rim = new List<int>();
            const int seg = 32;
            // Tyre: lathe of a rounded profile around the X axis.
            var profile = new List<Vector2>();
            float rimR = radius * (style == "bike" ? .82f : .66f), hw = width * .5f;
            for (int i = 0; i <= 8; i++) { float a = -Mathf.PI * .5f + i * Mathf.PI / 8; profile.Add(new Vector2(Mathf.Sin(a) * hw, radius - (1 - Mathf.Cos(a)) * width * .35f)); }
            profile.Insert(0, new Vector2(-hw * .95f, rimR)); profile.Add(new Vector2(hw * .95f, rimR));
            for (int s = 0; s <= seg; s++)
            {
                float a = s * Mathf.PI * 2 / seg;
                foreach (var p in profile) v.Add(new Vector3(p.x, Mathf.Cos(a) * p.y, Mathf.Sin(a) * p.y));
            }
            int pc = profile.Count;
            for (int s = 0; s < seg; s++) for (int i = 0; i < pc - 1; i++) { int a = s * pc + i, b = a + 1, c = a + pc, d = c + 1; tyre.AddRange(new[] { a, b, c, b, d, c }); }
            // Rim face with spokes on the outer side (+X), a recessed hub and a brake disc behind.
            int spokes = style == "bike" ? 10 : style == "sport" ? 10 : 6;
            float face = hw * .55f;
            int hub = v.Count; v.Add(new Vector3(face + .02f, 0, 0));
            for (int s = 0; s <= seg; s++) { float a = s * Mathf.PI * 2 / seg; v.Add(new Vector3(face, Mathf.Cos(a) * rimR * .2f, Mathf.Sin(a) * rimR * .2f)); }
            for (int s = 0; s < seg; s++) rim.AddRange(new[] { hub, hub + 1 + s, hub + 2 + s });
            int lip = v.Count;
            for (int s = 0; s <= seg; s++) { float a = s * Mathf.PI * 2 / seg; v.Add(new Vector3(face, Mathf.Cos(a) * rimR, Mathf.Sin(a) * rimR)); v.Add(new Vector3(face - .01f, Mathf.Cos(a) * rimR * .9f, Mathf.Sin(a) * rimR * .9f)); }
            for (int s = 0; s < seg; s++) { int a = lip + s * 2; rim.AddRange(new[] { a, a + 2, a + 1, a + 1, a + 2, a + 3 }); }
            for (int k = 0; k < spokes; k++)
            {
                float a = k * Mathf.PI * 2 / spokes, half = style == "bike" ? .015f : .09f;
                Vector3 Dir(float ang, float r) => new Vector3(face, Mathf.Cos(ang) * r, Mathf.Sin(ang) * r);
                int b0 = v.Count; v.Add(Dir(a - half * 1.6f, rimR * .2f)); v.Add(Dir(a + half * 1.6f, rimR * .2f)); v.Add(Dir(a + half * .8f, rimR * .9f)); v.Add(Dir(a - half * .8f, rimR * .9f));
                rim.AddRange(new[] { b0, b0 + 1, b0 + 2, b0, b0 + 2, b0 + 3 });
            }
            var mesh = new Mesh { name = key, subMeshCount = 2 }; mesh.SetVertices(v); mesh.SetTriangles(tyre, 0); mesh.SetTriangles(rim, 1);
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            // Tyre faces are wound inward by construction; flip them.
            var tt = mesh.GetTriangles(0); for (int i = 0; i < tt.Length; i += 3) (tt[i + 1], tt[i + 2]) = (tt[i + 2], tt[i + 1]); mesh.SetTriangles(tt, 0);
            var rr = mesh.GetTriangles(1); var nrm = Vector3.Cross(v[rr[1]] - v[rr[0]], v[rr[2]] - v[rr[0]]); if (nrm.x < 0) { for (int i = 0; i < rr.Length; i += 3) (rr[i + 1], rr[i + 2]) = (rr[i + 2], rr[i + 1]); mesh.SetTriangles(rr, 1); }
            mesh.RecalculateNormals();
            meshCache[key] = mesh; return mesh;
        }

        private Transform Wheel(Vector3 p, float radius, float width, bool front, string style, Material rimMat)
        {
            var mount = new GameObject(front ? "Front wheel mount" : "Rear wheel mount").transform; mount.SetParent(shell, false); mount.localPosition = p;
            var spin = new GameObject("Wheel").transform; spin.SetParent(mount, false);
            if (p.x < 0) spin.localRotation = Quaternion.Euler(0, 180, 0);
            Part("Wheel tyre and rim", WheelMesh(radius, width, style), new[] { Rubber, rimMat }, Vector3.zero, spin);
            if (style != "bike")
            {
                var disc = Part("Brake disc", CylinderMesh(), new[] { DarkAlloy }, new Vector3(Mathf.Sign(p.x) * -.02f, 0, 0), mount); disc.localScale = new Vector3(radius * 1.1f, .012f, radius * 1.1f); disc.localRotation = Quaternion.Euler(0, 0, 90);
                Box("Brake caliper", new Vector3(Mathf.Sign(p.x) * .02f, radius * .32f, -radius * .3f), new Vector3(.06f, radius * .35f, radius * .28f), front ? Simple("caliper", new Color(.75f, .08f, .05f), .3f, .6f) : DarkAlloy, mount);
            }
            wheels.Add(spin); if (front) steering.Add(mount);
            return mount;
        }

        // ================= Cars, SUVs, vans and pickups =================

        private void BuildCar(Spec s, Material paint)
        {
            float half = s.Length * .5f, hw = s.Width * .5f, fz = s.WheelBase * .5f + (type == VehicleBodyType.Van ? .25f : 0), rz = -s.WheelBase * .5f + (type == VehicleBodyType.Van ? .25f : 0);
            float wy = s.WheelRadius, arch = s.WheelRadius + .07f;
            System.Func<float, float> width = z => { float u = Mathf.Abs(z) / half; return hw * Mathf.Pow(Mathf.Max(0, 1 - Mathf.Pow(u, 7)), 1 / 7f); };
            System.Func<float, float> bottom = z =>
            {
                float b = s.Ride + Mathf.Max(0, Mathf.Abs(z) - half * .82f) * .5f;
                foreach (float c in new[] { fz, rz }) { float dz = z - c; if (Mathf.Abs(dz) < arch) b = Mathf.Max(b, wy + Mathf.Sqrt(arch * arch - dz * dz)); }
                return b;
            };
            float[] pz = { -half, -half * .9f, s.RearBaseZ, s.HoodZ, half * .9f, half };
            float[] py = { s.TailY - .15f, s.TailY, s.Belt, s.Belt - .04f, s.NoseY + .06f, s.NoseY - .08f };
            if (type == VehicleBodyType.Pickup) py = new[] { s.Belt - .02f, s.Belt, s.Belt, s.Belt - .03f, s.NoseY + .08f, s.NoseY - .06f };
            System.Func<float, float> top = z => Smooth(pz, py, z);
            string key = "body" + type;
            Part("Sculpted body", Loft(key, -half, half, 56, width, bottom, top, 4.5f, .06f), new[] { paint, paint }, Vector3.zero);

            // Greenhouse: glass below the roof line, paint on top (floating roof).
            float roofTop = s.Roof;
            float[] gz = { s.RoofEndZ - (s.RoofEndZ - s.RearBaseZ), s.RoofEndZ, s.WindTopZ, s.WindBaseZ };
            float[] gy = { s.Belt - .01f, roofTop, roofTop + .01f, s.Belt - .01f };
            System.Func<float, float> gTop = z => Smooth(gz, gy, z);
            System.Func<float, float> gWidth = z => width(z) * .93f;
            System.Func<float, float> gBottom = z => s.Belt - .06f;
            Part("Glass cabin and roof", Loft("cabin" + type, s.RearBaseZ, s.WindBaseZ, 28, gWidth, gBottom, gTop, 3.6f, .2f, roofTop - .07f), new[] { Glass, type == VehicleBodyType.Coupe || paintSeed % 3 == 0 ? Black : paint }, Vector3.zero);
            if (type == VehicleBodyType.Pickup) BuildBed(s, width);

            // Pillars and window trim.
            for (int side = -1; side <= 1; side += 2)
            {
                float bz = (s.WindTopZ + s.RoofEndZ) * .5f; float bw = gWidth(bz) * .985f;
                Box("B pillar", new Vector3(side * (bw - .01f), (s.Belt + roofTop) * .5f, bz), new Vector3(.03f, roofTop - s.Belt - .04f, .12f), Black, null, new Vector3(0, 0, side * -8));
                Box("Window chrome line", new Vector3(side * (width(0) * .935f), s.Belt - .02f, (s.WindBaseZ + s.RearBaseZ) * .5f), new Vector3(.015f, .025f, s.WindBaseZ - s.RearBaseZ - .2f), Chrome);
                Box("Side skirt", new Vector3(side * (hw - .03f), s.Ride + .06f, (fz + rz) * .5f), new Vector3(.05f, .1f, s.WheelBase - arch * 2 - .1f), Black);
                // Door shut lines and handles.
                foreach (float dz in type == VehicleBodyType.Van ? new[] { s.WindBaseZ - .25f, bz - .05f, -.9f } : new[] { s.WindBaseZ - .2f, bz, s.RearBaseZ + .25f })
                    Box("Door shut line", new Vector3(side * (width(dz) + .002f), (s.Ride + s.Belt) * .55f + .08f, dz), new Vector3(.008f, s.Belt - s.Ride - .32f, .012f), Black);
                foreach (float hz in new[] { bz + .45f, bz - .5f }) Box("Door handle", new Vector3(side * (width(hz) + .01f), s.Belt - .14f, hz), new Vector3(.025f, .035f, .17f), Chrome);
                // Mirror on a stalk at the base of the A pillar.
                var mz = s.WindBaseZ - .18f; Box("Mirror stalk", new Vector3(side * (width(mz) + .04f), s.Belt + .04f, mz), new Vector3(.1f, .04f, .06f), Black);
                Box("Mirror housing", new Vector3(side * (width(mz) + .13f), s.Belt + .1f, mz - .02f), new Vector3(.08f, .11f, .19f), paint);
                // Wheel arch liners give the arches depth.
                foreach (float c in new[] { fz, rz })
                {
                    var liner = Part("Wheel arch liner", CylinderMesh(), new[] { Black }, new Vector3(side * (hw - .16f), wy, c)); liner.localScale = new Vector3(arch * 2.02f, .14f, arch * 2.02f); liner.localRotation = Quaternion.Euler(0, 0, 90);
                }
            }
            string style = type == VehicleBodyType.Coupe ? "sport" : "car";
            var rimMat = type == VehicleBodyType.SUV || type == VehicleBodyType.Pickup ? DarkAlloy : Alloy;
            for (int side = -1; side <= 1; side += 2) { Wheel(new Vector3(side * (hw - s.TyreWidth * .5f + .015f), wy, fz), s.WheelRadius, s.TyreWidth, true, style, rimMat); Wheel(new Vector3(side * (hw - s.TyreWidth * .5f + .015f), wy, rz), s.WheelRadius, s.TyreWidth, false, style, rimMat); }

            // Lamps: slim LED headlights with a light bar, full-width tail bar (brake glow), plates and grille.
            float lz = half * .9f, noseW = width(lz);
            for (int side = -1; side <= 1; side += 2) Box("LED headlight", new Vector3(side * noseW * .66f, s.NoseY + .02f, lz + .02f), new Vector3(noseW * .5f, .07f, .12f), Glow("LED white", new Color(.85f, .95f, 1), 2.4f), null, new Vector3(0, side * -14, 0));
            Box("LED daytime bar", new Vector3(0, s.NoseY + .09f, half * .93f), new Vector3(noseW * 1.1f, .018f, .05f), Glow("LED white", new Color(.85f, .95f, 1), 2.4f));
            Box("Front grille", new Vector3(0, s.NoseY - .2f, half * .95f), new Vector3(noseW * 1.05f, .22f, .06f), Black);
            Box("Front number plate", new Vector3(0, s.Ride + .2f, half * .985f), new Vector3(.52f, .12f, .02f), Simple("plate", new Color(.95f, .95f, .9f), 0, .4f));
            float tz = -half * .93f, tailW = width(tz);
            brakeLights = Box("LED tail light bar", new Vector3(0, s.TailY - .05f, tz - .02f), new Vector3(tailW * 1.85f, .06f, .06f), Glow("LED red", new Color(1, .04f, .02f), 1.6f)).GetComponent<Renderer>();
            Box("Rear number plate", new Vector3(0, s.TailY - .32f, -half * .975f), new Vector3(.52f, .12f, .02f), Simple("plate", new Color(.95f, .95f, .9f), 0, .4f));
            Box("Rear diffuser", new Vector3(0, s.Ride + .08f, -half * .95f), new Vector3(tailW * 1.6f, .12f, .1f), Black);
            if (type == VehicleBodyType.Coupe || type == VehicleBodyType.Sedan) for (int side = -1; side <= 1; side += 2) { var pipe = Part("Exhaust tip", CylinderMesh(), new[] { Chrome }, new Vector3(side * .45f, s.Ride + .1f, -half * .97f)); pipe.localScale = new Vector3(.08f, .05f, .08f); pipe.localRotation = Quaternion.Euler(90, 0, 0); }
            if (type == VehicleBodyType.SUV || type == VehicleBodyType.Van) for (int side = -1; side <= 1; side += 2) Box("Roof rail", new Vector3(side * gWidth((s.WindTopZ + s.RoofEndZ) * .5f) * .78f, roofTop + .06f, (s.WindTopZ + s.RoofEndZ) * .5f), new Vector3(.04f, .05f, (s.WindTopZ - s.RoofEndZ) * .9f), Black);
            if (type == VehicleBodyType.Coupe) Box("Rear lip spoiler", new Vector3(0, s.TailY + .04f, -half * .82f), new Vector3(s.Width * .7f, .03f, .18f), Black);
        }

        private void BuildBed(Spec s, System.Func<float, float> width)
        {
            float half = s.Length * .5f, z0 = -half + .1f, z1 = s.RearBaseZ - .05f, hw = width(0) * .98f;
            Box("Pickup bed floor", new Vector3(0, s.Belt - .38f, (z0 + z1) * .5f), new Vector3(hw * 1.9f, .04f, z1 - z0), Black);
            Box("Pickup tailgate", new Vector3(0, s.Belt - .19f, z0 + .02f), new Vector3(hw * 1.94f, .42f, .05f), Paint(Paints[Mathf.Abs(paintSeed) % Paints.Length]));
            for (int side = -1; side <= 1; side += 2) Box("Pickup bed rail", new Vector3(side * hw, s.Belt + .02f, (z0 + z1) * .5f), new Vector3(.08f, .04f, z1 - z0), Black);
        }

        // ================= City bus =================

        private void BuildBus(Material paint)
        {
            const float length = 11.8f, width = 2.55f, height = 3.05f, ride = .32f; float half = length * .5f, hw = width * .5f;
            float fz = half - 2.5f, rz = -half + 3.0f, wr = .5f, arch = .58f;
            System.Func<float, float> w = z => hw * Mathf.Pow(Mathf.Max(0, 1 - Mathf.Pow(Mathf.Abs(z) / half, 14)), 1 / 14f);
            System.Func<float, float> bottom = z => { float b = ride; foreach (float c in new[] { fz, rz }) { float dz = z - c; if (Mathf.Abs(dz) < arch) b = Mathf.Max(b, wr + Mathf.Sqrt(arch * arch - dz * dz)); } return b; };
            System.Func<float, float> top = z => height - Mathf.Max(0, Mathf.Abs(z) - half * .9f) * .6f;
            var livery = Simple("bus livery white", new Color(.94f, .95f, .95f), .3f, .7f);
            Part("Bus body", Loft("bus body", -half, half, 60, w, bottom, top, 9f, .02f, 1.25f), new[] { paint, livery }, Vector3.zero);
            for (int side = -1; side <= 1; side += 2)
            {
                Box("Bus window band", new Vector3(side * (hw + .005f), 2.0f, -.3f), new Vector3(.02f, 1.15f, length - 2.4f), Glass);
                for (float z = -half + 1.6f; z < half - 1.6f; z += 1.55f) Box("Window pillar", new Vector3(side * (hw + .012f), 2.0f, z), new Vector3(.02f, 1.15f, .08f), Black);
                Box("Bus skirt", new Vector3(side * (hw + .01f), ride + .15f, 0), new Vector3(.02f, .22f, length - 1), Black);
                var mz = half - .1f; Box("Bus mirror arm", new Vector3(side * (hw + .2f), 2.4f, mz), new Vector3(.4f, .04f, .04f), Black); Box("Bus mirror", new Vector3(side * (hw + .4f), 2.1f, mz), new Vector3(.08f, .45f, .25f), Black);
                foreach (float c in new[] { fz, rz }) { var liner = Part("Wheel arch liner", CylinderMesh(), new[] { Black }, new Vector3(side * (hw - .2f), wr, c)); liner.localScale = new Vector3(arch * 2.02f, .18f, arch * 2.02f); liner.localRotation = Quaternion.Euler(0, 0, 90); }
                Wheel(new Vector3(side * (hw - .16f), wr, fz), wr, .3f, true, "car", Alloy); Wheel(new Vector3(side * (hw - .16f), wr, rz), wr, .3f, false, "car", Alloy);
            }
            Box("Bus windscreen", new Vector3(0, 1.85f, half + .005f), new Vector3(width - .25f, 1.7f, .02f), Glass);
            Box("Bus rear window", new Vector3(0, 2.2f, -half - .005f), new Vector3(width - .5f, .8f, .02f), Glass);
            // Passenger doors on the kerb side (right).
            foreach (float dz in new[] { half - 1.3f, -.6f }) { Box("Bus door glass", new Vector3(hw + .015f, 1.35f, dz), new Vector3(.02f, 2.2f, 1.2f), Glass); Box("Bus door seam", new Vector3(hw + .02f, 1.35f, dz), new Vector3(.02f, 2.2f, .03f), Black); }
            var sign = Box("Destination sign", new Vector3(0, 2.82f, half + .01f), new Vector3(1.9f, .3f, .03f), Black);
            var label = new GameObject("Destination text").AddComponent<TMPro.TextMeshPro>(); label.transform.SetParent(shell, false); label.transform.localPosition = new Vector3(0, 2.82f, half + .03f); label.transform.localRotation = Quaternion.Euler(0, 180, 0);
            label.text = "12  CITY CENTRE"; label.color = new Color(1, .65f, .1f); label.alignment = TMPro.TextAlignmentOptions.Center; label.rectTransform.sizeDelta = new Vector2(1.8f, .26f); label.enableAutoSizing = true; label.fontSizeMin = .5f; label.fontSizeMax = 2;
            Box("Roof air conditioning", new Vector3(0, height + .14f, -1.5f), new Vector3(1.6f, .28f, 3.2f), livery);
            for (int side = -1; side <= 1; side += 2) Box("LED headlight", new Vector3(side * (hw - .35f), .75f, half + .01f), new Vector3(.45f, .12f, .04f), Glow("LED white", new Color(.85f, .95f, 1), 2.4f));
            brakeLights = Box("LED tail light bar", new Vector3(0, .95f, -half - .01f), new Vector3(width - .3f, .12f, .04f), Glow("LED red", new Color(1, .04f, .02f), 1.6f)).GetComponent<Renderer>();
            Box("Bus bumper", new Vector3(0, .5f, half + .02f), new Vector3(width, .3f, .08f), Black); Box("Bus rear bumper", new Vector3(0, .5f, -half - .02f), new Vector3(width, .3f, .08f), Black);
        }

        // ================= Motorbike =================

        private void BuildBike(Material paint)
        {
            float wr = .33f, wb = 1.42f; var frame = DarkAlloy;
            Wheel(new Vector3(0, wr, wb * .5f), wr, .13f, true, "bike", Black);
            Wheel(new Vector3(0, wr, -wb * .5f), wr, .17f, false, "bike", Black);
            var fork = new GameObject("Front fork").transform; fork.SetParent(steering[0], false);
            for (int side = -1; side <= 1; side += 2) Box("Fork leg", new Vector3(side * .1f, .32f, -.12f), new Vector3(.05f, .72f, .05f), Chrome, fork, new Vector3(-24, 0, 0));
            Box("Handlebar", new Vector3(0, .78f, -.38f), new Vector3(.72f, .035f, .035f), Black, fork);
            for (int side = -1; side <= 1; side += 2) Box("Grip", new Vector3(side * .38f, .78f, -.38f), new Vector3(.12f, .045f, .045f), Rubber, fork);
            Box("Headlight cowl", new Vector3(0, .62f, -.18f), new Vector3(.28f, .22f, .2f), paint, fork);
            Box("LED headlight", new Vector3(0, .6f, -.07f), new Vector3(.2f, .08f, .03f), Glow("LED white", new Color(.85f, .95f, 1), 2.4f), fork);
            Box("Front mudguard", new Vector3(0, wr + .36f, .02f), new Vector3(.16f, .03f, .5f), paint, fork);
            // Frame, tank, seat, engine and exhaust.
            Box("Main frame", new Vector3(0, .62f, .12f), new Vector3(.08f, .08f, 1.0f), frame, null, new Vector3(18, 0, 0));
            var tank = Part("Fuel tank", SphereMesh(), new[] { paint }, new Vector3(0, .92f, .22f)); tank.localScale = new Vector3(.36f, .26f, .62f);
            Box("Tank stripe", new Vector3(0, 1.04f, .22f), new Vector3(.08f, .02f, .5f), Simple("bike stripe", new Color(.95f, .95f, .95f), 0, .6f));
            Box("Rider seat", new Vector3(0, .9f, -.32f), new Vector3(.3f, .1f, .62f), Black);
            Box("Tail section", new Vector3(0, .94f, -.72f), new Vector3(.24f, .14f, .4f), paint, null, new Vector3(-10, 0, 0));
            brakeLights = Box("LED tail light", new Vector3(0, .93f, -.93f), new Vector3(.16f, .05f, .03f), Glow("LED red", new Color(1, .04f, .02f), 1.6f)).GetComponent<Renderer>();
            Box("Engine block", new Vector3(0, .48f, .05f), new Vector3(.32f, .34f, .46f), DarkAlloy);
            Box("Engine fins", new Vector3(0, .6f, .2f), new Vector3(.36f, .04f, .2f), Alloy);
            var exhaust = Part("Exhaust", CylinderMesh(), new[] { Chrome }, new Vector3(.17f, .42f, -.5f)); exhaust.localScale = new Vector3(.09f, .38f, .09f); exhaust.localRotation = Quaternion.Euler(80, 0, 0);
            Box("Swing arm", new Vector3(0, .38f, -.38f), new Vector3(.24f, .05f, .7f), frame, null, new Vector3(-8, 0, 0));
            Box("Rear mudguard", new Vector3(0, wr + .3f, -.82f), new Vector3(.18f, .03f, .3f), paint, null, new Vector3(20, 0, 0));
            for (int side = -1; side <= 1; side += 2) Box("Foot peg", new Vector3(side * .2f, .38f, -.15f), new Vector3(.12f, .03f, .04f), Chrome);
            BuildRider();
        }

        private void BuildRider()
        {
            rider = new GameObject("Rider").transform; rider.SetParent(shell, false);
            var jacket = Simple("rider jacket", new Color(.08f, .09f, .1f), 0, .4f); var jeans = Simple("rider jeans", new Color(.12f, .17f, .28f), 0, .3f);
            Box("Rider torso", new Vector3(0, 1.32f, -.2f), new Vector3(.38f, .55f, .26f), jacket, rider, new Vector3(28, 0, 0));
            var helmet = Part("Rider helmet", SphereMesh(), new[] { Simple("helmet", new Color(.85f, .1f, .08f), .3f, .85f) }, new Vector3(0, 1.72f, -.02f), rider); helmet.localScale = Vector3.one * .3f;
            var visor = Part("Helmet visor", SphereMesh(), new[] { Glass }, new Vector3(0, 1.72f, .06f), rider); visor.localScale = new Vector3(.26f, .14f, .2f);
            for (int side = -1; side <= 1; side += 2)
            {
                Box("Rider arm", new Vector3(side * .25f, 1.28f, .1f), new Vector3(.09f, .09f, .5f), jacket, rider, new Vector3(20, side * -12, 0));
                Box("Rider thigh", new Vector3(side * .15f, .98f, -.08f), new Vector3(.13f, .13f, .42f), jeans, rider, new Vector3(-5, 0, 0));
                Box("Rider shin", new Vector3(side * .18f, .65f, -.15f), new Vector3(.11f, .42f, .11f), jeans, rider, new Vector3(-25, 0, 0));
            }
            rider.gameObject.SetActive(false);
        }

        // ================= Animation =================

        private void LateUpdate()
        {
            if (shell == null) return;
            float dt = Mathf.Max(Time.deltaTime, 1e-4f);
            Vector3 delta = transform.position - lastPosition; lastPosition = transform.position;
            float sign = GetComponent<EnterableVehicle>()?.ForwardSign ?? 1;
            float forward = Vector3.Dot(delta, transform.forward * sign) / dt;
            float yawRate = Mathf.DeltaAngle(lastYaw, transform.eulerAngles.y) / dt; lastYaw = transform.eulerAngles.y;
            if ((Camera.main != null && (Camera.main.transform.position - transform.position).sqrMagnitude > 160 * 160)) return;
            float radius = type == VehicleBodyType.CityBus ? .5f : type == VehicleBodyType.Motorbike ? .33f : .36f;
            wheelAngle += forward / radius * Mathf.Rad2Deg * dt;
            foreach (var w in wheels) w.localRotation = Quaternion.Euler(wheelAngle * (w.parent.localPosition.x < 0 ? -1 : 1), w.parent.localPosition.x < 0 ? 180 : 0, 0);
            float steer = Mathf.Abs(forward) > .5f ? Mathf.Clamp(yawRate / Mathf.Max(2f, Mathf.Abs(forward)) * 18f * Mathf.Sign(forward), -32, 32) : 0;
            foreach (var m in steering) m.localRotation = Quaternion.Slerp(m.localRotation, Quaternion.Euler(0, steer, 0), dt * 8);
            if (brakeLights != null)
            {
                float decel = (lastSpeed - Mathf.Abs(forward)) / dt; lastSpeed = Mathf.Abs(forward);
                brakeLights.GetPropertyBlock(block);
                block.SetColor("_EmissionColor", new Color(1, .04f, .02f) * (decel > 2.5f ? 6f : 1.6f));
                brakeLights.SetPropertyBlock(block); block.Clear();
            }
            if (rider != null)
            {
                bool riding = driving != null && driving.IsDriving && driving.ActiveVehicleTransform == transform;
                if (rider.gameObject.activeSelf != riding) rider.gameObject.SetActive(riding);
            }
        }
    }
}
