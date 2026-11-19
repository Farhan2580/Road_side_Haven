using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
namespace RoadsideHaven.Tests
{
    public sealed class TransitSquareTests
    {
        [UnityTest] public IEnumerator SavedSquareKeepsRoadsAndBoardingClearAndShopsCanBeEntered()
        {
            yield return new EnterPlayMode();Application.runInBackground=true;
            var loading=SceneManager.LoadSceneAsync("Assets/_Game/Generated/RoadsideHavenPrototype.unity");while(!loading.isDone)yield return null;
            float until=Time.realtimeSinceStartup+2;while(Time.realtimeSinceStartup<until)yield return null;
            var motel=Object.FindFirstObjectByType<MotelGameRoot>();var area=motel.transform.Find(TransitSquareLayout.Layer);Assert.IsNotNull(area);
            ValidateGeometry(motel,area);
            var owner=motel.GetComponentInChildren<OwnerSandboxController>(true);
            for(int shop=0;shop<3;shop++)
            {
                var entrance=TransitSquareLayout.Entrance(shop);owner.ExitVehicle(entrance+Vector3.forward*2);
                until=Time.realtimeSinceStartup+1;while(Time.realtimeSinceStartup<until)yield return null;
                Assert.Greater(ShopDoor(area,shop).OpenAmount,.9f,"Shop doorway does not open before the owner arrives");
                var body=owner.GetComponent<CharacterController>();
                for(int step=0;step<55;step++)body.Move(new Vector3(0,-.025f,-.08f));
                Assert.Less(owner.transform.position.z,entrance.z-1,"Unable to walk through the shop entrance");
                until=Time.realtimeSinceStartup+.6f;while(Time.realtimeSinceStartup<until)yield return null;
                Assert.IsFalse(ShopRoof(area,shop).GetComponentsInChildren<Renderer>().Any(r=>r.enabled),"The shop roof still covers the inside camera");
            }
            var life=area.GetComponent<TransitSquareLife>();Assert.GreaterOrEqual(life.WalkerCount,2);Assert.GreaterOrEqual(area.GetComponentsInChildren<HumanVisual>().Length,5);
            var fan=Named(area,"Rotating ceiling fan");var rotation=fan.localRotation;var ripple=Named(area,"Animated fountain ripple");var scale=ripple.localScale;
            until=Time.realtimeSinceStartup+.8f;while(Time.realtimeSinceStartup<until)yield return null;
            Assert.Greater(Quaternion.Angle(rotation,fan.localRotation),1,"Ceiling fan is not animating");Assert.Greater((scale-ripple.localScale).magnitude,.02f,"Fountain ripples are not animating");
            int near=area.GetComponentsInChildren<MeshRenderer>().Count(r=>r.enabled&&r.name.StartsWith("Transit static batch"));
            owner.ExitVehicle(new Vector3(-800,.3f,-600));until=Time.realtimeSinceStartup+1.3f;while(Time.realtimeSinceStartup<until)yield return null;
            int far=area.GetComponentsInChildren<MeshRenderer>().Count(r=>r.enabled&&r.name.StartsWith("Transit static batch"));Assert.Less(far,near,"Distant plaza geometry is still rendered");
            owner.ExitVehicle(new Vector3(-100,.2f,-32.2f));until=Time.realtimeSinceStartup+1.3f;while(Time.realtimeSinceStartup<until)yield return null;
            Assert.Greater(area.GetComponentsInChildren<MeshRenderer>().Count(r=>r.enabled&&r.name.StartsWith("Transit static batch")),far,"Returning does not restore plaza geometry");
            LogAssert.NoUnexpectedReceived();yield return new ExitPlayMode();
        }
        private static Transform Named(Transform root,string name)=>root.GetComponentsInChildren<Transform>().First(t=>t.name==name);
        private static Transform ShopRoot(Transform area,int shop)=>Named(area,TransitSquareLayout.ShopNames[shop]);
        private static DistrictSlidingDoor ShopDoor(Transform area,int shop)=>ShopRoot(area,shop).GetComponentInChildren<DistrictSlidingDoor>();
        private static Transform ShopRoof(Transform area,int shop)=>Named(ShopRoot(area,shop),"Shop roof "+shop);
        private static bool Obstacle(Collider c)=>c.enabled&&!c.isTrigger&&c.gameObject.activeInHierarchy&&c.bounds.max.y>.35f&&c.GetComponentInParent<Rigidbody>()==null&&c.GetComponentInParent<CharacterController>()==null&&c.GetComponentInParent<NavMeshAgent>()==null;
        private static void ValidateGeometry(MotelGameRoot motel,Transform area)
        {
            Assert.Less(area.GetComponentsInChildren<MeshRenderer>().Length,180,"Plaza draw-call budget exceeded");Assert.AreEqual(0,area.GetComponentsInChildren<Light>().Length,"Plaza added unbounded realtime lights");
            foreach(var filter in area.GetComponentsInChildren<MeshFilter>())Assert.IsNotNull(filter.sharedMesh,"Missing saved mesh: "+filter.name);
            Assert.Less(area.GetComponentsInChildren<MeshFilter>().Sum(f=>f.sharedMesh.vertexCount),180000,"Plaza geometry budget exceeded");
            Physics.SyncTransforms();
            var roads=motel.GetComponentsInChildren<MeshRenderer>().Where(r=>r.gameObject.activeInHierarchy&&TownPopulationController.RoadName(r.name)).Select(r=>Rect.MinMaxRect(r.bounds.min.x,r.bounds.min.z,r.bounds.max.x,r.bounds.max.z)).ToArray();
            foreach(var collider in area.GetComponentsInChildren<Collider>())
            {
                if(!collider.enabled||collider.isTrigger)continue;var b=collider.bounds;var rect=Rect.MinMaxRect(b.min.x,b.min.z,b.max.x,b.max.z);
                Assert.IsFalse(roads.Any(r=>r.Overlaps(rect)),"New plaza object occupies the public road: "+collider.name);
            }
            foreach(var collider in Physics.OverlapBox(new Vector3(-100,1,-27),new Vector3(5,.7f,2.8f)))
                if(collider.transform.IsChildOf(area))Assert.IsFalse(Obstacle(collider),"New object blocks residential bus boarding: "+collider.name);
            foreach(var route in new[]{TransitSquareLayout.ShopWalk,TransitSquareLayout.GardenWalk})for(int leg=1;leg<route.Length;leg++)
            {
                float length=Vector3.Distance(route[leg-1],route[leg]);for(float distance=0;distance<=length;distance+=.6f)
                {
                    var p=Vector3.Lerp(route[leg-1],route[leg],distance/length);
                    foreach(var collider in Physics.OverlapCapsule(p+Vector3.up*.4f,p+Vector3.up*1.45f,.22f))Assert.IsFalse(Obstacle(collider),"Plaza walking route blocked by "+collider.name+" at "+p);
                    Assert.IsTrue(Physics.RaycastAll(p+Vector3.up*.15f,Vector3.down,.3f).Any(h=>h.collider.transform.IsChildOf(area)),"Public path has no solid paving at "+p);
                }
            }
            foreach(var trunk in motel.GetComponentsInChildren<MeshRenderer>())if(trunk.name=="Tree trunk")for(int i=0;i<3;i++)Assert.IsFalse(TransitSquareLayout.Building(i).Contains(new Vector2(trunk.bounds.center.x,trunk.bounds.center.z)),"Existing tree left inside a new shop");
        }
    }
}
