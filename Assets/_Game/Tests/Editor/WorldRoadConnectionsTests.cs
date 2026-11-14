using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
namespace RoadsideHaven.Tests
{
 public sealed class WorldRoadConnectionsTests
 {
  [UnityTest] public IEnumerator EveryPublicRoadRemainsConnectedAfterRuntimeSetup()
  {
   yield return new EnterPlayMode();var load=SceneManager.LoadSceneAsync("Assets/_Game/Generated/RoadsideHavenPrototype.unity");while(!load.isDone)yield return null;yield return null;yield return null;yield return null;
   var motel=Object.FindFirstObjectByType<MotelGameRoot>();Assert.IsNotNull(motel.transform.Find("Axis Luma Detail Pass v2"));Assert.IsNotNull(motel.transform.Find("Audited Public Road Connections v1"));
   var graph=new TownRoadNetwork();int airport=0;
   foreach(var r in motel.GetComponentsInChildren<MeshRenderer>())if(r.gameObject.activeInHierarchy&&TownPopulationController.RoadName(r.name)&&r.GetComponentInParent<Rigidbody>()==null){graph.Add(r.bounds.center,r.bounds.size);if(r.name=="Airport access road")airport++;}
   Assert.AreEqual(3,airport,"Airport public approaches must be included, airside excluded");graph.Build(null);Assert.Greater(graph.Roads.Count,40);
   foreach(var road in graph.Roads)Assert.IsNotEmpty(graph.Route(new Vector3(0,.08f,-13),(road.A+road.B)*.5f),"Disconnected road: "+road.A+" -> "+road.B);
   LogAssert.NoUnexpectedReceived();yield return new ExitPlayMode();
  }
 }
}
