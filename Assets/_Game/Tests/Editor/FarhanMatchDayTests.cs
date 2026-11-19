using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
namespace RoadsideHaven.Tests
{
    public sealed class FarhanMatchDayTests
    {
        private static IEnumerator Seconds(float s) { float u = Time.time + s; while (Time.time < u) yield return null; }

        [Test] public void FixturesFollowTheCalendar()
        {
            Assert.IsTrue(FarhanMatchDay.IsMatchDay(1)); Assert.IsFalse(FarhanMatchDay.IsMatchDay(2)); Assert.IsTrue(FarhanMatchDay.IsMatchDay(3));
            var list = FarhanMatchDay.Fixtures(2, 3);
            CollectionAssert.AreEqual(new[] { 3, 5, 7 }, list.Select(f => f.day).ToArray());
            Assert.AreEqual(3, list.Select(f => f.opponent).Distinct().Count(), "The same opponent every week");
        }

        [UnityTest] public IEnumerator MatchDayTicketsBroadcastAndCrowd()
        {
            yield return new EnterPlayMode();
            Application.runInBackground = true;
            var loading = SceneManager.LoadSceneAsync("Assets/_Game/Generated/RoadsideHavenPrototype.unity"); while (!loading.isDone) yield return null;
            FarhanMatchDay match = null; float until = Time.realtimeSinceStartup + 40;
            while (Time.realtimeSinceStartup < until && (match == null || !match.Ready)) { var m = Object.FindFirstObjectByType<MotelGameRoot>(); if (m != null) match = m.GetComponent<FarhanMatchDay>(); yield return null; }
            Assert.IsNotNull(match, "Match day not installed"); Assert.IsTrue(match.Ready);
            var motel = match.GetComponent<MotelGameRoot>(); var arena = match.GetComponent<StadiumUpgrade>();
            Assert.GreaterOrEqual(match.Billboards.Count, 3, "No billboards");

            // Watch from the stands so the match is simulated, then kick off.
            var main = Camera.main; foreach (var b in main.GetComponents<MonoBehaviour>()) if (b.GetType().Namespace == "RoadsideHaven") b.enabled = false;
            main.transform.position = StadiumUpgrade.Centre + new Vector3(-55, 14, -10); main.transform.LookAt(StadiumUpgrade.Centre);
            match.StartMatchNow();
            Assert.AreEqual(FarhanMatchDay.Phase.FirstHalf, match.Current);
            yield return Seconds(1);
            Assert.IsTrue(match.Simulating, "Match is not simulated while watching");
            Assert.IsTrue(arena.FullHouse, "Seats are not full on match day");
            Assert.AreEqual(23, match.Footballers.Count(t => t.gameObject.activeInHierarchy), "22 players and a referee should be on the pitch");
            var players = match.Footballers.Select(t => t.position).ToArray();
            yield return Seconds(9);
            Assert.Greater(match.Minute, 1);
            Assert.Greater(match.Footballers.Zip(players, (t, p) => Vector3.Distance(t.position, p)).Average(), 2, "Players are not moving");
            Assert.Greater(Vector3.Distance(match.BallWorld, StadiumUpgrade.Centre), 1, "The ball never left the centre spot");
            Assert.Greater(match.FansActive + match.FansAdmitted, 0, "No fans arriving");
            Assert.AreSame(match.BroadcastMaterial, arena.ScreenPanels[0].sharedMaterial, "Big screen is not showing the broadcast");
            StringAssert.Contains(FarhanMatchDay.Home, match.BigScreenText);

            // A goal updates the score everywhere.
            int home = match.HomeGoals; match.ForceGoal(true);
            Assert.AreEqual(home + 1, match.HomeGoals); StringAssert.Contains("GOAL", match.Banner);

            // Buying a ticket at the kiosk and entering through a gate.
            var owner = motel.GetComponentInChildren<OwnerSandboxController>(true);
            owner.ExitVehicle(StadiumUpgrade.Kiosks[0] + new Vector3(1.5f, .2f, 0));
            yield return Seconds(.5f);
            if (motel.Coins < FarhanMatchDay.TicketPrice) motel.Credit(100);
            int coins = motel.Coins;
            Assert.IsTrue(match.TryBuyTicket(out var message), message);
            Assert.AreEqual(coins - FarhanMatchDay.TicketPrice, motel.Coins); Assert.IsTrue(match.HasTicket);
            Assert.IsFalse(match.TryBuyTicket(out _), "Bought a second ticket");
            var gate = arena.GatePoints[0];
            Assert.IsFalse(arena.GateOpen(0));
            Assert.IsTrue(match.ScanTicketAt(gate + new Vector3(-3, 0, 0)));
            yield return null;
            Assert.IsTrue(arena.GateOpen(0), "Gate did not open for a ticket holder");

            // Billboards cycle adverts, the schedule and the live score.
            var seen = new System.Collections.Generic.HashSet<string>();
            for (int i = 0; i < 6; i++) { seen.Add(match.Billboards[0].Title); yield return Seconds(7.1f); }
            Assert.IsTrue(seen.Contains("MATCH SCHEDULE"), "Schedule never shown: " + string.Join(", ", seen));
            Assert.IsTrue(seen.Any(s => s.StartsWith("LIVE")), "Live score never shown");
            Assert.GreaterOrEqual(seen.Count, 4);
            yield return Capture(match);
            yield return new ExitPlayMode();
        }

        private static IEnumerator Capture(FarhanMatchDay match)
        {
            var folder = System.Environment.GetEnvironmentVariable("STADIUM_SHOTS"); if (string.IsNullOrEmpty(folder)) yield break;
            var main = Camera.main; var cam = new GameObject("Match capture").AddComponent<Camera>(); cam.CopyFrom(main); cam.rect = new Rect(0, 0, 1, 1); cam.orthographic = false; cam.usePhysicalProperties = false; cam.fieldOfView = 55; main.enabled = false;
            var c = StadiumUpgrade.Centre;
            var views = new (string name, Vector3 pos, Vector3 look)[]
            {
                ("match_stand", c + new Vector3(-57, 7.5f, -15), match.BallWorld),
                ("match_aerial", c + new Vector3(-260, 150, -230), c),
                ("match_billboard", new Vector3(110, 8, -190), new Vector3(128, 10, -205)),
                ("match_plaza", new Vector3(120, 6, -240), new Vector3(160, 4, -250)),
            };
            foreach (var v in views)
            {
                cam.transform.position = v.pos; cam.transform.LookAt(v.look);
                for (int f = 0; f < 6; f++) yield return null;
                Save(cam, System.IO.Path.Combine(folder, v.name + ".png"));
            }
            var feed = match.BroadcastFeed; RenderTexture.active = feed; var tex = new Texture2D(feed.width, feed.height, TextureFormat.RGB24, false); tex.ReadPixels(new Rect(0, 0, feed.width, feed.height), 0, 0); tex.Apply();
            System.IO.File.WriteAllBytes(System.IO.Path.Combine(folder, "match_broadcast.png"), tex.EncodeToPNG()); RenderTexture.active = null; Object.Destroy(tex);
        }

        private static void Save(Camera cam, string path)
        {
            var rt = new RenderTexture(1280, 720, 24); cam.targetTexture = rt; cam.Render(); RenderTexture.active = rt;
            var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false); tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); tex.Apply();
            System.IO.File.WriteAllBytes(path, tex.EncodeToPNG()); cam.targetTexture = null; RenderTexture.active = null; Object.Destroy(rt); Object.Destroy(tex);
        }
    }
}
