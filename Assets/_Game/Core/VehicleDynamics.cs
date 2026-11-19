using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RoadsideHaven
{
    /// <summary>
    /// Driving physics feel on top of the arcade motor: body roll and pitch, collision damage with
    /// sparks, smoke and a weakened engine, knockable street props, and pedestrians who dodge or
    /// get knocked over and stand back up.
    /// </summary>
    public sealed class VehicleDynamics : MonoBehaviour
    {
        public static VehicleDynamics Active { get; private set; }
        private sealed class Lean { public Transform Pivot; public Quaternion Rest; public bool Created; }
        private sealed class Knock { public TownCitizenAgent Agent; public CharacterController Body; public float Until, StandAt; public Vector3 Push; public bool Dodge; public Quaternion Upright; }
        private sealed class Prop { public Rigidbody Body; public Vector3 Home; public Quaternion HomeRotation; public float LastSeen; }

        private readonly Dictionary<Rigidbody, float> damage = new Dictionary<Rigidbody, float>();
        private readonly Dictionary<Rigidbody, Lean> leans = new Dictionary<Rigidbody, Lean>();
        private readonly Dictionary<Rigidbody, ParticleSystem> smoke = new Dictionary<Rigidbody, ParticleSystem>();
        private readonly List<Knock> knocks = new List<Knock>();
        private readonly List<Prop> props = new List<Prop>();
        private readonly Dictionary<Color, Material> materials = new Dictionary<Color, Material>();
        private TownCitizenAgent[] citizens = new TownCitizenAgent[0];
        private WorldDrivingController driving;
        private Rigidbody current;
        private Vector3 lastVelocity;
        private float roll, pitch, nextCitizenScan;
        private ParticleSystem sparks;
        private GameObject root;
        private Shader lit, particle;
        private MaterialPropertyBlock block;
        public int PropCount => props.Count;
        public int KnockdownCount { get; private set; }
        public int DodgeCount { get; private set; }
        public float DamageOf(Rigidbody body) => body != null && damage.TryGetValue(body, out var d) ? d : 0;

        private void Awake() { Active = this; }

        private void Start()
        {
            driving = GetComponentInChildren<WorldDrivingController>(true);
            lit = Shader.Find("Universal Render Pipeline/Lit");
            particle = Resources.Load<Shader>("UltraParticle");
            block = new MaterialPropertyBlock();
            root = new GameObject("Vehicle Dynamics");
            SceneManager.MoveGameObjectToScene(root, gameObject.scene);
            sparks = Burst("Impact sparks", new Color(1, .75f, .3f), 300, .06f, .5f, 9, -1.2f);
            SpawnProps();
        }

        // ---------- Public hooks used by the driving controller and audio ----------

        /// <summary>Engine output multiplier: a wrecked vehicle limps along at half power.</summary>
        public static float SpeedFactor(Rigidbody body)
        {
            if (Active == null || body == null) return 1;
            float d = Active.DamageOf(body);
            return d < 70 ? 1 : Mathf.Lerp(1, .5f, (d - 70) / 30f);
        }

        public static void Repair(Rigidbody body)
        {
            if (Active == null || body == null) return;
            Active.damage[body] = 0; Active.Paint(body, 0);
            if (Active.smoke.TryGetValue(body, out var s) && s != null) { var e = s.emission; e.rateOverTime = 0; }
        }

        public void OnImpact(Rigidbody body, Vector3 point, float speed)
        {
            if (body == null || speed < 4) return;
            float d = Mathf.Min(100, DamageOf(body) + (speed - 4) * 3.2f);
            damage[body] = d; Paint(body, d);
            sparks.transform.position = point; sparks.Emit(Mathf.Clamp((int)(speed * 4), 8, 70));
            UpdateSmoke(body, d);
        }

        // ---------- Body lean ----------

        private Lean LeanFor(Rigidbody body)
        {
            if (leans.TryGetValue(body, out var lean)) return lean;
            var pivot = body.transform.Find("Vehicle mesh");
            bool created = false;
            if (pivot == null)
            {
                // Vehicles without a visual root get one; only renderer-only children move.
                pivot = new GameObject("Body lean pivot").transform; pivot.SetParent(body.transform, false); created = true;
                var move = new List<Transform>();
                foreach (Transform c in body.transform) if (c != pivot && c.GetComponent<Renderer>() != null && c.GetComponent<Collider>() == null) move.Add(c);
                foreach (var c in move) c.SetParent(pivot, true);
            }
            lean = new Lean { Pivot = pivot, Rest = pivot.localRotation, Created = created };
            leans[body] = lean; return lean;
        }

        private void FixedUpdate()
        {
            var vehicle = driving != null && driving.IsDriving && driving.ActiveVehicleTransform != null ? driving.ActiveVehicleTransform.GetComponent<Rigidbody>() : null;
            if (vehicle != current)
            {
                if (current != null && leans.TryGetValue(current, out var old) && old.Pivot != null) old.Pivot.localRotation = old.Rest;
                current = vehicle; roll = pitch = 0; if (vehicle != null) lastVelocity = vehicle.linearVelocity;
            }
            if (vehicle == null) return;
            var lean = LeanFor(vehicle);
            var v = vehicle.linearVelocity; var accel = (v - lastVelocity) / Time.fixedDeltaTime; lastVelocity = v;
            var access = vehicle.GetComponent<EnterableVehicle>(); float sign = access == null ? 1 : access.ForwardSign;
            var forward = vehicle.transform.forward * sign; var right = Vector3.Cross(Vector3.up, forward).normalized;
            float lateral = Vector3.Dot(accel, right), longitudinal = Vector3.Dot(accel, forward);
            // Body leans out of corners and dips under braking, like a car on soft springs.
            // Motorbikes lean into the turn; cars roll out of it.
            var design = vehicle.GetComponent<RealisticVehicleVisuals>(); bool bike = design != null && design.IsBike;
            roll = Mathf.Lerp(roll, bike ? Mathf.Clamp(lateral * 2.4f, -30f, 30f) : Mathf.Clamp(-lateral * .55f, -6.5f, 6.5f), Time.fixedDeltaTime * (bike ? 4 : 5));
            pitch = Mathf.Lerp(pitch, Mathf.Clamp(-longitudinal * .35f, -4f, 4f), Time.fixedDeltaTime * 5);
            lean.Pivot.localRotation = lean.Rest * Quaternion.Euler(pitch * sign, 0, roll * sign);
        }

        // ---------- Damage visuals ----------

        private void Paint(Rigidbody body, float d)
        {
            float k = d / 100f;
            foreach (var r in body.GetComponentsInChildren<Renderer>())
            {
                if (r is ParticleSystemRenderer || r.sharedMaterial == null || !r.sharedMaterial.HasProperty("_BaseColor")) continue;
                string n = r.name.ToLowerInvariant(); if (n.Contains("wheel") || n.Contains("tyre") || n.Contains("glass") || n.Contains("windshield") || n.Contains("light")) continue;
                r.GetPropertyBlock(block);
                var baseColor = r.sharedMaterial.GetColor("_BaseColor");
                block.SetColor("_BaseColor", Color.Lerp(baseColor, baseColor * .35f + new Color(.12f, .1f, .08f), k * .75f));
                block.SetFloat("_Smoothness", Mathf.Lerp(r.sharedMaterial.HasProperty("_Smoothness") ? r.sharedMaterial.GetFloat("_Smoothness") : .5f, .1f, k));
                r.SetPropertyBlock(block); block.Clear();
            }
        }

        private void UpdateSmoke(Rigidbody body, float d)
        {
            if (!smoke.TryGetValue(body, out var s) || s == null)
            {
                s = Burst("Engine smoke", new Color(.25f, .25f, .25f, .5f), 250, .8f, 2.5f, 1.2f, -.05f);
                s.transform.SetParent(body.transform, false);
                var bounds = new Bounds(body.worldCenterOfMass, Vector3.zero); foreach (var c in body.GetComponentsInChildren<Collider>()) if (c.enabled && !c.isTrigger) bounds.Encapsulate(c.bounds);
                var access = body.GetComponent<EnterableVehicle>(); float sign = access == null ? 1 : access.ForwardSign;
                s.transform.position = bounds.center + body.transform.forward * sign * bounds.extents.magnitude * .45f + Vector3.up * bounds.extents.y;
                var size = s.sizeOverLifetime; size.enabled = true; size.size = new ParticleSystem.MinMaxCurve(1, AnimationCurve.Linear(0, .4f, 1, 2.2f));
                s.Play(); smoke[body] = s;
            }
            var e = s.emission; e.rateOverTime = d < 40 ? 0 : Mathf.Lerp(4, 40, (d - 40) / 60f);
            var main = s.main; main.startColor = Color.Lerp(new Color(.7f, .7f, .7f, .4f), new Color(.08f, .08f, .08f, .7f), (d - 40) / 60f);
        }

        private ParticleSystem Burst(string name, Color color, int max, float size, float life, float speed, float gravity)
        {
            var go = new GameObject(name); go.transform.SetParent(root.transform, false);
            var ps = go.AddComponent<ParticleSystem>(); ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main; main.startColor = color; main.maxParticles = max; main.startSize = new ParticleSystem.MinMaxCurve(size * .6f, size); main.startLifetime = new ParticleSystem.MinMaxCurve(life * .6f, life);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed * .4f, speed); main.gravityModifier = -gravity; main.simulationSpace = ParticleSystemSimulationSpace.World; main.playOnAwake = false; main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
            var emission = ps.emission; emission.rateOverTime = 0;
            var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Sphere; shape.radius = .2f;
            var fade = ps.colorOverLifetime; fade.enabled = true; var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) }, new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(0, 1) }); fade.color = g;
            var r = go.GetComponent<ParticleSystemRenderer>(); if (particle != null) r.sharedMaterial = new Material(particle); r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return ps;
        }

        // ---------- Knockable props ----------

        private Material Mat(Color c)
        {
            if (materials.TryGetValue(c, out var m)) return m;
            m = new Material(lit) { color = c, enableInstancing = true }; materials[c] = m; return m;
        }

        private void AddProp(string kind, Vector3 p, float yaw)
        {
            if (Physics.CheckBox(p + Vector3.up * .6f, new Vector3(.35f, .45f, .35f), Quaternion.identity, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) return;
            var go = new GameObject("Knockable " + kind); go.transform.SetParent(root.transform, false); go.transform.SetPositionAndRotation(p, Quaternion.Euler(0, yaw, 0));
            float mass;
            if (kind == "cone")
            {
                Shape(go.transform, PrimitiveType.Cube, new Vector3(0, .02f, 0), new Vector3(.45f, .04f, .45f), new Color(.1f, .1f, .1f));
                Shape(go.transform, PrimitiveType.Cylinder, new Vector3(0, .38f, 0), new Vector3(.28f, .36f, .28f), new Color(1, .42f, .05f));
                Shape(go.transform, PrimitiveType.Cylinder, new Vector3(0, .46f, 0), new Vector3(.25f, .05f, .25f), Color.white);
                var c = go.AddComponent<BoxCollider>(); c.center = new Vector3(0, .38f, 0); c.size = new Vector3(.4f, .76f, .4f); mass = 4;
            }
            else if (kind == "barrel")
            {
                Shape(go.transform, PrimitiveType.Cylinder, new Vector3(0, .45f, 0), new Vector3(.6f, .45f, .6f), new Color(.15f, .35f, .55f));
                var c = go.AddComponent<CapsuleCollider>(); c.center = new Vector3(0, .45f, 0); c.radius = .3f; c.height = .9f; mass = 35;
            }
            else if (kind == "crate")
            {
                Shape(go.transform, PrimitiveType.Cube, new Vector3(0, .35f, 0), new Vector3(.7f, .7f, .7f), new Color(.6f, .42f, .22f));
                var c = go.AddComponent<BoxCollider>(); c.center = new Vector3(0, .35f, 0); c.size = new Vector3(.7f, .7f, .7f); mass = 20;
            }
            else
            {
                Shape(go.transform, PrimitiveType.Cylinder, new Vector3(0, .5f, 0), new Vector3(.55f, .5f, .55f), new Color(.15f, .4f, .2f));
                Shape(go.transform, PrimitiveType.Cylinder, new Vector3(0, 1.02f, 0), new Vector3(.58f, .03f, .58f), new Color(.1f, .1f, .1f));
                var c = go.AddComponent<CapsuleCollider>(); c.center = new Vector3(0, .5f, 0); c.radius = .28f; c.height = 1.05f; mass = 12;
            }
            var body = go.AddComponent<Rigidbody>(); body.mass = mass; body.linearDamping = .2f; body.angularDamping = .4f;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic; body.Sleep();
            props.Add(new Prop { Body = body, Home = p, HomeRotation = go.transform.rotation, LastSeen = Time.time });
        }

        private void Shape(Transform parent, PrimitiveType type, Vector3 p, Vector3 scale, Color color)
        {
            var go = GameObject.CreatePrimitive(type); Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false); go.transform.localPosition = p; go.transform.localScale = scale; go.GetComponent<MeshRenderer>().sharedMaterial = Mat(color);
        }

        private void SpawnProps()
        {
            Physics.SyncTransforms();
            // Cones around car-park edges, bins by the paths, barrels and crates in the farm yard.
            var lot = HavenExpansionLayout.LakeParking; for (int i = 0; i < 8; i++) AddProp("cone", new Vector3(lot.xMin + 2 + i * 5.8f, .09f, lot.center.y), 0);
            var sport = HavenExpansionLayout.SportsParking; for (int i = 0; i < 14; i++) AddProp("cone", new Vector3(sport.xMin + 3 + i * 7.5f, .09f, sport.center.y), 0);
            for (int i = 0; i < 4; i++) AddProp("bin", new Vector3(sport.xMin + 1.5f, .09f, sport.yMin + 6 + i * 11), 0);
            for (int i = 0; i < 3; i++) AddProp("bin", new Vector3(lot.xMax - 1.5f, .09f, lot.yMin + 8 + i * 14), 0);
            for (int i = 0; i < 6; i++) AddProp("barrel", new Vector3(-226 + i * 1.1f, .08f, -287), 0);
            for (int i = 0; i < 8; i++) AddProp("crate", new Vector3(-192 + i * .95f, .08f, -300), i * 12);
        }

        // ---------- Pedestrians ----------

        private void Update()
        {
            ResetFarProps();
            var vehicle = current;
            if (Time.unscaledTime >= nextCitizenScan) { nextCitizenScan = Time.unscaledTime + 1.5f; citizens = Object.FindObjectsByType<TownCitizenAgent>(FindObjectsSortMode.None); }
            if (vehicle != null)
            {
                var v = vehicle.linearVelocity; v.y = 0; float speed = v.magnitude;
                if (speed > 2.5f)
                {
                    var dir = v / speed; var origin = vehicle.worldCenterOfMass;
                    foreach (var c in citizens)
                    {
                        if (c == null || !c.enabled || !c.gameObject.activeInHierarchy) continue;
                        var to = c.transform.position - origin; to.y = 0; float ahead = Vector3.Dot(to, dir);
                        if (ahead < -1 || ahead > 4 + speed * .8f) continue;
                        float side = Vector3.Dot(to, Vector3.Cross(Vector3.up, dir));
                        if (Mathf.Abs(side) > 2.6f) continue;
                        var body = c.GetComponent<CharacterController>(); if (body == null) continue;
                        bool hit = ahead < 2.4f && Mathf.Abs(side) < 1.6f && speed > 4;
                        var push = hit ? (dir * speed * .5f + Vector3.up) : Vector3.Cross(Vector3.up, dir) * (side >= 0 ? 1 : -1) * 5.5f;
                        c.enabled = false;
                        knocks.Add(new Knock { Agent = c, Body = body, Push = push, Dodge = !hit, Until = Time.time + (hit ? .6f : .45f), StandAt = Time.time + (hit ? 3.2f : .45f), Upright = c.transform.rotation });
                        if (hit) { KnockdownCount++; WorldAudio.Active?.Impact(c.transform.position + Vector3.up, speed * .5f); } else DodgeCount++;
                    }
                }
            }
            for (int i = knocks.Count - 1; i >= 0; i--)
            {
                var k = knocks[i];
                if (k.Agent == null) { knocks.RemoveAt(i); continue; }
                if (Time.time < k.Until)
                {
                    if (k.Body.enabled) k.Body.Move((k.Push + Vector3.down * 2) * Time.deltaTime);
                    if (!k.Dodge) k.Agent.transform.rotation = Quaternion.Slerp(k.Agent.transform.rotation, Quaternion.LookRotation(new Vector3(k.Push.x, 0, k.Push.z).normalized + Vector3.down * .01f) * Quaternion.Euler(-85, 0, 0), Time.deltaTime * 10);
                    k.Push = Vector3.Lerp(k.Push, Vector3.zero, Time.deltaTime * 3);
                }
                else if (Time.time < k.StandAt) { }
                else if (Quaternion.Angle(k.Agent.transform.rotation, k.Upright) > 2 && !k.Dodge)
                    k.Agent.transform.rotation = Quaternion.RotateTowards(k.Agent.transform.rotation, Quaternion.Euler(0, k.Upright.eulerAngles.y, 0), 240 * Time.deltaTime);
                else { k.Agent.transform.rotation = Quaternion.Euler(0, k.Upright.eulerAngles.y, 0); k.Agent.enabled = true; knocks.RemoveAt(i); }
            }
        }

        private void ResetFarProps()
        {
            if (Time.frameCount % 60 != 0) return;
            var cam = Camera.main != null ? Camera.main.transform.position : Vector3.zero;
            foreach (var p in props)
            {
                if (p.Body == null) continue;
                if ((p.Body.position - cam).sqrMagnitude < 90 * 90) { p.LastSeen = Time.time; continue; }
                // Tidy knocked props once the player has left the area for a while.
                if (Time.time - p.LastSeen > 20 && (p.Body.position - p.Home).sqrMagnitude > .25f)
                { p.Body.linearVelocity = Vector3.zero; p.Body.angularVelocity = Vector3.zero; p.Body.position = p.Home; p.Body.rotation = p.HomeRotation; p.Body.Sleep(); }
            }
        }

        private void OnDestroy()
        {
            if (Active == this) Active = null;
            foreach (var l in leans.Values) if (l.Pivot != null) l.Pivot.localRotation = l.Rest;
            if (root != null) Destroy(root);
            foreach (var m in materials.Values) if (m != null) Destroy(m);
        }
    }
}
