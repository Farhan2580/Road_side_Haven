using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
namespace RoadsideHaven.Tests
{
    public sealed class ProjectRuntimeSafetyTests
    {
        [Test]
        public void SavedRoadMeshCanBeAssignedToNewComponent()
        {
            var root=new GameObject("Road initialization regression");var mesh=new Mesh();
            try
            {
                mesh.vertices=new[]{Vector3.zero,Vector3.right,Vector3.forward};mesh.triangles=new[]{0,2,1};
                var surface=root.AddComponent<RoadSurfacePhysics>();surface.UseSavedMesh(mesh);
                Assert.IsTrue(surface.HasSurface);Assert.AreSame(mesh,surface.SurfaceMesh);
            }
            finally{Object.DestroyImmediate(root);Object.DestroyImmediate(mesh);}
        }
        [Test]
        public void MissingTerrainDoesNotRemoveExistingGroundCollision()
        {
            var root=new GameObject("River without terrain");var ground=GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                ground.name="World foundation";ground.transform.SetParent(root.transform,false);
                new GameObject("Village City River v3").transform.SetParent(root.transform,false);
                RiverGroundPhysics.Apply(root.transform);
                Assert.IsTrue(ground.GetComponent<Collider>().enabled);
            }
            finally{Object.DestroyImmediate(root);}
        }
        [Test]
        public void HighwayCollisionHasNoOversizedTriangles()
        {
            var root=new GameObject("Highway tessellation");Mesh mesh=null;
            try
            {
                var road=GameObject.CreatePrimitive(PrimitiveType.Cube);road.name="Connected road";road.transform.SetParent(root.transform,false);road.transform.localScale=new Vector3(2000,.12f,12);
                mesh=RoadSurfacePhysics.Rebuild(root.transform).SurfaceMesh;var v=mesh.vertices;var t=mesh.triangles;
                for(int i=0;i<t.Length;i+=3)for(int edge=0;edge<3;edge++)Assert.Less(Vector3.Distance(v[t[i+edge]],v[t[i+(edge+1)%3]]),100);
            }
            finally{Object.DestroyImmediate(root);if(mesh!=null)Object.DestroyImmediate(mesh);}
        }
        [UnityTest]
        public IEnumerator MainSceneStartsAndRunsWithoutRuntimeErrors()
        {
            yield return new EnterPlayMode();
            Assert.IsTrue(Application.isPlaying); var load=SceneManager.LoadSceneAsync("Assets/_Game/Generated/RoadsideHavenPrototype.unity"); while(!load.isDone)yield return null;
            float deadline=Time.realtimeSinceStartup+12;while(Time.realtimeSinceStartup<deadline)yield return null;
            Assert.IsNotNull(Object.FindFirstObjectByType<MotelGameRoot>());
            Assert.IsNotNull(Camera.main);
            Assert.IsNotNull(Object.FindFirstObjectByType<ShopCustomerActor>(), "Smoke test must include the first shop customer.");
            Assert.AreEqual(0,RoadClearance.CountRoadOverlaps(Object.FindFirstObjectByType<MotelGameRoot>().transform),"Static object overlaps the road after startup.");
            LogAssert.NoUnexpectedReceived();
            yield return new ExitPlayMode();
        }
    }
}