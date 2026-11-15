using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
namespace RoadsideHaven.Tests
{
 public sealed class GrandBoulevardTests
 {
  [UnityTest] public IEnumerator EntrancesOpenForTheOwnerAndCirculationStaysClear()
  {
   yield return new EnterPlayMode();var load=SceneManager.LoadSceneAsync("Assets/_Game/Generated/RoadsideHavenPrototype.unity");while(!load.isDone)yield return null;yield return null;yield return null;yield return null;
   var motel=Object.FindFirstObjectByType<MotelGameRoot>();var district=motel.transform.Find("Grand Boulevard Hotel and Galleria v1");Assert.IsNotNull(district);var doors=district.GetComponentsInChildren<DistrictSlidingDoor>();Assert.AreEqual(4,doors.Length);
   var owner=Object.FindFirstObjectByType<OwnerSandboxController>();Assert.IsNotNull(owner);if(!owner.IsWalking)owner.ToggleWalking();Assert.IsTrue(owner.IsWalking);var controller=owner.GetComponent<CharacterController>();
   foreach(var door in doors)
   {
    controller.enabled=false;owner.transform.position=door.transform.TransformPoint(new Vector3(0,.2f,-2));controller.enabled=true;Physics.SyncTransforms();float until=Time.realtimeSinceStartup+3;while(door.OpenAmount<.99f&&Time.realtimeSinceStartup<until)yield return null;Assert.GreaterOrEqual(door.OpenAmount,.99f,"Entrance did not open for the owner");
    var probe=new GameObject("Physical lobby entrance probe");try{var cc=probe.AddComponent<CharacterController>();cc.height=1.8f;cc.center=Vector3.up*.9f;cc.radius=.28f;cc.skinWidth=.03f;cc.stepOffset=.22f;probe.transform.position=door.transform.TransformPoint(new Vector3(.8f,.25f,-1.6f));Physics.SyncTransforms();for(int i=0;i<90;i++)cc.Move(door.transform.forward*.1f-Vector3.up*.08f);Assert.Greater(door.transform.InverseTransformPoint(probe.transform.position).z,6,"Cannot walk through opening into the interior");}finally{Object.DestroyImmediate(probe);}
   }
   controller.enabled=false;owner.transform.position=new Vector3(0,.3f,-5);controller.enabled=true;float closeUntil=Time.realtimeSinceStartup+3;bool closing=true;while(closing&&Time.realtimeSinceStartup<closeUntil){closing=false;foreach(var door in doors)if(door.OpenAmount>=.05f)closing=true;yield return null;}foreach(var door in doors)Assert.Less(door.OpenAmount,.05f,"Door should close after player leaves");
   foreach(float z in new[]{-.5f,-25.5f})for(float x=445;x<579;x+=1)Clear(new Vector3(x,.95f,z),new Vector3(.24f,.65f,.24f));
   foreach(float x in new[]{458f,554f})for(float z=-1;z<21;z+=1)Clear(new Vector3(x,.95f,z),new Vector3(1.05f,.65f,1.7f));
   for(float x=458;x<555;x+=1)Clear(new Vector3(x,.95f,18),new Vector3(1.7f,.65f,1.05f));
   foreach(float x in new[]{447.5f,570f})for(float z=-76;z< -24.1f;z+=1)Clear(new Vector3(x,.95f,z),new Vector3(1.05f,.65f,1.7f));
   Assert.AreEqual(0,district.GetComponentsInChildren<Light>(true).Length,"New district must share the existing night light pool");LogAssert.NoUnexpectedReceived();yield return new ExitPlayMode();
  }
  private static void Clear(Vector3 p,Vector3 half){foreach(var c in Physics.OverlapBox(p,half))Assert.IsFalse(c.enabled&&!c.isTrigger&&c.GetComponentInParent<Rigidbody>()==null&&c.GetComponentInParent<CharacterController>()==null&&c.GetComponentInParent<NavMeshAgent>()==null&&c.bounds.max.y>.3f,"Circulation obstruction: "+c.name);}
 }
}
