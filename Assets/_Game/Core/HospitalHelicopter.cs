using UnityEngine;
namespace RoadsideHaven
{
    /// <summary>Deterministic air-ambulance circuit; skids sit on the pad, never below it.</summary>
    public sealed class HospitalHelicopter:MonoBehaviour
    {
        [SerializeField] Transform aircraft,rotor,tail;
        [SerializeField] Vector3 pad;
        public const float Cycle=100;
        public string Phase {get;private set;}
        public Transform Aircraft=>aircraft;
        public void Configure(Transform body,Transform main,Transform rear,Vector3 landing){aircraft=body;rotor=main;tail=rear;pad=landing;Evaluate(0);}
        public void Evaluate(float seconds)
        {
            if(aircraft==null)return;float t=Mathf.Repeat(seconds,Cycle),y=0;Vector3 offset;float pitch=0;
            if(t<24){Phase="Approaching";float u=Mathf.SmoothStep(0,1,t/24);offset=Vector3.Lerp(new Vector3(0,65,-350),new Vector3(0,24,0),u);pitch=-4*Mathf.Sin(u*Mathf.PI);}
            else if(t<38){Phase="Landing";y=Mathf.Lerp(24,0,Mathf.SmoothStep(0,1,(t-24)/14));offset=Vector3.up*y;}
            else if(t<66){Phase="Landed / medical transfer";offset=Vector3.zero;}
            else if(t<80){Phase="Taking off";offset=Vector3.up*Mathf.Lerp(0,36,Mathf.SmoothStep(0,1,(t-66)/14));}
            else {Phase="Departing";float u=Mathf.SmoothStep(0,1,(t-80)/20);offset=Vector3.Lerp(new Vector3(0,36,0),new Vector3(0,65,-350),u);pitch=4*Mathf.Sin(u*Mathf.PI);}
            aircraft.position=pad+offset;aircraft.rotation=Quaternion.Euler(pitch,t>=66?180*Mathf.SmoothStep(0,1,(t-66)/14):0,0);
            float spin=t>=42&&t<62?65:1150;if(rotor!=null)rotor.localRotation=Quaternion.Euler(0,seconds*spin,0);if(tail!=null)tail.localRotation=Quaternion.Euler(seconds*spin*1.8f,0,0);
        }
        void Update()=>Evaluate(Time.time);
    }
}
