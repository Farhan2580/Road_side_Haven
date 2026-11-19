using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
namespace RoadsideHaven.Tests
{
    public sealed class AirportUpgradeTests
    {
        [Test] public void CircuitTrafficIsContinuous()
        {
            Vector3 last = AirportUpgrade.CircuitPosition(0, out _);
            for (float t = .25f; t <= AirportUpgrade.CircuitSeconds + .01f; t += .25f)
            {
                var p = AirportUpgrade.CircuitPosition(t, out _);
                Assert.Less(Vector3.Distance(p, last), 30, "Circuit aircraft jumps at t=" + t);
                Assert.GreaterOrEqual(p.y, .1f); last = p;
            }
        }

        [UnityTest] public IEnumerator ModernAirportRoadsAircraftAndHelicopters()
        {
            yield return new EnterPlayMode();
            yield return Body(); // closures do not survive the play-mode domain reload
            yield return new ExitPlayMode();
        }
        private static IEnumerator Seconds(float s) { float u = Time.time + s; while (Time.time < u) yield return null; }
        private static IEnumerator Body()
        {
            Application.runInBackground = true;
            var loading = SceneManager.LoadSceneAsync("Assets/_Game/Generated/RoadsideHavenPrototype.unity"); while (!loading.isDone) yield return null;
            AirportUpgrade a = null; float until = Time.realtimeSinceStartup + 60;
            while (Time.realtimeSinceStartup < until && (a == null || !a.Ready)) { var m = Object.FindFirstObjectByType<MotelGameRoot>(); if (m != null) a = m.GetComponent<AirportUpgrade>(); yield return null; }
            Assert.IsNotNull(a, "Airport upgrade not installed"); Assert.IsTrue(a.Ready, "Airport upgrade never became ready");
            var root = a.Root;
            Assert.IsFalse(root.Find("Airport terminal").gameObject.activeInHierarchy, "Old box terminal still visible");
            Assert.GreaterOrEqual(a.Aircraft, 7); Assert.AreEqual(2, a.Helicopters); Assert.AreEqual(3, a.JetBridges);
            Assert.GreaterOrEqual(a.ForecourtVehicles, 5); Assert.GreaterOrEqual(a.People, 30);
            Physics.SyncTransforms();

            // Two-level forecourt: departures deck up top, arrivals lane underneath, smooth ramps.
            Vector3 L(float x, float y, float z) => root.TransformPoint(new Vector3(x, y, z));
            float zc = (AirportUpgrade.DeckSouth + AirportUpgrade.DeckNorth) * .5f;
            Assert.IsTrue(Physics.Raycast(L(770, 20, zc), Vector3.down, out var deck, 30), "No departures deck");
            Assert.AreEqual(AirportUpgrade.UpperLevel, deck.point.y, .3f, "Deck at the wrong height");
            Assert.IsTrue(Physics.Raycast(L(770, 3, zc), Vector3.down, out var lane, 10), "No arrivals lane under the deck");
            Assert.Less(lane.point.y, .4f, "Arrivals lane is not at ground level");
            float previous = -1;
            for (float x = AirportUpgrade.RampWest + 2; x <= AirportUpgrade.DeckWest - 2; x += 4)
            {
                Assert.IsTrue(Physics.Raycast(L(x, 20, zc), Vector3.down, out var hit, 30), "Ramp missing at x=" + x);
                Assert.Greater(hit.point.y, previous - .05f, "Ramp dips at x=" + x); Assert.Greater(hit.normal.y, .99f, "Ramp too steep"); previous = hit.point.y;
            }
            Assert.AreEqual(AirportUpgrade.UpperLevel, previous, .5f, "Ramp does not reach the deck");
            Assert.IsTrue(Physics.Raycast(L(750, 20, -262), Vector3.down, out var upper, 30)); Assert.AreEqual(AirportUpgrade.UpperLevel, upper.point.y, .3f, "No departures floor");

            // The airport is reachable by road from town.
            var motel = Object.FindFirstObjectByType<MotelGameRoot>(); var net = new TownRoadNetwork();
            foreach (var r in motel.GetComponentsInChildren<MeshRenderer>(true)) if (r.gameObject.activeInHierarchy && TownPopulationController.RoadName(r.name)) net.Add(r.bounds.center, r.bounds.size);
            net.Build(null);
            Assert.Greater(net.Route(new Vector3(0, 0, -13), L(790, 0, -220), false, 2.2f).Length, 1, "No road route from town to the terminal");

            // Things move: circuit traffic, sightseeing helicopter, forecourt cars climbing to the deck.
            var c0 = a.CircuitAircraft.position; var h0 = a.TourHelicopter.position;
            float maxY = 0; for (float t = 0; t < 14; t += .25f) { foreach (var v in a.ForecourtMovers) maxY = Mathf.Max(maxY, v.position.y); yield return Seconds(.25f); }
            Assert.Greater(Vector3.Distance(a.CircuitAircraft.position, c0), 20, "Circuit aircraft is not flying");
            Assert.Greater(Vector3.Distance(a.TourHelicopter.position, h0), 50, "Tour helicopter is not flying");
            Assert.Greater(maxY, 4, "No car drove up to the departures level");

            // Screenshots.
            var main = Camera.main; foreach (var b in main.GetComponents<MonoBehaviour>()) if (b.GetType().Namespace == "RoadsideHaven") b.enabled = false;
            yield return Capture("air_forecourt", L(560, 24, -160), L(760, 6, -250));
            yield return Capture("air_deck", L(700, 9, zc + 1), L(800, 6, -248));
            yield return Capture("air_arrivals_lane", L(866, 2, -236), L(760, 2, -240));
            yield return Capture("air_departures_hall", L(700, 9.5f, -262), L(800, 6, -290));
            yield return Capture("air_arrivals_hall", L(860, 2.2f, -258), L(760, 1, -282));
            yield return Capture("air_apron", L(690, 22, -410), L(820, 2, -330));
            yield return Capture("air_plane_close", L(872, 6, -372), L(890, 4, -348));
            yield return Capture("air_overview", L(400, 260, -80), L(780, 0, -380));
            yield return Capture("air_tower", L(600, 20, -280), L(637, 40, -310));
            var tour = a.TourHelicopter; yield return Capture("air_tour_heli", () => tour.position + tour.right * 14 + Vector3.up * 3 - tour.forward * 6, () => tour.position);
            var circ = a.CircuitAircraft; yield return Capture("air_circuit", () => circ.position + circ.right * 60 + Vector3.up * 12, () => circ.position);
            var hosp = motel.GetComponent<HospitalUpgrade>(); if (hosp != null && hosp.AirAmbulanceModel != null) { var hm = hosp.AirAmbulanceModel; yield return Capture("hosp_air_ambulance", () => hm.position + new Vector3(10, 4, -8), () => hm.position + Vector3.up * 1.5f); }
            motel.GetComponent<MotelAtmosphere>().PreviewNight(); yield return Seconds(1);
            yield return Capture("air_night", L(560, 30, -140), L(790, 8, -260));
        }
        private static IEnumerator Capture(string name, Vector3 pos, Vector3 look) => Capture(name, () => pos, () => look);
        private static IEnumerator Capture(string name, System.Func<Vector3> posF, System.Func<Vector3> lookF)
        {
            var folder = System.Environment.GetEnvironmentVariable("STADIUM_SHOTS"); if (string.IsNullOrEmpty(folder)) yield break;
            var main = Camera.main; var cam = new GameObject("Airport capture").AddComponent<Camera>(); cam.CopyFrom(main); cam.rect = new Rect(0, 0, 1, 1); cam.orthographic = false; cam.usePhysicalProperties = false; cam.fieldOfView = 60; cam.nearClipPlane = .2f; cam.farClipPlane = 6000;
            var pos = posF(); var look = lookF(); main.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(look - pos)); cam.enabled = false;
            for (int f = 0; f < 6; f++) yield return null; yield return Seconds(.7f);
            pos = posF(); look = lookF(); main.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(look - pos)); cam.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(look - pos));
            var rt = new RenderTexture(1280, 720, 24); cam.targetTexture = rt; cam.Render(); RenderTexture.active = rt;
            var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false); tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); tex.Apply();
            System.IO.File.WriteAllBytes(System.IO.Path.Combine(folder, name + ".png"), tex.EncodeToPNG()); cam.targetTexture = null; RenderTexture.active = null; Object.Destroy(rt); Object.Destroy(tex); Object.Destroy(cam.gameObject);
        }
    }
}
