using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
namespace RoadsideHaven.Tests
{
    public sealed class HeritageNeighborhoodTests
    {
        [Test] public void EveryPrivateGardenKeepsClearOfPublicRoadsAndRiver()
        {
            for(int site=0;site<2;site++)for(int i=0;i<12;i++)
            {
                var plot=HeritageNeighborhoodLayout.Plot(site,i);foreach(var road in HeritageNeighborhoodLayout.Roads(site))Assert.IsFalse(plot.Overlaps(road),"Garden occupies road: "+site+"/"+i);
                foreach(float x in new[]{plot.xMin,plot.center.x,plot.xMax})foreach(float z in new[]{plot.yMin,plot.center.y,plot.yMax})Assert.IsFalse(VillageRiverShape.Contains(new Vector2(x,z),10),"Garden intersects river bank");
                for(int j=i+1;j<12;j++)Assert.IsFalse(plot.Overlaps(HeritageNeighborhoodLayout.Plot(site,j)),"Private gardens overlap");
            }
        }
        [Test] public void WillowStreetKeepsTheFencedPlaygroundClear()
        {
            var park=Rect.MinMaxRect(678,404,702,442);
            foreach(var road in HeritageNeighborhoodLayout.Roads(1))Assert.IsFalse(park.Overlaps(road),"Street runs through the park");
        }
        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)] [TestCase(5)]
        public void SavedParkAccessHasContinuousAsphaltAndCarsPassWithoutHittingPaving(int approach)
        {
            EditorSceneManager.OpenScene("Assets/_Game/Generated/RoadsideHavenPrototype.unity");
            var motel=Object.FindFirstObjectByType<MotelGameRoot>();
            var mesh=motel.GetComponent<RoadSurfacePhysics>().SurfaceMesh;
            var preview=EditorSceneManager.NewPreviewScene();
            var starts=new[]{new Vector3(-140,.2f,-155),new Vector3(-54,.2f,-155),new Vector3(-48,.2f,-150),new Vector3(-48,.2f,-70),new Vector3(650,.2f,395),new Vector3(700,.2f,395)};
            var directions=new[]{Vector3.right,Vector3.left,Vector3.forward,Vector3.back,Vector3.right,Vector3.left};
            var start=starts[approach];var direction=directions[approach];float distance=approach<2?86:approach<4?80:50;
            try
            {
                var ground=new GameObject("Saved park road physics");SceneManager.MoveGameObjectToScene(ground,preview);
                var surface=ground.AddComponent<MeshCollider>();surface.sharedMesh=mesh;
                if(approach==0)for(float x=-51.5f;x<-44;x+=1)for(float z=-158.5f;z<-151;z+=1)
                    Assert.IsTrue(surface.Raycast(new Ray(new Vector3(x,1,z),Vector3.down),out _,2),"The park junction is missing a corner of its asphalt landing");
                for(float d=0;d<=distance;d+=1)
                {
                    var p=start+direction*d;
                    Assert.IsTrue(surface.Raycast(new Ray(p+Vector3.up,Vector3.down),out var hit,2),"Asphalt ends before the park access joins the main road at "+p);
                    Assert.AreEqual(.06f,hit.point.y,.001f,"Driving lane is not flat");
                }
                var corridor=new Bounds((start+start+direction*distance)*.5f,Vector3.one);
                corridor.Encapsulate(start);corridor.Encapsulate(start+direction*distance);corridor.Expand(new Vector3(6,6,6));
                foreach(var original in motel.GetComponentsInChildren<BoxCollider>())
                {
                    if(!original.enabled||original.isTrigger||!corridor.Intersects(original.bounds)||original.GetComponentInParent<Rigidbody>()!=null)continue;
                    var copy=new GameObject(original.name);SceneManager.MoveGameObjectToScene(copy,preview);
                    copy.transform.SetPositionAndRotation(original.transform.position,original.transform.rotation);copy.transform.localScale=original.transform.lossyScale;
                    var collider=copy.AddComponent<BoxCollider>();collider.center=original.center;collider.size=original.size;
                }
                var car=new GameObject("Park access driving probe");SceneManager.MoveGameObjectToScene(car,preview);
                car.transform.SetPositionAndRotation(start,Quaternion.LookRotation(direction));
                var box=car.AddComponent<BoxCollider>();box.center=Vector3.up*.7f;box.size=new Vector3(2.18f,1.4f,4.5f);
                var body=car.AddComponent<Rigidbody>();body.mass=1100;var motor=car.AddComponent<ArcadeVehicleMotor>();motor.Prepare();
                Physics.SyncTransforms();var physics=preview.GetPhysicsScene();
                for(int step=0;step<650&&Vector3.Dot(body.position-start,direction)<distance;step++)
                {motor.Drive(1,0,false,9,16,4,80,18,6,1,.02f);physics.Simulate(.02f);}
                Assert.GreaterOrEqual(Vector3.Dot(body.position-start,direction),distance,"Car stopped at transverse paving or the missing park road");
                Assert.Greater(Vector3.Dot(body.linearVelocity,direction),12,"Park access unexpectedly slows the car");
                Assert.Less(body.position.y,.16f,"Car has climbed raised paving on the road");
            }
            finally{EditorSceneManager.ClosePreviewScene(preview);}
        }
        [UnityTest] public IEnumerator SavedNeighbourhoodsHaveClearRoadsAccessibleGardensAndLivePlaygrounds()
        {
            yield return new EnterPlayMode();Application.runInBackground=true;var operation=SceneManager.LoadSceneAsync("Assets/_Game/Generated/RoadsideHavenPrototype.unity");while(!operation.isDone)yield return null;yield return null;yield return null;
            float readyUntil=Time.realtimeSinceStartup+2;while(Time.realtimeSinceStartup<readyUntil)yield return null;
            var motel=Object.FindFirstObjectByType<MotelGameRoot>();
            if(motel==null)throw new System.InvalidOperationException("Motel missing after scene load");
            Assert.IsNotNull(motel,"Missing active motel; scene roots: "+string.Join(",",SceneManager.GetActiveScene().GetRootGameObjects().Select(g=>g.name+":"+g.activeInHierarchy)));
            var motelTransform=motel.transform;
            if(motelTransform==null)throw new System.InvalidOperationException("Motel transform missing");
            var area=SavedArea(motel);
            if(area==null)throw new System.InvalidOperationException("Saved heritage layer missing");
            Assert.AreEqual(24,area.GetComponentsInChildren<Transform>().Count(t=>t.name.StartsWith("Heritage home ")));
            Assert.Less(area.GetComponentsInChildren<MeshRenderer>().Length,650);
            foreach(var mesh in area.GetComponentsInChildren<MeshFilter>())Assert.IsNotNull(mesh.sharedMesh,"Missing baked mesh: "+mesh.name);
            ValidateGeometry(motel,area);
            var life=area.GetComponent<HeritageNeighborhoodLife>();var owner=motel.GetComponentInChildren<OwnerSandboxController>(true);
            life.VisitNext();Assert.Less(Vector3.Distance(owner.transform.position,HeritageNeighborhoodLayout.Arrival(0)),.1f);life.VisitNext();Assert.Less(Vector3.Distance(owner.transform.position,HeritageNeighborhoodLayout.Arrival(1)),.1f);
            var swing=area.GetComponentsInChildren<Transform>().First(t=>t.name=="Animated swing");owner.ExitVehicle(swing.position+Vector3.back*10);var before=swing.localRotation;float until=Time.realtimeSinceStartup+2;while(Time.realtimeSinceStartup<until)yield return null;
            Assert.Greater(Quaternion.Angle(before,swing.localRotation),.2f,"Swing animation is not running");Assert.GreaterOrEqual(area.GetComponentsInChildren<TownCitizenAgent>().Length,4);
            int near=area.GetComponentsInChildren<MeshRenderer>().Count(r=>r.enabled&&r.name.StartsWith("Heritage batch"));
            owner.ExitVehicle(new Vector3(-800,.3f,-800));until=Time.realtimeSinceStartup+1.5f;while(Time.realtimeSinceStartup<until)yield return null;
            int far=area.GetComponentsInChildren<MeshRenderer>().Count(r=>r.enabled&&r.name.StartsWith("Heritage batch"));
            Assert.Less(far,near,"Distant residential detail is still submitted for rendering");
            owner.ExitVehicle(HeritageNeighborhoodLayout.Arrival(0));until=Time.realtimeSinceStartup+1.5f;while(Time.realtimeSinceStartup<until)yield return null;
            int returned=area.GetComponentsInChildren<MeshRenderer>().Count(r=>r.enabled&&r.name.StartsWith("Heritage batch"));Assert.Greater(returned,far,"Returning to the neighbourhood did not restore its geometry");
            string budget="Heritage visible static batches: nearby="+near+", distant="+far+", returned="+returned;
            LogAssert.Expect(LogType.Log,budget);Debug.Log(budget);
            LogAssert.NoUnexpectedReceived();yield return new ExitPlayMode();
        }
        private static void ValidateGeometry(MotelGameRoot motel,Transform area)
        {
            Physics.SyncTransforms();
            // Inspect the full road width, including each approach across the existing wider avenue.
            // The original centre-line check missed the raised slabs on either side of the junction.
            var roadBounds=motel.GetComponentsInChildren<MeshRenderer>().Where(r=>TownPopulationController.RoadName(r.name)&&r.gameObject.activeInHierarchy).Select(r=>r.bounds).ToArray();
            var clearAreas=HeritageNeighborhoodLayout.Roads(0).Concat(HeritageNeighborhoodLayout.Roads(1)).Concat(new[]{Rect.MinMaxRect(-361,-166,-339,-144),Rect.MinMaxRect(-126,-72,-100,-48),Rect.MinMaxRect(408,378,432,412)});
            foreach(var road in clearAreas)for(float x=road.xMin+.6f;x<road.xMax;x+=1.3f)for(float z=road.yMin+.6f;z<road.yMax;z+=1.3f)
            {
                // Approach rectangles include shoulders; only check points on an actual active road.
                bool onRoad=roadBounds.Any(b=>b.min.x<x&&b.max.x>x&&b.min.z<z&&b.max.z>z);
                if(!onRoad)continue;
                foreach(var c in Physics.OverlapBox(new Vector3(x,.16f,z),new Vector3(.12f,.085f,.12f)))
                    if(c.transform.IsChildOf(area)&&c.enabled&&!c.isTrigger&&c.GetComponentInParent<CharacterController>()==null)
                        Assert.LessOrEqual(c.bounds.max.y,.07f,"Raised paving on driving lane: "+c.name+" at "+x+","+z);
            }
            for(int site=0;site<2;site++)
            {
                var route=HeritageNeighborhoodLayout.ParkWalk(site,HeritageNeighborhoodLayout.House(site,0).x);
                for(int leg=1;leg<route.Length;leg++)
                {
                    float length=Vector3.Distance(route[leg-1],route[leg]);float previous=-1;
                    for(float distance=0;distance<=length;distance+=.4f)
                    {
                        var p=Vector3.Lerp(route[leg-1],route[leg],distance/length);p.y=.32f;
                        var floors=Physics.RaycastAll(p,Vector3.down,.4f).Where(h=>h.collider.transform.IsChildOf(area)||h.collider.name=="Continuous road physics").ToArray();
                        Assert.IsNotEmpty(floors,"Public park path has no solid floor at "+p);
                        float height=floors.Max(h=>h.point.y);Assert.LessOrEqual(height,.105f,"Park path has a raised step");
                        if(previous>=0)Assert.LessOrEqual(Mathf.Abs(height-previous),.05f,"Park path has a sudden level change at "+p);previous=height;
                        foreach(var c in Physics.OverlapCapsule(p+Vector3.up*.2f,p+Vector3.up*1.2f,.18f))Assert.IsFalse(StaticObstacle(c),"Park gate route blocked by "+c.name+" at "+p);
                    }
                }
            }
            foreach(int site in new[]{0,1})foreach(var road in HeritageNeighborhoodLayout.Roads(site))
            {
                bool horizontal=road.width>road.height;float length=horizontal?road.width:road.height;
                for(float d=2;d<length;d+=3)
                {
                    var point=horizontal?new Vector3(road.xMin+d,1,road.center.y):new Vector3(road.center.x,1,road.yMin+d);
                    foreach(var c in Physics.OverlapBox(point,new Vector3(.8f,.65f,.8f)))Assert.IsFalse(StaticObstacle(c),"Road entry obstructed by "+c.name+" at "+point);
                }
            }
            for(int site=0;site<2;site++)for(int i=0;i<12;i++)
            {
                var home=area.GetComponentsInChildren<Transform>().First(t=>t.name=="Heritage home "+site+"-"+i);
                for(float z=-19;z<18;z+=2)foreach(var c in Physics.OverlapCapsule(home.TransformPoint(new Vector3(-9.15f,.5f,z)),home.TransformPoint(new Vector3(-9.15f,1.55f,z)),.22f))Assert.IsFalse(StaticObstacle(c),"Garden side path blocked: "+c.name);
            }
            var graph=new TownRoadNetwork();foreach(var renderer in motel.GetComponentsInChildren<MeshRenderer>(true))if(renderer.gameObject.activeInHierarchy&&TownPopulationController.RoadName(renderer.name))graph.Add(renderer.bounds.center,renderer.bounds.size);graph.Build(null);
            for(int site=0;site<2;site++)Assert.Greater(graph.Route(new Vector3(-400,0,-13),new Vector3(HeritageNeighborhoodLayout.Centers[site].x,0,HeritageNeighborhoodLayout.Centers[site].y),false,2.2f).Length,1,"Neighbourhood road disconnected");
        }
        private static Transform SavedArea(MotelGameRoot motel)
        {
            foreach(Transform child in motel.transform)
                if(child.name==HeritageNeighborhoodLayout.Layer)return child;
            throw new System.InvalidOperationException("Saved heritage root missing in "+motel.name);
        }
        private static bool StaticObstacle(Collider c)=>c.enabled&&!c.isTrigger&&c.gameObject.activeInHierarchy&&c.bounds.max.y>.35f&&c.GetComponentInParent<Rigidbody>()==null&&c.GetComponentInParent<CharacterController>()==null&&c.GetComponentInParent<NavMeshAgent>()==null;
    }
}
