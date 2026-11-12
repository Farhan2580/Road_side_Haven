using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace RoadsideHaven
{
    /// <summary>Persistent world-wide trips; actors never recycle around the camera.</summary>
    public sealed class TownPopulationController : MonoBehaviour
    {
        [SerializeField,Range(8,80)] private int carCount=48;
        [SerializeField,Range(12,160)] private int citizenCount=96;
        private readonly List<TownVehicleAgent> traffic=new List<TownVehicleAgent>();
        private sealed class Citizen {public TownCitizenAgent Agent;public Vector3 Home,Work;public bool AtWork,Shopper,Patrol;public float Wait;}
        private readonly List<Citizen> citizens=new List<Citizen>();
        private readonly List<Vector3> homes=new List<Vector3>(),workplaces=new List<Vector3>(),clinics=new List<Vector3>(),stations=new List<Vector3>();
        private readonly List<Material> materials=new List<Material>();
        private TownRoadNetwork network;
        private WorldDrivingController driving;
        private MotelAtmosphere clock;
        private Material skin,uniform;
        private float timer;
        private int citizenCursor;
        private bool ready;
        private readonly List<Bounds> reserved=new List<Bounds>();
        public void ConfigureAllDistricts(){carCount=Mathf.Max(48,carCount);citizenCount=Mathf.Max(96,citizenCount);}
        private IEnumerator Start()
        {
            yield return null;yield return null;
            var motel=GetComponentInParent<MotelGameRoot>();driving=motel.GetComponentInChildren<WorldDrivingController>(true);clock=motel.GetComponent<MotelAtmosphere>();
            // Remove the old collision-blind decorative loops from public traffic.
            foreach(var legacy in motel.GetComponentsInChildren<RoadTraffic>(true))
            {legacy.enabled=false;legacy.gameObject.SetActive(false);}
            int parkedSlot=0;
            foreach(var parked in motel.GetComponentsInChildren<EnterableVehicle>(true))
            {
                var position=parked.transform.position;
                if(!parked.gameObject.activeInHierarchy||Mathf.Abs(position.x)>130||position.z<-24||position.z>-2)continue;
                var body=parked.GetComponent<Rigidbody>();int slot=parkedSlot%4;body.position=new Vector3(new[]{-21f,-14f,-20f,-13f}[slot],position.y,(slot<2?-29:-45)-6*(parkedSlot++/4));body.rotation=Quaternion.identity;
            }
            network=new TownRoadNetwork();
            foreach(var renderer in motel.GetComponentsInChildren<MeshRenderer>(true))
                if(renderer.gameObject.activeInHierarchy&&RoadName(renderer.name))network.Add(renderer.bounds.center,renderer.bounds.size);
            network.Build(new[]{new Vector3(-100,0,-13),new Vector3(-880,0,20),new Vector3(-950,0,230),new Vector3(180,0,650)});
            if(network.Roads.Count==0)yield break;
            foreach(var marker in motel.GetComponentsInChildren<MeshRenderer>(true))
            {
                if(!marker.gameObject.activeInHierarchy)continue;
                var b=marker.bounds;var point=new Vector3(b.center.x,.1f,b.min.z-1.5f);
                if(marker.name=="House doorway")homes.Add(point);
                if(marker.name=="Office entrance glass"||marker.name=="Office lobby floor")workplaces.Add(point);
                if(marker.name=="Clinic entrance")clinics.Add(point);
                if(marker.name=="Police station entrance"||marker.name=="Police box")stations.Add(point);
            }
            skin=Mat("Citizen skin",new Color(.67f,.45f,.3f));uniform=Mat("School uniform",new Color(.10f,.22f,.48f));
            Physics.SyncTransforms();
            var school=Vehicle("Yellow school bus",new Vector3(-100,.09f,-13+network.Nearest(new Vector3(-100,0,-13)).HalfWidth-1.8f),Quaternion.Euler(0,-90,0),new Vector3(2.85f,3.5f,12.8f));
            school.gameObject.AddComponent<BusVisuals>().Configure(true);school.gameObject.AddComponent<TownVehicleAgent>();
            school.gameObject.AddComponent<SchoolBusService>().Configure(network,clock,uniform,skin,transform);driving.RegisterVehicle(school);
            var coachRoute=network.Route(new Vector3(-240,0,-13),new Vector3(180,0,650),false,6);
            if(coachRoute.Length>1&&Clear(coachRoute[0],Quaternion.LookRotation(coachRoute[1]-coachRoute[0]),new Vector3(2.85f,3.5f,11)))
            {var coach=Vehicle("Silver intercity coach",coachRoute[0],Quaternion.LookRotation(coachRoute[1]-coachRoute[0]),new Vector3(2.85f,3.5f,11));coach.gameObject.AddComponent<BusVisuals>().Configure(false);var agent=coach.gameObject.AddComponent<TownVehicleAgent>();agent.SetRoute(coachRoute,10);traffic.Add(agent);driving.RegisterVehicle(coach);}
            for(int i=0;i<carCount;i++)
            {
                Vector3[] route=null;
                for(int attempt=0;attempt<40;attempt++)
                {
                    var road=network.Roads[Random.Range(0,network.Roads.Count)];
                    var from=Vector3.Lerp(road.A,road.B,Random.Range(.08f,.92f));
                    if(i<5){road=network.Nearest(new Vector3(0,0,-13));from=TownRoadNetwork.Project(new Vector3(Random.Range(-180,180),0,-13),road);}
                    var candidate=network.Route(from,RandomRoadPoint(),false,2.2f,i%2==0);
                    if(candidate.Length<2)continue;
                    if(Clear(candidate[0],Quaternion.LookRotation(candidate[1]-candidate[0]),new Vector3(2.18f,1.44f,4.5f))){route=candidate;break;}
                }
                if(route==null)continue;
                var body=Vehicle("City commuter car "+i,route[0],Quaternion.LookRotation(route[1]-route[0]),new Vector3(2.18f,1.44f,4.5f));
                body.gameObject.AddComponent<ModernCarVisuals>().Configure(i);
                var agent=body.gameObject.AddComponent<TownVehicleAgent>();agent.SetRoute(route,7+i%4);traffic.Add(agent);driving.RegisterVehicle(body);
                yield return null;
            }
            for(int i=0;i<citizenCount;i++)
            {
                bool doctor=i%12==0,police=i%12==1,jogger=i%10==3,shopper=(i%3==0||jogger)&&!doctor&&!police;
                var obj=new GameObject(doctor?"Doctor "+i:police?"Police officer "+i:"Citizen "+i);obj.transform.SetParent(transform,false);var agent=obj.AddComponent<TownCitizenAgent>();
                Color color=doctor?new Color(.92f,.95f,.96f):police?new Color(.04f,.09f,.22f):Color.HSVToRGB((i*.117f)%1,.45f,.55f);
                agent.Initialize(Mat("Clothes "+i,color),skin,false);agent.SetJogging(jogger);obj.AddComponent<WorldNpc>().Configure("town-citizen-"+i,doctor?"Doctor":police?"Police officer":jogger?"Jogger":shopper?"City shopper":"Office worker");
                var road=network.Roads[i%network.Roads.Count];var fallback=Sidewalk(road,Random.Range(.1f,.9f));
                var home=i%4==0&&homes.Count>0?homes[i%homes.Count]+Vector3.right*(i%3)*.6f:fallback;
                var options=doctor?clinics:police?stations:workplaces;
                var work=options.Count>0?options[i%options.Count]+Vector3.right*(i%3)*.8f:Sidewalk(road,.8f);
                agent.Warp(home);citizens.Add(new Citizen{Agent=agent,Home=home,Work=work,Shopper=shopper,Patrol=police,Wait=i%10});
            }
            ready=true;
        }
        public static bool RoadName(string name)=>name=="Connected road"||name=="Road"||name=="Highway extension"||name=="North avenue"||name=="Connecting avenue";
        private Vector3 RandomRoadPoint(){var road=network.Roads[Random.Range(0,network.Roads.Count)];return Vector3.Lerp(road.A,road.B,Random.Range(.08f,.92f));}
        private Material Mat(string name,Color color){var m=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=name,color=color};materials.Add(m);return m;}
        private static Vector3 Sidewalk(TownRoadNetwork.Road road,float t)
        {var forward=(road.B-road.A).normalized;return Vector3.Lerp(road.A,road.B,t)+Vector3.Cross(Vector3.up,forward)*(road.HalfWidth+1.5f);}
        private bool Clear(Vector3 position,Quaternion rotation,Vector3 size)
        {
            var bounds=new Bounds(position+Vector3.up*size.y*.5f,new Vector3(size.z+6,size.y,size.z+6));
            foreach(var occupied in reserved)if(occupied.Intersects(bounds))return false;
            Physics.SyncTransforms();
            return !Physics.CheckBox(position+Vector3.up*size.y*.5f,size*.5f+new Vector3(.5f,-.04f,1.5f),rotation,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore);
        }
        private Rigidbody Vehicle(string name,Vector3 position,Quaternion rotation,Vector3 size)
        {
            var obj=new GameObject(name);obj.transform.SetParent(transform,false);obj.transform.SetPositionAndRotation(position,rotation);
            var box=obj.AddComponent<BoxCollider>();box.size=size;box.center=Vector3.up*size.y*.5f;
            var body=obj.AddComponent<Rigidbody>();body.isKinematic=true;body.useGravity=false;body.interpolation=RigidbodyInterpolation.Interpolate;body.collisionDetectionMode=CollisionDetectionMode.ContinuousSpeculative;
            obj.AddComponent<EnterableVehicle>().Configure(1);reserved.Add(new Bounds(position+Vector3.up*size.y*.5f,new Vector3(size.z+6,size.y,size.z+6)));return body;
        }
        private void Update()
        {
            if(!ready)return;timer-=Time.deltaTime;if(timer>0)return;timer=.1f;
            float hour=clock==null?9:clock.Hour;bool workTime=hour>=8&&hour<17;
            int routeBudget=2;
            foreach(var car in traffic)
            {
                if(car==null||!car.enabled)continue;
                if(!car.Arrived&&car.BlockedSeconds<18)continue;
                // A stalled vehicle can request a different destination, but stays physically where it is.
                if(routeBudget--<=0)break;
                var route=network.Route(car.transform.position,RandomRoadPoint(),false,car.GetComponent<BusVisuals>()!=null?3.5f:2.2f);
                if(route.Length>1)car.SetRoute(route,car.GetComponent<BusVisuals>()!=null?9:8);
            }
            for(int update=0;update<Mathf.Min(4,citizens.Count);update++)
            {
                var person=citizens[citizenCursor++%citizens.Count];
                person.Wait-=.1f*Mathf.Max(1,citizens.Count/4f);if(!person.Agent.Arrived)continue;
                if(person.Patrol&&workTime)
                {person.AtWork=true;if(person.Wait>0)continue;person.Wait=12;var road=network.Nearest(person.Work);person.Agent.Walk(network.Route(person.Agent.transform.position,Sidewalk(road,Random.Range(.15f,.85f)),true));}
                else if(person.Shopper)
                {if(person.Wait>0)continue;person.Wait=20;var road=network.Roads[Random.Range(0,network.Roads.Count)];person.Agent.Walk(network.Route(person.Agent.transform.position,Sidewalk(road,Random.Range(.1f,.9f)),true));}
                else if(workTime!=person.AtWork)
                {person.AtWork=workTime;person.Agent.Walk(network.Route(person.Agent.transform.position,workTime?person.Work:person.Home,true));}
            }
        }
        private void OnDestroy(){foreach(var material in materials)if(material!=null)Destroy(material);}
    }
}
