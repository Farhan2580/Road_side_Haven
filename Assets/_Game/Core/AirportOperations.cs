using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;
namespace RoadsideHaven
{
    /// <summary>Deterministic airport traffic and pooled passenger animation. Geometry is baked in Edit Mode.</summary>
    public sealed class AirportOperations : MonoBehaviour
    {
        [Header("Aircraft")]
        [SerializeField] private Transform jet;
        [SerializeField] private Transform helicopter;
        [SerializeField] private Transform mainRotor;
        [SerializeField] private Transform tailRotor;
        [SerializeField] private Transform gear;
        [SerializeField] private Transform bridge;
        [Header("Terminal")]
        [SerializeField] private Transform[] passengers;
        [SerializeField] private Transform[] legs;
        [SerializeField] private Transform[] entranceDoors;
        [SerializeField] private Light[] apronLights;
        [SerializeField] private TMP_Text flightBoard;
        [Header("Timing")]
        [SerializeField,Min(10)] private float boardingSeconds=28;
        [SerializeField,Min(20)] private float flightSeconds=65;
        private float clock;
        private Transform radar;
        private OwnerSandboxController owner;
        private MotelAtmosphere atmosphere;
        private float nextLights;
        private int displayedPhase=-1;
        public const string RootName="Haven International Airport v1";
        public static readonly Vector3 Gate=new Vector3(780,.12f,-350);
        public static readonly Vector3 VisitPoint=new Vector3(790,.24f,-244);
        private static readonly Vector3[] Flight={
            new Vector3(560,24,-515),new Vector3(220,110,-515),new Vector3(-200,160,-250),
            new Vector3(-280,175,200),new Vector3(150,150,420),new Vector3(700,115,140),
            new Vector3(1050,60,-220),new Vector3(1080,28,-515),new Vector3(920,.12f,-515)};
        private static readonly string[] BoardText={"HV101  BOARDING / GATE A1","HV101  PUSHBACK","HV101  TAXIING","HV101  DEPARTING","HV101  IN FLIGHT","HV101  LANDED","HV101  ARRIVING","HV101  DISEMBARKING"};
        public int Phase {get;private set;}
        public void Configure(Transform plane,Transform heli,Transform rotor,Transform tail,Transform landingGear,Transform gateBridge,Transform[] people,Transform[] peopleLegs,Transform[] doors,Light[] lights,TMP_Text board)
        {jet=plane;helicopter=heli;mainRotor=rotor;tailRotor=tail;gear=landingGear;bridge=gateBridge;passengers=people;legs=peopleLegs;entranceDoors=doors;apronLights=lights;flightBoard=board;}
        private void Start(){radar=transform.Find("ATC rotating radar");owner=GetComponentInParent<MotelGameRoot>()?.GetComponentInChildren<OwnerSandboxController>(true);atmosphere=GetComponentInParent<MotelAtmosphere>();}
        private void Update()
        {
            clock+=Time.deltaTime;float t=clock;if(radar!=null)radar.Rotate(0,22*Time.deltaTime,0);
            if(Keyboard.current!=null&&Keyboard.current.f10Key.wasPressedThisFrame&&owner!=null&&owner.IsWalking&&!owner.HasSupplyCargo)VisitAirport();
            if(mainRotor!=null)mainRotor.Rotate(0,950*Time.deltaTime,0,Space.Self);
            if(tailRotor!=null)tailRotor.Rotate(1600*Time.deltaTime,0,0,Space.Self);
            float cycle=boardingSeconds+4+10+22+10+flightSeconds+8+22+20;
            float c=Mathf.Repeat(t,cycle),start=0;


            int phase;float progress;
            if(c<boardingSeconds){phase=0;progress=c/boardingSeconds;}
            else if(c<(start=boardingSeconds)+14){phase=1;progress=(c-start)/14;}
            else if(c<(start=boardingSeconds+14)+22){phase=2;progress=(c-start)/22;}
            else if(c<(start=boardingSeconds+36)+10){phase=3;progress=(c-start)/10;}
            else if(c<(start=boardingSeconds+46)+flightSeconds){phase=4;progress=(c-start)/flightSeconds;}
            else if(c<(start=boardingSeconds+46+flightSeconds)+8){phase=5;progress=(c-start)/8;}
            else if(c<(start=boardingSeconds+54+flightSeconds)+22){phase=6;progress=(c-start)/22;}
            else{phase=7;progress=Mathf.Clamp01((c-(boardingSeconds+76+flightSeconds))/20);}
            Phase=phase;
            if(displayedPhase!=phase){displayedPhase=phase;if(flightBoard!=null)flightBoard.text=BoardText[phase];}
            if(jet!=null)
            {
                Vector3 position=JetPosition(phase,progress);
                Vector3 next=JetPosition(phase,Mathf.Min(1,progress+.002f));var direction=next-position;
                jet.position=transform.TransformPoint(position);
                if(phase==0||phase==1||phase==7)jet.rotation=Quaternion.Slerp(jet.rotation,Quaternion.identity,Time.deltaTime*2);
                else if(direction.sqrMagnitude>.00001f)jet.rotation=Quaternion.Slerp(jet.rotation,Quaternion.LookRotation(direction,Vector3.up),Time.deltaTime*3);
                if(gear!=null){float extension=phase==4?Mathf.Clamp01(1-Mathf.Sin(progress*Mathf.PI)*2):1;gear.localScale=new Vector3(1,Mathf.Max(.02f,extension),1);gear.localPosition=Vector3.up*(1-extension)*1.5f;gear.gameObject.SetActive(extension>.02f);}
            }
            if(bridge!=null){float retracted=phase==0||phase==7?0:phase==1?Mathf.Clamp01(progress*3):phase==6?1-Mathf.Clamp01((progress-.85f)/.15f):1;bridge.localPosition=new Vector3(-6*retracted,0,0);}
            if(helicopter!=null)
            {
                float h=Mathf.Repeat(t,60);Vector3 p=new Vector3(600,.12f,-455);
                if(h>10&&h<18)p.y=Mathf.Lerp(.12f,48,Smooth((h-10)/8));
                else if(h>=18&&h<50){float a=(h-18)/32*Mathf.PI*2;p+=new Vector3(160*Mathf.Sin(a),48+25*Mathf.Sin(a*.5f),130*(1-Mathf.Cos(a)));helicopter.rotation=Quaternion.Slerp(helicopter.rotation,Quaternion.Euler(0,Mathf.Cos(a)*90,Mathf.Sin(a)*-8),Time.deltaTime*2);}
                else if(h>=50)p.y=Mathf.Lerp(48,.12f,Smooth((h-50)/10));
                helicopter.position=transform.TransformPoint(p);
            }
            AnimatePeople(phase,progress);
            if(entranceDoors!=null){float open=owner!=null&&Vector3.Distance(owner.transform.position,transform.TransformPoint(VisitPoint))<12?1:0;for(int i=0;i<entranceDoors.Length;i++)if(entranceDoors[i]!=null){var p=entranceDoors[i].localPosition;p.x=Mathf.Lerp(p.x,(i==0?-1:1)*(2+open*2),Time.deltaTime*4);entranceDoors[i].localPosition=p;}}
            if(t>=nextLights){nextLights=t+1;bool night=atmosphere!=null&&(atmosphere.Hour<6||atmosphere.Hour>18);if(apronLights!=null)foreach(var light in apronLights)if(light!=null)light.enabled=night&&Camera.main!=null&&Vector3.Distance(light.transform.position,Camera.main.transform.position)<230;}
        }
        public void VisitAirport()
        {
            if(owner==null)owner=GetComponentInParent<MotelGameRoot>()?.GetComponentInChildren<OwnerSandboxController>(true);
            if(owner==null||!owner.IsWalking||owner.HasSupplyCargo)return;
            owner.ExitVehicle(transform.TransformPoint(VisitPoint));
        }
        public static Vector3 JetPosition(int phase,float p)
        {
            p=Mathf.Clamp01(p);
            switch(phase){
                case 0:case 7:return Gate;
                case 1:return Vector3.Lerp(Gate,new Vector3(780,.12f,-405),Smooth(Mathf.Clamp01((p-.28f)/.72f)));
                case 2:return Curve(new Vector3(780,.12f,-405),new Vector3(870,.12f,-405),new Vector3(960,.12f,-515),new Vector3(920,.12f,-515),p);
                case 3:return Vector3.Lerp(new Vector3(920,.12f,-515),new Vector3(560,24,-515),p*p);
                case 4:return SampleFlight(p);
                case 5:return Vector3.Lerp(new Vector3(920,.12f,-515),new Vector3(600,.12f,-515),1-(1-p)*(1-p));
                default:return Curve(new Vector3(600,.12f,-515),new Vector3(550,.12f,-390),new Vector3(780,.12f,-420),Gate,p);
            }
        }
        private static Vector3 SampleFlight(float t)
        {float v=t*(Flight.Length-1);int i=Mathf.Min(Flight.Length-2,Mathf.FloorToInt(v));float f=v-i;var a=Flight[Mathf.Max(0,i-1)];var b=Flight[i];var c=Flight[i+1];var d=Flight[Mathf.Min(Flight.Length-1,i+2)];return .5f*((2*b)+(-a+c)*f+(2*a-5*b+4*c-d)*f*f+(-a+3*b-3*c+d)*f*f*f);}
        private static Vector3 Curve(Vector3 a,Vector3 b,Vector3 c,Vector3 d,float t){float s=1-t;return s*s*s*a+3*s*s*t*b+3*s*t*t*c+t*t*t*d;}
        private static float Smooth(float t)=>t*t*(3-2*t);
        private void AnimatePeople(int phase,float p)
        {
            if(passengers==null)return;
            for(int i=0;i<passengers.Length;i++)
            {
                var person=passengers[i];if(person==null)continue;
                bool active=phase==0||phase==7&&p>=i*.025f;person.gameObject.SetActive(active);if(!active)continue;
                float progress=Mathf.Clamp01((p-i*.025f)*1.6f);
                float path=phase==7?1-progress:progress;
                Vector3 a=new Vector3(750+(i%3)*.55f,.2f,-282-(i/3)*.65f),b=new Vector3(750,3.2f,-308),c=new Vector3(750,3.2f,-325),d=new Vector3(778,3.2f,-325),e=new Vector3(778,3.2f,-340);
                var rampStart=new Vector3(750,.2f,-288);Vector3 position=path<.12f?Vector3.Lerp(a,rampStart,path/.12f):path<.42f?Vector3.Lerp(rampStart,b,(path-.12f)/.3f):path<.62f?Vector3.Lerp(b,c,(path-.42f)/.2f):path<.83f?Vector3.Lerp(c,d,(path-.62f)/.21f):Vector3.Lerp(d,e,(path-.83f)/.17f);
                position=transform.TransformPoint(position);Vector3 delta=position-person.position;person.position=position;var horizontal=Vector3.ProjectOnPlane(delta,Vector3.up);if(horizontal.sqrMagnitude>.0001f)person.rotation=Quaternion.LookRotation(horizontal);
                if(phase==0&&progress>=1)person.gameObject.SetActive(false);
                if(legs!=null&&i*2+1<legs.Length){float gait=Mathf.Sin(clock*9+i)*25;legs[i*2].localRotation=Quaternion.Euler(gait,0,0);legs[i*2+1].localRotation=Quaternion.Euler(-gait,0,0);}
            }
        }
    }
}