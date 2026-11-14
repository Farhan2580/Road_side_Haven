using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
namespace RoadsideHaven.Tests
{
    public sealed class MainRoadFrontageTests
    {
        private static bool StaticObstacle(Collider c)=>c.enabled&&c.gameObject.activeInHierarchy&&!c.isTrigger&&c.GetComponentInParent<Rigidbody>()==null&&c.GetComponentInParent<CharacterController>()==null&&c.GetComponentInParent<NavMeshAgent>()==null&&c.bounds.max.y>.3f;
        [UnityTest]
        public IEnumerator BothSidewalksAndDrivewaysStayClearAndDisplayCarsCanBeTakenOver()
        {
            yield return new EnterPlayMode();var load=SceneManager.LoadSceneAsync("Assets/_Game/Generated/RoadsideHavenPrototype.unity");while(!load.isDone)yield return null;yield return null;yield return null;
            var motel=Object.FindFirstObjectByType<MotelGameRoot>();var area=motel.transform.Find("Main Road Axis and Luma Frontage v1");Assert.IsNotNull(area);
            foreach(float z in new[]{-.5f,-25.5f})for(float x=258;x<339;x+=1)foreach(var c in Physics.OverlapCapsule(new Vector3(x,.52f,z),new Vector3(x,1.65f,z),.25f))Assert.IsFalse(StaticObstacle(c),"Public sidewalk blocked: "+c.name);
            foreach(var lane in new[]{new Vector3(270,0,9),new Vector3(330,0,9),new Vector3(330,-37,-24.1f)})for(float z=lane.y;z<=lane.z;z+=1)foreach(var c in Physics.OverlapBox(new Vector3(lane.x,.95f,z),new Vector3(.95f,.65f,1.7f)))Assert.IsFalse(StaticObstacle(c),"Driveway blocked: "+c.name);
            var driving=motel.GetComponentInChildren<WorldDrivingController>(true);var cars=area.GetComponentsInChildren<EnterableVehicle>();Assert.AreEqual(2,cars.Length);
            foreach(var car in cars){var body=car.GetComponent<Rigidbody>();Assert.IsTrue(driving.IsRegistered(body));Assert.IsNotNull(car.GetComponent<ModernCarVisuals>());car.TakeControl();Assert.IsFalse(body.isKinematic);Assert.IsNotNull(car.GetComponent<ArcadeVehicleMotor>());body.isKinematic=true;}
            LogAssert.NoUnexpectedReceived();yield return new ExitPlayMode();
        }
    }
}