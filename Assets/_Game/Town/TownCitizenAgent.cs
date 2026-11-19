using UnityEngine;
namespace RoadsideHaven
{
    /// <summary>Individual sidewalk travellers with local separation, anticipation and articulated human visuals.</summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class TownCitizenAgent : MonoBehaviour
    {
        private CharacterController controller;
        private Transform visual;
        private HumanVisual human;
        private Vector3[] route;
        private int next,identity;
        private float vertical,nextThink,pace=1.5f;
        private bool jogging;
        private Vector3 velocity,steering;
        private readonly Collider[] neighbours=new Collider[20];
        public Vector3 Velocity=>velocity;
        public void SetJogging(bool value){jogging=value&&human.Identity.Age!=HumanAge.Senior;}
        public bool Arrived=>route==null||next>=route.Length;
        public void Initialize(Material clothing,Material skin,bool student)
        {
            controller=GetComponent<CharacterController>();identity=HumanIdentity.StableSeed(name);var role=student?HumanRole.Student:HumanIdentity.RoleFromName(name);
            visual=new GameObject("Citizen visual").transform;visual.SetParent(transform,false);human=HumanVisual.Create(visual,identity,role,false);pace=human.Identity.Pace;
            controller.height=human.Identity.Height;controller.center=Vector3.up*controller.height*.5f;controller.radius=.24f;controller.stepOffset=.25f;controller.skinWidth=.03f;
            nextThink=Time.time+(identity%17)*.01f;
        }
        public void Walk(Vector3[] points){route=points;next=0;gameObject.SetActive(true);}
        public void Warp(Vector3 point){if(controller==null)controller=GetComponent<CharacterController>();controller.enabled=false;transform.position=point+Vector3.up*.3f;controller.enabled=true;vertical=0;velocity=steering=Vector3.zero;}
        private void Think(Vector3 direction,float speed)
        {
            Vector3 avoid=Vector3.zero;int count=Physics.OverlapSphereNonAlloc(transform.position+Vector3.up*.8f,2.1f,neighbours,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore);
            for(int i=0;i<count;i++)
            {
                var c=neighbours[i];if(c==null||c.transform.IsChildOf(transform)||!(c is CharacterController))continue;
                var other=c.GetComponent<TownCitizenAgent>();avoid+=TownMotion.PersonalSpace(transform.position,direction*speed,c.transform.position,other==null?Vector3.zero:other.Velocity,identity);
            }
            steering=Vector3.ClampMagnitude(avoid,1);
            Vector3 origin=transform.position+Vector3.up*.85f;
            if(direction.sqrMagnitude>.1f&&Physics.SphereCast(origin,.19f,direction,out var hit,.9f,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore)&&!hit.transform.IsChildOf(transform)&&hit.normal.y<.6f)
            {
                var tangent=Vector3.ProjectOnPlane(direction,hit.normal);tangent.y=0;
                if(tangent.sqrMagnitude<.02f)tangent=Vector3.Cross(Vector3.up,direction);
                if(!Physics.SphereCast(origin,.2f,tangent.normalized,out _,.65f,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore))steering+=tangent.normalized;
                else steering=-direction*.8f;
            }
        }
        private void Update()
        {
            if(controller==null)return;Vector3 move=Vector3.zero;float speed=jogging?pace*1.75f:pace;
            if(route!=null&&next<route.Length)
            {
                Vector3 delta=route[next]-transform.position;delta.y=0;
                if(delta.magnitude<.55f)next++;
                else if(!StreetJunction.PedestrianMustWait(transform.position,route[next],speed))
                {
                    var direction=delta.normalized;
                    if(Time.time>=nextThink){nextThink=Time.time+.14f+(identity%7)*.009f;Think(direction,speed);}
                    // A small personal lane offset fades out at corners, preserving crossing endpoints.
                    float lane=Mathf.Sin(Time.time*.45f+(identity%997)*.01f)*.09f*Mathf.Clamp01(delta.magnitude-2);
                    move=Vector3.ClampMagnitude(direction+steering*.85f+Vector3.Cross(Vector3.up,direction)*lane,1)*speed;
                }
            }
            velocity=Vector3.MoveTowards(velocity,move,Time.deltaTime*(move.sqrMagnitude<.01f?8:4));
            vertical=controller.isGrounded?-2:Mathf.Max(-25,vertical-18*Time.deltaTime);Vector3 before=transform.position;
            controller.Move((velocity+Vector3.up*vertical)*Time.deltaTime);
            Vector3 actual=transform.position-before;actual.y=0;
            if(visual!=null&&actual.sqrMagnitude>.000001f)visual.rotation=Quaternion.Slerp(visual.rotation,Quaternion.LookRotation(actual),1-Mathf.Exp(-7*Time.deltaTime));
            human.Animate(Time.deltaTime>0?actual.magnitude/Time.deltaTime:0,jogging);
        }
    }
}
