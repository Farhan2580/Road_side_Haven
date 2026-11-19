using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
namespace RoadsideHaven.Tests
{
    public sealed class StadiumUpgradeTests
    {
        private static IEnumerator Seconds(float s) { float u = Time.time + s; while (Time.time < u) yield return null; }

        private static Collider TopCollider(Vector3 p, out float y)
        {
            y = float.MinValue; Collider best = null;
            foreach (var h in Physics.RaycastAll(p + Vector3.up * 5, Vector3.down, 10, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                if (h.point.y > y && h.collider.attachedRigidbody == null && h.collider.GetComponentInParent<CharacterController>() == null) { y = h.point.y; best = h.collider; }
            return best;
        }

        [UnityTest] public IEnumerator ArenaRoadAndPracticePitchAreBuilt()
        {
            yield return new EnterPlayMode();
            Application.runInBackground = true;
            var loading = SceneManager.LoadSceneAsync("Assets/_Game/Generated/RoadsideHavenPrototype.unity"); while (!loading.isDone) yield return null;
            StadiumUpgrade arena = null; float until = Time.realtimeSinceStartup + 30;
            while (Time.realtimeSinceStartup < until && (arena == null || !arena.Done)) { var m = Object.FindFirstObjectByType<MotelGameRoot>(); if (m != null) arena = m.GetComponent<StadiumUpgrade>(); yield return null; }
            Assert.IsNotNull(arena, "Stadium upgrade not installed"); Assert.IsTrue(arena.Done); Assert.IsNotNull(arena.Stadium, "Arena not built");
            Assert.Greater(arena.StrippedTriangles, 500, "Old grandstands were not removed");
            var bowl = arena.Stadium.Find("Stadium seating bowl"); Assert.IsNotNull(bowl); Assert.IsNotNull(bowl.GetComponent<MeshCollider>());
            Assert.Greater(arena.Stadium.Find("Stadium seats and fans").GetComponent<MeshFilter>().sharedMesh.vertexCount, 4000, "Too few seat rows");
            Assert.IsNotNull(arena.Stadium.Find("Stadium ring roof")); Assert.IsNotNull(arena.Stadium.Find("Stadium pitch markings"));
            Assert.GreaterOrEqual(arena.Floodlights.Count, 12, "Arena and practice floodlights missing");
            Physics.SyncTransforms();

            // A continuous road surface from the car park to the arena plaza, with no steps a car would bounce on.
            var path = new System.Collections.Generic.List<Vector3>();
            for (float z = -148; z > -262; z -= .5f) path.Add(new Vector3(112, 0, z));
            for (float x = 112; x < 152; x += .5f) path.Add(new Vector3(x, 0, -262));
            float last = float.NaN;
            foreach (var p in path)
            {
                var c = TopCollider(p, out float y); Assert.IsNotNull(c, "No surface at " + p);
                string n = c.name.ToLowerInvariant();
                Assert.IsTrue(n.Contains("stadium access road") || n.Contains("car park") || n.Contains("plaza") || n.Contains("walk"), "Road blocked or broken at " + p + " by " + c.name);
                if (!float.IsNaN(last)) Assert.Less(Mathf.Abs(y - last), .045f, "Step in the road at " + p);
                last = y;
            }
            // The corner entrances are closed by gates; once a gate opens they lead from the plaza onto the track.
            Assert.AreEqual(4, arena.GatePoints.Count);
            Assert.IsTrue(Physics.Linecast(new Vector3(150, 1.1f, StadiumUpgrade.Centre.z - StadiumUpgrade.Straight + .65f), new Vector3(StadiumUpgrade.Centre.x - HavenExpansionLayout.TrackOuter + 1, 1.1f, StadiumUpgrade.Centre.z - StadiumUpgrade.Straight + .65f)), "Gate does not stop people without a ticket");
            for (int g = 0; g < 4; g++) arena.OpenGate(g, 60);
            yield return Seconds(2.5f);
            foreach (float z in new[] { StadiumUpgrade.Centre.z - StadiumUpgrade.Straight, StadiumUpgrade.Centre.z + StadiumUpgrade.Straight })
            {
                var from = new Vector3(150, 1.1f, z + .65f); var to = new Vector3(StadiumUpgrade.Centre.x - HavenExpansionLayout.TrackOuter + 1, 1.1f, z + .65f);
                Assert.IsFalse(Physics.Linecast(from, to, out var hit, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) && hit.collider.attachedRigidbody == null, "Arena gate blocked at z " + z + (hit.collider != null ? " by " + hit.collider.name : ""));
            }
            // Clearing the road and plaza never leaves holes in the ground beside them.
            foreach (var p in new[] { new Vector3(122, 0, -240), new Vector3(105, 0, -200), new Vector3(130, 0, -180), new Vector3(92, 0, -270) })
                Assert.IsTrue(Physics.Raycast(p + Vector3.up * 10, Vector3.down, 20), "Hole in the ground at " + p);
            // Practice pitch: fenced, players training inside.
            Assert.IsNotNull(arena.Practice);
            var turf = TopCollider(new Vector3(StadiumUpgrade.PracticePitch.center.x, 0, StadiumUpgrade.PracticePitch.center.y), out _);
            Assert.AreEqual("Practice synthetic pitch surface", turf.name);
            Assert.AreEqual(6, arena.Players.Count);
            var starts = arena.Players.Select(a => a.transform.position).ToArray();
            yield return Seconds(6);
            for (int i = 0; i < arena.Players.Count; i++)
            {
                var pos = arena.Players[i].transform.position;
                Assert.IsTrue(StadiumUpgrade.PracticeFence.Contains(new Vector2(pos.x, pos.z)), "Player left the practice pitch");
                Assert.Greater(Vector3.Distance(pos, starts[i]), 1, "Player is not training");
            }
            // Errors fail the test automatically; a glyph warning from another district's animated sign is not ours to assert on.
            yield return Capture(arena);
            yield return new ExitPlayMode();
        }

        private static IEnumerator Capture(StadiumUpgrade arena)
        {
            var folder = System.Environment.GetEnvironmentVariable("STADIUM_SHOTS"); if (string.IsNullOrEmpty(folder)) yield break;
            var main = Camera.main; var cam = new GameObject("Stadium capture").AddComponent<Camera>(); cam.CopyFrom(main); cam.rect = new Rect(0, 0, 1, 1); cam.orthographic = false; cam.usePhysicalProperties = false; cam.fieldOfView = 55; main.enabled = false;
            var c = StadiumUpgrade.Centre;
            var views = new (string name, Vector3 pos, Vector3 look)[]
            {
                ("aerial", c + new Vector3(-150, 95, -120), c),
                ("inside", c + new Vector3(-30, 9, -40), c + new Vector3(20, 2, 30)),
                ("stands", c + new Vector3(0, 2, 0), c + new Vector3(60, 8, 10)),
                ("road", new Vector3(122, 6, -150), new Vector3(112, 0, -230)),
                ("plaza", new Vector3(125, 7, -300), new Vector3(170, 8, -230)),
                ("practice", new Vector3(100, 14, -300), new Vector3(63, 0, -250)),
            };
            foreach (var v in views)
            {
                cam.transform.position = v.pos; cam.transform.LookAt(v.look);
                for (int f = 0; f < 4; f++) yield return null;
                var rt = new RenderTexture(1280, 720, 24); cam.targetTexture = rt; cam.Render(); RenderTexture.active = rt;
                var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false); tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); tex.Apply();
                System.IO.File.WriteAllBytes(System.IO.Path.Combine(folder, v.name + ".png"), tex.EncodeToPNG());
                cam.targetTexture = null; RenderTexture.active = null; Object.Destroy(rt); Object.Destroy(tex);
            }
        }
    }
}
