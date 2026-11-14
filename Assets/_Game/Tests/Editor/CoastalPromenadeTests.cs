using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
namespace RoadsideHaven.Tests
{
    public sealed class CoastalPromenadeTests
    {
        private static bool StaticObstacle(Collider collider)
        {
            return collider.gameObject.activeInHierarchy&&!collider.isTrigger&&collider.GetComponentInParent<Rigidbody>()==null&&collider.GetComponentInParent<CharacterController>()==null&&collider.GetComponentInParent<NavMeshAgent>()==null&&collider.bounds.max.y>.3f;
        }
        [UnityTest]
        public IEnumerator RuntimeRepairsKeepWalkwayAndParkingEntranceClear()
        {
            yield return new EnterPlayMode();
            var load=SceneManager.LoadSceneAsync("Assets/_Game/Generated/RoadsideHavenPrototype.unity");while(!load.isDone)yield return null;
            yield return null;yield return null;Physics.SyncTransforms();
            var motel=Object.FindFirstObjectByType<MotelGameRoot>();Assert.IsNotNull(motel.transform.Find("Coastal Promenade v1"));
            for(float x=-22;x<=168;x+=.5f)
                foreach(var c in Physics.OverlapCapsule(new Vector3(x,.52f,-.5f),new Vector3(x,1.65f,-.5f),.24f))
                    Assert.IsFalse(StaticObstacle(c),"Static sidewalk obstruction at "+x+": "+c.name);
            for(float z=-1;z<=27;z+=.5f)
                foreach(var c in Physics.OverlapBox(new Vector3(118,.9f,z),new Vector3(.95f,.6f,2.15f)))
                    Assert.IsFalse(StaticObstacle(c),"Parking access blocked at "+z+": "+c.name);
            LogAssert.NoUnexpectedReceived();yield return new ExitPlayMode();
        }
    }
}