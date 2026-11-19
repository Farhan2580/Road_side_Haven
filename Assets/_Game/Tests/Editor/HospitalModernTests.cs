using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace RoadsideHaven.Tests
{
    public sealed class HospitalModernTests
    {
        static HospitalCampusLife Open(){EditorSceneManager.OpenScene("Assets/_Game/Generated/RoadsideHavenPrototype.unity");var h=Object.FindFirstObjectByType<HospitalCampusLife>();Assert.IsNotNull(h);HospitalArchitecture.Apply(h);return h;}
        [Test] public void ModernSkinReplacesTheGridAndKeepsItsGeometryPersistentAndBounded()
        {
            var h=Open();var modern=h.transform.Find(HospitalArchitecture.Layer);Assert.IsNotNull(modern);Assert.IsFalse(h.transform.Find("Medical campus architecture").gameObject.activeSelf);
            Assert.AreEqual(7,h.GetComponentsInChildren<Transform>().Count(t=>t.name.StartsWith("Hospital level ")));Assert.GreaterOrEqual(h.GetComponentsInChildren<Transform>().Count(t=>t.name=="Patient hospital bed"),60);
            foreach(var mesh in modern.GetComponentsInChildren<MeshFilter>())if(!mesh.name.StartsWith("Sign — "))Assert.IsTrue(AssetDatabase.Contains(mesh.sharedMesh),"Transient architecture mesh: "+mesh.name);
            Assert.Less(h.GetComponentsInChildren<MeshRenderer>().Length,320);Assert.AreEqual(0,modern.GetComponentsInChildren<Light>().Length,"The exterior must share the existing light budget");
        }
        [Test] public void ApplyingTheSkinTwicePreservesRoomsStaffAndLift()
        {
            var h=Open();var people=h.GetComponentsInChildren<HospitalPerson>();var floors=h.GetComponentsInChildren<Transform>().Where(t=>t.name.StartsWith("Hospital level ")).ToArray();
            Object.DestroyImmediate(h.transform.Find(HospitalArchitecture.Layer).gameObject);Assert.IsTrue(HospitalArchitecture.Apply(h));Assert.IsFalse(HospitalArchitecture.Apply(h));
            CollectionAssert.AreEquivalent(people,h.GetComponentsInChildren<HospitalPerson>());CollectionAssert.AreEquivalent(floors,h.GetComponentsInChildren<Transform>().Where(t=>t.name.StartsWith("Hospital level ")).ToArray());Assert.AreEqual(1,h.GetComponentsInChildren<HospitalArchitecture>().Length);
        }
        [Test] public void RooftopPergolaAndCrownLeaveTheCompleteHelipadAirspaceClear()
        {
            var h=Open();var pad=HospitalCampusLayout.Pad;var airspace=Rect.MinMaxRect(pad.x-15,pad.z-15,pad.x+15,pad.z+15);
            foreach(var c in h.transform.Find(HospitalArchitecture.Layer).GetComponentsInChildren<BoxCollider>()){
                if(!c.enabled||c.bounds.max.y<=pad.y+.02f)continue;var b=c.bounds;Assert.IsFalse(airspace.Overlaps(Rect.MinMaxRect(b.min.x,b.min.z,b.max.x,b.max.z)),"Helicopter obstruction: "+c.name);
            }
        }
        [Test] public void TheUpperAtriumAndAllOtherFacadeSectionsHavePhysicalGlazing()
        {
            var h=Open();Physics.SyncTransforms();
            foreach(int floor in new[]{1,3,5})foreach(float x in new[]{-40f,-28,-16.5f,-8,0,8,23,39}){
                var ray=new Ray(HospitalCampusLayout.Point(x,1.7f+floor*4.8f,-21),Vector3.back);bool hit=false;
                foreach(var c in h.transform.Find(HospitalArchitecture.Layer).GetComponentsInChildren<BoxCollider>())if(c.Raycast(ray,out _,4)){hit=true;break;}
                Assert.IsTrue(hit,"A person can walk through the facade at floor "+floor+", x="+x);
            }
        }
        [Test] public void FastTravelArrivesOnThePedestrianWalkOutsideEveryDrivingLane()
        {
            var arrival=HospitalCampusLayout.Entrance;foreach(var r in HospitalCampusLayout.Roads)Assert.IsFalse(r.Contains(new Vector2(arrival.x,arrival.z)),"Fast travel places the owner in moving traffic");
        }
    }
}
