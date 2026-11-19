using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
namespace RoadsideHaven.Tests
{
    public sealed class LivingWorldTests
    {
        private static IEnumerator Wait(float s) { float u = Time.realtimeSinceStartup + s; while (Time.realtimeSinceStartup < u) yield return null; }

        [UnityTest] public IEnumerator SkyWildlifeVehiclePhysicsPedestriansAndZoneSoundsWork()
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
            yield return Wait(3);
            var motel = Object.FindFirstObjectByType<MotelGameRoot>();
            var sky = motel.GetComponent<SkyLife>(); var dynamics = motel.GetComponent<VehicleDynamics>(); var audio = motel.GetComponent<WorldAudio>(); var weather = motel.GetComponent<WeatherSystem>();
            Assert.IsNotNull(sky, "Sky life not installed"); Assert.IsNotNull(dynamics, "Vehicle dynamics not installed");
            weather.AutomaticChanges = false; weather.SetWeather(WeatherKind.Clear, true);
            Assert.AreEqual(3, sky.PlaneCount); Assert.AreEqual(5, sky.BalloonCount); Assert.Greater(sky.BirdCount, 60); Assert.AreEqual(5, sky.DogCount); Assert.IsNotNull(sky.Helicopter);
            Assert.IsNotNull(sky.Leaves, "Wind particles missing");

            // Put the camera over the lake: birds there must fly, the helicopter must move, sound zone must be "park".
            var camera = Camera.main; foreach (var b in camera.GetComponents<MonoBehaviour>()) if (b.GetType().Namespace == "RoadsideHaven") b.enabled = false;
            camera.transform.position = new Vector3(-90, 30, 300);
            yield return Wait(.5f);
            var bird = sky.FirstBirdPosition; var heli = sky.Helicopter.position;
            yield return Wait(1);
            Assert.Greater((sky.FirstBirdPosition - bird).magnitude, .5f, "Birds are not flying");
            Assert.Greater((sky.Helicopter.position - heli).magnitude, 5f, "Helicopter is not flying");
            Assert.Greater(audio.ZoneCount, 5, "Too few sound zones");
            Assert.Greater(audio.ZoneWeight(WorldAudio.Zone.Park), .9f, "Lakeside is not treated as a park soundscape");
            camera.transform.position = new Vector3(230, 20, -250);
            yield return Wait(.3f);
            Assert.Greater(audio.ZoneWeight(WorldAudio.Zone.Stadium), .9f, "Stadium sound zone missing");
            Assert.Less(audio.ZoneWeight(WorldAudio.Zone.Park), .5f, "Park sound leaks into the stadium");

            // Damage, limp mode and repair.
            var car = motel.GetComponentsInChildren<Rigidbody>(true).First(r => r.name == "Player car");
            for (int i = 0; i < 3; i++) dynamics.OnImpact(car, car.position, 20);
            Assert.Greater(dynamics.DamageOf(car), 90, "Hard impacts did not damage the car");
            Assert.Less(VehicleDynamics.SpeedFactor(car), .7f, "A wrecked car still has full power");
            VehicleDynamics.Repair(car); Assert.AreEqual(0, dynamics.DamageOf(car), "Repair did not reset damage");

            // Loose props are real physics bodies.
            Assert.Greater(dynamics.PropCount, 20, "Too few knockable props");
            foreach (var body in Object.FindObjectsByType<Rigidbody>(FindObjectsSortMode.None).Where(r => r.name.StartsWith("Knockable"))) Assert.IsFalse(body.isKinematic);

            // Drive the car at a pedestrian: they must dodge or be knocked down, then the body leans in a turn.
            var owner = motel.GetComponentInChildren<OwnerSandboxController>(true); var driving = motel.GetComponentInChildren<WorldDrivingController>(true);
            // A free citizen: children walking with a parent return to them, and footballers keep training.
            TownCitizenAgent person = null; float waitCitizen = Time.realtimeSinceStartup + 30;
            while (person == null && Time.realtimeSinceStartup < waitCitizen) { person = Object.FindObjectsByType<TownCitizenAgent>(FindObjectsSortMode.None).FirstOrDefault(c => c.enabled && c.gameObject.activeInHierarchy && c.Leader == null && c.Follower == null && c.Available && c.name.StartsWith("Citizen")); if (person == null) yield return null; }
            Assert.IsNotNull(person, "No free citizen spawned");
            var start = new Vector3(0, .3f, -60);
            car.isKinematic = true; car.position = start; car.rotation = Quaternion.LookRotation(Vector3.right * (car.GetComponent<EnterableVehicle>()?.ForwardSign ?? 1)); Physics.SyncTransforms();
            owner.ExitVehicle(start + Vector3.forward * 2.2f);
            yield return Wait(.5f);
            Assert.IsTrue(driving.TryEnterOwner(owner.transform.position, 6), $"Could not enter the car for the test (owner at {owner.transform.position}, walking {owner.IsWalking}, entry distance {WorldDrivingController.EntryDistance(car, owner.transform.position):F2})");
            person.Warp(start + Vector3.right * 9);
            int before = dynamics.DodgeCount + dynamics.KnockdownCount;
            for (int i = 0; i < 30; i++) { car.linearVelocity = Vector3.right * 9; yield return new WaitForFixedUpdate(); }
            Assert.Greater(dynamics.DodgeCount + dynamics.KnockdownCount, before, "Pedestrian ignored a car driving at them");
            var pivot = car.transform.Find("Vehicle mesh"); var rest = pivot.localRotation;
            for (int i = 0; i < 25; i++) { car.linearVelocity = Quaternion.Euler(0, i * 6, 0) * Vector3.right * 12; yield return new WaitForFixedUpdate(); }
            Assert.Greater(Quaternion.Angle(rest, pivot.localRotation), .5f, "Car body does not lean in a turn");
            LogAssert.NoUnexpectedReceived();
        }
    }
}
