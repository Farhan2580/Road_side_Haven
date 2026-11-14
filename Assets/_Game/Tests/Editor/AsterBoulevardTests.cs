using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
namespace RoadsideHaven.Tests
{
 public sealed class AsterBoulevardTests
 {
  [UnityTest]public IEnumerator NewMarketRoadsAreFlatClearConnectedAndPedestriansCanCross()
  {
   yield return new EnterPlayMode();var load=SceneManager.LoadSceneAsync("Assets/_Game/Generated/RoadsideHavenPrototype.unity");while(!load.isDone)yield return null;yield return null;yield return null;yield return null;
   var motel=Object.FindFirstObjectByType<MotelGameRoot>();Assert.IsNotNull(motel.transform.Find("Aster Quarter Connected Boulevard v1"));var surface=motel.GetComponent<RoadSurfacePhysics>();Assert.IsTrue(surface.HasSurface);
   var meshCollider=surface.GetComponentInChildren<MeshCollider>(); // Use the explicit welded collider, not random grass below the road.
   foreach(var c in motel.GetComponentsInChildren<MeshCollider>())if(c.sharedMesh==surface.SurfaceMesh){meshCollider=c;break;}
   for(float z=-1;z<=131;z+=2)CheckRoad(meshCollider,new Vector3(348,0,z),new Vector3(3.55f,.65f,.4f));
   for(float x=193;x<=347;x+=2)CheckRoad(meshCollider,new Vector3(x,0,132),new Vector3(.4f,.65f,3.55f));
   var graph=new TownRoadNetwork();foreach(var r in motel.GetComponentsInChildren<MeshRenderer>())if(TownPopulationController.RoadName(r.name)&&r.GetComponentInParent<Rigidbody>()==null)graph.Add(r.bounds.center,r.bounds.size);graph.Build(null);Assert.IsNotEmpty(graph.Route(new Vector3(0,0,-13),new Vector3(270,0,132)));
   var probe=new GameObject("Aster crossing movement probe");try{var cc=probe.AddComponent<CharacterController>();cc.height=1.8f;cc.center=Vector3.up*.9f;cc.radius=.25f;cc.skinWidth=.03f;cc.stepOffset=.22f;probe.transform.position=new Vector3(270,.3f,120);Physics.SyncTransforms();for(int i=0;i<260;i++)cc.Move(new Vector3(0,-.1f,.1f));Assert.Greater(probe.transform.position.z,144,"The single pedestrian crossing must remain passable");Assert.Greater(probe.transform.position.y,-.05f);}finally{Object.DestroyImmediate(probe);}
   LogAssert.NoUnexpectedReceived();yield return new ExitPlayMode();
  }
  private static void CheckRoad(MeshCollider surface,Vector3 p,Vector3 half){Assert.IsTrue(surface.Raycast(new Ray(p+Vector3.up*2,Vector3.down),out var hit,3));Assert.AreEqual(.06f,hit.point.y,.005f,"Raised seam in driving surface");foreach(var c in Physics.OverlapBox(p+Vector3.up*.95f,half))Assert.IsFalse(c.enabled&&!c.isTrigger&&c.GetComponentInParent<Rigidbody>()==null&&c.GetComponentInParent<CharacterController>()==null&&c.GetComponentInParent<NavMeshAgent>()==null&&c.bounds.max.y>.3f,"Obstacle on new road: "+c.name);}
 }
}
