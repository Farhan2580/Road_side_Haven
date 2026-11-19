using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RoadsideHaven
{
    /// <summary>
    /// Gives the player's car the new design and parks a drivable line-up of every new vehicle type:
    /// a motorbike and SUV by the start, and sedan, hatchback, SUV, van, pickup, coupe, bikes and a
    /// city bus in the sports park car park.
    /// </summary>
    public sealed class VehicleFleet : MonoBehaviour
    {
        private readonly List<Rigidbody> spawned = new List<Rigidbody>();
        private readonly List<Rect> roads = new List<Rect>();
        private GameObject root;
        public IReadOnlyList<Rigidbody> Spawned => spawned;

        private IEnumerator Start()
        {
            yield return null; yield return null; yield return null;
            var driving = GetComponentInChildren<WorldDrivingController>(true);
            foreach (var r in GetComponentsInChildren<MeshRenderer>()) if (TownPopulationController.RoadName(r.name)) roads.Add(Rect.MinMaxRect(r.bounds.min.x, r.bounds.min.z, r.bounds.max.x, r.bounds.max.z));
            root = new GameObject("Vehicle line-up");
            SceneManager.MoveGameObjectToScene(root, gameObject.scene);
            foreach (var body in GetComponentsInChildren<Rigidbody>(true))
                if (body.name == "Player car") Redesign(body, VehicleBodyType.Sedan, 0);
            Physics.SyncTransforms();
            // Near the start, so the new vehicles can be tried straight away.
            TrySpawn(driving, VehicleBodyType.Motorbike, "Haven street bike", new[] { new Vector4(7.5f, 0, -9.9f, 90), new Vector4(-8, 0, -9.9f, 90), new Vector4(9.5f, 0, -37.5f, 0) }, 0, 5, 1.15f);
            TrySpawn(driving, VehicleBodyType.SUV, "Haven family SUV", new[] { new Vector4(-3.5f, 0, -9.9f, 90), new Vector4(7, 0, -38.5f, 0), new Vector4(-29, 0, -38.5f, 0) }, 0, 4, .95f);
            var lot = HavenExpansionLayout.SportsParking;
            var line = new (VehicleBodyType type, string name, float speed)[]
            {
                (VehicleBodyType.Sedan, "Haven executive sedan", 1f), (VehicleBodyType.Hatchback, "Haven city hatchback", 1f), (VehicleBodyType.SUV, "Haven explorer SUV", .95f),
                (VehicleBodyType.Van, "Haven delivery van", .85f), (VehicleBodyType.Pickup, "Haven ranch pickup", .95f), (VehicleBodyType.Coupe, "Haven sport coupe", 1.12f),
                (VehicleBodyType.Motorbike, "Haven sport bike", 1.15f), (VehicleBodyType.Motorbike, "Haven touring bike", 1.05f)
            };
            for (int i = 0; i < line.Length; i++) TrySpawn(driving, line[i].type, line[i].name, new[] { new Vector4(lot.xMin + 6 + i * 8, 0, lot.yMin + 9, 0) }, 0, 11 + i * 3, line[i].speed);
            TrySpawn(driving, VehicleBodyType.CityBus, "Haven city bus", new[] { new Vector4(lot.xMax - 9, 0, lot.yMin + 10, 0), new Vector4(lot.xMax - 9, 0, lot.center.y + 5, 0) }, 0, 2, .72f);
        }

        private static void Redesign(Rigidbody body, VehicleBodyType type, int seed)
        {
            var modern = body.GetComponent<ModernCarVisuals>();
            if (modern != null) Destroy(modern);
            foreach (Transform child in body.transform) if (child.name.StartsWith("Modern car design")) Destroy(child.gameObject);
            if (body.GetComponent<RealisticVehicleVisuals>() == null) body.gameObject.AddComponent<RealisticVehicleVisuals>().Configure(type, seed);
        }

        private void TrySpawn(WorldDrivingController driving, VehicleBodyType type, string name, Vector4[] candidates, float unused, int seed, float speed)
        {
            var size = RealisticVehicleVisuals.ColliderSize(type);
            foreach (var c in candidates)
            {
                var p = new Vector3(c.x, c.y, c.z); float yaw = c.w;
                if (Physics.Raycast(p + Vector3.up * 20, Vector3.down, out var hit, 40, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) p.y = hit.point.y + .02f;
                var rotation = Quaternion.Euler(0, yaw, 0);
                // Never park on a public carriageway: traffic would queue behind it.
                bool onRoad = false; foreach (var r in roads) if (r.Contains(new Vector2(p.x, p.z))) { onRoad = true; break; }
                if (onRoad) continue;
                if (Physics.CheckBox(p + Vector3.up * (size.y * .5f + .15f), new Vector3(size.x * .5f + .2f, size.y * .5f, size.z * .5f + .2f), rotation, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) continue;
                var go = new GameObject(name); go.transform.SetParent(root.transform, false); go.transform.SetPositionAndRotation(p, rotation);
                var box = go.AddComponent<BoxCollider>(); box.size = size; box.center = Vector3.up * size.y * .5f;
                var body = go.AddComponent<Rigidbody>();
                body.mass = type == VehicleBodyType.Motorbike ? 230 : type == VehicleBodyType.CityBus ? 9500 : type == VehicleBodyType.Van || type == VehicleBodyType.Pickup || type == VehicleBodyType.SUV ? 2000 : 1450;
                body.isKinematic = true; body.interpolation = RigidbodyInterpolation.Interpolate;
                go.AddComponent<EnterableVehicle>().Configure(1, speed);
                go.AddComponent<RealisticVehicleVisuals>().Configure(type, seed);
                if (driving != null) driving.RegisterVehicle(body);
                spawned.Add(body); Physics.SyncTransforms();
                return;
            }
        }

        private void OnDestroy() { if (root != null) Destroy(root); }
    }
}
