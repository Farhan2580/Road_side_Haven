using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

namespace RoadsideHaven
{
    /// <summary>
    /// Haven International becomes a modern international airport (all coordinates are airport-local):
    /// • a two-level terminal under an undulating roof on tree columns: glass curtain walls, an upper departures hall
    ///   (check-in islands, security, shops, flight boards, apron lounge) and a lower arrivals hall (baggage carousels,
    ///   meeters-and-greeters), linked by escalators;
    /// • the classic two-level forecourt: an elevated DEPARTURES drop-off road with ramps "up" and "down" and an
    ///   ARRIVALS pick-up lane beneath, overhead gantry signs, taxis and shuttles that drive the loop;
    /// • proper access roads (lane lines, edge lines, street lights), a landmark control tower, contact gates with
    ///   jet bridges, ground-service vehicles, realistic airliners in several liveries and a circuit of landings and
    ///   take-offs, plus realistic helicopters including a city sightseeing flight.
    /// </summary>
    public sealed class AirportUpgrade : MonoBehaviour
    {
        // ---------------- Layout (airport-local) ----------------
        public const float TerminalWest = 690, TerminalEast = 890, TerminalFront = -252, TerminalRear = -300, UpperLevel = 6;
        public const float DeckWest = 680, DeckEast = 860, DeckSouth = -243, DeckNorth = -233, RampWest = 590, RampEast = 930;
        private static readonly float[] NewGates = { 725, 835 };

        public bool Ready { get; private set; }
        public Transform Root => root;
        public AirportOperations Airport => ops;
        public int Aircraft => airliners.Count;
        public int Helicopters => helis.Count;
        public int ForecourtVehicles => movers.Count;
        public int People => people.Count;
        public int JetBridges { get; private set; }
        public string BoardText => board == null ? "" : board.text;
        public Vector3 DeckPoint(float x) => root.TransformPoint(new Vector3(x, UpperLevel + .1f, (DeckSouth + DeckNorth) * .5f));
        public Transform CircuitAircraft => circuit;
        public Transform TourHelicopter => tour;
        public IReadOnlyList<Transform> ForecourtMovers => movers.Select(m => m.T).ToList();

        private AirportOperations ops; private Transform root, art, circuit, tour, tug; private TextMeshPro board, boardArrivals;
        private readonly List<AircraftArt.Airliner> airliners = new List<AircraftArt.Airliner>();
        private readonly List<AircraftArt.Helicopter> helis = new List<AircraftArt.Helicopter>();
        private readonly List<Material> owned = new List<Material>();
        private readonly List<GameObject> hidden = new List<GameObject>();
        private readonly List<Renderer> beacons = new List<Renderer>();
        private Material white, glass, steel, dark, concrete, asphalt, paint, yellowPaint, wood, ledWarm, ledCool, signBlue, rubber, grass, bellows, taxiYellow, carpet, screen;
        private Mesh cube, cylinder, sphere;
        private MotelAtmosphere clock; private float nextBoard;

        private sealed class Mover { public Transform T; public Vector3[] Path; public float[] Waits; public int I; public float Wait, Speed; public bool Vehicle; public HumanVisual Human; }
        private readonly List<Mover> movers = new List<Mover>(), people = new List<Mover>();

        // =====================================================================
        private IEnumerator Start()
        {
            float give = Time.realtimeSinceStartup + 30;
            while (ops == null && Time.realtimeSinceStartup < give) { ops = GetComponentInChildren<AirportOperations>(true); if (ops == null) yield return null; }
            if (ops == null) yield break;
            yield return null; yield return null; // AirportOperations.Start converts its passengers first
            clock = GetComponent<MotelAtmosphere>();
            root = ops.transform; art = new GameObject("Airport modern upgrade").transform; art.SetParent(root, false);
            // Let the town populate first, then build over several frames so start-up never stalls.
            float settle = Time.time + 2; while (Time.time < settle) yield return null;
            Materials(); HideOld(); yield return null;
            Terminal(); yield return null; Forecourt(); AccessRoads(); yield return null; Tower(); Gates(); yield return null; Apron(); CarPark(); yield return null;
            Combine(art.Find("Static architecture")); yield return null;
            Planes(); yield return null; HelicoptersAll(); yield return null;
            ForecourtTraffic(); Passengers();
            Ready = true;
        }

