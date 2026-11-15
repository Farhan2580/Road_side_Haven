using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
namespace RoadsideHaven.Tests
{
 public sealed class HavenCommonsTests
 {
  [UnityTest]public IEnumerator FactoriesAreGoneAndTheNewParkIsAccessibleAndAnimated()
  {
   yield return new EnterPlayMode();var load=SceneManager.LoadSceneAsync("Assets/_Game/Generated/RoadsideHavenPrototype.unity");while(!load.isDone)yield return null;yield return null;yield return null;yield return null;
   var m=Object.FindFirstObjectByType<MotelGameRoot>();var area=m.transform.Find("Haven Commons Creative Park v1");Assert.IsNotNull(area);foreach(var r in m.GetComponentsInChildren<MeshRenderer>())Assert.IsFalse(r.name=="Warehouse"||r.name=="Factory chimney","Primitive factory remains active");
   var life=area.GetComponent<CommonsParkLife>();Assert.AreEqual(6,life.VisitorCount);var people=area.GetComponentsInChildren<TownCitizenAgent>();var before=new Vector3[people.Length];for(int i=0;i<people.Length;i++)before[i]=people[i].transform.position;
   Transform sculpture=null;Renderer water=null;MeshCollider track=null;foreach(var t in area.GetComponentsInChildren<Transform>())if(t.name=="Rotating orbital sculpture")sculpture=t;foreach(var r in area.GetComponentsInChildren<MeshRenderer>())if(r.name=="Fountain flowing surface")water=r;foreach(var c in area.GetComponentsInChildren<MeshCollider>())if(c.name=="Rideable wave concrete loop")track=c;Assert.IsNotNull(sculpture);Assert.IsNotNull(water);Assert.IsNotNull(track);var rotation=sculpture.rotation;var offset=water.sharedMaterial.GetTextureOffset("_BaseMap");
   var probe=new GameObject("Conservatory entrance movement probe");try{var cc=probe.AddComponent<CharacterController>();cc.height=1.8f;cc.center=Vector3.up*.9f;cc.radius=.25f;cc.stepOffset=.22f;cc.skinWidth=.03f;probe.transform.position=new Vector3(520,.3f,-104);Physics.SyncTransforms();for(int i=0;i<215;i++)cc.Move(new Vector3(0,-.08f,-.1f));Assert.Less(probe.transform.position.z,-124,"Dome doorway blocked");Assert.Greater(probe.transform.position.y,-.05f);}finally{Object.DestroyImmediate(probe);}
   for(int i=0;i<32;i++){float a=i*Mathf.PI/16;var p=new Vector3(640+Mathf.Cos(a)*22,4,260+Mathf.Sin(a)*15);Assert.IsTrue(track.Raycast(new Ray(p,Vector3.down),out var hit,5),"Missing pump-track collision");Assert.AreEqual(.14f+1.4f*Mathf.Pow(Mathf.Sin(a*2),2),hit.point.y,.03f);Assert.Greater(hit.normal.y,.6f);}
   float deadline=Time.realtimeSinceStartup+3;while(Time.realtimeSinceStartup<deadline)yield return null;
   for(int i=0;i<people.Length;i++){Assert.Greater(Vector3.Distance(before[i],people[i].transform.position),.7f,"Park visitor stuck");Assert.Greater(people[i].transform.position.y,-.2f);}Assert.Greater(Quaternion.Angle(rotation,sculpture.rotation),5);Assert.AreNotEqual(offset,water.sharedMaterial.GetTextureOffset("_BaseMap"));Assert.AreEqual(0,area.GetComponentsInChildren<Light>().Length);
   LogAssert.NoUnexpectedReceived();yield return new ExitPlayMode();
  }
 }
}
