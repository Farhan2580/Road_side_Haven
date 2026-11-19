using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace RoadsideHaven
{
    /// <summary>
    /// A living Haven Grand Medical Centre:
    /// • an AI ambulance answers real incidents in town (a citizen collapses, bystanders gather and phone),
    ///   drives there with siren and flashing beacons while traffic pulls over, paramedics bring the patient
    ///   back on a stretcher through the ER doors, and the patient is later discharged and walks home;
    /// • a second ambulance the player can drive for paid rescue missions (marker, distance, loading, reward);
    /// • rooftop air-ambulance transfers, doctors on ward rounds, nurses with trolleys, a moving reception
    ///   queue with a "now serving" board, a seated waiting room, wheelchairs, visitors with flowers and new
    ///   parents with their baby;
    /// • ICU heart monitors with scrolling ECG and beeps, PA announcements and glowing emergency signage.
    /// </summary>
    public sealed class HospitalUpgrade : MonoBehaviour
    {
        public enum Call { Idle, Responding, OnScene, Returning, Unloading }
        public enum Mission { None, ToPatient, Loading, ToHospital }

        // ---------------- Emergency bay layout (world space) ----------------
        /// <summary>AI ambulance waits under the ER porte-cochère, nose towards the emergency doors.</summary>
        public static readonly Vector3 BayHome = new Vector3(656, .09f, -451);
        /// <summary>The player's ambulance, facing the campus road.</summary>
        public static readonly Vector3 PlayerBay = new Vector3(668, .09f, -461);
        // Leaves forwards: a tight right turn north under the canopy (the garden planter and palm sit to the south),
        // past the parked ambulances' noses, then down the east side of the plaza to the campus road.
        private static readonly Vector3[] DepartPath = { new Vector3(650, 0, -451), new Vector3(648.8f, 0, -445.5f), new Vector3(652, 0, -442.6f), new Vector3(672, 0, -443), new Vector3(687.5f, 0, -449), new Vector3(690, 0, -462) };
        private static readonly Vector3 DepartFrom = new Vector3(690, 0, -466);
        private static readonly Vector3 ReturnRoad = new Vector3(690, 0, -469);
        private static readonly Vector3[] ArrivePath = { new Vector3(686, 0, -464), new Vector3(682, 0, -456), new Vector3(674, 0, -451.5f), new Vector3(664, 0, -451), BayHome };
        private static readonly Vector3[] UnloadPath = { new Vector3(660.4f, .12f, -451), new Vector3(659.5f, .12f, -454.2f), new Vector3(652, .12f, -454.2f), new Vector3(646.5f, .12f, -451.2f), new Vector3(641, .12f, -450.6f), new Vector3(638.3f, .12f, -450.5f) };
        public const int MissionBaseReward = 60;

        // ---------------- Public state (HUD, tests) ----------------
        public bool Ready { get; private set; }
        public Call State { get; private set; }
        public Mission MissionState { get; private set; }
        public TownVehicleAgent Ambulance => ai == null ? null : ai.Agent;
        public Rigidbody PlayerAmbulance => player == null ? null : player.Body;
        public bool AmbulanceSiren => ai != null && ai.SirenOn;
        public bool PlayerSiren => player != null && player.SirenOn;
        public TownCitizenAgent Patient => patient;
        public TownCitizenAgent MissionPatient => missionPatient;
        public int Rescued { get; private set; }
        public int MissionsCompleted { get; private set; }
        public int LastReward { get; private set; }
        public int Discharged { get; private set; }
        public int RooftopTransfers { get; private set; }
        public int QueueServed => token - 20;
        public string Toast => Time.unscaledTime < toastUntil ? toast : null;
        public string Announcement => Time.unscaledTime < announceUntil ? announcement : null;
        public string NowServing => boardText == null ? "" : boardText.text;
        public int DoctorsOnRounds => walkers.Count(w => w.Kind == "rounds");
        public int NursesWithTrolleys => walkers.Count(w => w.Kind == "trolley");
        public int SeatedVisitors => walkers.Count(w => w.Kind == "seated");
        public int QueueLength => queue.Count;
        public int Wheelchairs => walkers.Count(w => w.Kind == "wheelchair");
        public int Visitors => walkers.Count(w => w.Kind == "visitor");
        public int StretcherTeamsActive => walkers.Count(w => w.Kind == "stretcher" && w.Root.gameObject.activeSelf);
        public int MonitorCount => monitors.Count;
        public IReadOnlyList<string> SignTexts => signTexts;
        public IReadOnlyList<Transform> PeopleRoots => walkers.Select(w => w.Root).ToList();
        public Vector3 MissionTarget { get; private set; }

        // ---------------- Internals ----------------
        private sealed class Rig
        {
            public Rigidbody Body; public TownVehicleAgent Agent; public Transform T;
            public Renderer[] Red, Blue; public Light Flash; public AudioSource Siren; public bool SirenOn, Lights;
        }
        private sealed class Walker
        {
            public string Kind; public Transform Root; public HumanVisual Human; public int Floor; // -1 = outdoors, 9 = always
            public Vector3[] Path; public float[] Waits; public HumanGesture[] Gestures; public Vector3?[] Looks;
            public int I; public float Wait; public bool Loop = true; public bool Snap; public float Speed = 1.25f;
            public System.Action<Walker> Done; public HumanGesture Moving = HumanGesture.None; public Vector3? Look;
            public HumanVisual Patient; public Transform Front, Back; public HumanVisual FrontMedic, BackMedic;
            public float Hidden; public bool Inside; public int Seed; public GameObject Carry; public HumanVisual Companion;
        }
        private MotelGameRoot motel; private HospitalCampusLife campus; private TownRoadNetwork network;
        private WorldDrivingController driving; private OwnerSandboxController owner; private MotelAtmosphere clock;
        private HospitalHelicopter heli; private DistrictSlidingDoor erDoor, mainDoor;
        private Transform root; private Rig ai, player;
        private readonly List<Walker> walkers = new List<Walker>();
        private readonly List<Walker> queue = new List<Walker>();
        private readonly List<Material> owned = new List<Material>();
        private readonly List<Mesh> ownedMeshes = new List<Mesh>();
        private readonly List<string> signTexts = new List<string>();
        private readonly List<(TownCitizenAgent who, float at)> discharge = new List<(TownCitizenAgent, float)>();
        private readonly List<GameObject> monitors = new List<GameObject>();
        private readonly List<TextMeshPro> monitorText = new List<TextMeshPro>();
        private readonly List<Renderer> glowRed = new List<Renderer>(), beacons = new List<Renderer>();
        private readonly List<TextMeshPro> glowText = new List<TextMeshPro>();
        private Walker stretcher, roofTeam; private TownCitizenAgent patient, missionPatient;
        private Vector3[] lastRoute; private float nextIncident, callStarted, toastUntil, announceUntil, nextAnnouncement, nextQueue, nextHeart, visibilityAt, missionStarted, loadingUntil, nextGesture;
        private string toast, announcement; private string lastHeliPhase; private int token = 20;
        private Material ecg, redOn, redOff, blueOn, blueOff, glowRedMat, beaconMat, beaconOff, markerMat, white, dark, metal, teal, mattress, rubber, chairBlue, flowers, bundle, babySkin, sceneLight, battenburg, battenburgCab, chevron, gown;
        private readonly List<Texture2D> ownedTextures = new List<Texture2D>();
        private Texture2D Pattern(string name, System.Func<int, int, Color32> pixel)
        {
            var tex = new Texture2D(64, 64, TextureFormat.RGBA32, true) { name = name, wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear, anisoLevel = 4 };
            var px = new Color32[64 * 64]; for (int y = 0; y < 64; y++) for (int x = 0; x < 64; x++) px[y * 64 + x] = pixel(x, y);
            tex.SetPixels32(px); tex.Apply(true); ownedTextures.Add(tex); return tex;
        }
        private TextMeshPro boardText; private AudioSource beep, chime; private AudioClip sirenClip;
        private GameObject marker; private int insideFloor = -1; private bool insideBuilding;
        private Mesh cube, cylinder, sphere;
        private static readonly string[] Pages =
        {
            "Dr. Rahman, please report to Emergency. Dr. Rahman to Emergency.",
            "Code blue, ICU level four. Code blue, ICU level four.",
            "Visiting hours are 10 AM to 8 PM. Thank you for keeping our wards calm.",
            "Dr. Ayesha Karim, please call extension 4021.",
            "Pharmacy token 27, please come to counter two.",
            "Maternity ward: family visitors may now enter level five.",
            "Air ambulance inbound. Roof crew to the helipad, please.",
            "Reminder: please clean your hands at every ward entrance."
        };

        // =====================================================================
        // Set-up
        // =====================================================================
        private IEnumerator Start()
        {
            motel = GetComponent<MotelGameRoot>(); if (motel == null) yield break;
            clock = motel.GetComponent<MotelAtmosphere>();
            float give = Time.realtimeSinceStartup + 30;
            while (Time.realtimeSinceStartup < give)
            {
                campus = motel.GetComponentInChildren<HospitalCampusLife>(true);
                if (campus != null) break;
                yield return null;
            }
            if (campus == null) { campus = HospitalTravel.EnsureCampus(motel); if (campus == null) yield break; }
            yield return null; yield return null;
            driving = motel.GetComponentInChildren<WorldDrivingController>(true);
            owner = motel.GetComponentInChildren<OwnerSandboxController>(true);
            heli = campus.GetComponentInChildren<HospitalHelicopter>(true);
            foreach (var door in campus.GetComponentsInChildren<DistrictSlidingDoor>(true))
            {
                if (!door.gameObject.activeInHierarchy) continue;
                if ((door.transform.position - new Vector3(644, 0, -451)).sqrMagnitude < 9) erDoor = door;
                if ((door.transform.position - new Vector3(600, 0, -458)).sqrMagnitude < 9) mainDoor = door;
            }
            root = new GameObject("Hospital life upgrade").transform; root.SetParent(transform, false);
            root.gameObject.AddComponent<MotelDistrictArt>(); // moving people and vehicles: excluded from static road-clearance checks, like the campus itself
            Materials(); BuildNetwork(); ClearOldVehicles();
            ai = CreateAmbulance("Haven ambulance 1 (dispatch)", BayHome, -90, true);
            player = CreateAmbulance("Haven ambulance 2 (yours)", PlayerBay, 180, false);
            for (int n = 0; n < 4; n++) standby.Add(CreateAmbulance("Haven ambulance " + (n + 3) + " (standby)", new Vector3(660 + n * 8, .12f, -435), 180, false, false));
            ParkCars();
            yield return null; yield return null; // vehicle visuals build their shells in Start; livery goes on top
            Livery(ai, 1); Livery(player, 2); for (int n = 0; n < standby.Count; n++) Livery(standby[n], n + 3);
            sirenClip = SirenClip(); foreach (var rig in new[] { ai, player }) { rig.Siren.clip = sirenClip; }
            Signage(); Monitors(); LobbyBoard(); Audio(); AirAmbulance();
            Rounds(); Trolleys(); Lobby(); BuildWheelchairs(); VisitorsOutside(); GardenPatients(); EntranceLife(); Stretcher(); RoofTeam();
            Marker();
            nextIncident = Time.time + 45; nextAnnouncement = Time.time + 20; nextQueue = Time.time + 8;
            Ready = true;
        }

        // =====================================================================
        // Parking: the old block cars and block ambulances go, real cars park in the bays
        // =====================================================================
        private readonly List<(MeshFilter filter, Mesh original, Mesh copy)> stripped = new List<(MeshFilter, Mesh, Mesh)>();
        private readonly List<GameObject> hiddenOld = new List<GameObject>();
        private readonly List<Transform> parked = new List<Transform>();
        private readonly List<Rig> standby = new List<Rig>();
        public int ParkedCars => parked.Count;
        public int OldVehiclesRemoved => hiddenOld.Count;
        public int StrippedTriangles { get; private set; }
        public IReadOnlyList<Transform> ParkedCarList => parked;
        private void ClearOldVehicles()
        {
            // Each old vehicle is a group of colliders + labels; its visible parts were merged into the campus batches.
            var boxes = new List<(Matrix4x4 toLocal, Bounds local, Bounds world)>();
            foreach (var t in campus.GetComponentsInChildren<Transform>(true))
            {
                Bounds b;
                if (t.name == "Visitor car") b = new Bounds(new Vector3(0, 1.0f, 0), new Vector3(2.6f, 2.01f, 5.1f));
                else if (t.name.StartsWith("Emergency ambulance ")) b = new Bounds(new Vector3(0, 1.6f, .4f), new Vector3(3.0f, 3.18f, 7.0f));
                else continue;
                var world = new Bounds(t.TransformPoint(b.center), Vector3.zero);
                foreach (int x in new[] { -1, 1 }) foreach (int y in new[] { -1, 1 }) foreach (int z in new[] { -1, 1 }) world.Encapsulate(t.TransformPoint(b.center + Vector3.Scale(b.extents, new Vector3(x, y, z))));
                boxes.Add((t.worldToLocalMatrix, b, world)); hiddenOld.Add(t.gameObject);
            }
            foreach (var g in hiddenOld) g.SetActive(false);
            if (boxes.Count == 0) return;
            foreach (var f in campus.GetComponentsInChildren<MeshFilter>(true))
            {
                if (!f.name.StartsWith("Batched") || f.sharedMesh == null || !f.sharedMesh.isReadable) continue;
                var r = f.GetComponent<Renderer>(); if (r == null || !boxes.Any(b => b.world.Intersects(r.bounds))) continue;
                var mesh = f.sharedMesh; var v = mesh.vertices; var inside = new bool[v.Length]; var m = f.transform.localToWorldMatrix; int count = 0;
                for (int i = 0; i < v.Length; i++)
                {
                    var w = m.MultiplyPoint3x4(v[i]);
                    foreach (var b in boxes) if (b.world.Contains(w) && b.local.Contains(b.toLocal.MultiplyPoint3x4(w))) { inside[i] = true; count++; break; }
                }
                if (count == 0) continue;
                var copy = Instantiate(mesh); copy.name = mesh.name + " (old vehicles removed)"; int removed = 0;
                for (int s = 0; s < copy.subMeshCount; s++)
                {
                    var tris = copy.GetTriangles(s); var keep = new List<int>(tris.Length);
                    for (int i = 0; i < tris.Length; i += 3)
                        if (inside[tris[i]] && inside[tris[i + 1]] && inside[tris[i + 2]]) removed++; else { keep.Add(tris[i]); keep.Add(tris[i + 1]); keep.Add(tris[i + 2]); }
                    copy.SetTriangles(keep, s);
                }
                if (removed == 0) { Destroy(copy); continue; }
                stripped.Add((f, mesh, copy)); f.sharedMesh = copy; StrippedTriangles += removed;
            }
        }
        private void ParkCars()
        {
            var lots = new[] { Rect.MinMaxRect(545, -510, 690, -483), Rect.MinMaxRect(545, -554, 690, -533) };
            int index = 0;
            for (int l = 0; l < lots.Length; l++)
            {
                var a = lots[l]; float width = a.width / 27;
                for (int row = 0; row < 2; row++)
                {
                    float z = row == 0 ? a.yMin + 3 : a.yMax - 3, yaw = row == 0 ? 180 : 0;
                    for (int i = 1; i < 26; i++)
                    {
                        float x = a.xMin + (i + .5f) * width;
                        if (l == 0 && Mathf.Abs(x - 600) < 19) continue; // the accessible walkway crosses lot A
                        int hash = (i * 7349 + row * 1931 + l * 571 + 13) % 100; if (hash > (l == 0 ? 58 : 40)) continue;
                        var go = new GameObject("Parked car (hospital visitor)"); go.transform.SetParent(root, false);
                        go.transform.SetPositionAndRotation(new Vector3(x, .09f, z), Quaternion.Euler(0, yaw + (hash % 5 - 2) * .8f, 0));
                        var box = go.AddComponent<BoxCollider>(); box.size = new Vector3(1.95f, 1.5f, 4.6f); box.center = new Vector3(0, .75f, 0);
                        var body = go.AddComponent<Rigidbody>(); body.isKinematic = true; body.useGravity = false; body.interpolation = RigidbodyInterpolation.Interpolate;
                        go.AddComponent<EnterableVehicle>().Configure(1);
                        RealisticVehicleVisuals.ForTraffic(go, 300 + index * 5 + hash);
                        if (driving != null) driving.RegisterVehicle(body);
                        parked.Add(go.transform); index++;
                    }
                }
            }
        }

        private void BuildNetwork()
        {
            network = new TownRoadNetwork();
            foreach (var r in motel.GetComponentsInChildren<MeshRenderer>(true))
                if (r.gameObject.activeInHierarchy && TownPopulationController.RoadName(r.name)) network.Add(r.bounds.center, r.bounds.size);
            network.Build(null);
        }

        private Material Lit(string name, Color c, float smooth = .4f, float metallic = 0)
        {
            var m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name, color = c, enableInstancing = true };
            m.SetFloat("_Smoothness", smooth); m.SetFloat("_Metallic", metallic); owned.Add(m); return m;
        }
        private Material Glow(string name, Color c, float intensity)
        {
            var m = Lit(name, c * .7f); m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", c * intensity);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None; return m;
        }
        private void Materials()
        {
            redOn = Glow("Beacon red on", new Color(1, .05f, .04f), 9); redOff = Lit("Beacon red off", new Color(.35f, .03f, .03f), .8f);
            blueOn = Glow("Beacon blue on", new Color(.1f, .3f, 1), 9); blueOff = Lit("Beacon blue off", new Color(.04f, .08f, .3f), .8f);
            glowRedMat = Glow("Emergency sign red", new Color(1, .08f, .06f), 4); beaconMat = Glow("Aviation beacon", new Color(1, .1f, .05f), 12); beaconOff = Lit("Aviation beacon off", new Color(.3f, .04f, .03f));
            markerMat = Glow("Mission marker", new Color(1, .15f, .1f), 3); markerMat.SetFloat("_Surface", 1); markerMat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha); markerMat.SetFloat("_DstBlend", (float)BlendMode.One); markerMat.SetFloat("_ZWrite", 0); markerMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); markerMat.renderQueue = 3100; markerMat.color = new Color(1, .2f, .1f, .35f);
            white = Lit("Hospital white", new Color(.92f, .94f, .94f), .5f); dark = Lit("Hospital panel dark", new Color(.03f, .04f, .05f), .7f);
            metal = Lit("Hospital steel", new Color(.62f, .65f, .68f), .7f, .8f); teal = Lit("Hospital teal", new Color(.05f, .42f, .45f), .45f);
            mattress = Lit("Stretcher mattress", new Color(.85f, .55f, .12f), .35f); rubber = Lit("Hospital rubber", new Color(.03f, .03f, .035f), .2f);
            chairBlue = Lit("Wheelchair seat", new Color(.08f, .2f, .45f), .3f); flowers = Lit("Bouquet", new Color(.95f, .3f, .5f), .3f); bundle = Lit("Baby blanket", new Color(.92f, .85f, .95f), .2f);
            sceneLight = Glow("Ambulance scene light", new Color(1, .97f, .9f), 1.4f);
            var checks = Pattern("Battenburg", (x, y) => ((x / 32) + (y / 32)) % 2 == 0 ? new Color32(250, 220, 20, 255) : new Color32(20, 120, 60, 255));
            battenburg = Lit("Battenburg checks", Color.white, .5f); battenburg.mainTexture = checks; battenburg.mainTextureScale = new Vector2(7.5f, 1);
            battenburgCab = Lit("Battenburg checks cab", Color.white, .5f); battenburgCab.mainTexture = checks; battenburgCab.mainTextureScale = new Vector2(4.4f, .5f);
            var stripes = Pattern("Chevrons", (x, y) => ((x + y) / 16) % 2 == 0 ? new Color32(220, 20, 20, 255) : new Color32(250, 220, 20, 255));
            chevron = Lit("Rear chevrons", Color.white, .5f); chevron.mainTexture = stripes; chevron.mainTextureScale = new Vector2(4, 1);
            gown = Lit("IV fluid", new Color(.8f, .92f, 1f, 1), .9f);
            var tex = EcgTexture(); ecg =new Material(Shader.Find("Universal Render Pipeline/Unlit")) { name = "ICU ECG trace", mainTexture = tex }; owned.Add(ecg);
            var c = GameObject.CreatePrimitive(PrimitiveType.Cube); cube = c.GetComponent<MeshFilter>().sharedMesh; DestroyImmediate(c);
            var y = GameObject.CreatePrimitive(PrimitiveType.Cylinder); cylinder = y.GetComponent<MeshFilter>().sharedMesh; DestroyImmediate(y);
            var s = GameObject.CreatePrimitive(PrimitiveType.Sphere); sphere = s.GetComponent<MeshFilter>().sharedMesh; DestroyImmediate(s);
        }
        private Renderer Part(Transform parent, string name, Mesh mesh, Vector3 pos, Vector3 size, Material mat, Vector3? euler = null)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer)); go.transform.SetParent(parent, false);
            go.transform.localPosition = pos; go.transform.localScale = size; if (euler.HasValue) go.transform.localRotation = Quaternion.Euler(euler.Value);
            go.GetComponent<MeshFilter>().sharedMesh = mesh; var r = go.GetComponent<MeshRenderer>(); r.sharedMaterial = mat; return r;
        }
        private Renderer Box(Transform p, string n, Vector3 pos, Vector3 size, Material m) => Part(p, n, cube, pos, size, m);
        private TextMeshPro Text(Transform parent, string text, Vector3 pos, float yaw, Vector2 size, Color color, float max = 10)
        {
            var t = new GameObject("Sign " + text).AddComponent<TextMeshPro>(); t.transform.SetParent(parent, false);
            t.transform.localPosition = pos; t.transform.localRotation = Quaternion.Euler(0, yaw, 0);
            if (TMP_Settings.defaultFontAsset != null) t.font = TMP_Settings.defaultFontAsset;
            t.text = text; t.color = color; t.alignment = TextAlignmentOptions.Center; t.textWrappingMode = TextWrappingModes.NoWrap;
            t.enableAutoSizing = true; t.fontSizeMin = .1f; t.fontSizeMax = max; t.fontStyle = FontStyles.Bold; t.rectTransform.sizeDelta = size;
            var m = t.fontMaterial; if (m.HasProperty("_CullMode")) m.SetFloat("_CullMode", (float)CullMode.Back); owned.Add(m);
            signTexts.Add(text); return t;
        }

        // =====================================================================
        // Ambulances
        // =====================================================================
        private Rig CreateAmbulance(string name, Vector3 pos, float yaw, bool dispatch, bool enterable = true)
        {
            var go = new GameObject(name); go.transform.SetParent(root, false); go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0, yaw, 0));
            var size = RealisticVehicleVisuals.ColliderSize(VehicleBodyType.Van);
            var box = go.AddComponent<BoxCollider>(); box.size = size; box.center = Vector3.up * size.y * .5f;
            var body = go.AddComponent<Rigidbody>(); body.isKinematic = true; body.useGravity = false; body.interpolation = RigidbodyInterpolation.Interpolate; body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            var rig = new Rig { Body = body, T = go.transform };
            if (dispatch) rig.Agent = go.AddComponent<TownVehicleAgent>();
            else if (enterable) { go.AddComponent<EnterableVehicle>().Configure(1); if (driving != null) driving.RegisterVehicle(body); }
            go.AddComponent<RealisticVehicleVisuals>().Configure(VehicleBodyType.Van, 0); // paint 0 = ambulance white
            var src = go.AddComponent<AudioSource>(); src.loop = true; src.spatialBlend = 1; src.rolloffMode = AudioRolloffMode.Linear; src.minDistance = 14; src.maxDistance = 320; src.dopplerLevel = .5f; src.volume = .75f; src.playOnAwake = false;
            rig.Siren = src; return rig;
        }
        private void Livery(Rig rig, int number)
        {
            var t = new GameObject("Ambulance livery").transform; t.SetParent(rig.T, false);
            // White modular patient compartment over the van's rear (covers the dark glass greenhouse).
            Box(t, "Patient compartment", new Vector3(0, 1.65f, -.94f), new Vector3(2.04f, 1.34f, 2.98f), white);
            Box(t, "Compartment roof cap", new Vector3(0, 2.335f, -.94f), new Vector3(1.96f, .04f, 2.9f), metal);
            var reds = new List<Renderer>(); var blues = new List<Renderer>();
            // Low-profile LED light bar: six segments, red on the driver's left, blue on the right.
            Box(t, "LED bar base", new Vector3(0, 2.375f, .3f), new Vector3(1.72f, .05f, .34f), dark);
            for (int k = 0; k < 6; k++) { var seg = Box(t, "LED bar segment", new Vector3(-.7f + k * .28f, 2.45f, .3f), new Vector3(.25f, .1f, .28f), k < 3 ? redOff : blueOff); (k < 3 ? reds : blues).Add(seg); }
            Box(t, "LED bar centre", new Vector3(0, 2.45f, .3f), new Vector3(.05f, .1f, .29f), metal);
            reds.Add(Box(t, "Grille flasher red", new Vector3(-.42f, .78f, 2.47f), new Vector3(.22f, .07f, .03f), redOff));
            blues.Add(Box(t, "Grille flasher blue", new Vector3(.42f, .78f, 2.47f), new Vector3(.22f, .07f, .03f), blueOff));
            reds.Add(Box(t, "Rear corner flasher red", new Vector3(-.86f, 2.22f, -2.447f), new Vector3(.2f, .12f, .02f), redOff));
            blues.Add(Box(t, "Rear corner flasher blue", new Vector3(.86f, 2.22f, -2.447f), new Vector3(.2f, .12f, .02f), blueOff));
            foreach (int s in new[] { -1, 1 })
            {
                var list = s < 0 ? reds : blues; var off = s < 0 ? redOff : blueOff;
                list.Add(Box(t, "Upper side flasher", new Vector3(s * 1.027f, 2.22f, -2.25f), new Vector3(.012f, .12f, .2f), off));
                list.Add(Box(t, "Upper side flasher", new Vector3(s * 1.027f, 2.22f, .38f), new Vector3(.012f, .12f, .2f), off));
                list.Add(Box(t, "Mid side flasher", new Vector3(s * 1.0f, .72f, 1.9f), new Vector3(.02f, .08f, .16f), off));
                // High-visibility Battenburg checks (two rows on the box, one on the cab) with red pin-stripes.
                var big = Box(t, "Battenburg checks", new Vector3(s * 1.026f, 1.23f, -.94f), new Vector3(.012f, .42f, 2.98f), battenburg);
                var cab = Box(t, "Cab Battenburg checks", new Vector3(s * 1.0f, .9f, 1.45f), new Vector3(.02f, .2f, 1.75f), battenburgCab);
                Box(t, "Red pin-stripe", new Vector3(s * 1.027f, 1.47f, -.94f), new Vector3(.012f, .04f, 2.98f), glowRedMat);
                Box(t, "Red roof stripe", new Vector3(s * 1.027f, 2.14f, -.94f), new Vector3(.012f, .07f, 2.98f), glowRedMat);
                Box(t, "Compartment window", new Vector3(s * 1.027f, 1.9f, .2f), new Vector3(.01f, .34f, .5f), dark);
                Box(t, "Scene light", new Vector3(s * 1.03f, 2.24f, -1.0f), new Vector3(.02f, .08f, .34f), sceneLight);
                Box(t, "Side cross", new Vector3(s * 1.029f, 1.83f, -2.0f), new Vector3(.01f, .5f, .16f), glowRedMat);
                Box(t, "Side cross", new Vector3(s * 1.029f, 1.83f, -2.0f), new Vector3(.01f, .16f, .5f), glowRedMat);
                Text(t, "AMBULANCE", new Vector3(s * 1.032f, 1.83f, -.95f), s > 0 ? -90 : 90, new Vector2(1.35f, .24f), new Color(.05f, .2f, .5f), 2);
                Text(t, "HAVEN GRAND  " + number, new Vector3(s * 1.032f, 1.63f, -.95f), s > 0 ? -90 : 90, new Vector2(1.35f, .13f), new Color(.05f, .25f, .35f), 1);
            }
            // Kerb-side sliding door seams.
            foreach (float z in new[] { .4f, -.75f }) Box(t, "Side door seam", new Vector3(1.028f, 1.65f, z), new Vector3(.01f, 1.26f, .015f), dark);
            // Roof equipment.
            Box(t, "Roof air-conditioning unit", new Vector3(0, 2.44f, -1.55f), new Vector3(1.0f, .18f, .8f), white);
            Box(t, "Air-conditioning grille", new Vector3(0, 2.535f, -1.55f), new Vector3(.8f, .01f, .6f), dark);
            Box(t, "Radio antenna", new Vector3(.62f, 2.55f, -2.2f), new Vector3(.02f, .38f, .02f), dark);
            Box(t, "Roof cross", new Vector3(0, 2.36f, -.55f), new Vector3(.66f, .01f, .2f), glowRedMat);
            Box(t, "Roof cross", new Vector3(0, 2.36f, -.55f), new Vector3(.2f, .01f, .66f), glowRedMat);
            // Rear doors: windows, seam, red/yellow chevrons, step.
            Box(t, "Rear door seam", new Vector3(0, 1.65f, -2.446f), new Vector3(.025f, 1.3f, .012f), dark);
            foreach (int s in new[] { -1, 1 }) Box(t, "Rear door window", new Vector3(s * .48f, 1.98f, -2.447f), new Vector3(.62f, .38f, .012f), dark);
            Box(t, "Rear chevrons", new Vector3(0, 1.2f, -2.448f), new Vector3(1.98f, .42f, .012f), chevron);
            Box(t, "Rear step", new Vector3(0, .52f, -2.56f), new Vector3(1.7f, .07f, .26f), dark);
            Text(t, "AMBULANCE", new Vector3(0, 1.6f, -2.456f), 0, new Vector2(1.6f, .26f), new Color(.75f, .05f, .05f), 3);
            // Mirrored on the front so it reads correctly in a rear-view mirror.
            var front = Text(t, "AMBULANCE", new Vector3(0, 2.12f, .555f), 180, new Vector2(1.6f, .26f), new Color(.75f, .05f, .05f), 3); front.transform.localScale = new Vector3(-1, 1, 1);
            var lightGo = new GameObject("Beacon flash light"); lightGo.transform.SetParent(t, false); lightGo.transform.localPosition = new Vector3(0, 2.9f, .3f);
            var l = lightGo.AddComponent<Light>(); l.type = LightType.Point; l.range = 16; l.intensity = 0; l.shadows = LightShadows.None; l.enabled = false;
            rig.Red = reds.ToArray(); rig.Blue = blues.ToArray(); rig.Flash = l;
        }
        private void SetSiren(Rig rig, bool siren, bool lights)
        {
            if (rig == null) return;
            if (rig.SirenOn != siren)
            {
                rig.SirenOn = siren;
                if (siren) { if (rig.Siren.clip != null && !rig.Siren.isPlaying) rig.Siren.Play(); TownVehicleAgent.AddSiren(rig.T); }
                else { rig.Siren.Stop(); TownVehicleAgent.RemoveSiren(rig.T); }
            }
            rig.Lights = lights || siren;
            if (!rig.Lights) { foreach (var r in rig.Red) r.sharedMaterial = redOff; foreach (var r in rig.Blue) r.sharedMaterial = blueOff; rig.Flash.enabled = false; }
        }
        private void FlashBeacons(Rig rig)
        {
            if (rig == null || !rig.Lights) return;
            float t = Time.time * 7.5f; int phase = (int)t % 4; bool red = phase == 0 || phase == 1 && (t % 1) < .5f, blue = phase == 2 || phase == 3 && (t % 1) < .5f;
            foreach (var r in rig.Red) r.sharedMaterial = red ? redOn : redOff; foreach (var r in rig.Blue) r.sharedMaterial = blue ? blueOn : blueOff;
            bool near = Camera.main != null && (Camera.main.transform.position - rig.T.position).sqrMagnitude < 160 * 160;
            rig.Flash.enabled = near && (red || blue); rig.Flash.color = red ? new Color(1, .1f, .08f) : new Color(.15f, .35f, 1); rig.Flash.intensity = Night() * 6 + 1.5f;
        }

        // =====================================================================
        // Incidents and the AI dispatch cycle
        // =====================================================================
        /// <summary>Starts an incident right away (nearest suitable citizen to the camera) and dispatches the AI ambulance.</summary>
        public bool DispatchNow(TownCitizenAgent preferred = null)
        {
            if (!Ready || State != Call.Idle) { LastDispatchError = "busy"; return false; }
            Vector3[] route = null;
            var who = preferred != null && preferred.Available ? preferred : PickPatient(Camera.main != null ? Camera.main.transform.position : BayHome, 60, 2500, true, null, out route);
            if (who == null) return false;
            if (route == null) route = network.Route(DepartFrom, who.transform.position, false, 2.2f);
            if (route.Length < 2) { LastDispatchError = "no road route to the patient"; return false; }
            patient = who; Collapse(who);
            lastRoute = DepartPath.Concat(route).ToArray(); ai.Agent.SetRoute(lastRoute, 15);
            SetSiren(ai, true, true); State = Call.Responding; callStarted = Time.time;
            if (Near(who.transform.position, 220)) Say("Someone collapsed nearby! An ambulance from Haven Grand is on the way.");
            return true;
        }
        public string LastDispatchError { get; private set; } = "";
        /// <summary>Best citizen for an incident that an ambulance can actually reach by road (route returned).</summary>
        private TownCitizenAgent PickPatient(Vector3 around, float minFromHospital, float maxFromHospital, bool preferNear, TownCitizenAgent except, out Vector3[] route)
        {
            route = null; var hospital = HospitalCampusLayout.Centre; var list = new List<(TownCitizenAgent c, float score)>(); int seen = 0;
            foreach (var c in FindObjectsByType<TownCitizenAgent>(FindObjectsSortMode.None))
            {
                seen++;
                if (c == except || !c.Available || c.Attending || !c.name.StartsWith("Citizen") || c.Human.Identity.IsChild) continue;
                var p = c.transform.position; if (p.y < -1 || p.y > 2.5f) continue;
                float fromHospital = Flat(p - hospital).magnitude; if (fromHospital < minFromHospital || fromHospital > maxFromHospital) continue;
                if (HospitalCampusLayout.Campus.Contains(new Vector2(p.x, p.z))) continue;
                var road = network.Nearest(p); if (road == null || road.HalfWidth < 2.2f) continue;
                if (Flat(TownRoadNetwork.Project(p, road) - p).magnitude > road.HalfWidth + 12) continue;
                list.Add((c, preferNear ? Flat(p - around).magnitude : Random.value));
            }
            int tries = 0;
            foreach (var (c, _) in list.OrderBy(e => e.score))
            {
                if (++tries > 16) break;
                var r = network.Route(DepartFrom, c.transform.position, false, 2.2f);
                if (r.Length >= 2) { route = r; LastDispatchError = ""; return c; }
            }
            LastDispatchError = "citizens " + seen + ", candidates " + list.Count + ", none reachable by road (" + network.Roads.Count + " roads)";
            return null;
        }
        private void Collapse(TownCitizenAgent who)
        {
            who.Collapse();
            int helpers = 0; var at = who.transform.position;
            foreach (var c in FindObjectsByType<TownCitizenAgent>(FindObjectsSortMode.None))
            {
                if (helpers >= 2 || c == who || !c.Available || c.Human.Identity.IsChild) continue;
                if ((c.transform.position - at).sqrMagnitude > 20 * 20) continue;
                var side = Quaternion.Euler(0, 70 + helpers * 140, 0) * Vector3.forward * 1.7f;
                c.Attend(at + side, at + Vector3.up * .3f, 45); helpers++;
            }
        }
        private void UpdateDispatch()
        {
            if (ai == null || ai.Agent == null) return;
            switch (State)
            {
                case Call.Idle:
                    if (Time.time >= nextIncident) { if (!DispatchNow()) nextIncident = Time.time + 25; }
                    break;
                case Call.Responding:
                    if (patient == null) { ReturnToBase(); break; }
                    Unstick(ai);
                    if (ai.Agent.Arrived) { State = Call.OnScene; SetSiren(ai, false, true); StartPickup(); }
                    break;
                case Call.OnScene:
                    break; // the stretcher team drives the sequence
                case Call.Returning:
                    Unstick(ai);
                    if (ai.Agent.Arrived) { State = Call.Unloading; SetSiren(ai, false, false); StartUnload(); if (Near(BayHome, 90) || insideBuilding) Page("Ambulance arriving at the Emergency bay. Trauma team to ER."); }
                    break;
            }
        }
        private void Unstick(Rig rig)
        {
            var a = rig.Agent; bool seen = Near(rig.T.position, 90);
            if (a.BlockedSeconds > (seen ? 12 : 3)) a.Nudge();
            // A very long trip far from the player is fast-forwarded, never in view.
            if (!seen && Time.time - callStarted > 240) SkipTravel();
        }
        /// <summary>Fast-forwards a driving ambulance to the end of its route (tests and very long trips).</summary>
        public void SkipTravel()
        {
            if (lastRoute == null || lastRoute.Length < 2 || (State != Call.Responding && State != Call.Returning)) return;
            var end = lastRoute[lastRoute.Length - 1]; var before = lastRoute[lastRoute.Length - 2]; var dir = Flat(end - before);
            end.y = .09f; var rot = dir.sqrMagnitude > .01f ? Quaternion.LookRotation(dir) : ai.T.rotation;
            ai.Body.position = end; ai.Body.rotation = rot; ai.T.SetPositionAndRotation(end, rot); ai.Agent.SetRoute(new[] { end }, 15); callStarted = Time.time;
        }
        private void ReturnToBase()
        {
            var route = network.Route(ai.T.position, ReturnRoad, false, 2.2f);
            lastRoute = route.Concat(ArrivePath).ToArray(); ai.Agent.SetRoute(lastRoute, 13);
            SetSiren(ai, patient != null, true); State = Call.Returning; callStarted = Time.time;
        }
        private void StartPickup()
        {
            var rear = Flat(ai.T.position - ai.T.forward * 3.5f); rear.y = patient.transform.position.y;
            var p = patient.transform.position; var dir = Flat(p - rear); if (dir.sqrMagnitude < .01f) dir = ai.T.right; dir.Normalize();
            var stop = p - dir * 1.6f;
            Begin(stretcher, rear, new[] { rear, stop }, w =>
            {
                w.Wait = 3.5f; w.FrontMedic.Gesture = HumanGesture.Talk;
                StartCoroutine(Load(w, rear));
            });
        }
        private IEnumerator Load(Walker w, Vector3 rear)
        {
            yield return new WaitForSeconds(3.5f);
            if (patient != null) { Board(w, patient.Human.Identity); patient.Hide(); }
            w.FrontMedic.Gesture = HumanGesture.None;
            w.Path = new[] { w.Root.position, rear }; w.I = 0; w.Wait = 0; w.Loop = false;
            w.Done = t => { t.Root.gameObject.SetActive(false); ClearBoard(t); ReturnToBase(); };
        }
        private void StartUnload()
        {
            var identity = patient != null ? patient.Human.Identity : HumanIdentity.Create(9001, HumanRole.Civilian);
            Begin(stretcher, UnloadPath[0], UnloadPath, w =>
            {
                w.Root.gameObject.SetActive(false); ClearBoard(w);
                if (patient != null) { discharge.Add((patient, Time.time + 70)); Rescued++; }
                patient = null; State = Call.Idle; nextIncident = Time.time + Random.Range(100f, 170f);
            });
            Board(stretcher, identity);
        }
        private void Discharges()
        {
            for (int i = discharge.Count - 1; i >= 0; i--)
            {
                if (Time.time < discharge[i].at) continue; var who = discharge[i].who; discharge.RemoveAt(i);
                if (who == null) continue; Discharge(who);
            }
        }
        private void Discharge(TownCitizenAgent who)
        {
            var exit = new Vector3(600 + Random.Range(-1.5f, 1.5f), .2f, -461);
            var road = network.Roads.Count > 0 ? network.Roads[Random.Range(0, network.Roads.Count)] : null;
            var goal = road == null ? exit + Vector3.back * 30 : Vector3.Lerp(road.A, road.B, Random.Range(.2f, .8f));
            var walk = network.Route(exit, goal, true); if (walk.Length == 0) walk = new[] { new Vector3(600, .1f, -478) };
            who.Recover(exit, walk); Discharged++;
            if (Near(exit, 80)) Say("A patient has been discharged from Haven Grand - healthy again!");
        }
        /// <summary>Discharges everyone being treated (tests).</summary>
        public void DischargeAll() { foreach (var d in discharge.ToArray()) if (d.who != null) Discharge(d.who); discharge.Clear(); }

        // =====================================================================
        // Player ambulance missions
        // =====================================================================
        private bool DrivingAmbulance => driving != null && driving.IsDriving && player != null && driving.ActiveVehicleTransform == player.T;
        private void UpdateMission()
        {
            if (player == null) return;
            bool inside = DrivingAmbulance;
            if (inside && Keyboard.current != null && Keyboard.current.hKey.wasPressedThisFrame) SetSiren(player, !player.SirenOn, true);
            switch (MissionState)
            {
                case Mission.None:
                    if (inside) StartMission();
                    else if (player.SirenOn || player.Lights) SetSiren(player, false, false);
                    break;
                case Mission.ToPatient:
                    if (missionPatient == null || !missionPatient.Incapacitated) { MissionState = Mission.None; break; }
                    MissionTarget = missionPatient.transform.position;
                    if (inside && Flat(player.T.position - MissionTarget).magnitude < 11 && driving.SpeedKph < 9) { MissionState = Mission.Loading; loadingUntil = Time.time + 3; }
                    break;
                case Mission.Loading:
                    if (missionPatient == null) { MissionState = Mission.None; break; }
                    if (Flat(player.T.position - MissionTarget).magnitude > 14) { MissionState = Mission.ToPatient; break; }
                    if (Time.time >= loadingUntil) { missionPatient.Hide(); MissionState = Mission.ToHospital; MissionTarget = PlayerBay; Say("Patient on board! Rush back to Haven Grand ER."); }
                    break;
                case Mission.ToHospital:
                    MissionTarget = PlayerBay;
                    if (Flat(player.T.position - PlayerBay).magnitude < 16 && (driving == null || driving.SpeedKph < 9))
                    {
                        float minutes = (Time.time - missionStarted) / 60f; LastReward = MissionBaseReward + Mathf.RoundToInt(Mathf.Clamp(4 - minutes, 0, 4) * 30);
                        motel.Credit(LastReward); MissionsCompleted++; Rescued++;
                        discharge.Add((missionPatient, Time.time + 60)); missionPatient = null; MissionState = Mission.None; SetSiren(player, false, false);
                        Say("Patient saved! +" + LastReward + " coins. Stay in the ambulance for the next call."); Page("Patient received from ambulance two. Well done, crew.");
                        missionCooldown = Time.time + 6;
                    }
                    break;
            }
            if (marker != null)
            {
                bool show = MissionState != Mission.None; if (marker.activeSelf != show) marker.SetActive(show);
                if (show) { marker.transform.position = MissionTarget; marker.transform.Rotate(0, 40 * Time.deltaTime, 0); }
            }
        }
        private float missionCooldown;
        /// <summary>Starts a paid rescue mission for the player's ambulance.</summary>
        public bool StartMission()
        {
            if (!Ready || MissionState != Mission.None || Time.time < missionCooldown) return false;
            var who = PickPatient(BayHome, 150, 1100, false, patient, out _); if (who == null) { missionCooldown = Time.time + 10; return false; }
            missionPatient = who; Collapse(who); MissionState = Mission.ToPatient; missionStarted = Time.time; MissionTarget = who.transform.position;
            SetSiren(player, true, true); Say("EMERGENCY CALL: a patient needs help " + Mathf.RoundToInt(Flat(MissionTarget - player.T.position).magnitude) + " m away. Follow the red beacon! (H: siren)");
            return true;
        }
        private void Marker()
        {
            marker = new GameObject("Ambulance mission marker"); marker.transform.SetParent(root, false);
            var beam = Part(marker.transform, "Beacon beam", cylinder, new Vector3(0, 30, 0), new Vector3(1.6f, 30, 1.6f), markerMat); beam.shadowCastingMode = ShadowCastingMode.Off;
            var ring = Part(marker.transform, "Beacon ring", cylinder, new Vector3(0, .05f, 0), new Vector3(7, .02f, 7), markerMat); ring.shadowCastingMode = ShadowCastingMode.Off;
            marker.SetActive(false);
        }

        // =====================================================================
        // People: generic walkers (interior, exterior, teams)
        // =====================================================================
        private HumanIdentity Identity(int seed, string role)
        {
            var id = HumanIdentity.Create(seed, role == "civ" ? HumanRole.Civilian : HumanRole.Doctor, role == "civ" ? (HumanAge?)null : HumanAge.Adult);
            id.Backpack = false; id.Umbrella = false; id.Cap = false;
            if (role == "medic") { id.Shirt = new Color(.12f, .42f, .3f); id.Trousers = new Color(.07f, .16f, .14f); id.Top = HumanTop.Shirt; id.Handbag = false; id.Phone = false; }
            if (role == "nurse") { id.Shirt = new Color(.42f, .66f, .86f); id.Trousers = new Color(.38f, .6f, .8f); id.Top = HumanTop.Shirt; id.Handbag = false; }
            if (role == "patient") { id.Shirt = new Color(.62f, .81f, .82f); id.Trousers = new Color(.45f, .64f, .70f); id.Top = HumanTop.Shirt; id.Handbag = false; id.Phone = false; }
            return id;
        }
        private Walker NewWalker(string kind, string name, Vector3 at, int floor, HumanIdentity? id)
        {
            var go = new GameObject(name); go.transform.SetParent(root, false); go.transform.position = at;
            var w = new Walker { Kind = kind, Root = go.transform, Floor = floor, Seed = id.HasValue ? id.Value.Seed : 0 };
            if (id.HasValue) w.Human = HumanVisual.Create(go.transform, id.Value, true);
            walkers.Add(w); return w;
        }
        private static float FloorY(int floor) => HospitalCampusLayout.Storey * floor + .12f;
        private static Vector3 H(float x, int floor, float z) => HospitalCampusLayout.Point(x, FloorY(floor), z);

        private void Rounds()
        {
            for (int floor = 2; floor <= 5; floor++)
            {
                var pts = new List<Vector3>(); var waits = new List<float>(); var gestures = new List<HumanGesture>(); var looks = new List<Vector3?>();
                void Add(float x, float z, float wait = 0, HumanGesture g = HumanGesture.None, Vector3? look = null) { pts.Add(H(x, floor, z)); waits.Add(wait); gestures.Add(g); looks.Add(look); }
                for (int i = 0; i < 7; i++)
                {
                    float x = i < 4 ? -23 + i * 14 : -23 + (i - 4) * 14; int s = i < 4 ? 1 : -1; float bedZ = s * 13 + 1.5f;
                    Add(x, s * 1.2f); Add(x, s * 4.4f); Add(x - 2.6f, s * 6.5f);
                    Add(x - 1.45f, bedZ - .3f, 6 + (i % 3), HumanGesture.Talk, H(x - 3.3f, floor, bedZ + .85f) + Vector3.up * 1.1f);
                    Add(x - 2.6f, s * 6.5f); Add(x, s * 4.4f);
                }
                var w = NewWalker("rounds", "Doctor on ward rounds L" + floor, pts[0], floor, Identity(6100 + floor, "doctor"));
                w.Path = pts.ToArray(); w.Waits = waits.ToArray(); w.Gestures = gestures.ToArray(); w.Looks = looks.ToArray(); w.Speed = 1.1f;
                // A junior doctor or student follows a step behind on two floors.
                if (floor == 3 || floor == 5)
                {
                    var j = NewWalker("rounds", "Junior doctor L" + floor, pts[0] + Vector3.back, floor, Identity(6200 + floor, "nurse"));
                    j.Path = pts.Select(p => p + new Vector3(-.9f, 0, -.6f)).ToArray(); j.Waits = w.Waits; j.Gestures = w.Gestures.Select(g => g == HumanGesture.Talk ? HumanGesture.Relaxed : g).ToArray(); j.Looks = w.Looks; j.Speed = 1.1f;
                }
            }
        }
        private void Trolleys()
        {
            for (int floor = 1; floor <= 5; floor++)
            {
                var pts = new List<Vector3>(); var waits = new List<float>(); var looks = new List<Vector3?>();
                float[] xs = { -23, -9, 5, 19 };
                foreach (var x in xs) { pts.Add(H(x + 1.5f, floor, 1.6f)); waits.Add(3.5f); looks.Add(H(x, floor, 9) + Vector3.up * 1.4f); }
                for (int k = xs.Length - 1; k >= 0; k--) { if (k == 3) continue; pts.Add(H(xs[k] + 1.5f, floor, -1.6f)); waits.Add(3.5f); looks.Add(H(xs[k], floor, -9) + Vector3.up * 1.4f); }
                var w = NewWalker("trolley", "Nurse with medicine trolley L" + floor, pts[0], floor, null);
                var nurse = HumanVisual.Create(w.Root, Identity(6300 + floor, "nurse"), true); nurse.transform.localPosition = new Vector3(0, 0, -.55f); nurse.Gesture = HumanGesture.Umbrella;
                var cart = new GameObject("Medicine trolley").transform; cart.SetParent(w.Root, false); cart.localPosition = new Vector3(0, 0, .45f);
                Box(cart, "Trolley body", new Vector3(0, .62f, 0), new Vector3(.55f, .7f, .8f), white);
                Box(cart, "Trolley top", new Vector3(0, 1.0f, 0), new Vector3(.6f, .05f, .86f), teal);
                for (int d = 0; d < 3; d++) Box(cart, "Trolley drawer", new Vector3(0, .45f + d * .2f, -.41f), new Vector3(.46f, .02f, .02f), metal);
                Box(cart, "Medicine boxes", new Vector3(.1f, 1.08f, .15f), new Vector3(.2f, .12f, .25f), mattress);
                foreach (int a in new[] { -1, 1 }) foreach (int b in new[] { -1, 1 }) Part(cart, "Caster", sphere, new Vector3(a * .22f, .08f, b * .32f), Vector3.one * .12f, rubber);
                w.Path = pts.ToArray(); w.Waits = waits.ToArray(); w.Looks = looks.ToArray(); w.Speed = .9f; w.Human = nurse;
            }
        }
        private void Lobby()
        {
            // Reception queue in front of the middle counter.
            for (int k = 0; k < 5; k++)
            {
                var w = NewWalker("queue", "Reception queue visitor " + k, Slot(k), 0, Identity(6400 + k * 7, "civ"));
                w.Loop = false; w.Path = new[] { Slot(k) }; queue.Add(w);
            }
            // Seated waiting room on the lobby benches (bench centres at x = ±16, z = -8, -12, -16).
            int n = 0;
            for (int side = -1; side <= 1; side += 2) for (int row = 0; row < 3; row++)
                {
                    float yaw = side < 0 ? 90 : -90; var bench = H(side * 16, 0, -8 - row * 4); var rot = Quaternion.Euler(0, yaw, 0);
                    for (int seat = -1; seat <= 1; seat++)
                    {
                        if ((seat + row + side + 3) % 3 == 0) continue;
                        var id = Identity(6500 + n * 11, n % 5 == 0 ? "patient" : "civ"); n++;
                        var w = NewWalker("seated", "Waiting room visitor " + n, bench + rot * new Vector3(seat * 1.05f, 0, .1f), 0, id);
                        w.Root.rotation = Quaternion.Euler(0, yaw + 180, 0); w.Human.Seated = true; float s = id.Height / 1.8f;
                        w.Human.transform.localPosition = new Vector3(0, .57f - .44f * s, 0);
                        w.Human.Gesture = n % 3 == 0 ? HumanGesture.Phone : n % 3 == 1 ? HumanGesture.Relaxed : HumanGesture.Talk;
                    }
                }
            nextGesture = Time.time + 9;
        }
        private static Vector3 Slot(int k) => H(0, 0, -1.45f - k * .95f);
        private void AdvanceQueue()
        {
            if (queue.Count == 0) return;
            var front = queue[0]; queue.RemoveAt(0); token++;
            if (boardText != null) boardText.text = "NOW SERVING   A-0" + token + "   >  COUNTER 2";
            for (int k = 0; k < queue.Count; k++) { queue[k].Path = new[] { Slot(k) }; queue[k].I = 0; queue[k].Done = null; }
            var exits = new[]
            {
                new[] { H(-6, 0, -5), H(-28, 0, -3.5f), H(-36, 0, -3.5f), H(-36, 0, -.5f) },      // to the lift
                new[] { H(6, 0, -5), H(22, 0, -6), H(30, 0, -10) },                                 // to Emergency
                new[] { H(3, 0, -10), H(1, 0, -20), H(0, 0, -23.5f), H(0, 0, -28) },                // out of the main doors
            };
            front.Path = exits[token % exits.Length]; front.I = 0; front.Loop = false; front.Look = null;
            front.Done = w =>
            {
                w.Root.gameObject.SetActive(false); w.Hidden = Time.time + 6;
                w.Done = null; w.Path = new[] { H(0, 0, -28), H(0, 0, -21), H(.5f, 0, -12), Slot(queue.Count) }; w.I = 0; w.Root.position = H(0, 0, -28);
                queue.Add(w);
            };
            // Nobody is watching the lobby: the visitor simply rejoins the back of the line.
            if (!front.Root.gameObject.activeSelf)
            {
                front.Done = null; front.Hidden = 0; front.Path = new[] { Slot(queue.Count) }; front.I = 0; front.Root.position = Slot(queue.Count); queue.Add(front);
            }
        }
        private void BuildWheelchairs()
        {
            // Outdoors on the garden promenade, and one inside from the main doors to the lift.
            var outdoor = new[] { new Vector3(508, .12f, -486), new Vector3(508, .12f, -440), new Vector3(508, .12f, -383), new Vector3(508, .12f, -440) };
            Chair("Wheelchair stroll garden", outdoor, -1, 7700, new[] { 2f, 0, 8, 0 });
            var indoor = new[] { H(1.5f, 0, -21), H(-6, 0, -5), H(-28, 0, -3.5f), H(-36, 0, -3.5f), H(-36, 0, -1), H(-36, 0, -3.5f), H(-28, 0, -3.5f), H(-6, 0, -5), H(1.5f, 0, -21) };
            Chair("Wheelchair to the lift", indoor, 0, 7710, new[] { 4f, 0, 0, 0, 6, 0, 0, 0, 0 });
        }
        private void Chair(string name, Vector3[] path, int floor, int seed, float[] waits)
        {
            var w = NewWalker("wheelchair", name, path[0], floor, null); w.Path = path; w.Waits = waits; w.Speed = .85f; w.Snap = floor < 0;
            var chair = new GameObject("Wheelchair").transform; chair.SetParent(w.Root, false);
            Box(chair, "Seat", new Vector3(0, .5f, 0), new Vector3(.48f, .06f, .46f), chairBlue);
            Box(chair, "Backrest", new Vector3(0, .8f, -.24f), new Vector3(.48f, .55f, .05f), chairBlue);
            Box(chair, "Footrest", new Vector3(0, .14f, .36f), new Vector3(.4f, .03f, .16f), metal);
            foreach (int s in new[] { -1, 1 })
            {
                Part(chair, "Big wheel", cylinder, new Vector3(s * .3f, .3f, -.08f), new Vector3(.6f, .02f, .6f), rubber, new Vector3(0, 0, 90));
                Part(chair, "Castor", sphere, new Vector3(s * .2f, .06f, .3f), Vector3.one * .12f, rubber);
                Box(chair, "Push handle", new Vector3(s * .2f, 1.05f, -.33f), new Vector3(.04f, .04f, .18f), metal);
                Box(chair, "Armrest", new Vector3(s * .25f, .7f, 0), new Vector3(.04f, .04f, .4f), metal);
            }
            var pid = Identity(seed, "patient"); var p = HumanVisual.Create(w.Root, pid, true); p.Seated = true; p.transform.localPosition = new Vector3(0, .53f - .44f * pid.Height / 1.8f, -.02f); p.Gesture = HumanGesture.Relaxed;
            var helper = HumanVisual.Create(w.Root, Identity(seed + 1, seed % 2 == 0 ? "nurse" : "civ"), true); helper.transform.localPosition = new Vector3(0, 0, -.85f); helper.Gesture = HumanGesture.Umbrella;
            w.Human = helper; w.Patient = p;
        }
        private void VisitorsOutside()
        {
            for (int i = 0; i < 12; i++)
            {
                var id = Identity(7800 + i * 13, "civ"); if (i % 5 == 2) id = HumanIdentity.Create(7800 + i * 13, HumanRole.Civilian, HumanAge.Senior);
                var w = NewWalker("visitor", "Hospital visitor " + i, new Vector3(600, .1f, -494), -1, id);
                w.Snap = true; w.Speed = id.Age == HumanAge.Senior ? .8f : 1.1f + (i % 3) * .1f; w.Hidden = Time.time + i * 4; w.Root.gameObject.SetActive(false);
                // Some come as a family (a child holding a hand) or with a partner.
                if (i % 4 == 1 || i % 4 == 3)
                {
                    var other = HumanVisual.Create(w.Root, i % 4 == 1 ? HumanIdentity.Create(7900 + i, HumanRole.Civilian, HumanAge.Child) : Identity(7950 + i, "civ"), true);
                    other.transform.localPosition = new Vector3(.62f, 0, 0); other.Gesture = HumanGesture.HoldLeft; w.Human.transform.localPosition = new Vector3(-.2f, 0, 0); w.Companion = other;
                    w.Speed = Mathf.Min(w.Speed, i % 4 == 1 ? .95f : 1.1f);
                }
                VisitorIn(w);
            }
        }
        /// <summary>Walk between a parked car's driver door and the main entrance (lot A or, across the road, lot B).</summary>
        private Vector3[] CarToDoor(Walker w, out Vector3 start)
        {
            float lane = (w.Seed % 3 - 1) * 1.1f; var car = parked.Count > 0 ? parked[Random.Range(0, parked.Count)] : null;
            if (car == null) { start = new Vector3(600 + lane, .1f, -494); return new[] { start, new Vector3(600 + lane, .1f, -478), new Vector3(600 + lane, .1f, -462), new Vector3(600 + lane * .4f, .12f, -458.6f), new Vector3(600, .12f, -455) }; }
            var p = car.position; float aisle = p.z > -511 ? -496.5f : -543.5f; float side = p.z > aisle ? -1 : 1;
            start = p + car.right * 1.45f; start.y = .1f;
            return new[] { start, new Vector3(start.x, .1f, aisle + side * .3f), new Vector3(600 + lane, .1f, aisle), new Vector3(600 + lane, .1f, -478), new Vector3(600 + lane, .1f, -462), new Vector3(600 + lane * .4f, .12f, -458.6f), new Vector3(600, .12f, -455) };
        }
        private void VisitorIn(Walker w)
        {
            w.Path = CarToDoor(w, out var start); w.Root.position = start; w.Inside = false;
            w.I = 0; w.Loop = false; SetCarry(w, w.Seed % 3 == 0 ? "flowers" : null);
            w.Done = t => { t.Root.gameObject.SetActive(false); t.Hidden = Time.time + Random.Range(25f, 60f); t.Inside = true; t.Done = VisitorOut; };
        }
        private void VisitorOut(Walker w)
        {
            w.Path = CarToDoor(w, out _).Reverse().ToArray(); w.Root.position = w.Path[0];
            w.I = 0; w.Loop = false; SetCarry(w, w.Companion == null && Random.value < .4f ? "baby" : null);
            w.Done = t => { t.Root.gameObject.SetActive(false); t.Hidden = Time.time + Random.Range(10f, 40f); t.Done = null; VisitorIn(t); };
        }

        // ---------------- Patients outdoors, entrance life ----------------
        private void GardenPatients()
        {
            // Patients in hospital gowns resting on the healing-garden benches, some with a visitor beside them.
            for (int i = 0; i < 5; i++)
            {
                var bench = new Vector3(503, .12f, -450 + i * 16); var rot = Quaternion.Euler(0, 90, 0);
                for (int seat = -1; seat <= 1; seat += 2)
                {
                    if (seat > 0 && i % 2 == 1) continue;
                    var id = Identity(8100 + i * 5 + seat, seat < 0 ? "patient" : "civ"); if (i == 3 && seat < 0) id = HumanIdentity.Create(8100 + i, HumanRole.Civilian, HumanAge.Senior);
                    if (seat < 0) { id.Shirt = new Color(.62f, .81f, .82f); id.Trousers = new Color(.45f, .64f, .70f); id.Top = HumanTop.Shirt; id.Handbag = false; id.Backpack = false; id.Phone = false; }
                    var w = NewWalker("garden", "Garden bench " + (seat < 0 ? "patient " : "visitor ") + i, bench + rot * new Vector3(seat * 1.05f, 0, .1f), -1, id);
                    w.Root.rotation = Quaternion.Euler(0, 270, 0); w.Human.Seated = true; w.Human.transform.localPosition = new Vector3(0, .57f - .44f * id.Height / 1.8f, 0);
                    w.Human.Gesture = seat < 0 ? HumanGesture.Relaxed : HumanGesture.Talk; if (seat < 0 && i % 2 == 0) w.Human.LookTarget = bench + rot * new Vector3(1.05f, 1.2f, 0);
                }
            }
            // Patients walking slowly with a drip stand, a relative or nurse at their side.
            var loops = new[]
            {
                new[] { new Vector3(508, .12f, -470), new Vector3(508, .12f, -420), new Vector3(508, .12f, -392), new Vector3(508, .12f, -420) },
                new[] { new Vector3(530, .12f, -470), new Vector3(530, .12f, -405), new Vector3(520, .12f, -399), new Vector3(530, .12f, -405) },
            };
            for (int i = 0; i < loops.Length; i++)
            {
                var w = NewWalker("garden", "Patient walking with drip stand " + i, loops[i][0], -1, Identity(8300 + i, "patient")); w.Path = loops[i]; w.Waits = new[] { 6f, 0, 9, 0 }; w.Snap = true; w.Speed = .5f;
                w.Human.transform.localPosition = new Vector3(-.3f, 0, 0); w.Moving = HumanGesture.HoldLeft;
                var pole = new GameObject("Drip stand").transform; pole.SetParent(w.Root, false); pole.localPosition = new Vector3(-.85f, 0, .12f);
                Box(pole, "Pole", new Vector3(0, 1.0f, 0), new Vector3(.03f, 1.9f, .03f), metal);
                Box(pole, "Hook bar", new Vector3(0, 1.93f, 0), new Vector3(.3f, .02f, .02f), metal);
                Box(pole, "Fluid bag", new Vector3(.1f, 1.75f, 0), new Vector3(.12f, .22f, .05f), gown);
                for (int k = 0; k < 4; k++) { float a = k * Mathf.PI / 2; Box(pole, "Base leg", new Vector3(Mathf.Cos(a) * .15f, .06f, Mathf.Sin(a) * .15f), new Vector3(.3f * Mathf.Abs(Mathf.Cos(a)) + .03f, .02f, .3f * Mathf.Abs(Mathf.Sin(a)) + .03f), metal); }
                var helper = HumanVisual.Create(w.Root, Identity(8350 + i, i == 0 ? "nurse" : "civ"), true); helper.transform.localPosition = new Vector3(.38f, 0, 0); helper.Gesture = HumanGesture.HoldLeft; w.Companion = helper;
            }
        }
        private void EntranceLife()
        {
            // Security guard at the main doors and a patient in a wheelchair waiting for a lift home with family.
            var guard = NewWalker("entrance", "Hospital security guard", new Vector3(606.5f, .12f, -461.2f), -1, HumanIdentity.Create(8400, HumanRole.Police));
            guard.Root.rotation = Quaternion.Euler(0, 200, 0); guard.Human.Gesture = HumanGesture.LookAround;
            var waiting = new[] { new Vector3(593.5f, .12f, -462.5f) };
            Chair("Wheelchair waiting for pick-up", waiting, -1, 8410, new[] { 1f }); var chair = walkers[walkers.Count - 1]; chair.Loop = false; chair.Snap = true; chair.Root.rotation = Quaternion.Euler(0, 180, 0);
            chair.Human.Gesture = HumanGesture.Phone;
        }
        private void SetCarry(Walker w, string what)
        {
            if (w.Carry != null) Destroy(w.Carry); w.Carry = null; w.Moving = w.Companion != null ? HumanGesture.HoldRight : HumanGesture.None;
            if (what == null || w.Human == null) return;
            var go = new GameObject(what == "baby" ? "Newborn baby in blanket" : "Flower bouquet"); go.transform.SetParent(w.Human.RightHand, false);
            if (what == "baby")
            {
                Part(go.transform, "Blanket", sphere, new Vector3(-.12f, .02f, .08f), new Vector3(.2f, .18f, .42f), bundle);
                if (babySkin == null) babySkin = Lit("Baby skin", new Color(.86f, .66f, .52f));
                Part(go.transform, "Baby face", sphere, new Vector3(-.12f, .08f, .25f), Vector3.one * .1f, babySkin);
                w.Moving = HumanGesture.Umbrella;
            }
            else
            {
                Part(go.transform, "Stems", cylinder, new Vector3(0, -.05f, .05f), new Vector3(.05f, .14f, .05f), teal);
                Part(go.transform, "Blooms", sphere, new Vector3(0, .12f, .08f), new Vector3(.18f, .14f, .18f), flowers);
                w.Moving = HumanGesture.Umbrella;
            }
            w.Carry = go;
        }
        private Walker Team(string name)
        {
            var w = NewWalker("stretcher", name, Vector3.zero, 9, null); w.Loop = false; w.Snap = true; w.Speed = 1.35f;
            var s = new GameObject("Stretcher").transform; s.SetParent(w.Root, false);
            Box(s, "Stretcher frame", new Vector3(0, .78f, 0), new Vector3(.62f, .07f, 2.0f), metal);
            Box(s, "Stretcher mattress", new Vector3(0, .87f, 0), new Vector3(.58f, .1f, 1.9f), mattress);
            Box(s, "Stretcher pillow", new Vector3(0, .95f, .75f), new Vector3(.45f, .07f, .3f), white);
            foreach (int a in new[] { -1, 1 }) foreach (int b in new[] { -1, 1 })
                {
                    Box(s, "Stretcher leg", new Vector3(a * .25f, .45f, b * .75f), new Vector3(.04f, .65f, .04f), metal);
                    Part(s, "Stretcher wheel", sphere, new Vector3(a * .25f, .08f, b * .75f), Vector3.one * .14f, rubber);
                }
            Box(s, "IV pole", new Vector3(-.28f, 1.35f, .9f), new Vector3(.03f, 1.1f, .03f), metal);
            Box(s, "Oxygen bottle", new Vector3(.18f, .62f, -.5f), new Vector3(.14f, .14f, .5f), teal);
            w.FrontMedic = HumanVisual.Create(w.Root, Identity(6800 + walkers.Count, "medic"), true); w.FrontMedic.transform.localPosition = new Vector3(0, 0, 1.45f);
            w.BackMedic = HumanVisual.Create(w.Root, Identity(6850 + walkers.Count, "medic"), true); w.BackMedic.transform.localPosition = new Vector3(0, 0, -1.45f); w.BackMedic.Gesture = HumanGesture.Umbrella;
            w.Root.gameObject.SetActive(false); return w;
        }
        private void Stretcher() { stretcher = Team("Paramedic stretcher team"); }
        private void RoofTeam() { roofTeam = Team("Rooftop transfer team"); roofTeam.Speed = 1.2f; }
        private void Begin(Walker w, Vector3 at, Vector3[] path, System.Action<Walker> done)
        {
            w.Root.position = at; w.Path = path; w.I = 0; w.Wait = 0; w.Loop = false; w.Done = done; w.Hidden = 0;
            var dir = Flat(path[path.Length > 1 ? 1 : 0] - at); if (dir.sqrMagnitude > .01f) w.Root.rotation = Quaternion.LookRotation(dir);
            w.Root.gameObject.SetActive(true);
        }
        private void Board(Walker w, HumanIdentity id)
        {
            ClearBoard(w); var p = HumanVisual.Create(w.Root.Find("Stretcher"), id, false);
            p.transform.localRotation = Quaternion.Euler(-90, 180, 0); p.transform.localPosition = new Vector3(0, 1.0f, -id.Height * .5f + .05f); w.Patient = p;
        }
        private void ClearBoard(Walker w) { if (w.Patient != null) Destroy(w.Patient.gameObject); w.Patient = null; }

        private void UpdateRoofTransfer()
        {
            if (heli == null || roofTeam == null) return; var phase = heli.Phase;
            if (phase != lastHeliPhase && phase == "Landed / medical transfer" && Near(HospitalCampusLayout.Pad, 260))
            {
                float y = FloorY(6); var pad = HospitalCampusLayout.Pad;
                var path = new[] { pad + new Vector3(3.6f, 0, 0), H(8, 6, -2), H(-14, 6, -2), H(-26, 6, -1.5f), H(-30, 6, -5), H(-36, 6, -4.6f), H(-36, 6, -.5f) };
                Begin(roofTeam, path[0], path, w => { w.Root.gameObject.SetActive(false); ClearBoard(w); RooftopTransfers++; });
                Board(roofTeam, Identity(9100 + RooftopTransfers, "patient"));
            }
            lastHeliPhase = phase;
        }

        private void UpdateWalkers()
        {
            float dt = Time.deltaTime;
            foreach (var w in walkers)
            {
                if (!w.Root.gameObject.activeSelf)
                {
                    if (w.Hidden > 0 && Time.time >= w.Hidden && (w.Kind == "visitor" || w.Kind == "queue")) { w.Hidden = 0; if (w.Kind == "visitor" && w.Inside && w.Done != null) { var d = w.Done; w.Done = null; d(w); } w.Root.gameObject.SetActive(true); }
                    continue;
                }
                if (w.Path == null || w.Path.Length == 0) continue;
                if (w.Wait > 0)
                {
                    w.Wait -= dt;
                    if (w.Look.HasValue) { var f = Flat(w.Look.Value - w.Root.position); if (f.sqrMagnitude > .01f) w.Root.rotation = Quaternion.Slerp(w.Root.rotation, Quaternion.LookRotation(f), 1 - Mathf.Exp(-5 * dt)); if (w.Human != null) w.Human.LookTarget = w.Look; }
                    continue;
                }
                if (w.I >= w.Path.Length) continue;
                var target = w.Path[w.I]; var pos = w.Root.position; var delta = target - pos; if (!w.Snap) delta.y = target.y - pos.y; var flat = Flat(delta);
                if (flat.magnitude < .08f)
                {
                    float wait = w.Waits != null && w.I < w.Waits.Length ? w.Waits[w.I] : 0; w.Wait = wait;
                    w.Look = w.Looks != null && w.I < w.Looks.Length ? w.Looks[w.I] : null;
                    if (w.Human != null && w.Kind != "trolley" && w.Kind != "wheelchair") w.Human.Gesture = w.Gestures != null && w.I < w.Gestures.Length ? w.Gestures[w.I] : w.Moving;
                    w.I++;
                    if (w.I >= w.Path.Length) { if (w.Loop) w.I = 0; else { var d = w.Done; if (d != null) { w.Done = null; d(w); } } }
                    continue;
                }
                if (w.Human != null && w.Kind != "trolley" && w.Kind != "wheelchair" && w.Kind != "seated") { w.Human.Gesture = w.Moving; w.Human.LookTarget = null; }
                var step = flat.normalized * Mathf.Min(flat.magnitude, w.Speed * dt); var next = pos + step;
                if (w.Snap) next.y = Ground(next, pos.y); else next.y = Mathf.MoveTowards(pos.y, target.y, 3 * dt);
                w.Root.position = next; w.Root.rotation = Quaternion.Slerp(w.Root.rotation, Quaternion.LookRotation(flat), 1 - Mathf.Exp(-6 * dt));
                // Automatic doors open for people walking through them.
                if (mainDoor != null && (next - mainDoor.transform.position).sqrMagnitude < 25) mainDoor.HoldOpen(1.5f);
                if (erDoor != null && (next - erDoor.transform.position).sqrMagnitude < 36) erDoor.HoldOpen(1.5f);
            }
        }
        private readonly RaycastHit[] hits = new RaycastHit[12];
        private float Ground(Vector3 at, float current)
        {
            int n = Physics.RaycastNonAlloc(new Vector3(at.x, current + 2.2f, at.z), Vector3.down, hits, 6, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            float best = float.MinValue;
            for (int i = 0; i < n; i++) { var c = hits[i].collider; if (c is CharacterController || c.attachedRigidbody != null || c.transform.IsChildOf(root)) continue; if (hits[i].normal.y < .6f) continue; best = Mathf.Max(best, hits[i].point.y); }
            return best == float.MinValue ? current : Mathf.MoveTowards(current, best, 4 * Time.deltaTime + .02f);
        }

        // =====================================================================
        // Visibility (interior floors follow the campus rules)
        // =====================================================================
        private void Visibility()
        {
            var view = Camera.main; var me = owner != null && owner.IsWalking ? owner.transform.position : view != null ? view.transform.position : Vector3.zero;
            var d = me - HospitalCampusLayout.Centre; bool close = d.x * d.x + d.z * d.z < 220 * 220;
            insideBuilding = owner != null && owner.IsWalking && Mathf.Abs(d.x) < 46 && Mathf.Abs(d.z) < 25 && me.y < HospitalCampusLayout.Storey * 7;
            insideFloor = insideBuilding ? Mathf.Clamp(Mathf.RoundToInt((me.y - HospitalCampusLayout.Ground) / HospitalCampusLayout.Storey), 0, HospitalCampusLayout.Floors) : -1;
            foreach (var w in walkers)
            {
                if (w.Floor == 9 || w.Floor < 0) continue;
                bool show = close && (insideBuilding ? Mathf.Abs(w.Floor - insideFloor) <= 1 : w.Floor == 0);
                if (w.Kind == "queue" && w.Hidden > Time.time) show = false;
                if (w.Root.gameObject.activeSelf != show) w.Root.gameObject.SetActive(show);
            }
            bool icu = insideBuilding && Mathf.Abs(insideFloor - 4) <= 1;
            foreach (var m in monitors) if (m.activeSelf != icu) m.SetActive(icu);
            if (beep != null) { if (icu && !beep.isPlaying) beep.Play(); else if (!icu && beep.isPlaying) beep.Stop(); }
        }

        // =====================================================================
        // Signage, monitors, boards, audio
        // =====================================================================
        private void Signage()
        {
            var s = new GameObject("Emergency signage").transform; s.SetParent(root, false);
            // Glowing EMERGENCY sign on the ER porte-cochère roof, facing the road (south) and the approach (east).
            foreach (var face in new[] { (pos: new Vector3(655, 5.05f, -456.75f), yaw: 0f), (pos: new Vector3(662.75f, 5.05f, -451), yaw: -90f) })
            {
                var g = new GameObject("ER roof sign").transform; g.SetParent(s, false); g.SetPositionAndRotation(face.pos, Quaternion.Euler(0, face.yaw, 0));
                Box(g, "Sign panel", new Vector3(0, 0, .12f), new Vector3(9.4f, 1.6f, .2f), white);
                glowRed.Add(Box(g, "Sign red edge top", new Vector3(0, .82f, .0f), new Vector3(9.4f, .06f, .05f), glowRedMat));
                glowRed.Add(Box(g, "Sign red edge bottom", new Vector3(0, -.82f, 0f), new Vector3(9.4f, .06f, .05f), glowRedMat));
                glowRed.Add(Box(g, "Sign cross", new Vector3(-3.9f, 0, 0), new Vector3(.38f, 1.2f, .06f), glowRedMat));
                glowRed.Add(Box(g, "Sign cross", new Vector3(-3.9f, 0, 0), new Vector3(1.2f, .38f, .06f), glowRedMat));
                foreach (int x in new[] { -4, 4 }) Box(g, "Sign post", new Vector3(x, -.8f, .15f), new Vector3(.12f, .9f, .12f), metal);
                glowText.Add(Text(g, "EMERGENCY", new Vector3(.6f, .05f, 0), 0, new Vector2(7.2f, 1.25f), new Color(1, .12f, .08f), 12));
            }
            // Pylon at the road corner, pointing ambulances and visitors to the ER.
            var pylon = new GameObject("ER direction pylon").transform; pylon.SetParent(s, false); pylon.SetPositionAndRotation(new Vector3(694.6f, .1f, -465.2f), Quaternion.Euler(0, -30, 0));
            Box(pylon, "Pylon body", new Vector3(0, 2.4f, 0), new Vector3(1.6f, 4.8f, .45f), dark);
            glowRed.Add(Box(pylon, "Pylon cross", new Vector3(0, 4.1f, -.24f), new Vector3(.3f, .95f, .03f), glowRedMat));
            glowRed.Add(Box(pylon, "Pylon cross", new Vector3(0, 4.1f, -.24f), new Vector3(.95f, .3f, .03f), glowRedMat));
            glowText.Add(Text(pylon, "EMERGENCY", new Vector3(0, 3.1f, -.24f), 0, new Vector2(1.45f, .4f), new Color(1, .15f, .1f), 4));
            Text(pylon, "AMBULANCE BAY\n24 HOURS", new Vector3(0, 2.3f, -.24f), 0, new Vector2(1.4f, .6f), Color.white, 3);
            Text(pylon, "< MAIN ENTRANCE\n< VISITOR PARKING", new Vector3(0, 1.3f, -.24f), 0, new Vector2(1.4f, .6f), new Color(.6f, .9f, 1), 3);
            // A large red cross high on the east façade, seen from the highway.
            var cross = new GameObject("East facade medical cross").transform; cross.SetParent(s, false); cross.SetPositionAndRotation(HospitalCampusLayout.Point(45.05f, 23.5f, -10), Quaternion.Euler(0, -90, 0));
            Box(cross, "Cross backing", new Vector3(0, 0, .08f), new Vector3(5, 5, .1f), white);
            glowRed.Add(Box(cross, "Cross", Vector3.zero, new Vector3(1.3f, 4.2f, .1f), glowRedMat)); glowRed.Add(Box(cross, "Cross", Vector3.zero, new Vector3(4.2f, 1.3f, .1f), glowRedMat));
            // Aviation warning lights on the roof crowns.
            float roof = FloorY(6);
            foreach (var p in new[] { HospitalCampusLayout.Point(43, roof + 3.9f, -22), HospitalCampusLayout.Point(43, roof + 3.9f, 22), HospitalCampusLayout.Point(-43.5f, roof + 3.9f, 22), HospitalCampusLayout.Point(-43.5f, roof + 1.4f, -22) })
            { var r = Part(s, "Aviation warning light", sphere, Vector3.zero, Vector3.one * .45f, beaconOff); r.transform.position = p; beacons.Add(r); }
        }
        public Transform AirAmbulanceModel { get; private set; }
        /// <summary>The rooftop air ambulance gets the realistic helicopter model (its flight animation is unchanged).</summary>
        private void AirAmbulance()
        {
            var heliLife = campus.GetComponentInChildren<HospitalHelicopter>(true); if (heliLife == null) return;
            var rotor = heliLife.transform.Find("Animated main rotor"); var tail = heliLife.transform.Find("Animated tail rotor");
            AircraftArt.HideOld(heliLife.transform, null);
            AirAmbulanceModel = AircraftArt.BuildHelicopter(heliLife.transform, AirlineLivery.Rescue, rotor, tail).Root;
        }
        private void Monitors()
        {
            var parent = new GameObject("ICU heart monitors").transform; parent.SetParent(root, false);
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad); var mesh = quad.GetComponent<MeshFilter>().sharedMesh; DestroyImmediate(quad);
            for (int i = 0; i < 7; i++)
                foreach (float bx in new[] { -3.3f, 3.3f })
                {
                    float x = i < 4 ? -23 + i * 14 : -23 + (i - 4) * 14; int side = i < 4 ? 1 : -1;
                    var go = new GameObject("ICU monitor"); go.transform.SetParent(parent, false);
                    go.transform.position = H(x + bx + 1.25f, 4, side * 13 + 1.5f - .695f) + Vector3.up * 1.7f;
                    var screen = Part(go.transform, "ECG screen", mesh, new Vector3(0, .04f, 0), new Vector3(.72f, .3f, 1), ecg); screen.shadowCastingMode = ShadowCastingMode.Off;
                    var t = Text(go.transform, "HR 72   SpO2 98%", new Vector3(0, -.15f, -.002f), 0, new Vector2(.7f, .1f), new Color(.3f, 1, .5f), 1); signTexts.RemoveAt(signTexts.Count - 1);
                    monitorText.Add(t); monitors.Add(go); go.SetActive(false);
                }
        }
        private void LobbyBoard()
        {
            var g = new GameObject("Reception now-serving board").transform; g.SetParent(root, false); g.position = H(0, 0, 3.85f) + Vector3.up * 2.35f;
            Box(g, "Board", new Vector3(0, 0, .06f), new Vector3(6.2f, .9f, .1f), dark);
            boardText = Text(g, "NOW SERVING   A-0" + token + "   >  COUNTER 2", Vector3.zero, 0, new Vector2(5.9f, .55f), new Color(1, .7f, .15f), 5);
            glowText.Add(boardText);
        }
        private void Audio()
        {
            var go = new GameObject("ICU monitor beeps"); go.transform.SetParent(root, false); go.transform.position = H(0, 4, 8) + Vector3.up * 2;
            beep = go.AddComponent<AudioSource>(); beep.clip = BeepClip(); beep.loop = true; beep.spatialBlend = 1; beep.minDistance = 4; beep.maxDistance = 32; beep.volume = .35f; beep.playOnAwake = false;
            var pa = new GameObject("Hospital PA chime"); pa.transform.SetParent(root, false);
            chime = pa.AddComponent<AudioSource>(); chime.clip = ChimeClip(); chime.spatialBlend = 0; chime.volume = .45f; chime.playOnAwake = false;
        }
        private AudioClip SirenClip()
        {
            // Two-tone wail: 700–1200 Hz over 3.2 s (exactly 3040 cycles, so the loop is seamless).
            int rate = 44100; float period = 3.2f; int n = (int)(rate * period); var data = new float[n]; double phase = 0;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)rate; float f = 700 + 500 * (.5f + .5f * Mathf.Sin(2 * Mathf.PI * t / period - Mathf.PI / 2));
                phase += 2 * System.Math.PI * f / rate; float s = (float)System.Math.Sin(phase);
                data[i] = .55f * s + .25f * (float)System.Math.Sin(phase * 3) + .12f * (float)System.Math.Sin(phase * 5);
            }
            var clip = AudioClip.Create("Ambulance siren", n, 1, rate, false); clip.SetData(data, 0); return clip;
        }
        private AudioClip BeepClip()
        {
            int rate = 44100, n = (int)(rate * .84f); var data = new float[n];
            for (int i = 0; i < n; i++) { float t = i / (float)rate; if (t < .11f) data[i] = Mathf.Sin(2 * Mathf.PI * 960 * t) * Mathf.Min(1, t * 200) * Mathf.Min(1, (.11f - t) * 200) * .5f; }
            var clip = AudioClip.Create("ICU beep", n, 1, rate, false); clip.SetData(data, 0); return clip;
        }
        private AudioClip ChimeClip()
        {
            int rate = 44100, n = (int)(rate * 1.3f); var data = new float[n];
            for (int i = 0; i < n; i++) { float t = i / (float)rate; float a = t < .55f ? Mathf.Sin(2 * Mathf.PI * 784 * t) * Mathf.Exp(-t * 4) : 0; float b = t >= .5f ? Mathf.Sin(2 * Mathf.PI * 622 * (t - .5f)) * Mathf.Exp(-(t - .5f) * 3.5f) : 0; data[i] = (a + b) * .5f; }
            var clip = AudioClip.Create("PA chime", n, 1, rate, false); clip.SetData(data, 0); return clip;
        }
        private Texture2D EcgTexture()
        {
            const int w = 256, h = 64; var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear, name = "ECG trace" };
            var px = new Color32[w * h]; var bg = new Color32(4, 14, 10, 255); var grid = new Color32(10, 40, 26, 255); var line = new Color32(70, 255, 120, 255);
            for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) px[y * w + x] = (x % 16 == 0 || y % 16 == 0) ? grid : bg;
            float Wave(float u)
            {
                u = Mathf.Repeat(u, 1); float v = .38f;
                v += .07f * Mathf.Exp(-Mathf.Pow((u - .14f) / .025f, 2));
                v -= .06f * Mathf.Exp(-Mathf.Pow((u - .27f) / .008f, 2)); v += .5f * Mathf.Exp(-Mathf.Pow((u - .3f) / .009f, 2)); v -= .14f * Mathf.Exp(-Mathf.Pow((u - .33f) / .009f, 2));
                v += .12f * Mathf.Exp(-Mathf.Pow((u - .55f) / .045f, 2)); return v;
            }
            int prev = (int)(Wave(0) * h);
            for (int x = 0; x < w; x++)
            {
                int y = Mathf.Clamp((int)(Wave(x / (float)w * 2) * h), 1, h - 2); int a = Mathf.Min(prev, y), b = Mathf.Max(prev, y);
                for (int k = a - 1; k <= b + 1; k++) if (k >= 0 && k < h) px[k * w + x] = line; prev = y;
            }
            tex.SetPixels32(px); tex.Apply(); return tex;
        }

        // =====================================================================
        // Frame update
        // =====================================================================
        private void Update()
        {
            if (!Ready) return;
            if (owner == null) owner = motel.GetComponentInChildren<OwnerSandboxController>(true);
            if (Time.unscaledTime >= visibilityAt) { visibilityAt = Time.unscaledTime + .35f; Visibility(); }
            UpdateDispatch(); UpdateMission(); Discharges(); UpdateRoofTransfer(); UpdateWalkers();
            FlashBeacons(ai); FlashBeacons(player);
            if (Time.time >= nextQueue) { nextQueue = Time.time + Random.Range(7f, 11f); AdvanceQueue(); }
            if (Time.time >= nextGesture)
            {
                nextGesture = Time.time + 9; int k = 0;
                foreach (var w in walkers) if (w.Kind == "seated") { k++; if ((k + (int)Time.time) % 4 == 0) w.Human.Gesture = (HumanGesture)new[] { HumanGesture.Phone, HumanGesture.Relaxed, HumanGesture.Talk, HumanGesture.LookAround }[(k + (int)(Time.time / 9)) % 4]; }
            }
            // Signs glow brighter after dark; aviation lights blink.
            float night = Night(); float pulse = .85f + .15f * Mathf.Sin(Time.time * 2.4f);
            glowRedMat.SetColor("_EmissionColor", new Color(1, .08f, .06f) * Mathf.Lerp(1.6f, 6f, night) * pulse);
            foreach (var t in glowText) if (t != null && t.fontMaterial.HasProperty("_FaceColor")) t.fontMaterial.SetColor("_FaceColor", Color.white * Mathf.Lerp(1f, 3.2f, night));
            bool blink = (Time.time % 1.6f) < .25f; foreach (var b in beacons) b.sharedMaterial = blink ? beaconMat : beaconOff;
            if (ecg != null && monitors.Count > 0 && monitors[0].activeSelf) ecg.mainTextureOffset = new Vector2(Time.time * .45f, 0);
            if (Time.time >= nextHeart && monitorText.Count > 0 && monitors[0].activeSelf)
            {
                nextHeart = Time.time + 2.5f;
                for (int i = 0; i < monitorText.Count; i++) monitorText[i].text = "HR " + (64 + (i * 7 + (int)Time.time) % 28) + "   SpO2 " + (95 + (i + (int)Time.time / 3) % 5) + "%";
            }
            if (Time.time >= nextAnnouncement) { nextAnnouncement = Time.time + Random.Range(35f, 55f); if (insideBuilding || Near(HospitalCampusLayout.Centre, 75)) Page(Pages[Random.Range(0, Pages.Length)]); }
        }
        private float Night() { float h = clock == null ? 12 : clock.Hour; return h < 5.5f || h > 19.5f ? 1 : h < 7 ? Mathf.InverseLerp(7, 5.5f, h) : h > 18 ? Mathf.InverseLerp(18, 19.5f, h) : 0; }
        private void Say(string text) { toast = text; toastUntil = Time.unscaledTime + 5.5f; }
        private void Page(string text) { announcement = text; announceUntil = Time.unscaledTime + 7; if (chime != null) chime.Play(); }
        private static Vector3 Flat(Vector3 v) { v.y = 0; return v; }
        private static bool Near(Vector3 p, float range) { var c = Camera.main; return c != null && (c.transform.position - p).sqrMagnitude < range * range; }

        // =====================================================================
        // HUD
        // =====================================================================
        private GUIStyle box, line;
        private void OnGUI()
        {
            if (!Ready) return;
            if (box == null)
            {
                box = new GUIStyle(GUI.skin.box) { fontSize = 16, alignment = TextAnchor.MiddleCenter, wordWrap = true, richText = true }; box.normal.textColor = Color.white;
                line = new GUIStyle(GUI.skin.label) { fontSize = 15, alignment = TextAnchor.MiddleCenter, wordWrap = true, richText = true, fontStyle = FontStyle.Italic }; line.normal.textColor = new Color(.85f, .97f, 1);
            }
            float cx = Screen.width * .5f;
            if (MissionState != Mission.None && player != null)
            {
                float d = Flat(MissionTarget - player.T.position).magnitude; string where = Direction(MissionTarget);
                string text = MissionState == Mission.Loading ? "<color=#ffcc33>LOADING PATIENT...</color>  " + Mathf.RoundToInt(100 * (1 - (loadingUntil - Time.time) / 3)) + "%"
                    : MissionState == Mission.ToPatient ? "<color=#ff4444>●</color> AMBULANCE CALL  |  Patient " + Mathf.RoundToInt(d) + " m " + where + "  |  " + Clock(Time.time - missionStarted)
                    : "<color=#ff4444>●</color> PATIENT ON BOARD  |  Haven Grand ER " + Mathf.RoundToInt(d) + " m " + where + "  |  " + Clock(Time.time - missionStarted);
                GUI.Box(new Rect(cx - 320, 82, 640, 32), text, box);
                if (DrivingAmbulance) GUI.Label(new Rect(cx - 320, 116, 640, 20), "H: siren on/off   |   stop next to the beacon to load the patient", line);
            }
            else if (owner != null && owner.IsWalking && player != null && (owner.transform.position - player.T.position).sqrMagnitude < 7 * 7)
                GUI.Box(new Rect(cx - 300, Screen.height - 200, 600, 34), "E: drive Haven Ambulance 2 - answer emergency calls for coins", box);
            if (Announcement != null) GUI.Label(new Rect(cx - 420, Screen.height - 236, 840, 30), "PA:  \"" + Announcement + "\"", line);
            if (Toast != null) GUI.Box(new Rect(cx - 330, Screen.height - 270, 660, 32), Toast, box);
        }
        private static string Clock(float s) => ((int)s / 60).ToString("00") + ":" + ((int)s % 60).ToString("00");
        private static string Direction(Vector3 target)
        {
            var cam = Camera.main; if (cam == null) return "";
            var f = Flat(cam.transform.forward); var to = Flat(target - cam.transform.position); if (f.sqrMagnitude < .01f || to.sqrMagnitude < .01f) return "";
            float a = Vector3.SignedAngle(f, to, Vector3.up);
            return Mathf.Abs(a) < 25 ? "ahead" : Mathf.Abs(a) > 150 ? "behind" : a > 0 ? (a < 70 ? "ahead-right" : "right") : (a > -70 ? "ahead-left" : "left");
        }

        private void OnDestroy()
        {
            if (ai != null) TownVehicleAgent.RemoveSiren(ai.T); if (player != null) TownVehicleAgent.RemoveSiren(player.T);
            foreach (var who in new[] { patient, missionPatient }) if (who != null) who.Recover(who.transform.position);
            foreach (var d in discharge) if (d.who != null) d.who.Recover(d.who.transform.position);
            foreach (var m in owned) if (m != null) Destroy(m);
            foreach (var m in ownedMeshes) if (m != null) Destroy(m);
            if (ecg != null && ecg.mainTexture != null) Destroy(ecg.mainTexture);
            foreach (var t in ownedTextures) if (t != null) Destroy(t);
            foreach (var s in stripped) if (s.filter != null) { s.filter.sharedMesh = s.original; Destroy(s.copy); }
            foreach (var g in hiddenOld) if (g != null) g.SetActive(true);
            if (root != null) Destroy(root.gameObject);
        }
    }
}