        // ---------------- materials & helpers ----------------
        private Material Lit(string n, Color c, float smooth = .4f, float metal = 0) { var m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = n, color = c, enableInstancing = true }; m.SetFloat("_Smoothness", smooth); m.SetFloat("_Metallic", metal); owned.Add(m); return m; }
        private Material Glow(string n, Color c, float k) { var m = Lit(n, c * .7f); m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", c * k); m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None; return m; }
        private void Materials()
        {
            white = Lit("Terminal white", new Color(.93f, .94f, .95f), .55f); steel = Lit("Terminal steel", new Color(.62f, .66f, .7f), .75f, .8f);
            dark = Lit("Terminal graphite", new Color(.08f, .09f, .1f), .5f); concrete = Lit("Terminal concrete", new Color(.62f, .63f, .62f), .2f);
            asphalt = Lit("Forecourt asphalt", new Color(.11f, .12f, .13f), .15f); paint = Lit("Road paint white", new Color(.92f, .92f, .88f), .2f); yellowPaint = Lit("Road paint yellow", new Color(.98f, .78f, .1f), .2f);
            wood = Glow("Roof timber soffit", new Color(.9f, .74f, .54f), .22f); wood.SetFloat("_Smoothness", .35f); rubber = Lit("Rubber", new Color(.03f, .03f, .03f), .2f); grass = Lit("Planting", new Color(.2f, .45f, .16f), .1f);
            bellows = Lit("Jet bridge bellows", new Color(.12f, .12f, .13f), .2f); carpet = Lit("Terminal stone floor", new Color(.78f, .76f, .72f), .6f); taxiYellow = Lit("Taxi yellow", new Color(1f, .78f, .05f), .75f, .1f);
            ledWarm = Glow("Warm LED", new Color(1, .82f, .55f), 2.2f); ledCool = Glow("Cool LED", new Color(.7f, .9f, 1), 2.5f); signBlue = Glow("Sign blue", new Color(.05f, .2f, .45f), .6f); screen = Glow("Screen", new Color(.05f, .08f, .14f), .5f);
            glass = Lit("Terminal glass", new Color(.36f, .55f, .62f, .32f), .95f, .2f);
            glass.SetFloat("_Surface", 1); glass.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha); glass.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha); glass.SetFloat("_ZWrite", 0); glass.SetFloat("_Cull", 0);
            glass.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); glass.renderQueue = 3000; glass.SetShaderPassEnabled("ShadowCaster", false);
            var c = GameObject.CreatePrimitive(PrimitiveType.Cube); cube = c.GetComponent<MeshFilter>().sharedMesh; DestroyImmediate(c);
            c = GameObject.CreatePrimitive(PrimitiveType.Cylinder); cylinder = c.GetComponent<MeshFilter>().sharedMesh; DestroyImmediate(c);
            c = GameObject.CreatePrimitive(PrimitiveType.Sphere); sphere = c.GetComponent<MeshFilter>().sharedMesh; DestroyImmediate(c);
        }
        private Transform Group(string name, Transform parent = null) { var t = parent == null ? art.Find(name) : parent.Find(name); if (t != null) return t; t = new GameObject(name).transform; t.SetParent(parent ?? art, false); return t; }
        private Transform Static => Group("Static architecture");
        private GameObject Part(Transform p, string n, Mesh mesh, Vector3 pos, Vector3 size, Material m, Vector3? euler = null, bool solid = false)
        {
            var go = new GameObject(n, typeof(MeshFilter), typeof(MeshRenderer)); go.transform.SetParent(p, false); go.transform.localPosition = pos; go.transform.localScale = size;
            if (euler.HasValue) go.transform.localRotation = Quaternion.Euler(euler.Value);
            go.GetComponent<MeshFilter>().sharedMesh = mesh; go.GetComponent<MeshRenderer>().sharedMaterial = m;
            if (solid) go.AddComponent<BoxCollider>();
            return go;
        }
        private GameObject Box(Transform p, string n, Vector3 pos, Vector3 size, Material m, bool solid = false, Vector3? euler = null) => Part(p, n, cube, pos, size, m, euler, solid);
        private GameObject Beam(Transform p, string n, Vector3 a, Vector3 b, float r, Material m)
        {
            var go = Part(p, n, cylinder, (a + b) * .5f, new Vector3(r * 2, (b - a).magnitude * .5f, r * 2), m);
            go.transform.localRotation = Quaternion.FromToRotation(Vector3.up, b - a); return go;
        }
        /// <summary>Sloped slab whose top surface runs from a (low) to b (high) along X, width along Z.</summary>
        private void Ramp(Transform p, string n, Vector3 a, Vector3 b, float width, Material m, float thick = .5f)
        {
            if (b.x < a.x) { var s = a; a = b; b = s; }
            var d = b - a; float len = d.magnitude; float ang = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
            var mid = (a + b) * .5f; var normal = new Vector3(-Mathf.Sin(ang * Mathf.Deg2Rad), Mathf.Cos(ang * Mathf.Deg2Rad), 0);
            Box(p, n, mid - normal * thick * .5f, new Vector3(len, thick, width), m, true, new Vector3(0, 0, ang));
        }
        private TextMeshPro Text(Transform p, string text, Vector3 pos, float yaw, Vector2 size, Color color, float max = 20, bool bold = true)
        {
            var t = new GameObject("Sign " + text).AddComponent<TextMeshPro>(); t.transform.SetParent(p, false); t.transform.localPosition = pos; t.transform.localRotation = Quaternion.Euler(0, yaw, 0);
            if (TMP_Settings.defaultFontAsset != null) t.font = TMP_Settings.defaultFontAsset;
            t.text = text; t.color = color; t.alignment = TextAlignmentOptions.Center; t.textWrappingMode = TextWrappingModes.NoWrap; t.enableAutoSizing = true; t.fontSizeMin = .1f; t.fontSizeMax = max;
            if (bold) t.fontStyle = FontStyles.Bold; t.rectTransform.sizeDelta = size; var m = t.fontMaterial; if (m.HasProperty("_CullMode")) m.SetFloat("_CullMode", (float)CullMode.Back); owned.Add(m); return t;
        }

        // ---------------- remove the old boxes ----------------
        private void HideOld()
        {
            foreach (var n in new[] { "Airport terminal", "ATC tower base", "ATC control room", "ATC roof", "ATC antenna", "TERMINAL / DEPARTURES", "HAVEN INTERNATIONAL AIRPORT" })
                foreach (Transform c in root) if (c.name == n || c.name.StartsWith(n)) { c.gameObject.SetActive(false); hidden.Add(c.gameObject); }
        }

        // =====================================================================
        // Terminal
        // =====================================================================
        /// <summary>Underside height of the undulating roof.</summary>
        public static float RoofAt(float x, float z)
        {
            float u = Mathf.InverseLerp(672, 908, x), v = Mathf.InverseLerp(-308, -226, z);
            return 18.5f + 2.4f * Mathf.Sin(u * Mathf.PI * 3 - .4f) * (.6f + .4f * Mathf.Sin(v * Mathf.PI)) + 2.2f * Mathf.Sin(v * Mathf.PI);
        }
        private void Terminal()
        {
            var t = Static;
            // Floors: arrivals (ground) and departures (upper) with an atrium and escalators.
            Box(t, "Arrivals hall floor", new Vector3(790, .04f, -276), new Vector3(TerminalEast - TerminalWest, .24f, TerminalRear - TerminalFront < 0 ? 48 : 48), carpet, true);
            foreach (var piece in new[] { Rect.MinMaxRect(TerminalWest, -300, 770, -252), Rect.MinMaxRect(810, -300, TerminalEast, -252), Rect.MinMaxRect(770, -300, 810, -280) })
                Box(t, "Departures level floor", new Vector3(piece.center.x, UpperLevel - .15f, piece.center.y), new Vector3(piece.width, .3f, piece.height), carpet, true);
            Box(t, "Departures slab edge", new Vector3(790, UpperLevel - .45f, -280.2f), new Vector3(40, .6f, .4f), white);
            foreach (float x in new[] { 779f, 783f, 797f, 801f })
            {
                var e = new GameObject("Escalator").transform; e.SetParent(t, false); e.localPosition = new Vector3(x, 0, -262); e.localRotation = Quaternion.Euler(0, -90, 0);
                Ramp(e, "Escalator steps", new Vector3(-18, UpperLevel, 0), new Vector3(0, .16f, 0), 1.3f, steel, .4f);
                foreach (int s in new[] { -1, 1 }) Box(e, "Escalator glass balustrade", new Vector3(-9, 3.6f, s * .75f), new Vector3(18.9f, 1.0f, .05f), glass, false, new Vector3(0, 0, -18.4f));
            }
            // Curtain walls: front, rear and sides follow the roof line; mullions every 6 m.
            for (float x = TerminalWest; x < TerminalEast; x += 6)
            {
                float cx = x + 3, hf = RoofAt(cx, TerminalFront) - .4f, hr = RoofAt(cx, TerminalRear) - .4f;
                bool frontDoor = Mathf.Abs(cx - 790) < 4 || Mathf.Abs(cx - 730) < 4 || Mathf.Abs(cx - 850) < 4;
                if (!frontDoor) Box(t, "Front curtain wall", new Vector3(cx, hf * .5f, TerminalFront), new Vector3(5.9f, hf, .12f), glass, true);
                else
                {
                    // Open doorways on both levels: arrivals at the ground, departures onto the kerbside walkway.
                    Box(t, "Front curtain wall", new Vector3(cx, (4.2f + UpperLevel - .3f) * .5f, TerminalFront), new Vector3(5.9f, UpperLevel - .3f - 4.2f, .12f), glass);
                    Box(t, "Front curtain wall", new Vector3(cx, (UpperLevel + 3.4f + hf) * .5f, TerminalFront), new Vector3(5.9f, hf - UpperLevel - 3.4f, .12f), glass);
                    Box(t, "Door transom", new Vector3(cx, UpperLevel + 3.3f, TerminalFront), new Vector3(5.9f, .25f, .3f), steel);
                }
                if (frontDoor) { Box(t, "Door transom", new Vector3(cx, 4.3f, TerminalFront), new Vector3(5.9f, .25f, .3f), steel); Text(t, "ARRIVALS", new Vector3(cx, 4.8f, TerminalFront - .2f), 180, new Vector2(5, .7f), new Color(1, .85f, .3f), 6); }
                bool rampGap = Mathf.Abs(cx - 750) < 4;
                Box(t, "Rear curtain wall", new Vector3(cx, (hr + (rampGap ? UpperLevel : 0)) * .5f, TerminalRear), new Vector3(5.9f, hr - (rampGap ? UpperLevel : 0), .12f), glass, !rampGap);
                Box(t, "Front mullion", new Vector3(x, hf * .5f, TerminalFront - .1f), new Vector3(.18f, hf, .35f), steel);
                Box(t, "Rear mullion", new Vector3(x, hr * .5f, TerminalRear + .1f), new Vector3(.18f, hr, .35f), steel);
                foreach (float y in new[] { UpperLevel - .2f, 12.5f }) { Box(t, "Transom", new Vector3(cx, y, TerminalFront - .1f), new Vector3(6, .14f, .3f), steel); Box(t, "Transom", new Vector3(cx, y, TerminalRear + .1f), new Vector3(6, .14f, .3f), steel); }
            }
            foreach (float x in new[] { TerminalWest, TerminalEast })
                for (float z = TerminalRear; z < TerminalFront; z += 6) { float h = RoofAt(x, z + 3) - .4f; Box(t, "Side curtain wall", new Vector3(x, h * .5f, z + 3), new Vector3(.12f, h, 5.9f), glass, true); Box(t, "Side mullion", new Vector3(x, h * .5f, z), new Vector3(.35f, h, .18f), steel); }
            // Departures entrances on the upper level (to the drop-off deck).
            foreach (float x in new[] { 730f, 790f, 850f })
            {
                Box(t, "Departures entrance canopy", new Vector3(x, UpperLevel + 3.4f, TerminalFront + 1.5f), new Vector3(10, .25f, 3), steel);
                Text(t, "DEPARTURES", new Vector3(x, UpperLevel + 4.4f, TerminalFront - .25f), 180, new Vector2(9, 1), Color.white, 8);
            }
            // The undulating roof with timber soffit, LED edge and tree columns.
            RoofMesh(t);
            for (float x = 700; x <= 880; x += 36) foreach (float z in new[] { -232f, -276f, -306f }) TreeColumn(t, x, z);
            Text(t, "HAVEN INTERNATIONAL AIRPORT", new Vector3(790, RoofAt(790, -228) + 2.2f, -227.6f), 180, new Vector2(90, 5), Color.white, 40);
            Text(t, "HAVEN INTERNATIONAL AIRPORT", new Vector3(790, RoofAt(790, -308) + 2.2f, -308.4f), 0, new Vector2(90, 5), Color.white, 40);
            Interior();
        }
        private void RoofMesh(Transform t)
        {
            const int nx = 48, nz = 18; float x0 = 672, x1 = 908, z0 = -308, z1 = -226;
            var top = new List<Vector3>(); var bottom = new List<Vector3>(); var uv = new List<Vector2>(); var tris = new List<int>(); var trisB = new List<int>();
            for (int j = 0; j <= nz; j++) for (int i = 0; i <= nx; i++)
                {
                    float x = Mathf.Lerp(x0, x1, i / (float)nx), z = Mathf.Lerp(z0, z1, j / (float)nz), y = RoofAt(x, z);
                    bottom.Add(new Vector3(x, y, z)); top.Add(new Vector3(x, y + 1.1f - .5f * Mathf.Abs(j / (float)nz - .5f), z)); uv.Add(new Vector2(x * .25f, z * .25f));
                    if (i > 0 && j > 0) { int a = j * (nx + 1) + i, b = a - 1, c = a - (nx + 1), d = c - 1; tris.AddRange(new[] { d, b, a, d, a, c }); trisB.AddRange(new[] { d, a, b, d, c, a }); }
                }
            Mesh M(List<Vector3> v, List<int> tr, string n) { var m = new Mesh { name = n, indexFormat = IndexFormat.UInt32 }; m.SetVertices(v); m.SetUVs(0, uv); m.SetTriangles(tr, 0); m.RecalculateNormals(); m.RecalculateBounds(); return m; }
            var roofTop = Part(t, "Undulating roof", M(top, tris, "Terminal roof top"), Vector3.zero, Vector3.one, white);
            Part(t, "Roof timber soffit", M(bottom, trisB, "Terminal roof soffit"), Vector3.zero, Vector3.one, wood);
            // Fascia and LED edge around the perimeter.
            for (int i = 0; i < nx; i++)
                foreach (float z in new[] { z0, z1 })
                {
                    float xa = Mathf.Lerp(x0, x1, i / (float)nx), xb = Mathf.Lerp(x0, x1, (i + 1) / (float)nx);
                    var a = new Vector3(xa, RoofAt(xa, z), z); var b = new Vector3(xb, RoofAt(xb, z), z);
                    var f = Box(t, "Roof fascia", (a + b) * .5f + Vector3.up * .5f, new Vector3((b - a).magnitude + .05f, .95f, .25f), white); f.transform.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(b.y - a.y, b.x - a.x) * Mathf.Rad2Deg);
                    var l = Box(t, "Roof LED edge", (a + b) * .5f - Vector3.up * .05f + Vector3.forward * (z < -260 ? -.14f : .14f), new Vector3((b - a).magnitude + .05f, .08f, .05f), ledCool); l.transform.localRotation = f.transform.localRotation;
                }
            // Skylight ribbons.
            for (float x = 700; x <= 880; x += 30) Box(t, "Skylight", new Vector3(x, RoofAt(x, -276) + 1.15f, -276), new Vector3(3, .06f, 40), glass);
        }
        private void TreeColumn(Transform t, float x, float z)
        {
            float top = RoofAt(x, z); float trunkTop = top - 5.5f;
            Beam(t, "Tree column trunk", new Vector3(x, 0, z), new Vector3(x, trunkTop, z), .55f, white);
            for (int k = 0; k < 4; k++)
            {
                float a = k * Mathf.PI / 2 + Mathf.PI / 4; var end = new Vector3(x + Mathf.Cos(a) * 5f, 0, z + Mathf.Sin(a) * 4f); end.y = RoofAt(end.x, end.z) - .05f;
                Beam(t, "Tree column branch", new Vector3(x, trunkTop - .3f, z), end, .22f, white);
            }
        }
        private void Interior()
        {
            var t = Group("Terminal interior");
            float up = UpperLevel;
            // Check-in islands (departures level).
            for (int i = 0; i < 4; i++)
            {
                float x = 712 + i * 18; if (x > 765) x += 48;
                var island = new GameObject("Check-in island " + (char)('A' + i)).transform; island.SetParent(t, false); island.localPosition = new Vector3(x, up, -283);
                Box(island, "Island spine", new Vector3(0, 1.6f, 0), new Vector3(1.2f, 3.2f, 16), white, true);
                Box(island, "Island header", new Vector3(0, 3.6f, 0), new Vector3(1.6f, .9f, 16.4f), signBlue);
                foreach (int s in new[] { -1, 1 })
                {
                    Text(island, "CHECK-IN  " + (char)('A' + i), new Vector3(s * .82f, 3.6f, 0), s > 0 ? -90 : 90, new Vector2(15, .7f), Color.white, 6);
                    for (int k = 0; k < 5; k++) { Box(island, "Check-in desk", new Vector3(s * 1.5f, .55f, -6 + k * 3), new Vector3(1.4f, 1.1f, 2.2f), white, true); Box(island, "Desk screen", new Vector3(s * 1.1f, 1.45f, -6 + k * 3), new Vector3(.06f, .45f, .7f), screen); Box(island, "Bag belt", new Vector3(s * 1.8f, .35f, -5 + k * 3), new Vector3(1.0f, .2f, .8f), dark); }
                }
            }
            // Security lanes and duty free.
            for (int i = 0; i < 6; i++) { float x = 742 + i * 3.4f; Box(t, "Security scanner arch", new Vector3(x, up + 1.2f, -296), new Vector3(1.2f, 2.4f, .5f), steel); Box(t, "Security belt", new Vector3(x + 1.2f, up + .5f, -295), new Vector3(.8f, 1, 4), dark); }
            Text(t, "SECURITY  /  ALL GATES", new Vector3(752, up + 4.2f, -293.5f), 180, new Vector2(18, 1.2f), new Color(1, .85f, .3f), 8);
            foreach (var shop in new[] { (x: 838f, n: "DUTY FREE"), (x: 858f, n: "CAFE"), (x: 878f, n: "BOOKS & GIFTS") })
            {
                Box(t, "Shop unit", new Vector3(shop.x, up + 2, -259), new Vector3(14, 4, 7), white, true);
                Box(t, "Shop glow front", new Vector3(shop.x, up + 2, -262.6f), new Vector3(13.6f, 3.6f, .1f), ledWarm);
                Text(t, shop.n, new Vector3(shop.x, up + 4.5f, -262.7f), 0, new Vector2(12, 1), Color.white, 8);
            }
            // Flight information boards (both halls).
            var fids = new GameObject("Departures board").transform; fids.SetParent(t, false); fids.localPosition = new Vector3(790, up + 5.2f, -279.5f);
            Box(fids, "Board frame", Vector3.zero, new Vector3(22, 5.2f, .3f), dark);
            board = Text(fids, "", new Vector3(0, 0, -.17f), 180, new Vector2(21, 5), new Color(1, .82f, .25f), 4, false); board.alignment = TextAlignmentOptions.TopLeft; board.textWrappingMode = TextWrappingModes.Normal; board.enableAutoSizing = false; board.fontSize = 3.2f;
            var arr = new GameObject("Arrivals board").transform; arr.SetParent(t, false); arr.localPosition = new Vector3(790, 3.3f, -258.5f);
            Box(arr, "Board frame", Vector3.zero, new Vector3(16, 3.6f, .3f), dark);
            boardArrivals = Text(arr, "", new Vector3(0, 0, -.17f), 0, new Vector2(15, 3.4f), new Color(.6f, .95f, 1f), 4, false); boardArrivals.alignment = TextAlignmentOptions.TopLeft; boardArrivals.textWrappingMode = TextWrappingModes.Normal; boardArrivals.enableAutoSizing = false; boardArrivals.fontSize = 2.6f;
            // Arrivals hall: baggage carousels.
            for (int i = 0; i < 3; i++)
            {
                var c = new GameObject("Baggage carousel " + (i + 1)).transform; c.SetParent(t, false); c.localPosition = new Vector3(712 + i * 22 + (i > 1 ? 76 : 0), .16f, -284);
                foreach (int s in new[] { -1, 1 }) Box(c, "Carousel run", new Vector3(s * 1.4f, .3f, 0), new Vector3(1.0f, .6f, 12), steel, true);
                foreach (int s in new[] { -1, 1 }) Part(c, "Carousel end", cylinder, new Vector3(0, .3f, s * 6), new Vector3(3.8f, .3f, 3.8f), steel);
                Box(c, "Carousel belt", new Vector3(0, .62f, 0), new Vector3(3.6f, .04f, 12), dark);
                for (int k = 0; k < 6; k++) Box(c, "Suitcase", new Vector3(((k % 2) * 2 - 1) * 1.5f, .78f, -5 + k * 2), new Vector3(.45f, .3f, .7f), k % 3 == 0 ? signBlue : k % 3 == 1 ? taxiYellow : dark);
                Text(c, "BAGGAGE  " + (i + 1), new Vector3(0, 3.2f, -6.5f), 180, new Vector2(5, .8f), Color.white, 5);
            }
            // Apron-side lounge seating upstairs and ceiling light lines on both levels.
            for (int r = 0; r < 3; r++) for (int i = 0; i < 6; i++) Box(t, "Lounge seat row", new Vector3(824 + i * 10, up + .45f, -292.5f - r * 2.6f), new Vector3(9, .9f, .8f), signBlue, true);
            for (float x = 700; x <= 880; x += 12) { Box(t, "Ceiling light line", new Vector3(x, up - .5f, -276), new Vector3(.25f, .06f, 44), ledWarm); }
            Text(t, "ARRIVALS  /  BAGGAGE CLAIM  /  EXIT", new Vector3(790, 4.8f, -262.5f), 180, new Vector2(30, 1.1f), new Color(.6f, .95f, 1), 8);
            Text(t, "WELCOME TO HAVEN", new Vector3(790, 4.8f, -296.5f), 0, new Vector2(30, 1.6f), Color.white, 10);
            // Indoor planters.
            for (float x = 705; x <= 875; x += 34) { Box(t, "Indoor planter", new Vector3(x, .6f, -256), new Vector3(3, .9f, 1.5f), white, true); Part(t, "Indoor plant", sphere, new Vector3(x, 1.5f, -256), new Vector3(2.6f, 1.6f, 1.3f), grass); }
        }

        // =====================================================================
        // Forecourt: departures up, arrivals down
        // =====================================================================
        private void Forecourt()
        {
            var t = Static; float zc = (DeckSouth + DeckNorth) * .5f, w = DeckNorth - DeckSouth, up = UpperLevel;
            // Departures deck and its ramps ("up" from the west, "down" to the east).
            Box(t, "Departures road deck", new Vector3((DeckWest + DeckEast) * .5f, up - .25f, zc), new Vector3(DeckEast - DeckWest, .5f, w), asphalt, true);
            Ramp(t, "Departures ramp up", new Vector3(RampWest, .07f, zc), new Vector3(DeckWest, up, zc), w, asphalt);
            Ramp(t, "Departures ramp down", new Vector3(RampEast, .07f, zc), new Vector3(DeckEast, up, zc), w, asphalt);
            // Ramp and deck parapets with LED rails.
            void Parapet(Vector3 a, Vector3 b)
            {
                var d = b - a; float ang = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg; var mid = (a + b) * .5f;
                Box(t, "Road parapet", mid + Vector3.up * .55f, new Vector3(d.magnitude, 1.1f, .35f), concrete, true, new Vector3(0, 0, ang));
                Box(t, "Parapet LED rail", mid + Vector3.up * 1.12f, new Vector3(d.magnitude, .06f, .38f), ledCool, false, new Vector3(0, 0, ang));
            }
            foreach (float z in new[] { DeckSouth - .2f, DeckNorth + .2f })
            {
                Parapet(new Vector3(RampWest, .07f, z), new Vector3(DeckWest, up, z));
                Parapet(new Vector3(DeckEast, up, z), new Vector3(RampEast, .07f, z));
            }
            Parapet(new Vector3(DeckWest, up, DeckNorth + .2f), new Vector3(DeckEast, up, DeckNorth + .2f));
            // Kerbside walkway joining the deck to the departures doors; arrivals canopy below.
            Box(t, "Departures kerb walkway", new Vector3((DeckWest + DeckEast) * .5f, up - .2f, (DeckSouth + TerminalFront) * .5f), new Vector3(DeckEast - DeckWest, .4f, DeckSouth - TerminalFront), concrete, true);
            Box(t, "Departures kerb", new Vector3((DeckWest + DeckEast) * .5f, up + .07f, DeckSouth - .1f), new Vector3(DeckEast - DeckWest, .15f, .35f), white, true);
            for (float x = DeckWest + 15; x < DeckEast; x += 30) { Box(t, "Kerbside bench", new Vector3(x, up + .3f, -248), new Vector3(3, .45f, .6f), steel, true); }
            // Columns under the deck and ramps.
            for (float x = DeckWest + 8; x < DeckEast; x += 18) foreach (float z in new[] { DeckSouth - .6f, DeckNorth + .6f }) Box(t, "Deck column", new Vector3(x, (up - .5f) * .5f, z), new Vector3(.9f, up - .5f, .9f), concrete, true);
            foreach (var x in new[] { 640f, 662f, 878f, 900f }) { float h = x < 700 ? Mathf.Lerp(0, up, (x - RampWest) / (DeckWest - RampWest)) : Mathf.Lerp(up, 0, (x - DeckEast) / (RampEast - DeckEast)); if (h > 1.8f) foreach (float z in new[] { DeckSouth + 1.5f, DeckNorth - 1.5f }) Box(t, "Ramp pier", new Vector3(x, (h - .6f) * .5f, z), new Vector3(.8f, h - .6f, .8f), concrete, true); }
            for (float x = DeckWest + 2; x < DeckEast; x += 10) Box(t, "Under-deck light", new Vector3(x, up - .53f, zc), new Vector3(2, .05f, .4f), ledWarm);
            // Ground level: arrivals pick-up lane merging with the access road, kerb and crossings.
            Box(t, "Arrivals pick-up lane", new Vector3((DeckWest + DeckEast) * .5f, .03f, (DeckSouth + -227) * .5f), new Vector3(DeckEast - DeckWest + 20, .1f, -227 - DeckSouth), asphalt, true);
            Box(t, "West ramp link", new Vector3(582, .03f, (DeckSouth - 227) * .5f), new Vector3(24, .1f, -227 - DeckSouth), asphalt, true);
            Box(t, "East turnaround", new Vector3(930, .03f, -224), new Vector3(46, .1f, 38), asphalt, true);
            Box(t, "Arrivals kerb", new Vector3(790, .1f, DeckSouth - .25f), new Vector3(DeckEast - DeckWest, .16f, .4f), white, true);
            for (float x = DeckWest + 6; x < DeckEast; x += 6) Box(t, "Pick-up bay line", new Vector3(x, .085f, DeckSouth + 1.2f), new Vector3(.12f, .01f, 2.4f), yellowPaint);
            for (int i = 0; i < 8; i++) Box(t, "Zebra crossing", new Vector3(790, .085f, DeckSouth + .8f + i * 1.3f), new Vector3(4, .01f, .6f), paint);
            for (int i = 0; i < 8; i++) Box(t, "Zebra crossing", new Vector3(730, .085f, DeckSouth + .8f + i * 1.3f), new Vector3(4, .01f, .6f), paint);
            Text(t, "ARRIVALS  PICK-UP", new Vector3(760, .09f, DeckSouth + 5), 0, new Vector2(20, 2.4f), new Color(.98f, .78f, .1f), 14).transform.localRotation = Quaternion.Euler(90, 0, 0);
            Text(t, "DEPARTURES  DROP-OFF", new Vector3(770, up + .02f, zc), 0, new Vector2(22, 2.6f), Color.white, 14).transform.localRotation = Quaternion.Euler(90, 0, 0);
            // Deck lighting and signs.
            for (float x = DeckWest + 5; x < DeckEast; x += 25) { Box(t, "Deck lamp post", new Vector3(x, up + 3.5f, DeckNorth + .4f), new Vector3(.18f, 7, .18f), steel); Box(t, "Deck lamp head", new Vector3(x, up + 7, DeckNorth - .6f), new Vector3(.5f, .14f, 2.2f), ledWarm); }
            Gantry(t, 520, "DEPARTURES  /  UPPER LEVEL  ->", "ARRIVALS  /  LOWER LEVEL");
            Gantry(t, 420, "HAVEN INTERNATIONAL  /  ALL TERMINALS", "TAXI  /  BUS  /  PARKING");
            var cab = new GameObject("Taxi rank sign").transform; cab.SetParent(t, false); cab.localPosition = new Vector3(845, 0, DeckSouth - 1.4f);
            Box(cab, "Pole", new Vector3(0, 1.6f, 0), new Vector3(.12f, 3.2f, .12f), steel); Box(cab, "Panel", new Vector3(0, 3, 0), new Vector3(2, .8f, .1f), taxiYellow);
            Text(cab, "TAXI", new Vector3(0, 3, -.07f), 0, new Vector2(1.8f, .6f), Color.black, 4);
            var bus = new GameObject("Shuttle bus stop").transform; bus.SetParent(t, false); bus.localPosition = new Vector3(700, 0, DeckSouth - 1.4f);
            Box(bus, "Shelter roof", new Vector3(0, 2.6f, 0), new Vector3(6, .12f, 2), glass); Box(bus, "Shelter back", new Vector3(0, 1.3f, .9f), new Vector3(6, 2.4f, .06f), glass);
            Box(bus, "Shelter post", new Vector3(-2.9f, 1.3f, -.8f), new Vector3(.1f, 2.6f, .1f), steel); Box(bus, "Shelter post", new Vector3(2.9f, 1.3f, -.8f), new Vector3(.1f, 2.6f, .1f), steel);
            Text(bus, "AIRPORT SHUTTLE  /  CITY", new Vector3(0, 2.25f, -.95f), 0, new Vector2(5.6f, .4f), Color.white, 3);
        }
        private void Gantry(Transform t, float x, string left, string right)
        {
            var g = new GameObject("Overhead gantry sign").transform; g.SetParent(t, false); g.localPosition = new Vector3(x, 0, -220);
            foreach (int s in new[] { -1, 1 }) Box(g, "Gantry leg", new Vector3(0, 3.8f, s * 8.6f), new Vector3(.45f, 7.6f, .45f), steel, true);
            Box(g, "Gantry truss", new Vector3(0, 7.4f, 0), new Vector3(.6f, .6f, 17.6f), steel);
            foreach (var p in new[] { (z: -3.6f, txt: left), (z: 3.6f, txt: right) })
            {
                Box(g, "Gantry panel", new Vector3(-.35f, 6.2f, p.z), new Vector3(.12f, 2, 6.8f), signBlue);
                Text(g, p.txt, new Vector3(-.43f, 6.2f, p.z), 90, new Vector2(6.4f, 1.6f), Color.white, 6);
            }
        }

        // =====================================================================
        // Access roads: markings and lights
        // =====================================================================
        private void AccessRoads()
        {
            var t = Static;
            // (start, end) of each existing access road centreline, airport-local.
            var roads = new[] { (new Vector3(-200, 0, -1357), new Vector3(-200, 0, -213)), (new Vector3(-207, 0, -220), new Vector3(907, 0, -220)), (new Vector3(-887, 0, -1350), new Vector3(-193, 0, -1350)) };
            foreach (var (a, b) in roads)
            {
                var dir = (b - a).normalized; var side = Vector3.Cross(Vector3.up, dir); float len = (b - a).magnitude; float yaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
                for (float d = 6; d < len - 6; d += 12) Box(t, "Lane dash", a + dir * d + Vector3.up * .065f, new Vector3(.18f, .01f, 4.5f), yellowPaint, false, new Vector3(0, yaw, 0));
                foreach (int s in new[] { -1, 1 }) Box(t, "Edge line", (a + b) * .5f + side * s * 6.3f + Vector3.up * .065f, new Vector3(.2f, .01f, len - 2), paint, false, new Vector3(0, yaw, 0));
                for (float d = 20; d < len - 10; d += 48)
                    foreach (int s in new[] { -1, 1 })
                    {
                        var p = a + dir * (d + (s > 0 ? 24 : 0)) + side * s * 8.3f;
                        Box(t, "Street light pole", p + Vector3.up * 4.5f, new Vector3(.2f, 9, .2f), steel, true);
                        Box(t, "Street light arm", p + Vector3.up * 9 - side * s * 1.1f, new Vector3(.12f, .12f, 2.2f), steel, false, new Vector3(0, yaw + 90, 0));
                        Box(t, "Street light head", p + Vector3.up * 8.9f - side * s * 2.1f, new Vector3(.45f, .12f, .9f), ledWarm, false, new Vector3(0, yaw, 0));
                    }
            }
            // Entrance landmark where the airport road begins.
            var gate = new GameObject("Airport entrance landmark").transform; gate.SetParent(t, false); gate.localPosition = new Vector3(-186, 0, -1300);
            Box(gate, "Landmark wing", new Vector3(0, 6, 0), new Vector3(2, 12, 7), white, true, new Vector3(0, 0, -12));
            Text(gate, "HAVEN INTERNATIONAL AIRPORT", new Vector3(-1.05f, 6.5f, 0), 90, new Vector2(6.6f, 4), new Color(.05f, .2f, .45f), 10);
            Box(gate, "Landmark light", new Vector3(1.2f, 6, 0), new Vector3(.08f, 11, .2f), ledCool, false, new Vector3(0, 0, -12));
        }

        // =====================================================================
        // Control tower
        // =====================================================================
        private void Tower()
        {
            var t = Static; var c = new Vector3(637, 0, -310);
            Part(t, "Tower base pavilion", cylinder, c + new Vector3(0, 3, 0), new Vector3(20, 3, 20), glass, null, true);
            Part(t, "Tower base roof", cylinder, c + new Vector3(0, 6.1f, 0), new Vector3(21, .15f, 21), white);
            Part(t, "Tower shaft", cylinder, c + new Vector3(0, 27, 0), new Vector3(6.5f, 24, 6.5f), white, null, true);
            for (int i = 0; i < 4; i++) { float a = i * Mathf.PI / 2; Beam(t, "Tower fin", c + new Vector3(Mathf.Cos(a) * 3.2f, 6, Mathf.Sin(a) * 3.2f), c + new Vector3(Mathf.Cos(a) * 4.8f, 48, Mathf.Sin(a) * 4.8f), .35f, steel); }
            Part(t, "Tower cab floor", cylinder, c + new Vector3(0, 50.5f, 0), new Vector3(16, .7f, 16), white);
            Part(t, "Tower cab glass", cylinder, c + new Vector3(0, 53.5f, 0), new Vector3(15.5f, 2.6f, 15.5f), glass);
            Part(t, "Tower cab roof", cylinder, c + new Vector3(0, 56.4f, 0), new Vector3(18, .4f, 18), dark);
            Part(t, "Tower cab glow", cylinder, c + new Vector3(0, 51.2f, 0), new Vector3(15.6f, .08f, 15.6f), ledCool);
            Beam(t, "Tower mast", c + new Vector3(0, 56.6f, 0), c + new Vector3(0, 66, 0), .2f, steel);
            var beacon = Part(t, "Tower beacon", sphere, c + new Vector3(0, 66.3f, 0), Vector3.one * .7f, AircraftArt.Red); beacons.Add(beacon.GetComponent<Renderer>());
            var radar = root.Find("ATC rotating radar"); if (radar != null) { radar.localPosition = c + new Vector3(0, 57.2f, 0); foreach (var r in radar.GetComponentsInChildren<Renderer>()) r.sharedMaterial = white; }
            Text(t, "ATC", new Vector3(c.x, 47, c.z - 3.3f), 0, new Vector2(4, 2), new Color(.05f, .2f, .45f), 12);
        }

        // =====================================================================
        // Gates and jet bridges
        // =====================================================================
        private void Gates()
        {
            var t = Static;
            foreach (float gx in NewGates.Concat(new[] { 890f })) JetBridge(t, gx);
            // Stand markings for every contact gate.
            foreach (float gx in NewGates.Concat(new[] { 780f, 890f }))
            {
                Box(t, "Stand lead-in line", new Vector3(gx, .08f, -362), new Vector3(.3f, .01f, 60), yellowPaint);
                Box(t, "Stand stop bar", new Vector3(gx, .08f, -332.5f), new Vector3(4, .01f, .3f), yellowPaint);
                Text(t, (gx < 760 ? "A1" : gx < 800 ? "A2" : gx < 860 ? "A3" : "A4"), new Vector3(gx, .09f, -392), 0, new Vector2(6, 4), new Color(.98f, .78f, .1f), 30).transform.localRotation = Quaternion.Euler(90, 180, 0);
                var vdgs = new GameObject("Docking guidance display").transform; vdgs.SetParent(t, false); vdgs.localPosition = new Vector3(gx, 8.2f, TerminalRear - .5f);
                Box(vdgs, "Display", Vector3.zero, new Vector3(2.4f, 1.4f, .25f), dark); Text(vdgs, "STOP", new Vector3(0, 0, .14f), 0, new Vector2(2.2f, 1), new Color(1, .3f, .2f), 6);
            }
        }
        private void JetBridge(Transform t, float gx)
        {
            var g = new GameObject("Jet bridge " + gx).transform; g.SetParent(t, false); JetBridges++;
            float x = gx - 3.6f; var a = new Vector3(x, UpperLevel + .1f, TerminalRear - 1); var b = new Vector3(x, 2.3f, -338.6f);
            var d = b - a; float len = d.magnitude; float pitch = Mathf.Atan2(-d.y, -d.z) * Mathf.Rad2Deg; var mid = (a + b) * .5f; var rot = Quaternion.Euler(pitch, 180, 0);
            var tube = new GameObject("Telescopic tunnel").transform; tube.SetParent(g, false); tube.localPosition = mid + Vector3.up * 1.45f; tube.localRotation = rot;
            Box(tube, "Tunnel floor", new Vector3(0, -1.4f, 0), new Vector3(3, .2f, len), steel, true);
            Box(tube, "Tunnel roof", new Vector3(0, 1.4f, 0), new Vector3(3.1f, .2f, len), white);
            foreach (int s in new[] { -1, 1 }) { Box(tube, "Tunnel glass side", new Vector3(s * 1.5f, .2f, 0), new Vector3(.08f, 2.4f, len), glass, true); Box(tube, "Tunnel side band", new Vector3(s * 1.52f, -1.05f, 0), new Vector3(.06f, .5f, len), white); }
            for (int k = 1; k < 3; k++) Box(tube, "Telescope joint", new Vector3(0, 0, -len * .5f + k * len / 3), new Vector3(3.3f, 3.0f, .3f), white);
            Text(tube, "HAVEN INTERNATIONAL", new Vector3(-1.56f, .9f, 0), 90, new Vector2(10, .5f), new Color(.05f, .2f, .45f), 3);
            // Drive column with wheels under the outer end, cab and bellows at the aircraft door.
            Box(g, "Drive column", new Vector3(x, 1.0f, -333), new Vector3(.5f, 2.0f, .5f), steel);
            Box(g, "Drive bogie", new Vector3(x, .4f, -333), new Vector3(2.2f, .5f, 1.2f), dark);
            foreach (int s in new[] { -1, 1 }) Part(g, "Bogie wheel", cylinder, new Vector3(x + s * .9f, .4f, -333), new Vector3(.8f, .2f, .8f), rubber, new Vector3(0, 0, 90));
            var cab = new GameObject("Bridge cab").transform; cab.SetParent(g, false); cab.localPosition = new Vector3(gx - 2.35f, 3.75f, -340); cab.localRotation = Quaternion.Euler(0, -90, 0);
            Box(cab, "Cab body", Vector3.zero, new Vector3(3.2f, 3, 2.4f), white);
            Box(cab, "Bellows canopy", new Vector3(0, 0, 1.4f), new Vector3(2.9f, 2.8f, .6f), bellows);
            Box(g, "Rotunda", new Vector3(x, UpperLevel + 1.5f, TerminalRear - 1.2f), new Vector3(4, 3.2f, 2.4f), white);
        }

        // =====================================================================
        // Apron: ground service equipment
        // =====================================================================
        private void Apron()
        {
            var t = Group("Ground service equipment");
            foreach (float gx in NewGates.Concat(new[] { 890f }))
            {
                Gse(t, "Fuel truck", new Vector3(gx + 9, 0, -351), 90, new Color(.95f, .95f, .95f), 9, 2.5f, 2.8f, true);
                Gse(t, "Baggage tractor", new Vector3(gx + 5.5f, 0, -363), 180, new Color(.95f, .55f, .05f), 2.6f, 1.5f, 1.6f, false);
                for (int k = 0; k < 3; k++) Cart(t, new Vector3(gx + 5.5f, 0, -366.5f - k * 3.2f));
                Gse(t, "Belt loader", new Vector3(gx + 3.2f, 0, -355), 180, new Color(.95f, .95f, .95f), 6, 1.8f, 1.2f, false, true);
                Gse(t, "Catering truck", new Vector3(gx + 4.6f, 0, -340.5f), -90, new Color(.95f, .95f, .95f), 6.5f, 2.4f, 3.8f, true);
                var cone = new Vector3(gx, 0, -320); foreach (float dx in new[] { -18f, 18f }) Part(t, "Safety cone", cylinder, new Vector3(gx + dx, .35f, -356), new Vector3(.35f, .35f, .35f), taxiYellow);
            }
            // Pushback tug that meets HV101 before departure.
            tug = Gse(t, "Pushback tug", AirportOperations.Gate + new Vector3(0, 0, 21), 180, new Color(.95f, .75f, .05f), 4.5f, 2.6f, 1.4f, false).transform;
            Gse(t, "Follow-me car", new Vector3(900, 0, -420), 90, new Color(.98f, .78f, .05f), 4.2f, 1.8f, 1.5f, false);
            Text(t, "FOLLOW ME", new Vector3(900, 2.05f, -420), 90, new Vector2(1.8f, .4f), Color.black, 3).transform.localRotation = Quaternion.Euler(90, 90, 0);
        }
        private GameObject Gse(Transform t, string name, Vector3 at, float yaw, Color body, float length, float width, float height, bool box, bool belt = false)
        {
            var g = new GameObject(name); g.transform.SetParent(t, false); g.transform.localPosition = at; g.transform.localRotation = Quaternion.Euler(0, yaw, 0);
            var paintM = Lit(name + " paint", body, .6f, .1f);
            Box(g.transform, "Chassis", new Vector3(0, .55f, 0), new Vector3(width, .5f, length), dark);
            Box(g.transform, "Cab", new Vector3(0, 1.2f + height * .2f, length * .5f - .9f), new Vector3(width, .9f + height * .4f, 1.6f), paintM);
            Box(g.transform, "Cab glass", new Vector3(0, 1.45f + height * .25f, length * .5f - .08f), new Vector3(width * .9f, .6f, .05f), AircraftArt.Glass);
            if (box) Box(g.transform, name == "Fuel truck" ? "Fuel tank" : "Body", new Vector3(0, .8f + height * .5f, -.9f), new Vector3(width, height, length - 2), paintM);
            if (belt) Box(g.transform, "Conveyor belt", new Vector3(0, 1.8f, -.8f), new Vector3(.8f, .12f, length + 1), rubber, false, new Vector3(-18, 0, 0));
            foreach (int s in new[] { -1, 1 }) foreach (float z in new[] { -length * .35f, length * .35f }) Part(g.transform, "Wheel", cylinder, new Vector3(s * width * .5f, .38f, z), new Vector3(.76f, .14f, .76f), rubber, new Vector3(0, 0, 90));
            var beacon = Part(g.transform, "Amber beacon", sphere, new Vector3(0, 1.75f + height * .4f, length * .5f - .9f), Vector3.one * .22f, Lit("Amber", new Color(1, .6f, .05f)));
            return g;
        }
        private void Cart(Transform t, Vector3 at)
        {
            var g = new GameObject("Baggage cart").transform; g.SetParent(t, false); g.localPosition = at;
            Box(g, "Cart bed", new Vector3(0, .5f, 0), new Vector3(1.6f, .12f, 2.6f), steel);
            Box(g, "Cart roof", new Vector3(0, 1.9f, 0), new Vector3(1.7f, .08f, 2.7f), Lit("Cart canopy", new Color(.1f, .3f, .55f)));
            foreach (int s in new[] { -1, 1 }) foreach (int z in new[] { -1, 1 }) { Box(g, "Cart post", new Vector3(s * .8f, 1.2f, z * 1.25f), new Vector3(.06f, 1.4f, .06f), steel); Part(g, "Cart wheel", cylinder, new Vector3(s * .7f, .2f, z * 1f), new Vector3(.4f, .08f, .4f), rubber, new Vector3(0, 0, 90)); }
            for (int k = 0; k < 4; k++) Box(g, "Bag", new Vector3((k % 2 - .5f) * .7f, .78f, (k / 2 - .5f) * 1.1f), new Vector3(.55f, .4f, .8f), k % 2 == 0 ? signBlue : dark);
        }

        // =====================================================================
        // Car park in front of the terminal
        // =====================================================================
        private void CarPark()
        {
            int n = 0;
            for (int row = 0; row < 2; row++) for (int i = 0; i < 25; i++)
                {
                    int hash = (i * 7919 + row * 104729) % 100; if (hash > 55) continue;
                    var go = new GameObject("Parked car (airport)"); go.transform.SetParent(Group("Car park"), false);
                    go.transform.localPosition = new Vector3(768.5f + i * 7, .09f, row == 0 ? -184 : -176); go.transform.localRotation = Quaternion.Euler(0, row == 0 ? 0 : 180, 0);
                    var box = go.AddComponent<BoxCollider>(); box.size = new Vector3(1.95f, 1.5f, 4.6f); box.center = new Vector3(0, .75f, 0);
                    var body = go.AddComponent<Rigidbody>(); body.isKinematic = true; body.useGravity = false;
                    RealisticVehicleVisuals.ForTraffic(go, 500 + n * 3 + hash); n++;
                }
        }

        // =====================================================================
        // Aircraft
        // =====================================================================
        private void Planes()
        {
            var a = Group("Aircraft");
            // Re-skin the scheduled jet (keeps its landing-gear transform for retraction), the parked jet and the regional aircraft.
            foreach (var (name, livery) in new[] { ("HV101 scheduled passenger jet", AirlineLivery.Haven), ("HV202 parked passenger jet", AirlineLivery.Aurora), ("Hangar regional aircraft", AirlineLivery.Haven) })
            {
                var p = root.Find(name); if (p == null) continue; AircraftArt.HideOld(p, null);
                var gear = p.Find("Retractable landing gear");
                airliners.Add(AircraftArt.BuildAirliner(p, livery, gear));
            }
            // New aircraft parked at the new contact gates.
            var liveries = new[] { AirlineLivery.Farhan, AirlineLivery.Skybridge };
            for (int i = 0; i < NewGates.Length; i++)
            {
                var p = new GameObject(liveries[i].Name + " at gate").transform; p.SetParent(a, false); p.localPosition = new Vector3(NewGates[i], .12f, -350); p.localScale = Vector3.one * (i == 1 ? 1.12f : 1);
                var col = p.gameObject.AddComponent<CapsuleCollider>(); col.direction = 2; col.center = new Vector3(0, 3.2f, 0); col.radius = 1.9f; col.height = 35;
                airliners.Add(AircraftArt.BuildAirliner(p, liveries[i]));
            }
            // A second aircraft flying a continuous circuit on the 1500 m runway: approach, landing, roll-out, back-track, take-off.
            circuit = new GameObject("Circuit traffic (" + AirlineLivery.Skybridge.Name + ")").transform; circuit.SetParent(a, false);
            var built = AircraftArt.BuildAirliner(circuit, AirlineLivery.Farhan); circuitGear = built.Gear; airliners.Add(built);
            // High-altitude overflight across the region.
            cruise = new GameObject("Cruising airliner").transform; cruise.SetParent(a, false); airliners.Add(AircraftArt.BuildAirliner(cruise, AirlineLivery.Aurora));
            foreach (var l in airliners) beacons.AddRange(l.Blink);
        }
        private Transform circuitGear, cruise;
        public const float CircuitSeconds = 150;
        /// <summary>Circuit position at time t (airport-local) and whether the gear is down.</summary>
        public static Vector3 CircuitPosition(float t, out bool gearDown)
        {
            t = Mathf.Repeat(t, CircuitSeconds); float rz = -850; gearDown = true;
            // 0-22 s final approach from the east, 22-38 s landing roll, 38-52 back-track, 52-60 turn, 60-78 take-off roll+climb, 78-150 circuit.
            if (t < 14) { float u = t / 14; return new Vector3(Mathf.Lerp(2450, 1350, u), Mathf.Lerp(70, .12f, u * u * (3 - 2 * u)), rz); }
            if (t < 38) { float u = (t - 14) / 24; float e = 1 - (1 - u) * (1 - u); return new Vector3(Mathf.Lerp(1350, 410, e), .12f, rz); }
            if (t < 52) { float u = (t - 38) / 14; float a = u * Mathf.PI; return new Vector3(410 - Mathf.Sin(a) * 12, .12f, rz - 9 + Mathf.Cos(a) * 9); }
            if (t < 60) { float u = (t - 52) / 8; return new Vector3(Mathf.Lerp(410, 440, u), .12f, rz - 18 + 18 * u * u); }
            if (t < 82) { float u = (t - 60) / 22; float x = Mathf.Lerp(440, 1450, u * u); float y = u < .55f ? .12f : Mathf.Lerp(.12f, 90, (u - .55f) / .45f); gearDown = u < .8f; return new Vector3(x, y, rz); }
            { float u = (t - 82) / 68; gearDown = u > .82f; float a = u * Mathf.PI * 2; float x = 1450 + Mathf.Sin(a) * 650 - u * 0; float z = rz + 650 * (1 - Mathf.Cos(a)) * .55f;
              // Blend into the final-approach start (1900, 70, rz).
              var circle = new Vector3(Mathf.Lerp(1450, 2450, u) + Mathf.Sin(a) * 600, Mathf.Lerp(90, 70, u) + Mathf.Sin(a * .5f) * 140, z); return circle; }
        }
        private Vector3 lastCircuit;

        private void HelicoptersAll()
        {
            // The existing airport helicopter keeps its flight pattern; only its looks change.
            var old = root.Find("Haven rescue helicopter");
            if (old != null) { var rotor = old.Find("Main rotor"); var tail = old.Find("Tail rotor"); AircraftArt.HideOld(old, null); helis.Add(AircraftArt.BuildHelicopter(old, AirlineLivery.Haven, rotor, tail)); }
            // Sightseeing helicopter on a city tour.
            tour = new GameObject("City tour helicopter").transform; tour.SetParent(Group("Aircraft"), false);
            helis.Add(AircraftArt.BuildHelicopter(tour, AirlineLivery.Tours));
            foreach (var h in helis) beacons.AddRange(h.Blink);
        }
        /// <summary>World-space tour route over the city (stadium, hospital, downtown, airport).</summary>
        private static readonly Vector3[] TourRoute =
        {
            new Vector3(1800, 140, 1540), new Vector3(1100, 160, 1100), new Vector3(600, 150, 300), new Vector3(150, 130, -250), new Vector3(-250, 140, -120),
            new Vector3(-400, 150, 300), new Vector3(0, 170, 600), new Vector3(600, 150, -430), new Vector3(1000, 150, 400), new Vector3(1600, 150, 1200)
        };

        // =====================================================================
        // Forecourt traffic and passengers
        // =====================================================================
        private void ForecourtTraffic()
        {
            var t = Group("Forecourt traffic"); float zc = (DeckSouth + DeckNorth) * .5f, up = UpperLevel;
            // Drop-off loop: east-bound kerb lane, up the ramp, stop at the doors, down the far ramp, turn and return west.
            var loop = new[]
            {
                new Vector3(560, .09f, -224), new Vector3(586, .09f, zc - 1.6f), new Vector3(RampWest + 4, .32f, zc - 1.6f), new Vector3(DeckWest, up + .09f, zc - 1.6f),
                new Vector3(760, up + .09f, zc - 2.6f), new Vector3(DeckEast, up + .09f, zc - 1.6f), new Vector3(RampEast - 4, .32f, zc - 1.6f), new Vector3(938, .09f, -232),
                new Vector3(944, .09f, -222), new Vector3(936, .09f, -213.5f), new Vector3(900, .09f, -216.5f), new Vector3(600, .09f, -216.5f), new Vector3(560, .09f, -216.5f)
            };
            var waits = new float[loop.Length]; waits[4] = 7;
            for (int i = 0; i < 5; i++)
            {
                bool shuttle = i == 2; var go = new GameObject(shuttle ? "Airport shuttle bus" : i % 2 == 0 ? "Airport taxi" : "Drop-off car"); go.transform.SetParent(t, false);
                var size = shuttle ? RealisticVehicleVisuals.ColliderSize(VehicleBodyType.CityBus) : new Vector3(1.95f, 1.5f, 4.6f);
                var box = go.AddComponent<BoxCollider>(); box.size = size; box.center = Vector3.up * size.y * .5f; var body = go.AddComponent<Rigidbody>(); body.isKinematic = true; body.useGravity = false;
                if (shuttle) go.AddComponent<RealisticVehicleVisuals>().Configure(VehicleBodyType.CityBus, 4); else go.AddComponent<RealisticVehicleVisuals>().Configure(i % 2 == 0 ? VehicleBodyType.Sedan : VehicleBodyType.SUV, 11 + i * 3);
                var m = new Mover { T = go.transform, Path = loop, Waits = waits, Speed = shuttle ? 7 : 9, Vehicle = true, I = (i * 3) % loop.Length };
                go.transform.localPosition = loop[(m.I + loop.Length - 1) % loop.Length]; movers.Add(m);
                if (!shuttle && i % 2 == 0) StartCoroutine(TaxiLivery(go));
            }
            // Taxis waiting at the arrivals rank under the deck.
            for (int i = 0; i < 4; i++)
            {
                var go = new GameObject("Taxi at rank"); go.transform.SetParent(t, false); go.transform.localPosition = new Vector3(842 - i * 6.2f, .09f, DeckSouth + 1.5f); go.transform.localRotation = Quaternion.Euler(0, 90, 0);
                var box = go.AddComponent<BoxCollider>(); box.size = new Vector3(1.95f, 1.5f, 4.6f); box.center = new Vector3(0, .75f, 0); var body = go.AddComponent<Rigidbody>(); body.isKinematic = true; body.useGravity = false;
                go.AddComponent<RealisticVehicleVisuals>().Configure(VehicleBodyType.Sedan, 40 + i); StartCoroutine(TaxiLivery(go));
            }
        }
        private IEnumerator TaxiLivery(GameObject car)
        {
            yield return null; yield return null; if (car == null) yield break;
            foreach (var r in car.GetComponentsInChildren<Renderer>()) if (r.sharedMaterial != null && r.sharedMaterial.name.StartsWith("Car paint")) r.sharedMaterial = taxiYellow;
            var sign = Box(car.transform, "Taxi roof sign", new Vector3(0, 1.55f, -.2f), new Vector3(.7f, .2f, .3f), Glow("Taxi sign", new Color(1, .9f, .4f), 1.5f));
            Text(car.transform, "TAXI", new Vector3(0, 1.55f, -.36f), 0, new Vector2(.65f, .18f), Color.black, 2);
        }
        private void Passengers()
        {
            var t = Group("Passengers"); float up = UpperLevel;
            // Departures: travellers walking from the drop-off kerb into the terminal with luggage.
            for (int i = 0; i < 10; i++)
            {
                float x = 700 + i * 16; var start = new Vector3(x + 3, up + .1f, DeckSouth - 1.2f); var door = new Vector3(i % 3 == 0 ? 730 : i % 3 == 1 ? 790 : 850, up + .1f, TerminalFront + .2f); var inside = door + new Vector3((i % 2 - .5f) * 2, 0, -14);
                var m = Walker(t, "Departing traveller " + i, new[] { start, door + new Vector3(0, 0, 1.6f), door, inside }, new[] { 0f, 0, 0, 3 + i % 4 }, 1.15f, 9000 + i);
                Suitcase(m);
            }
            // Arrivals hall: meeters-and-greeters waiting at the barrier, some with name boards; arriving passengers walking out.
            for (int i = 0; i < 12; i++)
            {
                var p = new Vector3(762 + i * 4.2f + (i % 2) * .8f, .16f, -270 + (i % 3) * .9f);
                var m = Walker(t, "Arrivals greeter " + i, new[] { p }, null, 0, 9100 + i); m.T.localRotation = Quaternion.Euler(0, 180, 0);
                m.Human.Gesture = i % 4 == 0 ? HumanGesture.HoldRight : i % 3 == 0 ? HumanGesture.Phone : HumanGesture.LookAround;
                if (i % 4 == 0) { var sign = Box(m.Human.RightHand, "Name board", new Vector3(0, .15f, .15f), new Vector3(.6f, .4f, .02f), white); }
            }
            foreach (int s in new[] { -1, 1 }) Box(t, "Arrivals barrier", new Vector3(790 + s * 16, .6f, -271.3f), new Vector3(24, 1.0f, .1f), glass, true);
            for (int i = 0; i < 6; i++)
            {
                var path = new[] { new Vector3(770 + i * 4, .16f, -290), new Vector3(789.5f + (i - 3) * .5f, .16f, -275), new Vector3(789.5f + (i - 3) * .5f, .16f, -262), new Vector3(789 + (i - 3) * .8f, .16f, TerminalFront + .5f), new Vector3(780 + i * 6, .1f, DeckSouth + .3f) };
                var m = Walker(t, "Arriving passenger " + i, path, new[] { 2f + i, 0, 0, 0, 6 }, 1.1f, 9200 + i); m.I = i % path.Length; Suitcase(m);
            }
            // Departures lounge: travellers seated by the apron windows watching the aircraft.
            for (int r = 0; r < 3; r++) for (int i = 0; i < 6; i++)
                {
                    if ((r + i) % 3 == 1) continue;
                    var id = HumanIdentity.Create(9300 + r * 10 + i, HumanRole.Civilian);
                    var go = new GameObject("Lounge traveller"); go.transform.SetParent(t, false); go.transform.localPosition = new Vector3(824 + i * 10 + (r % 2) * 2, up, -292.1f - r * 2.6f); go.transform.localRotation = Quaternion.Euler(0, 180, 0);
                    var h = HumanVisual.Create(go.transform, id, true); h.Seated = true; h.transform.localPosition = new Vector3(0, .9f - .44f * id.Height / 1.8f, .1f); h.Gesture = (i + r) % 2 == 0 ? HumanGesture.Phone : HumanGesture.Relaxed;
                    people.Add(new Mover { T = go.transform, Human = h });
                }
        }
        private Mover Walker(Transform t, string name, Vector3[] path, float[] waits, float speed, int seed)
        {
            var go = new GameObject(name); go.transform.SetParent(t, false); go.transform.localPosition = path[0];
            var h = HumanVisual.Create(go.transform, HumanIdentity.Create(seed, HumanRole.Civilian), true);
            var m = new Mover { T = go.transform, Path = path, Waits = waits, Speed = speed, Human = h }; people.Add(m); return m;
        }
        private void Suitcase(Mover m)
        {
            var s = new GameObject("Rolling suitcase").transform; s.SetParent(m.T, false); s.localPosition = new Vector3(.45f, 0, -.45f);
            Box(s, "Case", new Vector3(0, .38f, 0), new Vector3(.38f, .56f, .24f), m.Human.Identity.Seed % 2 == 0 ? signBlue : dark);
            Box(s, "Handle", new Vector3(0, .82f, .09f), new Vector3(.25f, .35f, .02f), steel);
            m.Human.Gesture = HumanGesture.HoldRight;
        }

        // =====================================================================
        // Mesh combining for the static architecture
        // =====================================================================
        private void Combine(Transform group)
        {
            if (group == null) return;
            var filters = group.GetComponentsInChildren<MeshFilter>().Where(f => f.GetComponent<TMP_Text>() == null && f.GetComponent<MeshRenderer>() != null).ToArray();
            foreach (var g in filters.GroupBy(f => f.GetComponent<MeshRenderer>().sharedMaterial))
            {
                var list = g.ToList();
                for (int start = 0; start < list.Count; start += 1500)
                {
                    var chunk = list.Skip(start).Take(1500).ToArray();
                    var mesh = new Mesh { name = "Airport " + g.Key.name, indexFormat = IndexFormat.UInt32 };
                    mesh.CombineMeshes(chunk.Select(f => new CombineInstance { mesh = f.sharedMesh, transform = group.worldToLocalMatrix * f.transform.localToWorldMatrix }).ToArray(), true, true); mesh.RecalculateBounds();
                    var go = new GameObject("Combined " + g.Key.name, typeof(MeshFilter), typeof(MeshRenderer)); go.transform.SetParent(group, false);
                    go.GetComponent<MeshFilter>().sharedMesh = mesh; go.GetComponent<MeshRenderer>().sharedMaterial = g.Key; combined.Add(mesh);
                    foreach (var f in chunk) { Destroy(f.GetComponent<MeshRenderer>()); Destroy(f); }
                }
            }
        }
        private readonly List<Mesh> combined = new List<Mesh>();

        // =====================================================================
        // Frame update
        // =====================================================================
        private readonly RaycastHit[] hits = new RaycastHit[8];
        private void Update()
        {
            if (!Ready) return; float dt = Time.deltaTime;
            // Circuit aircraft.
            if (circuit != null)
            {
                var p = CircuitPosition(Time.time, out bool gear); var next = CircuitPosition(Time.time + .5f, out _);
                circuit.localPosition = p; var dir = next - p; if (dir.sqrMagnitude > .01f) { var look = Quaternion.LookRotation(dir); float bank = Vector3.SignedAngle(Vector3.ProjectOnPlane(dir, Vector3.up), Vector3.ProjectOnPlane(p - lastCircuit, Vector3.up), Vector3.up); circuit.localRotation = Quaternion.Slerp(circuit.localRotation, look * Quaternion.Euler(0, 0, Mathf.Clamp(bank * 3, -25, 25)), 1 - Mathf.Exp(-3 * dt)); }
                lastCircuit = p; if (circuitGear != null) circuitGear.gameObject.SetActive(gear);
            }
            if (cruise != null)
            {
                float u = Mathf.Repeat(Time.time / 140f, 1); var a = new Vector3(-2600, 520, -400); var b = new Vector3(2800, 560, -2200);
                cruise.position = root.TransformPoint(Vector3.Lerp(a, b, u)); cruise.rotation = Quaternion.LookRotation(root.TransformDirection(b - a)); if (airliners.Count > 0) { var gearT = airliners[airliners.Count - 1].Gear; if (gearT != null) gearT.gameObject.SetActive(false); }
            }
            // City tour helicopter.
            if (tour != null && helis.Count > 1)
            {
                float speed = 32; float total = 0; for (int i = 0; i < TourRoute.Length; i++) total += Vector3.Distance(TourRoute[i], TourRoute[(i + 1) % TourRoute.Length]);
                float d = Mathf.Repeat(Time.time * speed, total); int k = 0; float seg = Vector3.Distance(TourRoute[0], TourRoute[1]);
                while (d > seg) { d -= seg; k = (k + 1) % TourRoute.Length; seg = Vector3.Distance(TourRoute[k], TourRoute[(k + 1) % TourRoute.Length]); }
                var a = TourRoute[k]; var b = TourRoute[(k + 1) % TourRoute.Length]; var pos = Vector3.Lerp(a, b, d / seg);
                tour.position = pos; var heading = Quaternion.LookRotation(Vector3.ProjectOnPlane(b - a, Vector3.up)); tour.rotation = Quaternion.Slerp(tour.rotation, heading * Quaternion.Euler(6, 0, 0), 1 - Mathf.Exp(-1.5f * dt));
                var h = helis[1]; if (h.Rotor != null) h.Rotor.Rotate(0, 1100 * dt, 0, Space.Self); if (h.Tail != null) h.Tail.Rotate(1800 * dt, 0, 0, Space.Self);
            }
            // Pushback tug: hitched to HV101's nose gear from boarding to the end of pushback.
            if (tug != null && ops != null)
            {
                var jet = root.Find("HV101 scheduled passenger jet"); bool hitched = ops.Phase <= 1;
                if (jet != null && hitched) { tug.position = jet.TransformPoint(new Vector3(0, -.12f, 19.8f)); tug.rotation = jet.rotation * Quaternion.Euler(0, 180, 0); }
                else if (jet != null && ops.Phase == 2) { tug.localPosition = Vector3.MoveTowards(tug.localPosition, AirportOperations.Gate + new Vector3(16, 0, 10), 6 * dt); }
            }
            // Forecourt vehicles and people.
            foreach (var m in movers) Step(m, dt);
            foreach (var m in people) if (m.Path != null && m.Path.Length > 1) Step(m, dt);
            // Beacons and strobes blink.
            bool on = (Time.time % 1.2f) < .12f; foreach (var b in beacons) if (b != null) b.enabled = on;
            if (Time.time >= nextBoard) { nextBoard = Time.time + 2; UpdateBoards(); }
        }
        private void Step(Mover m, float dt)
        {
            if (m.Wait > 0) { m.Wait -= dt; return; }
            var target = m.Path[m.I]; var pos = m.T.localPosition; var delta = target - pos; var flat = new Vector3(delta.x, 0, delta.z);
            if (flat.magnitude < (m.Vehicle ? 1.2f : .12f)) { m.Wait = m.Waits != null && m.I < m.Waits.Length ? m.Waits[m.I] : 0; m.I = (m.I + 1) % m.Path.Length; if (!m.Vehicle && m.I == 0) m.T.localPosition = m.Path[0]; return; }
            float speed = m.Speed; if (m.Vehicle) { var nxt = m.Path[(m.I + 1) % m.Path.Length]; if (flat.magnitude < 8 && m.Waits != null && m.Waits[m.I] > 0) speed = Mathf.Max(1.5f, flat.magnitude * .8f); }
            var step = delta.normalized * Mathf.Min(delta.magnitude, speed * dt); m.T.localPosition = pos + step;
            var face = Quaternion.LookRotation(m.Vehicle ? new Vector3(step.x, step.y, step.z) : flat, Vector3.up); m.T.localRotation = Quaternion.Slerp(m.T.localRotation, face, 1 - Mathf.Exp(-(m.Vehicle ? 4 : 7) * dt));
        }
        private static readonly string[] Cities = { "DUBAI", "LONDON", "SINGAPORE", "KUALA LUMPUR", "ISTANBUL", "TOKYO", "KOLKATA", "DOHA", "BANGKOK", "TORONTO" };
        private void UpdateBoards()
        {
            if (board == null) return; float hour = clock == null ? 12 : clock.Hour; var sb = new System.Text.StringBuilder("DEPARTURES                                    GATE     STATUS\n");
            string[] status = { "BOARDING", "PUSHBACK", "TAXIING", "DEPARTED", "DEPARTED", "DEPARTED", "DEPARTED", "GATE OPEN" };
            for (int i = 0; i < 8; i++)
            {
                float h = Mathf.Repeat(hour + i * .55f + .3f, 24); string time = ((int)h).ToString("00") + ":" + ((int)((h % 1) * 60) / 5 * 5).ToString("00");
                var air = i % 4 == 0 ? AirlineLivery.Haven : i % 4 == 1 ? AirlineLivery.Farhan : i % 4 == 2 ? AirlineLivery.Skybridge : AirlineLivery.Aurora;
                string flight = air.Code + (101 + i * 37) % 900; string st = i == 0 ? status[ops != null ? ops.Phase : 0] : i == 1 ? "FINAL CALL" : i < 4 ? "CHECK-IN OPEN" : "ON TIME";
                sb.Append(time).Append("  ").Append(flight.PadRight(7)).Append(Cities[(i + (int)hour) % Cities.Length].PadRight(16)).Append(("A" + (i % 4 + 1)).PadRight(6)).Append(st).Append('\n');
            }
            board.text = sb.ToString();
            if (boardArrivals != null)
            {
                var a = new System.Text.StringBuilder("ARRIVALS                         STATUS\n");
                for (int i = 0; i < 5; i++) { float h = Mathf.Repeat(hour + i * .7f - .5f, 24); a.Append(((int)h).ToString("00") + ":" + ((int)((h % 1) * 60) / 5 * 5).ToString("00")).Append("  HV").Append(200 + i * 13).Append("  ").Append(Cities[(i * 3 + (int)hour) % Cities.Length].PadRight(14)).Append(i == 0 ? "LANDED" : i == 1 ? "ON APPROACH" : "EXPECTED").Append('\n'); }
                boardArrivals.text = a.ToString();
            }
        }

        private void OnDestroy()
        {
            foreach (var g in hidden) if (g != null) g.SetActive(true);
            foreach (var m in owned) if (m != null) Destroy(m);
            foreach (var m in combined) if (m != null) Destroy(m);
            if (art != null) Destroy(art.gameObject);
        }
    }
}
