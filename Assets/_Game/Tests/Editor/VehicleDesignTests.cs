using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
namespace RoadsideHaven.Tests
{
    public sealed class VehicleDesignTests
    {
        private static IEnumerator Wait(float s) { float u = Time.realtimeSinceStartup + s; while (Time.realtimeSinceStartup < u) yield return null; }

        [UnityTest] public IEnumerator NewVehicleDesignsAreBuiltDrivableAndUsedByTraffic()
        {
            yield return new EnterPlayMode();
            yield return Body();
            yield return new ExitPlayMode();
        }

        private static IEnumerator Body()
        {
            Application.runInBackground = true;
            var loading = SceneManager.LoadSceneAsync("Assets/_Game/Generated/RoadsideHavenPrototype.unity"); while (!loading.isDone) yield return null;
            float until = Time.realtimeSinceStartup + 15; while (Time.realtimeSinceStartup < until && Object.FindFirstObjectByType<MotelGameRoot>() == null) yield return null;
            yield return Wait(5);
            var motel = Object.FindFirstObjectByType<MotelGameRoot>();
            var driving = motel.GetComponentInChildren<WorldDrivingController>(true);
            var fleet = motel.GetComponent<VehicleFleet>(); Assert.IsNotNull(fleet, "Vehicle fleet not installed");
            Assert.GreaterOrEqual(fleet.Spawned.Count, 9, "Too few drivable line-up vehicles");
            var types = fleet.Spawned.Select(b => b.GetComponent<RealisticVehicleVisuals>().Type).Distinct().ToArray();
            foreach (VehicleBodyType t in System.Enum.GetValues(typeof(VehicleBodyType))) Assert.Contains(t, types, "Line-up is missing a " + t);
            var roads = motel.GetComponentsInChildren<MeshRenderer>().Where(r => TownPopulationController.RoadName(r.name)).Select(r => Rect.MinMaxRect(r.bounds.min.x, r.bounds.min.z, r.bounds.max.x, r.bounds.max.z)).ToArray();
            foreach (var body in fleet.Spawned)
            {
                Assert.IsFalse(roads.Any(r => r.Contains(new Vector2(body.position.x, body.position.z))), body.name + " is parked on a public road");
                var design = body.GetComponent<RealisticVehicleVisuals>();
                Assert.IsTrue(design.Built, body.name + " design not built");
                Assert.AreEqual(design.IsBike ? 2 : 4, design.WheelCount, body.name + " wheel count");
                Assert.IsTrue(driving.IsRegistered(body), body.name + " cannot be driven");
                int vertices = design.Shell.GetComponentsInChildren<MeshFilter>().Sum(f => f.sharedMesh.vertexCount);
                Assert.Less(vertices, 30000, body.name + " is too heavy");
            }

            // Traffic uses a mix of everyday designs, plus city buses.
            var trafficDesigns = Object.FindObjectsByType<TownVehicleAgent>(FindObjectsSortMode.None).Select(a => a.GetComponent<RealisticVehicleVisuals>()).Where(d => d != null).ToArray();
            Assert.GreaterOrEqual(trafficDesigns.Select(d => d.Type).Distinct().Count(), 4, "Traffic lacks variety");
            Assert.GreaterOrEqual(trafficDesigns.Count(d => d.Type == VehicleBodyType.CityBus), 1, "No city buses in traffic");

            // The player's car has the new design and the old box model is hidden.
            var car = motel.GetComponentsInChildren<Rigidbody>(true).First(r => r.name == "Player car");
            var carDesign = car.GetComponent<RealisticVehicleVisuals>(); Assert.IsNotNull(carDesign, "Player car was not redesigned");
            foreach (var r in car.GetComponentsInChildren<Renderer>()) if (!r.transform.IsChildOf(carDesign.Shell) && !(r is ParticleSystemRenderer)) Assert.IsFalse(r.enabled, "Old car model still visible: " + r.name);

            // Ride the motorbike: rider appears, wheels spin and the bike leans into a turn.
            var bike = fleet.Spawned.First(b => b.GetComponent<RealisticVehicleVisuals>().IsBike);
            var owner = motel.GetComponentInChildren<OwnerSandboxController>(true);
            var camera = Camera.main; foreach (var b in camera.GetComponents<MonoBehaviour>()) if (b.GetType().Namespace == "RoadsideHaven") b.enabled = false;
            camera.transform.position = bike.position + new Vector3(0, 5, -8);
            owner.ExitVehicle(bike.position + bike.transform.right * 1.6f);
            yield return Wait(.4f);
            Assert.IsTrue(driving.TryEnterOwner(owner.transform.position, 6), "Could not get on the motorbike");
            yield return Wait(.2f);
            var bikeDesign = bike.GetComponent<RealisticVehicleVisuals>();
            Assert.IsTrue(bikeDesign.RiderVisible, "No rider on the driven motorbike");
            var wheel = bikeDesign.Shell.GetComponentsInChildren<Transform>().First(t => t.name == "Wheel"); var spin = wheel.localRotation;
            var pivot = bikeDesign.Shell; var rest = pivot.localRotation; float maxLean = 0;
            for (int i = 0; i < 40; i++)
            {
                bike.linearVelocity = Quaternion.Euler(0, i * 4, 0) * bike.transform.forward * 10;
                yield return new WaitForFixedUpdate();
                maxLean = Mathf.Max(maxLean, Quaternion.Angle(rest, pivot.localRotation));
            }
            Assert.Greater(Quaternion.Angle(spin, wheel.localRotation), 5, "Wheels do not spin");
            Assert.Greater(maxLean, 4, "Motorbike does not lean into the turn");
            LogAssert.NoUnexpectedReceived();
        }
    }
}
