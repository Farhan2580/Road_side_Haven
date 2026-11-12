using UnityEngine;
namespace RoadsideHaven
{
    /// <summary>Collision-tested lane traffic. Route changes never teleport a live vehicle.</summary>
    [RequireComponent(typeof(Rigidbody))]
    public sealed class TownVehicleAgent : MonoBehaviour
    {
        [SerializeField,Min(1)] private float cruiseSpeed=9;
        [SerializeField,Min(10)] private float turnRate=70;
        private Rigidbody body;
        private BoxCollider box;
        private Vector3[] route;
        private int next;
        private float speed;
        private readonly RaycastHit[] hits=new RaycastHit[48];
        private readonly Collider[] overlaps=new Collider[48];
        public bool Arrived=>route!=null&&next>=route.Length;
        public bool HasRoute=>route!=null&&route.Length>0;
        public float BlockedSeconds {get;private set;}
        public void SetRoute(Vector3[] points,float metersPerSecond=9)
        {route=points;next=0;cruiseSpeed=metersPerSecond;BlockedSeconds=0;}
        private void Awake(){body=GetComponent<Rigidbody>();box=GetComponent<BoxCollider>();body.isKinematic=true;body.useGravity=false;body.interpolation=RigidbodyInterpolation.Interpolate;}
        private bool Other(Collider c)=>c!=null&&!c.isTrigger&&!c.transform.IsChildOf(transform);
        private void FixedUpdate()
        {
            if(route==null||next>=route.Length)return;
            Vector3 delta=route[next]-body.position;delta.y=0;
            while(delta.magnitude<1.1f){next++;if(next>=route.Length){speed=0;return;}delta=route[next]-body.position;delta.y=0;}
            if(delta.sqrMagnitude<.01f)return;
            float angle=Vector3.Angle(transform.forward,delta);
            float wanted=cruiseSpeed*Mathf.Lerp(1,.18f,Mathf.Clamp01(angle/70));
            if(next==route.Length-1)wanted=Mathf.Min(wanted,Mathf.Max(.7f,delta.magnitude*.7f));
            wanted=Mathf.Min(wanted,StreetJunction.VehicleLimit(body.position,delta.normalized,box==null?2:box.size.z*.5f,speed));
            Vector3 center=box==null?Vector3.up*.8f:box.center;
            Vector3 extent=box==null?new Vector3(1,.65f,2):box.size*.48f;
            // Sweep the entire vehicle, including its sides. A nose-only ray missed cars already overlapping.
            int count=Physics.BoxCastNonAlloc(body.position+body.rotation*center,extent,delta.normalized,hits,body.rotation,Mathf.Max(1.8f,speed*.8f),Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore);
            bool blocked=false;
            for(int i=0;i<count;i++)if(Other(hits[i].collider)&&hits[i].normal.y<.65f){wanted=0;blocked=true;break;}
            speed=Mathf.MoveTowards(speed,wanted,(wanted<speed?18:2.5f)*Time.fixedDeltaTime);
            var rotation=Quaternion.RotateTowards(body.rotation,Quaternion.LookRotation(delta),turnRate*Time.fixedDeltaTime);
            var position=body.position+delta.normalized*Mathf.Min(delta.magnitude,speed*Time.fixedDeltaTime);position.y=.09f;
            count=Physics.OverlapBoxNonAlloc(position+rotation*center,extent,overlaps,rotation,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore);
            for(int i=0;i<count;i++)if(Other(overlaps[i])){blocked=true;speed=0;position=body.position;rotation=body.rotation;break;}
            BlockedSeconds=blocked?BlockedSeconds+Time.fixedDeltaTime:0;
            body.MoveRotation(rotation);body.MovePosition(position);
        }
    }
}
