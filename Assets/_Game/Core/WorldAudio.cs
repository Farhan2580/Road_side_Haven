using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace RoadsideHaven
{
    /// <summary>
    /// Whole-game soundscape from procedurally synthesised clips: player and traffic engines,
    /// tyre skids, impacts and horn (N), footsteps by surface, day/night/city/wind ambience,
    /// positional water, sliding doors and UI clicks.
    /// </summary>
    public sealed class WorldAudio : MonoBehaviour
    {
        private const int TrafficVoices = 5, WaterVoices = 4, OneShotVoices = 8;
        public enum Zone { City, Park, Farm, Stadium, Beach, Suburb, Airport }
        /// <summary>1 outdoors, lower indoors; other systems (rain) scale outdoor sound by it.</summary>
        public static float OutdoorLevel { get; private set; } = 1f;
        public static WorldAudio Active { get; private set; }
        public bool Indoors { get; private set; }
        public int ZoneCount => zones.Count;
        public float ZoneWeight(Zone kind) => zoneWeight[(int)kind];
        public int AmbientEventCount { get; private set; }
        public int FootstepCount { get; private set; }
        public int DoorSoundCount { get; private set; }
        public bool EnginePlaying => engine != null && engine.isPlaying;
        public float BirdVolume => birds == null ? 0 : birds.volume;
        public IEnumerable<AudioClip> AllClips { get { foreach (var c in clips) yield return c; } }

        private readonly List<AudioClip> clips = new List<AudioClip>();
        private AudioClip engineClip, skidClip, impactClip, hornClip, doorClip, clickClip, splashClip;
        private AudioClip[] grassSteps, hardSteps;
        private GameObject root;
        private AudioSource engine, skid, horn, birds, crickets, city, wind, footsteps;
        private readonly AudioSource[] traffic = new AudioSource[TrafficVoices];
        private readonly Transform[] trafficTargets = new Transform[TrafficVoices];
        private readonly Vector3[] trafficLast = new Vector3[TrafficVoices];
        private readonly AudioSource[] water = new AudioSource[WaterVoices];
        private readonly AudioSource[] oneShots = new AudioSource[OneShotVoices];
        private int nextOneShot;
        private OwnerSandboxController owner;
        private WorldDrivingController driving;
        private MotelAtmosphere atmosphere;
        private readonly List<Rect> roads = new List<Rect>();
        private readonly List<Renderer> waterBodies = new List<Renderer>();
        private DistrictSlidingDoor[] doors = new DistrictSlidingDoor[0];
        private float[] doorLast = new float[0];
        private readonly HashSet<Button> wiredButtons = new HashSet<Button>();
        private Transform attachedVehicle;
        private Vector3 lastOwner, lastVehicleVelocity;
        private float stepTimer, nextScan, nextButtons, previousSpeed, nextEvent = 4, nextIndoorCheck;
        private readonly List<(Rect area, Zone kind)> zones = new List<(Rect, Zone)>();
        private readonly float[] zoneWeight = new float[7];
        private AudioSource waves, crowd, jets, music, room;
        private AudioLowPassFilter[] muffles;
        private AudioClip[] gulls, barks; private AudioClip moo, rooster, cheer, whistle, siren, sparks;
        public AudioClip SparksClip => sparks;
        public AudioClip BarkClip(int i) => barks[Mathf.Abs(i) % barks.Length];

        private void Start()
        {
            owner = GetComponentInChildren<OwnerSandboxController>(true);
            driving = GetComponentInChildren<WorldDrivingController>(true);
            atmosphere = GetComponent<MotelAtmosphere>();
            if (Object.FindFirstObjectByType<AudioListener>() == null && Camera.main != null) Camera.main.gameObject.AddComponent<AudioListener>();

            engineClip = Keep(ProceduralAudio.Engine(1)); var trafficClip = Keep(ProceduralAudio.Engine(4));
            skidClip = Keep(ProceduralAudio.Skid()); impactClip = Keep(ProceduralAudio.Impact()); hornClip = Keep(ProceduralAudio.Horn());
            doorClip = Keep(ProceduralAudio.DoorSwoosh()); clickClip = Keep(ProceduralAudio.Click()); splashClip = Keep(ProceduralAudio.Splash());
            grassSteps = new AudioClip[4]; hardSteps = new AudioClip[4];
            for (int i = 0; i < 4; i++) { grassSteps[i] = Keep(ProceduralAudio.Footstep(true, i)); hardSteps[i] = Keep(ProceduralAudio.Footstep(false, i)); }

            root = new GameObject("World Audio");
            SceneManager.MoveGameObjectToScene(root, gameObject.scene);
            engine = Source("Player engine", engineClip, true, 1f, .5f);
            skid = Source("Tyre skid", skidClip, true, 0, .5f);
            horn = Source("Horn", hornClip, true, 0, .5f);
            footsteps = Source("Footsteps", null, false, .55f, .4f);
            birds = Source("Birds", Keep(ProceduralAudio.Birds()), true, 0, 0);
            crickets = Source("Crickets", Keep(ProceduralAudio.Crickets()), true, 0, 0);
            city = Source("City hum", Keep(ProceduralAudio.CityHum()), true, 0, 0);
            wind = Source("Wind", Keep(ProceduralAudio.Wind()), true, 0, 0);
            waves = Source("Sea waves", Keep(ProceduralAudio.Waves()), true, 0, 0); crowd = Source("Stadium crowd", Keep(ProceduralAudio.Crowd()), true, 0, 0);
            jets = Source("Airport jets", Keep(ProceduralAudio.JetRumble()), true, 0, 0); music = Source("Shop music", Keep(ProceduralAudio.ShopMusic()), true, 0, 0); room = Source("Room tone", Keep(ProceduralAudio.RoomTone()), true, 0, 0);
            foreach (var bed in new[] { birds, crickets, city, wind, waves, crowd, jets, music, room }) bed.Play();
            muffles = new[] { birds, crickets, city, wind, waves, crowd, jets }.Select(b => { var m = b.gameObject.AddComponent<AudioLowPassFilter>(); m.cutoffFrequency = 22000; return m; }).ToArray();
            gulls = new[] { Keep(ProceduralAudio.Gull(0)), Keep(ProceduralAudio.Gull(1)) }; barks = new[] { Keep(ProceduralAudio.Bark(0)), Keep(ProceduralAudio.Bark(1)), Keep(ProceduralAudio.Bark(2)) };
            moo = Keep(ProceduralAudio.Moo()); rooster = Keep(ProceduralAudio.Rooster()); cheer = Keep(ProceduralAudio.Cheer()); whistle = Keep(ProceduralAudio.Whistle()); siren = Keep(ProceduralAudio.Siren()); sparks = Keep(ProceduralAudio.Sparks());
            BuildZones();
            var waterClip = Keep(ProceduralAudio.Water());
            for (int i = 0; i < WaterVoices; i++) { water[i] = Source("Water " + i, waterClip, true, 0, 1); water[i].minDistance = 3; water[i].maxDistance = 45; }
            for (int i = 0; i < TrafficVoices; i++) { traffic[i] = Source("Traffic engine " + i, trafficClip, true, 0, 1); traffic[i].minDistance = 4; traffic[i].maxDistance = 55; traffic[i].pitch = .85f + i * .07f; }
            for (int i = 0; i < OneShotVoices; i++) { oneShots[i] = Source("One shot " + i, null, false, 1, 1); oneShots[i].minDistance = 2; oneShots[i].maxDistance = 35; }

            foreach (var r in GetComponentsInChildren<MeshRenderer>())
            {
                if (TownPopulationController.RoadName(r.name)) roads.Add(Rect.MinMaxRect(r.bounds.min.x, r.bounds.min.z, r.bounds.max.x, r.bounds.max.z));
                string n = r.name.ToLowerInvariant(), m = r.sharedMaterial == null ? "" : r.sharedMaterial.name.ToLowerInvariant();
                if ((n.Contains("water") || n.Contains("fountain") || n.Contains("lake") || n.Contains("river") || m.Contains("water")) && !n.Contains("bottle")) waterBodies.Add(r);
            }
            if (owner != null) lastOwner = owner.transform.position;
        }

        private AudioClip Keep(AudioClip clip) { clips.Add(clip); return clip; }
        private void Awake() { Active = this; }

        private void BuildZones()
        {
            foreach (Transform district in transform)
            {
                string n = district.name; Zone? kind = null;
                if (n.Contains("Downtown") || n.StartsWith("Village City World") || n.StartsWith("Grand Boulevard") || n.StartsWith("Meridian") || n.StartsWith("Main Road Axis") || n.StartsWith("Aster") || n.StartsWith("Coastal City") || n.StartsWith("Expanded District")) kind = Zone.City;
                else if (n.StartsWith("Lakeside") || n.Contains("Commons") || n.StartsWith("Palm Courtyard") || n.StartsWith("Residential Transit")) kind = Zone.Park;
                else if (n.Contains("Farm")) kind = Zone.Farm;
                else if (n.Contains("Sports")) kind = Zone.Stadium;
                else if (n.Contains("Airport")) kind = Zone.Airport;
                else if (n.StartsWith("Palm Grove")) kind = Zone.Suburb;
                if (n.StartsWith("Heritage Residential")) { foreach (Transform hood in district) AddZone(hood, Zone.Suburb, 4000); continue; }
                if (kind != null) AddZone(district, kind.Value, 900000);
            }
            foreach (var r in GetComponentsInChildren<MeshRenderer>()) if (r.name == "Beach sand") zones.Add((Rect.MinMaxRect(r.bounds.min.x, r.bounds.min.z, r.bounds.max.x, r.bounds.max.z), Zone.Beach));
        }

        private void AddZone(Transform t, Zone kind, float maxArea)
        {
            var rs = t.GetComponentsInChildren<Renderer>(); if (rs.Length < 3) return;
            var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
            if (b.size.x * b.size.z > maxArea * 10) return;
            zones.Add((Rect.MinMaxRect(b.min.x, b.min.z, b.max.x, b.max.z), kind));
        }

        /// <summary>Plays a positional one-shot through the shared voice pool.</summary>
        public void PlayWorld(AudioClip clip, Vector3 position, float volume, float pitch = 1) { if (clip != null) PlayAt(clip, position, volume, pitch); }

        private AudioSource Source(string name, AudioClip clip, bool loop, float volume, float spatial)
        {
            var go = new GameObject(name); go.transform.SetParent(root.transform, false);
            var s = go.AddComponent<AudioSource>();
            s.clip = clip; s.loop = loop; s.volume = volume; s.spatialBlend = spatial; s.playOnAwake = false;
            s.rolloffMode = AudioRolloffMode.Linear; s.dopplerLevel = spatial > .5f ? .4f : 0;
            return s;
        }

        private void PlayAt(AudioClip clip, Vector3 position, float volume, float pitch = 1)
        {
            var s = oneShots[nextOneShot]; nextOneShot = (nextOneShot + 1) % OneShotVoices;
            s.transform.position = position; s.pitch = pitch; s.PlayOneShot(clip, volume);
        }

        private void Update()
        {
            var listener = Camera.main != null ? Camera.main.transform.position : transform.position;
            float day = atmosphere == null ? 1 : atmosphere.Daylight;
            var weather = WeatherSystem.Active;
            float rain = weather == null ? 0 : weather.Rain, gale = weather == null ? .3f : weather.Wind;
            UpdateVehicle();
            UpdateFootsteps(rain);
            UpdateAmbience(listener, day, rain, gale);
            if (Time.unscaledTime >= nextScan) { nextScan = Time.unscaledTime + .5f; AssignTraffic(listener); AssignWater(listener); }
            UpdateTraffic();
            UpdateDoors();
            if (Time.unscaledTime >= nextButtons) { nextButtons = Time.unscaledTime + 2f; WireButtons(); }
        }

        // ---------- Player vehicle ----------

        private void UpdateVehicle()
        {
            var vehicle = driving != null && driving.IsDriving ? driving.ActiveVehicleTransform : null;
            if (vehicle != attachedVehicle)
            {
                attachedVehicle = vehicle;
                if (vehicle != null && vehicle.GetComponent<VehicleImpactSound>() == null) vehicle.gameObject.AddComponent<VehicleImpactSound>().Configure(this);
                if (vehicle == null) { engine.Stop(); skid.Stop(); horn.Stop(); }
                else engine.Play();
            }
            if (vehicle == null) return;
            var body = vehicle.GetComponent<Rigidbody>();
            foreach (var s in new[] { engine, skid, horn }) s.transform.position = vehicle.position;
            float kph = driving.SpeedKph;
            float accel = (kph - previousSpeed) / Mathf.Max(Time.deltaTime, 1e-3f); previousSpeed = kph;
            // Five-speed gearbox feel: revs climb within each gear then drop on the shift.
            float[] shifts = { 0, 28, 58, 92, 128, 999 };
            int gear = 0; while (gear < 4 && kph > shifts[gear + 1]) gear++;
            float revs = Mathf.InverseLerp(shifts[gear], shifts[gear + 1], kph);
            engine.pitch = Mathf.Lerp(engine.pitch, (gear == 0 ? .75f : .9f) + revs * .85f + gear * .05f, Time.deltaTime * 8f);
            engine.volume = Mathf.Lerp(engine.volume, Mathf.Clamp(.32f + Mathf.Max(0, accel) * .03f + kph * .002f, .3f, .85f), Time.deltaTime * 6f);
            if (body != null)
            {
                var v = body.linearVelocity; float lateral = Mathf.Abs(Vector3.Dot(v, vehicle.right));
                float braking = Mathf.Max(0, -accel / 3.6f);
                float target = kph > 22 ? Mathf.Clamp01((lateral - 3.5f) / 5f + (braking - 7f) / 8f) : 0;
                skid.volume = Mathf.Lerp(skid.volume, target * .6f, Time.deltaTime * 10f);
                if (skid.volume > .02f && !skid.isPlaying) skid.Play(); else if (skid.volume <= .02f && skid.isPlaying) skid.Stop();
            }
            var keyboard = Keyboard.current;
            bool honk = keyboard != null && keyboard.nKey.isPressed;
            horn.volume = honk ? .7f : 0;
            if (honk && !horn.isPlaying) horn.Play(); else if (!honk && horn.isPlaying) horn.Stop();
        }

        public void Impact(Vector3 point, float speed)
        {
            if (speed < 2.5f) return;
            if (speed > 6f) PlayAt(sparks, point, Mathf.Clamp01(speed / 20f), Random.Range(.9f, 1.2f));
            PlayAt(impactClip, point, Mathf.Clamp01(speed / 14f), Random.Range(.85f, 1.1f));
        }

        // ---------- Footsteps ----------

        private void UpdateFootsteps(float rain)
        {
            if (owner == null || !owner.IsWalking) { if (owner != null) lastOwner = owner.transform.position; return; }
            Vector3 p = owner.transform.position;
            Vector3 delta = p - lastOwner; delta.y = 0; lastOwner = p;
            float speed = delta.magnitude / Mathf.Max(Time.deltaTime, 1e-3f);
            if (speed < .6f || speed > 20f) { stepTimer = Mathf.Min(stepTimer, .1f); return; }
            stepTimer -= Time.deltaTime;
            if (stepTimer > 0) return;
            stepTimer = Mathf.Clamp(.62f - speed * .055f, .26f, .55f);
            bool grass = false;
            if (Physics.Raycast(p + Vector3.up * .5f, Vector3.down, out var hit, 1.5f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                string n = hit.collider.name.ToLowerInvariant();
                grass = GroundSurfaceStyle.SurfaceFor(hit.collider.name) == 0 || n.Contains("grass") || n.Contains("lawn") || n.Contains("ground") || n.Contains("foundation") || n.Contains("field");
            }
            var set = grass ? grassSteps : hardSteps;
            footsteps.transform.position = p;
            footsteps.pitch = Random.Range(.9f, 1.12f);
            footsteps.PlayOneShot(set[Random.Range(0, set.Length)], grass ? .5f : .6f);
            if (rain > .3f && !grass) footsteps.PlayOneShot(splashClip, .18f * rain);
            FootstepCount++;
        }

        // ---------- Ambience ----------

        private void UpdateAmbience(Vector3 listener, float day, float rain, float gale)
        {
            float nearestRoad = 999;
            var xz = new Vector2(listener.x, listener.z);
            foreach (var r in roads)
            {
                float dx = Mathf.Max(r.xMin - xz.x, 0, xz.x - r.xMax), dz = Mathf.Max(r.yMin - xz.y, 0, xz.y - r.yMax);
                nearestRoad = Mathf.Min(nearestRoad, Mathf.Sqrt(dx * dx + dz * dz));
            }
            for (int i = 0; i < zoneWeight.Length; i++) zoneWeight[i] = 0;
            foreach (var z in zones)
            {
                float dx = Mathf.Max(z.area.xMin - xz.x, 0, xz.x - z.area.xMax), dz = Mathf.Max(z.area.yMin - xz.y, 0, xz.y - z.area.yMax);
                float w = 1 - Mathf.Clamp01(Mathf.Sqrt(dx * dx + dz * dz) / 50f);
                zoneWeight[(int)z.kind] = Mathf.Max(zoneWeight[(int)z.kind], w);
            }
            if (Time.unscaledTime >= nextIndoorCheck) { nextIndoorCheck = Time.unscaledTime + .25f; Indoors = CheckIndoors(); }
            float outdoor = Indoors ? .35f : 1f; OutdoorLevel = Mathf.MoveTowards(OutdoorLevel, outdoor, Time.deltaTime * 2);
            foreach (var m in muffles) if (m != null) m.cutoffFrequency = Mathf.Lerp(m.cutoffFrequency, Indoors ? 900 : 22000, Time.deltaTime * 4);

            float city = Mathf.Max(zoneWeight[(int)Zone.City], Mathf.Clamp01(1 - nearestRoad / 90f) * .6f);
            float green = Mathf.Max(zoneWeight[(int)Zone.Park], Mathf.Max(zoneWeight[(int)Zone.Farm] * .9f, zoneWeight[(int)Zone.Suburb] * .85f));
            float daytime = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.25f, .7f, day)), night = 1 - Mathf.InverseLerp(.1f, .45f, day), dry = 1 - rain;
            float k = Time.deltaTime * 1.2f;
            birds.volume = Mathf.Lerp(birds.volume, daytime * dry * (1 - Mathf.Clamp01(gale - .7f)) * (.06f + green * .34f) * (1 - zoneWeight[(int)Zone.Beach] * .7f) * OutdoorLevel, k);
            crickets.volume = Mathf.Lerp(crickets.volume, night * dry * (.05f + green * .25f) * (1 - city * .6f) * OutdoorLevel, k);
            city = city * Mathf.Lerp(.1f, .3f, day) * (1 - green * .5f);
            this.city.volume = Mathf.Lerp(this.city.volume, city * OutdoorLevel, k);
            wind.volume = Mathf.Lerp(wind.volume, (.03f + gale * gale * .3f + zoneWeight[(int)Zone.Beach] * .08f) * OutdoorLevel, k);
            waves.volume = Mathf.Lerp(waves.volume, zoneWeight[(int)Zone.Beach] * (.45f + gale * .2f) * OutdoorLevel, k);
            crowd.volume = Mathf.Lerp(crowd.volume, zoneWeight[(int)Zone.Stadium] * Mathf.Lerp(.08f, .32f, day) * dry * OutdoorLevel, k);
            jets.volume = Mathf.Lerp(jets.volume, zoneWeight[(int)Zone.Airport] * .35f * OutdoorLevel, k);
            music.volume = Mathf.Lerp(music.volume, Indoors ? .22f : 0, k * 2); room.volume = Mathf.Lerp(room.volume, Indoors ? .12f : 0, k * 2);
            if (Time.time >= nextEvent) { nextEvent = Time.time + Random.Range(3f, 8f); AmbientEvent(listener, day, dry); }
        }

        private void AmbientEvent(Vector3 listener, float day, float dry)
        {
            // Local, location-specific one-shots placed around the listener inside the zone.
            var options = new List<(AudioClip clip, float weight, float volume, float distance)>();
            float hour = atmosphere == null ? 12 : atmosphere.Hour;
            float city = zoneWeight[(int)Zone.City], farm = zoneWeight[(int)Zone.Farm], suburb = zoneWeight[(int)Zone.Suburb], stadium = zoneWeight[(int)Zone.Stadium], beach = zoneWeight[(int)Zone.Beach], park = zoneWeight[(int)Zone.Park];
            if (city > .1f) { options.Add((hornClip, city * 2, .25f, 60)); options.Add((siren, city * .5f, .3f, 180)); }
            if (farm > .1f) { options.Add((moo, farm * 3, .5f, 45)); options.Add((rooster, farm * (hour > 4.5f && hour < 9.5f ? 4 : .4f), .45f, 60)); options.Add((barks[0], farm, .35f, 50)); }
            if (suburb > .1f || park > .1f) { options.Add((barks[1], (suburb + park * .5f) * 1.5f, .3f, 45)); options.Add((barks[2], suburb, .25f, 70)); }
            if (stadium > .1f && day > .3f) { options.Add((cheer, stadium * 2, .45f, 40)); options.Add((whistle, stadium * 1.5f, .35f, 60)); }
            if (beach > .1f) options.Add((gulls[Random.Range(0, gulls.Length)], beach * 3 * dry, .35f, 30));
            float total = 0; foreach (var o in options) total += o.weight;
            if (total <= 0 || Random.value > Mathf.Clamp01(total)) return;
            float roll = Random.value * total;
            foreach (var o in options)
            {
                if ((roll -= o.weight) > 0) continue;
                var dir = Quaternion.Euler(0, Random.Range(0f, 360f), 0) * Vector3.forward;
                PlayAt(o.clip, listener + dir * Random.Range(o.distance * .4f, o.distance) + Vector3.up * 2, o.volume * OutdoorLevel, Random.Range(.92f, 1.08f));
                AmbientEventCount++; return;
            }
        }

        private bool CheckIndoors()
        {
            if (owner == null || !owner.IsWalking) return false;
            var p = owner.transform.position;
            for (int i = 0; i < 3; i++) if (TransitSquareLayout.Building(i).Contains(new Vector2(p.x, p.z))) return true;
            // A ceiling close overhead (not a tree crown or awning far above) means we are inside.
            if (Physics.Raycast(p + Vector3.up * 1.7f, Vector3.up, out var hit, 8f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                string n = hit.collider.name.ToLowerInvariant();
                return !(n.Contains("tree") || n.Contains("crown") || n.Contains("canopy") || n.Contains("umbrella") || n.Contains("awning") || n.Contains("roof column")) && hit.collider.attachedRigidbody == null;
            }
            return false;
        }
        private void AssignWater(Vector3 listener)
        {
            // Closest point on each water body, so a long river or a lake sounds from its nearest shore.
            var best = new List<(float d, Vector3 p, float size)>();
            foreach (var r in waterBodies)
            {
                if (r == null || !r.gameObject.activeInHierarchy) continue;
                var p = r.bounds.ClosestPoint(listener); float d = (p - listener).sqrMagnitude;
                if (d < 60 * 60) best.Add((d, p, r.bounds.size.magnitude));
            }
            best.Sort((a, b) => a.d.CompareTo(b.d));
            for (int i = 0; i < WaterVoices; i++)
            {
                if (i < best.Count)
                {
                    water[i].transform.position = best[i].p;
                    water[i].volume = Mathf.Clamp(.25f + best[i].size * .01f, .25f, .6f);
                    if (!water[i].isPlaying) water[i].Play();
                }
                else if (water[i].isPlaying) water[i].Stop();
            }
        }

        // ---------- Traffic ----------

        private void AssignTraffic(Vector3 listener)
        {
            var agents = Object.FindObjectsByType<TownVehicleAgent>(FindObjectsSortMode.None);
            var list = new List<(float d, Transform t)>();
            foreach (var a in agents)
            {
                if (!a.enabled || !a.gameObject.activeInHierarchy) continue;
                float d = (a.transform.position - listener).sqrMagnitude;
                if (d < 60 * 60) list.Add((d, a.transform));
            }
            list.Sort((a, b) => a.d.CompareTo(b.d));
            for (int i = 0; i < TrafficVoices; i++)
            {
                var t = i < list.Count ? list[i].t : null;
                if (t != trafficTargets[i]) { trafficTargets[i] = t; if (t != null) trafficLast[i] = t.position; }
                if (t == null && traffic[i].isPlaying) traffic[i].Stop();
                else if (t != null && !traffic[i].isPlaying) traffic[i].Play();
            }
        }

        private void UpdateTraffic()
        {
            for (int i = 0; i < TrafficVoices; i++)
            {
                var t = trafficTargets[i]; if (t == null) continue;
                float speed = (t.position - trafficLast[i]).magnitude / Mathf.Max(Time.deltaTime, 1e-3f); trafficLast[i] = t.position;
                traffic[i].transform.position = t.position;
                traffic[i].volume = Mathf.Lerp(traffic[i].volume, Mathf.Clamp(.12f + speed * .02f, .1f, .4f), Time.deltaTime * 4);
                traffic[i].pitch = Mathf.Lerp(traffic[i].pitch, .75f + Mathf.Clamp01(speed / 16f) * .6f + i * .03f, Time.deltaTime * 4);
            }
        }

        // ---------- Doors and UI ----------

        private void UpdateDoors()
        {
            if (doors.Length == 0 || Time.frameCount % 120 == 0)
            {
                var found = Object.FindObjectsByType<DistrictSlidingDoor>(FindObjectsSortMode.None);
                if (found.Length != doors.Length) { doors = found; doorLast = new float[found.Length]; for (int i = 0; i < found.Length; i++) doorLast[i] = found[i].OpenAmount; }
            }
            for (int i = 0; i < doors.Length; i++)
            {
                var door = doors[i]; if (door == null) continue;
                float now = door.OpenAmount;
                bool opened = doorLast[i] < .05f && now >= .05f, closed = doorLast[i] > .95f && now <= .95f;
                if (opened || closed) { PlayAt(doorClip, door.transform.position + Vector3.up * 1.5f, .55f, opened ? 1.05f : .9f); DoorSoundCount++; }
                doorLast[i] = now;
            }
        }

        private void WireButtons()
        {
            foreach (var button in Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (button == null || !wiredButtons.Add(button)) continue;
                button.onClick.AddListener(() => { if (footsteps != null) footsteps.PlayOneShot(clickClip, .45f); });
            }
        }

        private void OnDestroy()
        {
            if (Active == this) Active = null; OutdoorLevel = 1;
            foreach (var c in clips) if (c != null) Destroy(c);
        }
    }

    /// <summary>Reports collisions of the driven vehicle (not ground contact) to the soundscape.</summary>
    public sealed class VehicleImpactSound : MonoBehaviour
    {
        private WorldAudio audioSystem;
        private float quietUntil;
        public void Configure(WorldAudio system) => audioSystem = system;
        private void OnCollisionEnter(Collision collision)
        {
            if (audioSystem == null || Time.time < quietUntil || collision.contactCount == 0) return;
            if (collision.GetContact(0).normal.y > .7f) return;
            quietUntil = Time.time + .25f;
            float speed = collision.relativeVelocity.magnitude;
            audioSystem.Impact(collision.GetContact(0).point, speed);
            // Light loose props (cones, bins) fly away without denting the car.
            if (collision.rigidbody == null || collision.rigidbody.mass > 150) VehicleDynamics.Active?.OnImpact(GetComponent<Rigidbody>(), collision.GetContact(0).point, speed);
        }
    }
}
