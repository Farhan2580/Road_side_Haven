using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
namespace RoadsideHaven.Tests
{
    public sealed class HospitalUpgradeTests
    {
        private static IEnumerator Seconds(float s) { float u = Time.time + s; while (Time.time < u) yield return null; }
        private static IEnumerator Until(System.Func<bool> condition, float seconds) { float u = Time.time + seconds; while (Time.time < u && !condition()) yield return null; }
        private static Vector3 H(float x, int floor, float z) => HospitalCampusLayout.Point(x, HospitalCampusLayout.Storey * floor + .12f, z);

        [UnityTest] public IEnumerator AmbulanceRescueMissionAndLivingHospital()
        {
            yield return new EnterPlayMode();
            // Lambdas capture locals in a closure that does not survive the play-mode domain reload, so the body runs in its own iterator.
            yield return Body();
            yield return new ExitPlayMode();
        }

        private static IEnumerator Body()
        {
            Application.runInBackground = true;
            var loading = SceneManager.LoadSceneAsync("Assets/_Game/Generated/RoadsideHavenPrototype.unity"); while (!loading.isDone) yield return null;
            HospitalUpgrade h = null; float until = Time.realtimeSinceStartup + 60;
            while (Time.realtimeSinceStartup < until && (h == null || !h.Ready)) { var m = Object.FindFirstObjectByType<MotelGameRoot>(); if (m != null) h = m.GetComponent<HospitalUpgrade>(); yield return null; }
            Assert.IsNotNull(h, "Hospital upgrade not installed"); Assert.IsTrue(h.Ready, "Hospital upgrade never became ready");
            var motel = h.GetComponent<MotelGameRoot>(); var driving = motel.GetComponentInChildren<WorldDrivingController>(true); var owner = motel.GetComponentInChildren<OwnerSandboxController>(true);
            yield return Until(() => Object.FindObjectsByType<TownCitizenAgent>(FindObjectsSortMode.None).Count(c => c.Available) > 150, 45);

            // The hospital is alive.
            Assert.GreaterOrEqual(h.DoctorsOnRounds, 4, "No doctors on ward rounds");
            Assert.AreEqual(5, h.NursesWithTrolleys, "Nurse trolley rounds");
            Assert.GreaterOrEqual(h.SeatedVisitors, 8, "Waiting room is empty");
            Assert.AreEqual(5, h.QueueLength, "Reception queue");
            Assert.AreEqual(3, h.Wheelchairs); Assert.AreEqual(12, h.Visitors); Assert.AreEqual(14, h.MonitorCount, "ICU heart monitors");
            Assert.AreEqual(16, h.OldVehiclesRemoved, "Old block cars and ambulances still in the parking"); Assert.Greater(h.StrippedTriangles, 500, "Old vehicle geometry not removed");
            Assert.GreaterOrEqual(h.ParkedCars, 20, "Parking is empty"); Assert.IsTrue(h.ParkedCarList.All(c => c.GetComponentInChildren<Renderer>() != null), "Parked cars have no model");
            Assert.IsTrue(h.SignTexts.Contains("EMERGENCY"), "No emergency signage");
            Assert.Less(Vector3.Distance(h.Ambulance.transform.position, HospitalUpgrade.BayHome), 1.5f, "Ambulance is not waiting in the ER bay");

            var main = Camera.main; foreach (var b in main.GetComponents<MonoBehaviour>()) if (b.GetType().Namespace == "RoadsideHaven") b.enabled = false;
            yield return Capture("hosp_er", new Vector3(694, 9, -478), new Vector3(658, 2.5f, -451));
            yield return Capture("hosp_parking", new Vector3(640, 14, -470), new Vector3(612, 0, -500));
            yield return Capture("hosp_ambulance_close", new Vector3(676, 2.6f, -444.5f), new Vector3(668, 1.4f, -435));
            yield return Capture("hosp_ambulance_rear", new Vector3(664, 2.4f, -427), new Vector3(668, 1.4f, -433));
            yield return Capture("hosp_garden", new Vector3(490, 4, -462), new Vector3(506, 1, -434));

            // Traffic makes way for a siren.
            var car = Object.FindObjectsByType<TownVehicleAgent>(FindObjectsSortMode.None).FirstOrDefault(a => a.name.StartsWith("City commuter car") && a.HasRoute && !a.Arrived && a.Speed > 2);
            if (car != null)
            {
                var siren = new GameObject("Test siren").transform; siren.SetPositionAndRotation(car.transform.position - car.transform.forward * 12, car.transform.rotation);
                TownVehicleAgent.AddSiren(siren); yield return Seconds(1);
                Assert.IsTrue(car.Yielding, "Traffic did not pull over for an ambulance");
                TownVehicleAgent.RemoveSiren(siren); Object.Destroy(siren.gameObject);
            }

            // An incident in town: the AI ambulance responds with siren.
            main.transform.position = new Vector3(0, 30, -13); main.transform.rotation = Quaternion.Euler(60, 0, 0);
            Assert.IsTrue(h.DispatchNow(), "Could not dispatch the ambulance: " + h.LastDispatchError);
            var patient = h.Patient; Assert.IsNotNull(patient); Assert.IsTrue(patient.Incapacitated, "Patient did not collapse");
            Assert.AreEqual(HospitalUpgrade.Call.Responding, h.State); Assert.IsTrue(h.AmbulanceSiren);
            Assert.IsTrue(TownVehicleAgent.Sirens.Contains(h.Ambulance.transform)); Assert.IsTrue(h.Ambulance.Emergency);
            var bay = h.Ambulance.transform.position; yield return Seconds(7);
            Assert.Greater(Vector3.Distance(h.Ambulance.transform.position, bay), 8, "Ambulance never left the bay");

            // On scene: paramedics bring the patient to the ambulance on a stretcher.
            h.SkipTravel(); yield return Until(() => h.State == HospitalUpgrade.Call.OnScene, 5);
            Assert.AreEqual(HospitalUpgrade.Call.OnScene, h.State, "Ambulance did not arrive on scene");
            Assert.Less(Vector3.Distance(h.Ambulance.transform.position, patient.transform.position), 16, "Ambulance stopped too far from the patient");
            Assert.AreEqual(1, h.StretcherTeamsActive, "No stretcher team");
            yield return Seconds(3);
            var p = patient.transform.position; yield return Capture("hosp_scene", p + new Vector3(9, 5, -9), p);
            yield return Until(() => h.State == HospitalUpgrade.Call.Returning, 35);
            Assert.IsTrue(patient.Away, "Patient was not taken on board"); Assert.AreEqual(HospitalUpgrade.Call.Returning, h.State);

            // Back at the ER: stretcher through the emergency doors, then discharge.
            h.SkipTravel(); yield return Until(() => h.State == HospitalUpgrade.Call.Unloading, 5);
            Assert.AreEqual(HospitalUpgrade.Call.Unloading, h.State, "Ambulance did not return to the ER");
            yield return Seconds(6); yield return Capture("hosp_unload", new Vector3(668, 6, -466), new Vector3(651, 1, -452));
            yield return Until(() => h.State == HospitalUpgrade.Call.Idle, 40);
            Assert.AreEqual(1, h.Rescued, "Rescue not completed");
            h.DischargeAll(); yield return null;
            Assert.IsFalse(patient.Away); Assert.IsFalse(patient.Incapacitated); Assert.AreEqual(1, h.Discharged);

            // Player ambulance mission.
            owner.ExitVehicle(HospitalUpgrade.PlayerBay + new Vector3(-2.6f, .2f, 0)); yield return Seconds(.5f);
            int coins = motel.Coins;
            Assert.IsTrue(driving.TryEnterOwner(owner.transform.position, 4), "Could not get into the player ambulance");
            yield return Until(() => h.MissionState == HospitalUpgrade.Mission.ToPatient, 3);
            Assert.AreEqual(HospitalUpgrade.Mission.ToPatient, h.MissionState, "No mission offered"); Assert.IsTrue(h.PlayerSiren);
            var target = h.MissionPatient; Assert.IsNotNull(target); Assert.IsTrue(target.Incapacitated);
            yield return Capture("hosp_mission", HospitalUpgrade.PlayerBay + new Vector3(8, 6, -14), HospitalUpgrade.PlayerBay);
            var body = h.PlayerAmbulance; var near = target.transform.position + Vector3.right * 5 + Vector3.up * .3f;
            body.position = near; body.transform.position = near; body.linearVelocity = Vector3.zero;
            yield return Until(() => h.MissionState == HospitalUpgrade.Mission.ToHospital, 8);
            Assert.AreEqual(HospitalUpgrade.Mission.ToHospital, h.MissionState, "Patient was not loaded"); Assert.IsTrue(target.Away);
            var home = HospitalUpgrade.PlayerBay + Vector3.up * .3f; body.position = home; body.transform.position = home; body.linearVelocity = Vector3.zero;
            yield return Until(() => h.MissionsCompleted == 1, 5);
            Assert.AreEqual(1, h.MissionsCompleted, "Mission not completed"); Assert.GreaterOrEqual(motel.Coins - coins, HospitalUpgrade.MissionBaseReward);
            for (int i = 0; i < 60 && driving.IsDriving; i++) { body.linearVelocity = Vector3.zero; driving.TryExitOwner(); yield return null; }

            // Inside: the queue moves and the board updates; the ICU and wards are staffed.
            owner.ExitVehicle(H(4, 0, -12)); yield return Seconds(1);
            int served = h.QueueServed; yield return Seconds(12);
            Assert.Greater(h.QueueServed, served, "Reception queue never moved"); StringAssert.Contains("A-0", h.NowServing);
            yield return Capture("hosp_lobby", H(10, 0, -19) + Vector3.up * 2.2f, H(-2, 0, -2) + Vector3.up * 1);
            owner.ExitVehicle(H(-10, 4, 0)); yield return Seconds(1.5f);
            yield return Capture("hosp_icu", H(-21.5f, 4, 7) + Vector3.up * 1.8f, H(-22, 4, 13.8f) + Vector3.up * 1.6f);
            owner.ExitVehicle(H(0, 3, 0)); yield return Seconds(1.5f);
            yield return Capture("hosp_ward", H(10, 3, 0) + Vector3.up * 1.7f, H(-20, 3, 1) + Vector3.up * 1.2f);
            owner.ExitVehicle(HospitalUpgrade.PlayerBay + new Vector3(-8, .2f, -12)); yield return Seconds(1);
            motel.GetComponent<MotelAtmosphere>().PreviewNight(); yield return Seconds(1);
            yield return Capture("hosp_night", new Vector3(712, 22, -505), new Vector3(645, 6, -450));
        }

        private static IEnumerator Capture(string name, Vector3 pos, Vector3 look)
        {
            var folder = System.Environment.GetEnvironmentVariable("STADIUM_SHOTS"); if (string.IsNullOrEmpty(folder)) yield break;
            var main = Camera.main; var cam = new GameObject("Hospital capture").AddComponent<Camera>(); cam.CopyFrom(main); cam.rect = new Rect(0, 0, 1, 1); cam.orthographic = false; cam.usePhysicalProperties = false; cam.fieldOfView = 60; cam.nearClipPlane = .1f;
            main.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(look - pos));
            cam.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(look - pos)); cam.enabled = false;
            for (int f = 0; f < 6; f++) yield return null; yield return Seconds(.7f); // people refresh their distance LOD every 0.45 s
            var rt = new RenderTexture(1280, 720, 24); cam.targetTexture = rt; cam.Render(); RenderTexture.active = rt;
            var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false); tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); tex.Apply();
            System.IO.File.WriteAllBytes(System.IO.Path.Combine(folder, name + ".png"), tex.EncodeToPNG()); cam.targetTexture = null; RenderTexture.active = null; Object.Destroy(rt); Object.Destroy(tex); Object.Destroy(cam.gameObject);
        }
    }
}
