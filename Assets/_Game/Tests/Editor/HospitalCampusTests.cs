using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
namespace RoadsideHaven.Tests
{
    public sealed class HospitalCampusTests
    {
        static Transform Open(){EditorSceneManager.OpenScene("Assets/_Game/Generated/RoadsideHavenPrototype.unity");var g=GameObject.Find(HospitalCampusLayout.RootName);Assert.IsNotNull(g);return g.transform;}
        [Test] public void SavedHospitalContainsSixFurnishedFloorsRoofAndPersistentGeometry()
        {
            var root=Open();var all=root.GetComponentsInChildren<Transform>();Assert.AreEqual(7,all.Count(t=>t.name.StartsWith("Hospital level ")));Assert.GreaterOrEqual(all.Count(t=>t.name.StartsWith("Clinical room ")),35);Assert.GreaterOrEqual(all.Count(t=>t.name=="Patient hospital bed"),60);Assert.AreEqual(4,all.Count(t=>t.name.StartsWith("Emergency ambulance ")));Assert.GreaterOrEqual(root.GetComponentsInChildren<HospitalPerson>().Length,60);
            foreach(var f in root.GetComponentsInChildren<MeshFilter>())if(!f.name.StartsWith("Sign — "))Assert.IsTrue(AssetDatabase.Contains(f.sharedMesh),"Unsaved mesh "+f.name);
            Assert.AreEqual(0,root.GetComponentsInChildren<Light>().Length,"The saved campus must use pooled runtime lights");Assert.Less(root.GetComponentsInChildren<MeshRenderer>().Length,300,"Geometry must remain combined per floor");
        }
        [Test] public void HospitalRoadsAreFlatContinuousAndFreeOfRaisedPavingAndBuildings()
        {
            var root=Open();var motel=root.GetComponentInParent<MotelGameRoot>();var mesh=motel.GetComponent<RoadSurfacePhysics>().SurfaceMesh;Assert.IsNotNull(mesh);
            var go=new GameObject("Saved road probe");var collider=go.AddComponent<MeshCollider>();collider.sharedMesh=mesh;Physics.SyncTransforms();
            try{foreach(var rect in HospitalCampusLayout.Roads){bool ns=rect.height>rect.width;float length=ns?rect.height:rect.width;for(float d=1;d<length-1;d+=2){var p=ns?new Vector3(rect.center.x,2,rect.yMin+d):new Vector3(rect.xMin+d,2,rect.center.y);Assert.IsTrue(collider.Raycast(new Ray(p,Vector3.down),out var hit,3),"Unconnected road at "+p);Assert.AreEqual(.06f,hit.point.y,.002f);}}
                foreach(var c in motel.GetComponentsInChildren<BoxCollider>()){if(!c.enabled||c.isTrigger||c.bounds.max.y<.35f||c.bounds.min.y>2.5f||c.GetComponentInParent<Rigidbody>()!=null)continue;var b=c.bounds;var footprint=Rect.MinMaxRect(b.min.x,b.min.z,b.max.x,b.max.z);foreach(var r in HospitalCampusLayout.Roads)Assert.IsFalse(r.Overlaps(footprint),"Road blocked by "+c.name+" at "+b.center);}}
            finally{Object.DestroyImmediate(go);}
        }
        [Test] public void ACharacterCanWalkBothStairFlightsOnEveryStorey()
        {
            var root=Open();var preview=EditorSceneManager.NewPreviewScene();
            try{
                foreach(var original in root.GetComponentsInChildren<BoxCollider>()){
                    if(!original.enabled||original.isTrigger||original.name.StartsWith("Sliding")||original.GetComponentInParent<HospitalHelicopter>()!=null)continue;
                    var b=original.bounds;if(b.max.x<HospitalCampusLayout.Centre.x+28||b.min.x>HospitalCampusLayout.Centre.x+44||b.max.z<HospitalCampusLayout.Centre.z-24||b.min.z>HospitalCampusLayout.Centre.z+24)continue;
                    var g=new GameObject(original.name);SceneManager.MoveGameObjectToScene(g,preview);g.transform.SetPositionAndRotation(original.transform.position,original.transform.rotation);g.transform.localScale=original.transform.lossyScale;var c=g.AddComponent<BoxCollider>();c.center=original.center;c.size=original.size;
                }
                for(int f=0;f<6;f++){
                    var g=new GameObject("Hospital walking probe");SceneManager.MoveGameObjectToScene(g,preview);g.transform.position=HospitalCampusLayout.Point(33.25f,.16f+f*4.8f,-10.5f);var cc=g.AddComponent<CharacterController>();cc.height=1.8f;cc.center=Vector3.up*.9f;cc.radius=.28f;cc.stepOffset=.22f;cc.skinWidth=.025f;cc.slopeLimit=45;Physics.SyncTransforms();
                    Walk(cc,HospitalCampusLayout.Point(33.25f,0,.4f));Assert.Greater(g.transform.position.y,.12f+f*4.8f+2.25f,"First flight blocked on floor "+f);
                    Walk(cc,HospitalCampusLayout.Point(40.5f,0,.4f));Walk(cc,HospitalCampusLayout.Point(40.5f,0,-11));Assert.Greater(g.transform.position.y,.12f+(f+1)*4.8f-.15f,"Second flight blocked on floor "+f);Object.DestroyImmediate(g);
                }
            }finally{EditorSceneManager.ClosePreviewScene(preview);}
        }
        static void Walk(CharacterController cc,Vector3 target){for(int i=0;i<650;i++){var d=target-cc.transform.position;d.y=0;if(d.sqrMagnitude<.01f)return;cc.Move(d.normalized*Mathf.Min(.04f,d.magnitude)+Vector3.down*.025f);}}
        [TestCase(false)] [TestCase(true)] public void CarsDriveThroughTheHospitalJunctionWithoutAFalseObstacle(bool northbound)
        {
            var root=Open();var motel=root.GetComponentInParent<MotelGameRoot>();var preview=EditorSceneManager.NewPreviewScene();var start=northbound?new Vector3(705,.2f,-520):new Vector3(760,.2f,-520);var direction=northbound?Vector3.forward:Vector3.left;float length=northbound?61:55;
            try{
                var road=new GameObject("Hospital road physics");SceneManager.MoveGameObjectToScene(road,preview);road.AddComponent<MeshCollider>().sharedMesh=motel.GetComponent<RoadSurfacePhysics>().SurfaceMesh;
                var corridor=new Bounds(start,new Vector3(8,8,8));corridor.Encapsulate(start+direction*length);corridor.Expand(8);
                foreach(var original in root.GetComponentsInChildren<BoxCollider>()){
                    if(!original.enabled||original.isTrigger||!original.bounds.Intersects(corridor)||original.GetComponentInParent<HospitalHelicopter>()!=null)continue;
                    var g=new GameObject(original.name);SceneManager.MoveGameObjectToScene(g,preview);g.transform.SetPositionAndRotation(original.transform.position,original.transform.rotation);g.transform.localScale=original.transform.lossyScale;var c=g.AddComponent<BoxCollider>();c.center=original.center;c.size=original.size;
                }
                var car=new GameObject("Hospital driving probe");SceneManager.MoveGameObjectToScene(car,preview);car.transform.SetPositionAndRotation(start,Quaternion.LookRotation(direction));var box=car.AddComponent<BoxCollider>();box.size=new Vector3(2.18f,1.4f,4.5f);box.center=Vector3.up*.7f;var body=car.AddComponent<Rigidbody>();body.mass=1100;var motor=car.AddComponent<ArcadeVehicleMotor>();motor.Prepare();Physics.SyncTransforms();var physics=preview.GetPhysicsScene();
                for(int i=0;i<650&&Vector3.Dot(body.position-start,direction)<length;i++){motor.Drive(1,0,false,9,16,4,80,18,6,1,.02f);physics.Simulate(.02f);}
                Assert.GreaterOrEqual(Vector3.Dot(body.position-start,direction),length,"Car stopped at the hospital junction");
            }finally{EditorSceneManager.ClosePreviewScene(preview);}
        }
        [Test] public void AirAmbulanceLandsOnTheRoofAndDeparturesClearTheBuilding()
        {
            var root=Open();var heli=root.GetComponentInChildren<HospitalHelicopter>();Assert.IsNotNull(heli);
            for(float t=0;t<100;t+=.2f){heli.Evaluate(t);Assert.GreaterOrEqual(heli.Aircraft.position.y,HospitalCampusLayout.Pad.y-.001f,"Helicopter goes below rooftop");if(t>=38&&t<66)Assert.Less((heli.Aircraft.position-HospitalCampusLayout.Pad).magnitude,.001f,"Aircraft is not parked on its landing point");}
            heli.Evaluate(72);Assert.AreEqual("Taking off",heli.Phase);Assert.Greater(heli.Aircraft.position.y,HospitalCampusLayout.Pad.y+3);
        }
        [UnityTest] public IEnumerator LiftReachesAllFloorsAndOwnerCanEnterFromThePlaza()
        {
            yield return new EnterPlayMode();Application.runInBackground=true;var load=SceneManager.LoadSceneAsync("Assets/_Game/Generated/RoadsideHavenPrototype.unity");while(!load.isDone)yield return null;
            float until=Time.realtimeSinceStartup+3;while(Time.realtimeSinceStartup<until)yield return null;
            var life=Object.FindFirstObjectByType<HospitalCampusLife>();var owner=Object.FindFirstObjectByType<OwnerSandboxController>();Assert.IsNotNull(life);owner.ExitVehicle(HospitalCampusLayout.Entrance);life.VisitHospital();
            until=Time.realtimeSinceStartup+2;while(Time.realtimeSinceStartup<until)yield return null;
            var door=life.GetComponentsInChildren<DistrictSlidingDoor>().First(d=>d.name=="Automatic main entrance");owner.ExitVehicle(HospitalCampusLayout.Point(0,.2f,-26));until=Time.realtimeSinceStartup+3;while(Time.realtimeSinceStartup<until&&door.OpenAmount<.99f)yield return null;Assert.Greater(door.OpenAmount,.95f);
            Physics.SyncTransforms();var character=owner.GetComponent<CharacterController>();for(int i=0;i<70;i++)character.Move(Vector3.forward*.1f+Vector3.down*.02f);Assert.Greater(owner.transform.position.z,HospitalCampusLayout.Centre.z-21.5f,"Main entrance is physically blocked");
            owner.ExitVehicle(HospitalCampusLayout.LiftLanding(0));
            for(int f=1;f<=6;f++){Assert.IsTrue(life.RequestFloor(f));until=Time.realtimeSinceStartup+1;while(Time.realtimeSinceStartup<until)yield return null;Assert.AreEqual(f,life.CurrentFloor);Assert.Less(Vector3.Distance(owner.transform.position,HospitalCampusLayout.LiftLanding(f)),.3f);}
            Assert.IsTrue(life.RequestFloor(0));until=Time.realtimeSinceStartup+1;while(Time.realtimeSinceStartup<until)yield return null;Assert.AreEqual(0,life.CurrentFloor);
            Assert.LessOrEqual(life.GetComponentsInChildren<Light>().Length,6);Assert.Greater(life.GetComponentsInChildren<HospitalPerson>().Count(p=>p.Human!=null),5);
            foreach(var slab in life.GetComponentsInChildren<MeshRenderer>())if(slab.name=="Batched Pale terrazzo")Assert.IsTrue(slab.enabled,"Interior culling hid a structural floor or the roof");
            yield return new ExitPlayMode();
        }
    }
}
