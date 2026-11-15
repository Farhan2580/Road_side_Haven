using System.Collections;using System.Linq;using NUnit.Framework;using UnityEngine;using UnityEngine.SceneManagement;using UnityEngine.TestTools;
namespace RoadsideHaven.Tests
{
 public sealed class NightBoulevardTests
 {
  [UnityTest]public IEnumerator NightPlazaLightsUseTheExistingPoolAndGuestsMove()
  {
   yield return new EnterPlayMode();var load=SceneManager.LoadSceneAsync("Assets/_Game/Generated/RoadsideHavenPrototype.unity");while(!load.isDone)yield return null;yield return null;yield return null;yield return null;
   var m=Object.FindFirstObjectByType<MotelGameRoot>();var area=m.transform.Find("Meridian Night Boulevard v1");Assert.IsNotNull(area);Assert.AreEqual(0,area.GetComponentsInChildren<Light>(true).Length);var owner=Object.FindFirstObjectByType<OwnerSandboxController>();if(!owner.IsWalking)owner.ToggleWalking();owner.ExitVehicle(new Vector3(574,.2f,35));m.GetComponent<MotelAtmosphere>().PreviewNight();var guests=area.GetComponentsInChildren<TownCitizenAgent>();Assert.AreEqual(3,guests.Length);var positions=guests.Select(g=>g.transform.position).ToArray();float until=Time.realtimeSinceStartup+4;while(Time.realtimeSinceStartup<until)yield return null;
   var lights=Object.FindObjectsByType<Light>(FindObjectsSortMode.None).Where(l=>l.name.StartsWith("Pooled night light ")).ToArray();Assert.LessOrEqual(lights.Length,20);Assert.IsTrue(lights.Any(l=>l.enabled&&Vector3.Distance(l.transform.position,new Vector3(562,4.8f,35))<15),"Pickup area has no active pooled lighting at night");for(int i=0;i<guests.Length;i++)Assert.Greater(Vector3.Distance(positions[i],guests[i].transform.position),.7f);
   var driving=m.GetComponentInChildren<WorldDrivingController>();foreach(var car in area.GetComponentsInChildren<EnterableVehicle>())Assert.IsTrue(driving.IsRegistered(car.GetComponent<Rigidbody>()));
   foreach(float z in new[]{-.5f,-25.5f})for(float x=445;x<710;x+=2)foreach(var c in Physics.OverlapCapsule(new Vector3(x,.55f,z),new Vector3(x,1.65f,z),.25f))Assert.IsFalse(c.enabled&&!c.isTrigger&&c.GetComponentInParent<Rigidbody>()==null&&c.GetComponentInParent<CharacterController>()==null&&c.bounds.max.y>.3f,"Public sidewalk blocked: "+c.name);
   LogAssert.NoUnexpectedReceived();yield return new ExitPlayMode();
  }
 }
}
