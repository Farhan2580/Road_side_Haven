using UnityEngine;
namespace RoadsideHaven
{
    /// <summary>Collision-tested traffic with anticipation, individual pace and gradual following brakes.</summary>
    [RequireComponent(typeof(Rigidbody))]
    public sealed class TownVehicleAgent : MonoBehaviour
    {
        [SerializeField,Min(1)] private float cruiseSpeed=9;
        [SerializeField,Min(10)] private float turnRate=70;
        private Rigidbody body;
        private BoxCollider box;
        private Vector3[] route;
        private int next;
        private float speed,personality=1;
        private readonly RaycastHit[] hits=new RaycastHit[48];
        private readonly Collider[] overlaps=new Collider[48];
        public bool Arrived=>route!=null&&next>=route.Length;
        public bool HasRoute=>route!=null&&route.Length>0;
        public float BlockedSeconds {get;private set;}
        public float Speed=>speed;
        public void SetRoute(Vector3[] points,float metersPerSecond=9)
        {route=points;next=0;cruiseSpeed=metersPerSecond;BlockedSeconds=0;}
        private void Awake(){body=GetComponent<Rigidbody>();box=GetComponent<BoxCollider>();body.isKinematic=true;body.useGravity=false;body.interpolation=RigidbodyInterpolation.Interpolate;personality=.88f+HumanIdentity.StableSeed(name)%25*.01f;}
        private bool Other(Collider c)=>c!=null&&!c.isTrigger&&!c.transform.IsChildOf(transform);
        private void FixedUpdate()
        {
            if(route==null||next>=route.Length)return;
            Vector3 delta=route[next]-body.position;delta.y=0;
            while(delta.magnitude<1.1f){next++;if(next>=route.Length){speed=0;return;}delta=route[next]-body.position;delta.y=0;}
            if(delta.sqrMagnitude<.01f)return;
            float angle=Vector3.Angle(transform.forward,delta);
            float wanted=cruiseSpeed*personality*Mathf.Lerp(1,.18f,Mathf.Clamp01(angle/70));
            if(next==route.Length-1)wanted=Mathf.Min(wanted,Mathf.Max(.7f,delta.magnitude*.7f));
            wanted=Mathf.Min(wanted,StreetJunction.VehicleLimit(body.position,delta.normalized,box==null?2:box.size.z*.5f,speed));
            Vector3 center=box==null?Vector3.up*.8f:box.center;
            Vector3 extent=box==null?new Vector3(1,.65f,2):box.size*.48f;
            float lookAhead=Mathf.Max(4,speed*1.3f+speed*speed/12);
            int count=Physics.BoxCastNonAlloc(body.position+body.rotation*center,extent,delta.normalized,hits,body.rotation,lookAhead,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore);
            bool blocked=false;
            for(int i=0;i<count;i++)
            {
                if(!Other(hits[i].collider)||hits[i].normal.y>=.65f)continue;
                var lead=hits[i].rigidbody;float leadSpeed=lead==null?0:Mathf.Max(0,Vector3.Dot(lead.linearVelocity,delta.normalized));
                wanted=Mathf.Min(wanted,TownMotion.FollowingSpeed(hits[i].distance,leadSpeed,wanted));if(hits[i].distance<1.1f)blocked=true;
            }
            speed=Mathf.MoveTowards(speed,wanted,(wanted<speed?6.5f:2.2f)*Time.fixedDeltaTime);
            var rotation=Quaternion.RotateTowards(body.rotation,Quaternion.LookRotation(delta),turnRate*Time.fixedDeltaTime);
            var position=body.position+delta.normalized*Mathf.Min(delta.magnitude,speed*Time.fixedDeltaTime);position.y=.09f;
            count=Physics.OverlapBoxNonAlloc(position+rotation*center,extent,overlaps,rotation,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore);
            for(int i=0;i<count;i++)if(Other(overlaps[i])){blocked=true;speed=0;position=body.position;rotation=body.rotation;break;}
            BlockedSeconds=blocked?BlockedSeconds+Time.fixedDeltaTime:0;
            body.MoveRotation(rotation);body.MovePosition(position);
        }
    }
}
