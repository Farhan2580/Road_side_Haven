using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
namespace RoadsideHaven.Tests
{
 public sealed class CommonsDetailsTests
 {
  [UnityTest]public IEnumerator DenseGardensKeepWalkingAndParkingAccessibleAndVisitorsMove()
  {
   yield return new EnterPlayMode();var load=SceneManager.LoadSceneAsync("Assets/_Game/Generated/RoadsideHavenPrototype.unity");while(!load.isDone)yield return null;yield return null;yield return null;yield return null;
   var m=Object.FindFirstObjectByType<MotelGameRoot>();var area=m.transform.Find("Haven Commons Gardens and Street Details v1");Assert.IsNotNull(area);var life=area.GetComponent<CommonsDetailLife>();Assert.AreEqual(6,life.WalkerCount);var people=area.GetComponentsInChildren<TownCitizenAgent>();var before=new Vector3[people.Length];for(int i=0;i<people.Length;i++)before[i]=people[i].transform.position;
   var probe=new GameObject("Rose arcade walking probe");try{var cc=probe.AddComponent<CharacterController>();cc.height=1.8f;cc.center=Vector3.up*.9f;cc.radius=.25f;cc.skinWidth=.03f;cc.stepOffset=.22f;probe.transform.position=new Vector3(760.8f,.3f,-30);Physics.SyncTransforms();for(int i=0;i<600;i++)cc.Move(new Vector3(0,-.08f,-.1f));Assert.Less(probe.transform.position.z,-88,"Central rose arcade walkway blocked");Assert.Greater(probe.transform.position.y,-.05f);}finally{Object.DestroyImmediate(probe);}
   foreach(float z in new[]{-.5f,-25.5f})for(float x=624;x<960;x+=1)Clear(new Vector3(x,.95f,z),new Vector3(.24f,.65f,.24f));
   for(float x=615;x<=646;x+=1)Clear(new Vector3(x,.95f,-47),new Vector3(.4f,.65f,1.6f));for(float z=-75;z<=-38;z+=1)Clear(new Vector3(646,.95f,z),new Vector3(1.1f,.65f,1.7f));
   for(float x=650;x<=945;x+=2)foreach(float z in new[]{-100f,-94f,212f,217f})Clear(new Vector3(x,.95f,z),new Vector3(.24f,.65f,.24f));
   var cars=area.GetComponentsInChildren<EnterableVehicle>();Assert.AreEqual(2,cars.Length);var driving=m.GetComponentInChildren<WorldDrivingController>();foreach(var car in cars){Assert.IsTrue(driving.IsRegistered(car.GetComponent<Rigidbody>()));car.TakeControl();Assert.IsFalse(car.GetComponent<Rigidbody>().isKinematic);Assert.IsNotNull(car.GetComponent<ArcadeVehicleMotor>());car.GetComponent<Rigidbody>().isKinematic=true;}
   float until=Time.realtimeSinceStartup+4;while(Time.realtimeSinceStartup<until)yield return null;for(int i=0;i<people.Length;i++){Assert.Greater(Vector3.Distance(before[i],people[i].transform.position),.7f,"Detail visitor is stuck");Assert.Greater(people[i].transform.position.y,-.2f,"Visitor fell through garden paths");}Assert.AreEqual(0,area.GetComponentsInChildren<Light>(true).Length);
   LogAssert.NoUnexpectedReceived();yield return new ExitPlayMode();
  }
  private static void Clear(Vector3 p,Vector3 half){foreach(var c in Physics.OverlapBox(p,half))Assert.IsFalse(c.enabled&&!c.isTrigger&&c.GetComponentInParent<Rigidbody>()==null&&c.GetComponentInParent<CharacterController>()==null&&c.GetComponentInParent<NavMeshAgent>()==null&&c.bounds.max.y>.3f,"Reserved route blocked by "+c.name);}
 }
}
