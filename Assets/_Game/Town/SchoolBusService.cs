using System.Collections.Generic;
using UnityEngine;
namespace RoadsideHaven
{
    /// <summary>Visible home-stop-campus student journeys with boarding, alighting, dwell and return service.</summary>
    public sealed class SchoolBusService : MonoBehaviour
    {
        private enum State {Home,ToPickup,WaitOut,BusOut,ToCampus,Campus,ToReturn,WaitBack,BusBack,ToHome}
        private sealed class Student {public TownCitizenAgent Agent;public Vector3 Home,Campus,Wait;public int Destination;public State State;public bool Approaching;}
        private readonly List<Student> students=new List<Student>();
        private TownRoadNetwork network;
        private TownVehicleAgent driver;
        private BusVisuals visuals;
        private MotelAtmosphere clock;
        private readonly Vector3[] stops={new Vector3(-100,0,-13),new Vector3(-880,0,20),new Vector3(-950,0,230)};
        private readonly int[] circuit={0,1,2,1,0};
        private int leg;
        private bool atStop;
        private float dwell,alightTimer;
        public int Passengers {get;private set;}
        public void Configure(TownRoadNetwork roads,MotelAtmosphere time,Material uniform,Material skin,Transform population)
        {
            network=roads;clock=time;driver=GetComponent<TownVehicleAgent>();visuals=GetComponent<BusVisuals>();
            for(int i=0;i<8;i++)
            {
                int destination=i%2+1;var obj=new GameObject("Student "+(i+1));obj.transform.SetParent(population,false);
                var agent=obj.AddComponent<TownCitizenAgent>();agent.Initialize(uniform,skin,true);obj.AddComponent<WorldNpc>().Configure("student-"+i,destination==1?"School student":"College student");
                var home=new Vector3(-105+i%4*10,0,0);agent.Warp(home);
                students.Add(new Student{Agent=agent,Home=home,Destination=destination,Campus=destination==1?new Vector3(-880+i*.55f,0,82):new Vector3(-880+i*.55f,0,222),Wait=destination==1?new Vector3(-880+i*.55f,0,33):new Vector3(-937,0,230+i*.55f),State=State.Home});
            }
            driver.SetRoute(System.Array.Empty<Vector3>());atStop=true;visuals.SetDoor(true);
        }
        public void ResumeService()
        {
            if(network==null)return;var body=GetComponent<Rigidbody>();body.isKinematic=true;body.useGravity=false;driver.enabled=true;
            atStop=false;visuals.SetDoor(false);Depart();
        }
        private void Depart()
        {
            var route=network.Route(transform.position,stops[circuit[leg]],false,2.8f);
            if(route.Length==0)return;
            if(route.Length>1)
            {
                var direction=route[route.Length-1]-route[route.Length-2];direction.y=0;direction.Normalize();
                var road=network.Nearest(stops[circuit[leg]]);var bay=TownRoadNetwork.Project(stops[circuit[leg]],road)+Vector3.Cross(Vector3.up,direction)*(road.HalfWidth-1.8f);
                var points=new List<Vector3>(route);points[points.Count-1]=bay;driver.SetRoute(points.ToArray(),9);
            }else driver.SetRoute(route,9);atStop=false;dwell=0;visuals.SetDoor(false);
        }
        private void Walk(Student student,Vector3 destination)
        {
            var from=student.Agent.transform.position;
            var route=Vector3.Distance(destination,visuals.DoorPoint)<.1f?new[]{destination}:network.Route(from,destination,true);
            student.Agent.Walk(route.Length>0?route:new[]{destination});
        }
        private void Update()
        {
            if(network==null)return;float hour=clock==null?8:clock.Hour;bool schoolTime=hour>=7&&hour<16;
            foreach(var student in students)
            {
                if(student.State==State.Home&&schoolTime){student.State=State.ToPickup;Walk(student,new Vector3(-100+(students.IndexOf(student)%4)*.6f,0,0));}
                if(student.State==State.ToPickup&&student.Agent.Arrived)student.State=State.WaitOut;
                if(student.State==State.ToCampus&&student.Agent.Arrived){student.State=State.Campus;student.Agent.gameObject.SetActive(false);}
                if(student.State==State.Campus&&!schoolTime)
                {student.Agent.gameObject.SetActive(true);student.Agent.Warp(student.Campus);student.State=State.ToReturn;Walk(student,student.Wait);}
                if(student.State==State.ToReturn&&student.Agent.Arrived)student.State=State.WaitBack;
                if(student.State==State.ToHome&&student.Agent.Arrived)student.State=State.Home;
            }
            if(!driver.enabled){visuals.SetDoor(false);return;}
            if(!atStop)
            {
                if(!driver.Arrived)return;atStop=true;dwell=0;alightTimer=0;visuals.SetDoor(true);
            }
            dwell+=Time.deltaTime;alightTimer-=Time.deltaTime;int stop=circuit[leg];bool pending=false;
            foreach(var student in students)
            {
                bool leaving=student.State==State.BusOut&&student.Destination==stop||student.State==State.BusBack&&stop==0;
                if(leaving)
                {
                    pending=true;if(alightTimer>0)continue;alightTimer=.8f;
                    student.Agent.gameObject.SetActive(true);student.Agent.Warp(visuals.DoorPoint);Passengers=Mathf.Max(0,Passengers-1);
                    bool home=student.State==State.BusBack;student.State=home?State.ToHome:State.ToCampus;Walk(student,home?student.Home:student.Campus);continue;
                }
                bool boarding=student.State==State.WaitOut&&stop==0&&schoolTime||student.State==State.WaitBack&&student.Destination==stop;
                if(!boarding||Passengers>=12)continue;
                pending=true;
                if(!student.Approaching){student.Approaching=true;Walk(student,visuals.DoorPoint);}
                if(Vector3.Distance(student.Agent.transform.position,visuals.DoorPoint)<1.6f)
                {student.State=stop==0?State.BusOut:State.BusBack;student.Approaching=false;student.Agent.gameObject.SetActive(false);Passengers++;}
            }
            if(dwell<7||pending&&dwell<35)return;
            if(stop==0&&(hour<6||hour>=20))return;
            foreach(var student in students)if(student.Approaching){student.Approaching=false;Walk(student,student.State==State.WaitOut?new Vector3(-100,0,0):student.Wait);}
            leg=leg>=circuit.Length-1?1:leg+1;Depart();
        }
    }
}
