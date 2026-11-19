using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
namespace RoadsideHaven.Tests
{
    public sealed class HavenExpansionTests
    {
        private static IEnumerator Wait(float s) { float u = Time.realtimeSinceStartup + s; while (Time.realtimeSinceStartup < u) yield return null; }
        private static bool Obstacle(Collider c) => c.enabled && !c.isTrigger && c.gameObject.activeInHierarchy && c.bounds.max.y > .35f && c.GetComponentInParent<CharacterController>() == null && c.GetComponentInParent<NavMeshAgent>() == null && (c.attachedRigidbody == null || c.attachedRigidbody.isKinematic && c.GetComponentInParent<TownCitizenAgent>() == null);

        [UnityTest] public IEnumerator NewDistrictsStayOffRoadsHaveClearWalksAnimateAndKeepTheLakeFreeOfGrass()
        {
            yield return new EnterPlayMode();
            // Lambdas below capture locals; run them in a fresh iterator created after the play-mode domain reload,
            // otherwise the compiler-generated closure object from before the reload is lost.
            yield return Body();
            yield return new ExitPlayMode();
        }

        private static IEnumerator Body()
        {
            Application.runInBackground = true;
            var loading = SceneManager.LoadSceneAsync("Assets/_Game/Generated/RoadsideHavenPrototype.unity"); while (!loading.isDone) yield return null;
            // In a full run the previous test's teardown can delay the load; wait for the world itself.
            float until = Time.realtimeSinceStartup + 15;
            while (Time.realtimeSinceStartup < until && Object.FindFirstObjectByType<MotelGameRoot>() == null) yield return null;
            yield return Wait(3);
            var motel = Object.FindFirstObjectByType<MotelGameRoot>();
            Assert.IsNotNull(motel, "Game world did not load");
            var districts = new[] { HavenExpansionLayout.Lakeside, HavenExpansionLayout.Sports, HavenExpansionLayout.Farm }.Select(n => motel.transform.Find(n)).ToArray();
            foreach (var d in districts) Assert.IsNotNull(d, "A new district is missing");
            Physics.SyncTransforms();
            var roads = motel.GetComponentsInChildren<MeshRenderer>().Where(r => r.gameObject.activeInHierarchy && TownPopulationController.RoadName(r.name)).Select(r => Rect.MinMaxRect(r.bounds.min.x, r.bounds.min.z, r.bounds.max.x, r.bounds.max.z)).ToArray();
            foreach (var d in districts)
            {
                Assert.Less(d.GetComponentsInChildren<MeshRenderer>().Length, 260, d.name + " draw-call budget exceeded");
                Assert.Less(d.GetComponentsInChildren<MeshFilter>().Where(f => f.sharedMesh != null).Sum(f => f.sharedMesh.vertexCount), 450000, d.name + " geometry budget exceeded");
                Assert.AreEqual(0, d.GetComponentsInChildren<Light>().Length, d.name + " added realtime lights");
                foreach (var f in d.GetComponentsInChildren<MeshFilter>()) Assert.IsNotNull(f.sharedMesh, "Missing saved mesh in " + d.name + ": " + f.name);
                foreach (var c in d.GetComponentsInChildren<Collider>())
                {
                    if (!c.enabled || c.isTrigger) continue; var b = c.bounds; var rect = Rect.MinMaxRect(b.min.x, b.min.z, b.max.x, b.max.z);
                    Assert.IsFalse(roads.Any(r => r.Overlaps(rect)), d.name + " object occupies a public road: " + c.name + " " + rect);
                }
            }

            // Walking loops: the lake promenade and the running track are paved and unobstructed.
            foreach (var route in new[] { HavenExpansionLayout.LakeLoop(), HavenExpansionLayout.TrackLoop() })
                for (int leg = 1; leg < route.Length; leg++)
                {
                    float length = Vector3.Distance(route[leg - 1], route[leg]);
                    for (float s = 0; s <= length; s += 1f)
                    {
                        var p = Vector3.Lerp(route[leg - 1], route[leg], s / length);
                        foreach (var c in Physics.OverlapCapsule(p + Vector3.up * .45f, p + Vector3.up * 1.5f, .22f)) Assert.IsFalse(Obstacle(c), "Walking loop blocked by " + c.name + " at " + p);
                        Assert.IsTrue(Physics.Raycast(p + Vector3.up * .5f, Vector3.down, out var hit, 1f) && districts.Any(d => hit.collider.transform.IsChildOf(d)), "Walking loop has no paving at " + p);
                    }
                }
            // The pier is walkable from the promenade.
            var c0 = HavenExpansionLayout.LakeCentre; float pierX = HavenExpansionLayout.LakeOutline(0, 1.03f).x - 15;
            Assert.IsTrue(Physics.Raycast(new Vector3(pierX, 3, c0.z), Vector3.down, out var deck, 5) && deck.point.y > .25f, "Pier deck is not solid");

            // Life: the windmill turns, boats bob and people use the loops.
            var farmLife = districts[2].GetComponent<HavenExpansionLife>(); var lakeLife = districts[0].GetComponent<HavenExpansionLife>();
            Assert.IsNotNull(farmLife.FirstSpinner); Assert.IsNotNull(lakeLife.FirstBobber);
            var spin = farmLife.FirstSpinner.localRotation; var bob = lakeLife.FirstBobber.localPosition;
            var camera = Camera.main; foreach (var b in camera.GetComponents<MonoBehaviour>()) if (b.GetType().Namespace == "RoadsideHaven") b.enabled = false;
            camera.transform.position = new Vector3(-200, 30, 0);
            yield return Wait(1);
            Assert.Greater(Quaternion.Angle(spin, farmLife.FirstSpinner.localRotation), 1, "Windmill is not turning");
            Assert.Greater((bob - lakeLife.FirstBobber.localPosition).magnitude, .001f, "Boats are not bobbing");
            Assert.GreaterOrEqual(lakeLife.WalkerCount + districts[1].GetComponent<HavenExpansionLife>().WalkerCount, 4);

            // Grass never grows in the lake.
            var grass = motel.GetComponent<GrassField>();
            if (grass != null && grass.Ready)
            {
                grass.GenerateAround(c0, 110);
                Assert.Greater(grass.InstanceCount, 500, "No grass around the lake park");
                foreach (var p in grass.InstancePositions()) Assert.IsFalse(HavenExpansionLayout.InLake(new Vector2(p.x, p.z), 1.04f), "Grass grows in the lake at " + p);
            }

            // Sound: the soundscape installed every clip and plays daytime ambience.
            var audio = motel.GetComponent<WorldAudio>();
            Assert.IsNotNull(audio, "World audio was not installed");
            foreach (var clip in audio.AllClips) Assert.Greater(ProceduralAudio.Rms(clip), .01f, "Silent clip: " + clip.name);
            Assert.Greater(audio.AllClips.Count(), 15);
            LogAssert.NoUnexpectedReceived();
        }
    }
}
