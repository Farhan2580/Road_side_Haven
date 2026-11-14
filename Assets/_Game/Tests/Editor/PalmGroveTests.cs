using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
namespace RoadsideHaven.Tests
{
    public sealed class PalmGroveTests
    {
        private static bool StaticObstacle(Collider c)=>c.enabled&&c.gameObject.activeInHierarchy&&!c.isTrigger&&c.GetComponentInParent<Rigidbody>()==null&&c.GetComponentInParent<CharacterController>()==null&&c.GetComponentInParent<NavMeshAgent>()==null&&c.bounds.max.y>.3f;
        [UnityTest]
        public IEnumerator NewResidentialRoadStaysLevelAndSidewalksRemainClear()
        {
            yield return new EnterPlayMode();var load=SceneManager.LoadSceneAsync("Assets/_Game/Generated/RoadsideHavenPrototype.unity");while(!load.isDone)yield return null;yield return null;yield return null;
            var motel=Object.FindFirstObjectByType<MotelGameRoot>();Assert.IsNotNull(motel.transform.Find("Palm Grove Residences v1"));var surface=motel.GetComponent<RoadSurfacePhysics>();Assert.IsTrue(surface.HasSurface);
            for(float x=180;x<338;x+=2)
            {
                bool found=false;foreach(var hit in Physics.RaycastAll(new Vector3(x,2,90),Vector3.down,4))if(hit.collider is MeshCollider mesh&&mesh.sharedMesh==surface.SurfaceMesh){Assert.AreEqual(.06f,hit.point.y,.005f);Assert.Greater(hit.normal.y,.99f);found=true;}Assert.IsTrue(found,"Driving surface gap at "+x);
            }
            foreach(float z in new[]{84.2f,95.8f})for(float x=244;x<338;x+=1)foreach(var c in Physics.OverlapCapsule(new Vector3(x,.5f,z),new Vector3(x,1.65f,z),.25f))Assert.IsFalse(StaticObstacle(c),"Sidewalk blocked: "+c.name+" at "+x+","+z);
            for(float z=69;z<=84;z+=.5f)foreach(var c in Physics.OverlapCapsule(new Vector3(323.5f,.5f,z),new Vector3(323.5f,1.65f,z),.25f))Assert.IsFalse(StaticObstacle(c),"Playground entrance blocked: "+c.name);
            LogAssert.NoUnexpectedReceived();yield return new ExitPlayMode();
        }
    }
}