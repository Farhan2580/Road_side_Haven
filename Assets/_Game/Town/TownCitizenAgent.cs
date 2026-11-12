using UnityEngine;
namespace RoadsideHaven
{
    /// <summary>Collision-aware sidewalk walking with a simple articulated shared-material citizen.</summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class TownCitizenAgent : MonoBehaviour
    {
        private CharacterController controller;
        private Transform visual;
        private Transform[] limbs;
        private Vector3[] route;
        private int next;
        private float vertical,gait;
        private bool jogging;
        public void SetJogging(bool value){jogging=value;}
        public bool Arrived=>route==null||next>=route.Length;
        public void Initialize(Material clothing,Material skin,bool student)
        {
            controller=GetComponent<CharacterController>();controller.height=student?1.45f:1.75f;controller.center=Vector3.up*controller.height*.5f;controller.radius=.23f;controller.stepOffset=.25f;controller.skinWidth=.03f;
            visual=new GameObject("Citizen visual").transform;visual.SetParent(transform,false);visual.localScale=Vector3.one*(student?.83f:1);
            Part(visual,new Vector3(0,1,0),new Vector3(.44f,.58f,.28f),clothing);
            Part(visual,new Vector3(0,1.48f,0),new Vector3(.3f,.34f,.3f),skin);
            limbs=new Transform[4];for(int i=0;i<4;i++)
            {
                var limb=new GameObject("Walking limb").transform;limb.SetParent(visual,false);limb.localPosition=new Vector3((i%2==0?-1:1)*(i<2?.29f:.12f),i<2?1.2f:.72f,0);limbs[i]=limb;
                Part(limb,Vector3.down*.3f,new Vector3(.13f,.6f,.14f),i<2?skin:clothing);
            }
        }
        private static void Part(Transform parent,Vector3 position,Vector3 scale,Material material)
        {
            var obj=GameObject.CreatePrimitive(PrimitiveType.Capsule);obj.transform.SetParent(parent,false);obj.transform.localPosition=position;obj.transform.localScale=new Vector3(scale.x,scale.y*.5f,scale.z);obj.GetComponent<Renderer>().sharedMaterial=material;
            var collider=obj.GetComponent<Collider>();collider.enabled=false;if(Application.isPlaying)Destroy(collider);else DestroyImmediate(collider);
        }
        public void Walk(Vector3[] points){route=points;next=0;gameObject.SetActive(true);}
        public void Warp(Vector3 point){if(controller==null)controller=GetComponent<CharacterController>();controller.enabled=false;transform.position=point+Vector3.up*.3f;controller.enabled=true;vertical=0;}
        private void Update()
        {
            Vector3 move=Vector3.zero;
            if(route!=null&&next<route.Length)
            {
                Vector3 delta=route[next]-transform.position;delta.y=0;
                if(delta.magnitude<.7f)next++;
                else
                {
                    move=StreetJunction.PedestrianMustWait(transform.position,route[next],jogging?2.9f:1.65f)?Vector3.zero:delta.normalized;
                    if(Physics.Raycast(transform.position+Vector3.up*.75f,move,out var hit,.85f,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore)&&!hit.transform.IsChildOf(transform))
                    {Vector3 side=Vector3.Cross(Vector3.up,move);if(Physics.Raycast(transform.position+Vector3.up*.75f,side,.8f))side=-side;move=(move*.15f+side).normalized;}
                }
            }
            vertical=controller.isGrounded?-2:Mathf.Max(-25,vertical-18*Time.deltaTime);
            controller.Move((move*(jogging?2.9f:1.65f)+Vector3.up*vertical)*Time.deltaTime);
            if(visual!=null&&move.sqrMagnitude>.01f)visual.rotation=Quaternion.Slerp(visual.rotation,Quaternion.LookRotation(move),Time.deltaTime*8);
            gait+=move.magnitude*Time.deltaTime*(jogging?11:7);
            if(limbs!=null)for(int i=0;i<4;i++)limbs[i].localRotation=Quaternion.Euler(Mathf.Sin(gait+(i==0||i==3?0:Mathf.PI))*25*move.magnitude,0,0);
        }
    }
}
