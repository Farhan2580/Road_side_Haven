using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
namespace RoadsideHaven.Tests
{
    public sealed class TreeUpgradeTests
    {
        private static IEnumerator Wait(float s) { float u = Time.realtimeSinceStartup + s; while (Time.realtimeSinceStartup < u) yield return null; }

        [Test] public void GeneratedTreesHaveBarkFoliageAndCutOutLeaves()
        {
            foreach (TreeSpecies s in System.Enum.GetValues(typeof(TreeSpecies)))
                for (int lod = 0; lod < 2; lod++)
                {
                    var mesh = TreeArt.Get(s, 1, lod);
                    Assert.AreEqual(2, mesh.subMeshCount, s + " submeshes");
                    Assert.Greater(mesh.GetIndexCount(0), 0, s + " has no bark");
                    Assert.Greater(mesh.GetIndexCount(1), 0, s + " has no foliage");
                    Assert.Less(mesh.vertexCount, lod == 0 ? 16000 : 5000, s + " LOD" + lod + " too heavy");
                    Assert.Greater(mesh.bounds.size.y, TreeArt.CanonicalHeight(s) * .8f, s + " height");
                }
            Assert.Less(TreeArt.Get(TreeSpecies.Broadleaf, 0, 1).vertexCount, TreeArt.Get(TreeSpecies.Broadleaf, 0, 0).vertexCount * .6f, "LOD1 is not lighter");
            var mats = TreeArt.Materials(TreeSpecies.Broadleaf);
            Assert.AreEqual("RoadsideHaven/Foliage Lit", mats[1].shader.name);
            Assert.AreEqual(1, mats[1].GetFloat("_Leaf")); Assert.AreEqual(0, mats[0].GetFloat("_Leaf"));
            var tex = TreeArt.LeafAtlas; Assert.AreEqual(512, tex.width); Assert.Greater(tex.mipmapCount, 4);
        }

        [UnityTest] public IEnumerator SceneTreesAreReplacedWithRealisticTrees()
        {
            yield return new EnterPlayMode();
            Application.runInBackground = true;
            var loading = SceneManager.LoadSceneAsync("Assets/_Game/Generated/RoadsideHavenPrototype.unity"); while (!loading.isDone) yield return null;
            float until = Time.realtimeSinceStartup + 20; TreeUpgrade upgrade = null;
            while (Time.realtimeSinceStartup < until && (upgrade == null || !upgrade.Done)) { var m = Object.FindFirstObjectByType<MotelGameRoot>(); if (m != null) upgrade = m.GetComponent<TreeUpgrade>(); yield return null; }
            Assert.IsNotNull(upgrade, "Tree upgrade not installed"); Assert.IsTrue(upgrade.Done);
            var motel = upgrade.GetComponent<MotelGameRoot>();
            Assert.GreaterOrEqual(upgrade.Trees.Count, 500, "Too few trees upgraded");
            foreach (TreeSpecies s in System.Enum.GetValues(typeof(TreeSpecies))) Assert.IsTrue(upgrade.Trees.Any(t => t.Species == s), "No " + s + " trees");
            Assert.Greater(upgrade.PalmsFromFoliage, 20, "District palms were not upgraded");
            // No primitive tree placeholders remain visible.
            foreach (var r in motel.GetComponentsInChildren<MeshRenderer>())
            {
                if (r.GetComponent<MeshFilter>() != null && r.GetComponent<MeshFilter>().sharedMesh == TreeUpgrade.HiddenMesh) continue;
                if (!r.enabled || r.GetComponentInParent<LODGroup>() is LODGroup g && g.name.StartsWith("Realistic")) continue;
                string n = r.name.ToLowerInvariant();
                if ((n.Contains("palm foliage") || n.Contains("palm frond")) && (r.GetComponent<MeshFilter>().sharedMesh.name.EndsWith("without palms") || r.bounds.size.y < 2.5f)) continue; // shrubs sharing the palm material remain
                Assert.IsFalse(n.StartsWith("tree crown") || n == "tree trunk" || n.Contains("palm foliage") || n.Contains("palm frond"), "Old tree still visible: " + r.name + " in " + r.transform.parent.name);
            }
            foreach (var t in upgrade.Trees)
            {
                Assert.Greater(t.Height, 2.5f); Assert.Less(t.Height, 22f, t.Tree.name + " is absurdly tall");
                Assert.IsTrue(t.Tree.GetComponent<LODGroup>().lodCount == 2);
            }
            var wind = Shader.GetGlobalVector("_TreeWind"); Assert.Greater(wind.z, 0, "Trees get no wind");
            yield return Capture(upgrade);
            LogAssert.NoUnexpectedReceived();
            yield return new ExitPlayMode();
        }

        private static IEnumerator Capture(TreeUpgrade upgrade)
        {
            var folder = System.Environment.GetEnvironmentVariable("TREE_SHOTS"); if (string.IsNullOrEmpty(folder)) yield break;
            var main = Camera.main; var camera = new GameObject("Tree capture camera").AddComponent<Camera>(); camera.CopyFrom(main); camera.fieldOfView = 50; camera.nearClipPlane = .2f; main.enabled = false;
            int shot = 0;
            foreach (var s in new[] { TreeSpecies.Broadleaf, TreeSpecies.Palm, TreeSpecies.Pine, TreeSpecies.Blossom })
            {
                var tree = upgrade.Trees.Where(t => t.Species == s).OrderBy(t => Physics.CheckSphere(t.Base + Vector3.up * t.Height * .5f + new Vector3(t.Height * .75f, t.Height * .05f, -t.Height * .95f), 1.5f) ? 1 : 0).First();
                var target = tree.Base + Vector3.up * tree.Height * .5f;
                camera.transform.position = target + new Vector3(tree.Height * .75f, tree.Height * .05f, -tree.Height * .95f);
                camera.transform.LookAt(target);
                for (int f = 0; f < 6; f++) yield return null;
                var rt = new RenderTexture(1280, 720, 24); camera.targetTexture = rt; camera.Render();
                RenderTexture.active = rt; var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false); tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); tex.Apply();
                System.IO.File.WriteAllBytes(System.IO.Path.Combine(folder, "tree" + shot++ + "_" + s + ".png"), tex.EncodeToPNG());
                camera.targetTexture = null; RenderTexture.active = null; Object.Destroy(rt); Object.Destroy(tex);
            }
        }
    }
}
