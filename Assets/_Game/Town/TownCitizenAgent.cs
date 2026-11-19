using System.Collections.Generic;
using UnityEngine;
namespace RoadsideHaven
{
    public enum CitizenMood { Idle, Walking, Waiting, Chatting, Dodging, Following, Recovering }
    /// <summary>
    /// Individual sidewalk travellers with articulated human visuals and a small mind of their own:
    /// they notice and step away from approaching vehicles, work their way around anything blocking
    /// them (side-step, try the other side, skip ahead, and as a last resort quietly reposition when
    /// nobody is looking), stop to chat with sociable neighbours, hurry and shelter or open an
    /// umbrella in the rain, look at the player, passing cars and friends, fill idle time with their
    /// phone or by looking around, and children walk hand in hand with a parent.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class TownCitizenAgent : MonoBehaviour
    {
        private static readonly List<TownCitizenAgent> active=new List<TownCitizenAgent>();
        private static Transform player;
        private static float nextPlayerSearch;
        private CharacterController controller;
        private Transform visual;
        private HumanVisual human;
        private Vector3[] route;
        private int next,identity,stuckStage;
        private float vertical,nextThink,pace=1.5f,nextPerceive,nextChatCheck,bestDistance=float.MaxValue,stuckTime,detourUntil,detourSide=1,wallSeen=-9,dodgeUntil,chatUntil,idleSwitch;
        private bool jogging,raining;
        private Vector3 velocity,steering,dodge,wallNormal;
        private Vector3? look;
        private TownCitizenAgent leader,follower,partner;
        private HumanGesture idleGesture;
        private GameObject umbrella;
        private readonly Collider[] neighbours=new Collider[20];
        private static readonly Collider[] nearby=new Collider[32];
        public Vector3 Velocity=>velocity;
        public HumanVisual Human=>human;
        public CitizenMood Mood {get;private set;}
        public TownCitizenAgent Leader=>leader;
        public TownCitizenAgent Follower=>follower;
        public TownCitizenAgent Partner=>partner;
        /// <summary>Times this person worked their own way out of being blocked.</summary>
        public int ProblemsSolved {get;private set;}
        public int DangersAvoided {get;private set;}
        public int Conversations {get;private set;}
        public bool UmbrellaOpen=>umbrella!=null&&umbrella.activeSelf;
        /// <summary>Collapsed on the ground, waiting for paramedics.</summary>
        public bool Incapacitated {get;private set;}
        /// <summary>Taken away (ambulance / hospital ward); invisible until discharged.</summary>
        public bool Away {get;private set;}
        /// <summary>Free to be picked for an incident or to help: not hurt, not away, not with family or a chat partner.</summary>
        public bool Available=>!Incapacitated&&!Away&&leader==null&&follower==null&&partner==null&&human!=null&&isActiveAndEnabled;
        private float attendUntil;private Vector3 attendLook;
        public void Collapse()
        {
            if(human==null)return;EndChat();route=null;Incapacitated=true;Away=false;velocity=Vector3.zero;dodgeUntil=0;
            human.Gesture=HumanGesture.None;human.LookTarget=null;human.Animate(0);if(umbrella!=null)umbrella.SetActive(false);
            human.transform.localRotation=Quaternion.Euler(-90,180,0);human.transform.localPosition=new Vector3(0,.14f,-.85f);Mood=CitizenMood.Recovering;
        }
        public void Hide()
        {
            Away=true;Incapacitated=false;route=null;EndChat();if(visual!=null)visual.gameObject.SetActive(false);if(controller!=null)controller.enabled=false;
        }
        /// <summary>Discharged: standing again at <paramref name="at"/>, optionally walking a route.</summary>
        public void Recover(Vector3 at,Vector3[] walk=null)
        {
            Incapacitated=false;Away=false;attendUntil=0;if(visual!=null)visual.gameObject.SetActive(true);
            if(human!=null){human.transform.localRotation=Quaternion.identity;human.transform.localPosition=Vector3.zero;}
            Warp(at);Mood=CitizenMood.Idle;if(walk!=null&&walk.Length>0)Walk(walk);
        }
        /// <summary>A bystander: go to <paramref name="spot"/>, then watch <paramref name="lookAt"/> (and phone for help).</summary>
        public void Attend(Vector3 spot,Vector3 lookAt,float seconds)
        {
            if(!Available)return;route=new[]{spot};next=0;ResetProgress();attendUntil=Time.time+seconds;attendLook=lookAt;
        }
        public bool Attending=>Time.time<attendUntil;
        public void SetJogging(bool value){jogging=value&&human.Identity.Age!=HumanAge.Senior&&follower==null;}
        public bool Arrived=>leader!=null||route==null||next>=route.Length;
        public void Initialize(Material clothing,Material skin,bool student)
        {
            var role=student?HumanRole.Student:HumanIdentity.RoleFromName(name);
            Setup(HumanIdentity.Create(HumanIdentity.StableSeed(name),role));
        }
        /// <summary>Creates the person with a specific age, e.g. a child walking with a parent.</summary>
        public void InitializeAs(HumanAge age)=>Setup(HumanIdentity.Create(HumanIdentity.StableSeed(name),HumanRole.Civilian,age));
        private void Setup(HumanIdentity person)
        {
            controller=GetComponent<CharacterController>();identity=person.Seed;
            visual=new GameObject("Citizen visual").transform;visual.SetParent(transform,false);human=HumanVisual.Create(visual,person,false);pace=person.Pace;
            controller.height=person.Height;controller.center=Vector3.up*controller.height*.5f;controller.radius=person.IsChild?.18f:.24f;controller.stepOffset=Mathf.Min(.25f,person.Height*.2f);controller.skinWidth=.03f;
            nextThink=Time.time+(identity%17)*.01f;nextPerceive=Time.time+(identity%23)*.011f;idleSwitch=Time.time+(identity%9);
        }
        public void Walk(Vector3[] points){route=points;next=0;gameObject.SetActive(true);ResetProgress();EndChat();}
        public void Warp(Vector3 point){if(controller==null)controller=GetComponent<CharacterController>();controller.enabled=false;transform.position=point+Vector3.up*.3f;controller.enabled=true;vertical=0;velocity=steering=Vector3.zero;ResetProgress();}
        /// <summary>Walks beside <paramref name="parent"/>, holding hands, instead of following a route.</summary>
        public void Follow(TownCitizenAgent parent)
        {
            if(parent==null||parent==this)return;leader=parent;parent.follower=this;parent.jogging=false;route=null;
            Warp(parent.transform.position+parent.Side*.65f);
        }
        private Vector3 Side=>visual!=null?visual.right:transform.right;
        private void OnEnable(){if(!active.Contains(this))active.Add(this);}
        private void OnDisable(){active.Remove(this);EndChat();}
        private void OnDestroy(){if(umbrella!=null)Destroy(umbrella.GetComponent<MeshFilter>().sharedMesh);if(follower!=null&&follower.leader==this)follower.leader=null;if(leader!=null&&leader.follower==this)leader.follower=null;}
        private void ResetProgress(){bestDistance=float.MaxValue;stuckTime=0;stuckStage=0;detourUntil=0;}
        private void EndChat(){if(partner!=null&&partner.partner==this){partner.partner=null;partner.chatUntil=0;}partner=null;chatUntil=0;}
        private bool Seen()
        {
            var camera=Camera.main;if(camera==null||human==null||human.Renderer==null)return false;
            return human.Renderer.isVisible&&(camera.transform.position-transform.position).sqrMagnitude<70*70;
        }
        private void Think(Vector3 direction,float speed)
        {
            Vector3 avoid=Vector3.zero;int count=Physics.OverlapSphereNonAlloc(transform.position+Vector3.up*.8f,2.1f,neighbours,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore);
            for(int i=0;i<count;i++)
            {
                var c=neighbours[i];if(c==null||c.transform.IsChildOf(transform)||!(c is CharacterController))continue;
                var other=c.GetComponent<TownCitizenAgent>();if(other!=null&&(other==leader||other==follower))continue;
                avoid+=TownMotion.PersonalSpace(transform.position,direction*speed,c.transform.position,other==null?Vector3.zero:other.Velocity,identity);
            }
            steering=Vector3.ClampMagnitude(avoid,1);
            Vector3 origin=transform.position+Vector3.up*Mathf.Min(.85f,controller.height*.55f);
            if(direction.sqrMagnitude>.1f&&Physics.SphereCast(origin,.19f,direction,out var hit,.9f,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore)&&!hit.transform.IsChildOf(transform)&&hit.normal.y<.6f)
            {
                // While working round an obstacle, stay committed to the chosen side until the way ahead opens.
                wallNormal=hit.normal;wallNormal.y=0;wallNormal.Normalize();wallSeen=Time.time;
                if(stuckStage>0){steering+=Axis(direction)*detourSide;detourUntil=Mathf.Max(detourUntil,Time.time+.6f);return;}
                var tangent=Vector3.ProjectOnPlane(direction,hit.normal);tangent.y=0;
                if(tangent.sqrMagnitude<.02f)tangent=Vector3.Cross(Vector3.up,direction)*detourSide;
                if(!Physics.SphereCast(origin,.2f,tangent.normalized,out _,.65f,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore))steering+=tangent.normalized;
                else steering=-direction*.8f;
            }
        }
        // ---------- Perception and decisions ----------
        private void Perceive()
        {
            var me=human.Identity;
            raining=WeatherSystem.Active!=null&&WeatherSystem.Active.Rain>.35f;
            look=null;
            // 1. Danger: a vehicle whose path will pass through me soon.
            if(Time.time>=dodgeUntil)
            {
                int count=Physics.OverlapSphereNonAlloc(transform.position+Vector3.up,12,nearby,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore);
                for(int i=0;i<count;i++)
                {
                    var body=nearby[i].attachedRigidbody;if(body==null||body.transform.IsChildOf(transform))continue;
                    var traffic=body.GetComponent<TownVehicleAgent>();
                    Vector3 v=traffic!=null?body.transform.forward*traffic.Speed:body.isKinematic?Vector3.zero:body.linearVelocity;v.y=0;
                    float speed=v.magnitude;if(speed<2.2f)continue;
                    Vector3 rel=transform.position-body.position;rel.y=0;
                    float t=Vector3.Dot(rel,v)/(speed*speed);if(t<0||t>2.4f)continue;
                    Vector3 miss=rel-v*t;var e=nearby[i].bounds.extents;float halfWidth=nearby[i] is BoxCollider box?box.size.x*.5f*body.transform.lossyScale.x:Mathf.Min(e.x,e.z);float reach=halfWidth+.9f;
                    if(miss.magnitude>reach)continue;
                    var away=miss.sqrMagnitude>.01f?miss.normalized:Vector3.Cross(Vector3.up,v/speed)*(identity%2==0?1:-1);
                    dodge=away;dodgeUntil=Time.time+Mathf.Lerp(.7f,1.2f,(reach-miss.magnitude)/reach);DangersAvoided++;look=body.position+Vector3.up;EndChat();
                    break;
                }
            }
            else look=transform.position+dodge*-3+Vector3.up;
            // 2. Social life: sociable neighbours who are both free stop for a chat.
            if(partner==null&&leader==null&&Arrived&&Time.time>=nextChatCheck&&Time.time>=dodgeUntil)
            {
                nextChatCheck=Time.time+1.2f+(identity%7)*.1f;
                foreach(var other in active)
                {
                    if(other==this||other.partner!=null||other.leader!=null||other.Incapacitated||other.Away||!other.Arrived||other.human==null||Time.time<other.dodgeUntil)continue;
                    if((other.transform.position-transform.position).sqrMagnitude>3.2f*3.2f)continue;
                    if((me.Sociability+other.human.Identity.Sociability)*.5f<.45f)continue;
                    partner=other;other.partner=this;float length=Mathf.Lerp(4,11,me.Sociability);chatUntil=other.chatUntil=Time.time+length;
                    Conversations++;other.Conversations++;nextChatCheck=other.nextChatCheck=Time.time+length+15;break;
                }
            }
            if(partner!=null&&(Time.time>=chatUntil||partner==null||!partner.isActiveAndEnabled))EndChat();
            // 3. Attention: the player, a chat partner, or my parent.
            if(look==null)
            {
                if(Time.unscaledTime>=nextPlayerSearch){nextPlayerSearch=Time.unscaledTime+4;var owner=Object.FindFirstObjectByType<OwnerSandboxController>();player=owner!=null?owner.transform:null;}
                if(partner!=null)look=partner.transform.position+Vector3.up*partner.controller.height*.92f;
                else if(player!=null&&me.Curiosity>.35f&&(player.position-transform.position).sqrMagnitude<7*7)look=player.position+Vector3.up*1.6f;
                else if(leader!=null&&(identity+(int)Time.time/3)%3==0)look=leader.transform.position+Vector3.up*leader.controller.height*.92f;
            }
            human.LookTarget=look;
            // 4. Body language.
            if(Time.time>=idleSwitch)
            {
                idleSwitch=Time.time+4+(identity%5);int roll=(identity+(int)(Time.time*7))%5;
                idleGesture=me.Phone&&roll<2?HumanGesture.Phone:roll==2?HumanGesture.LookAround:HumanGesture.Relaxed;
            }
            HumanGesture gesture=HumanGesture.None;
            bool moving=velocity.sqrMagnitude>.2f;
            if(Time.time<dodgeUntil)gesture=HumanGesture.None;
            else if(leader!=null)gesture=Vector3.Dot(leader.transform.position-transform.position,Side)>0?HumanGesture.HoldRight:HumanGesture.HoldLeft;
            else if(follower!=null&&(follower.transform.position-transform.position).sqrMagnitude<1.6f)gesture=Vector3.Dot(follower.transform.position-transform.position,Side)>0?HumanGesture.HoldRight:HumanGesture.HoldLeft;
            else if(raining)gesture=me.Umbrella?HumanGesture.Umbrella:moving?HumanGesture.Shelter:HumanGesture.Relaxed;
            else if(partner!=null)gesture=((int)(Time.time/2.6f)+(identity%2))%2==0?HumanGesture.Talk:HumanGesture.Relaxed;
            else if(!moving)gesture=idleGesture;
            else if(me.Phone&&me.Curiosity<.5f&&((int)(Time.time/6)+identity)%3==0)gesture=HumanGesture.Phone;
            if(Time.time<attendUntil&&Arrived&&Time.time>=dodgeUntil){gesture=identity%2==0?HumanGesture.Phone:HumanGesture.Relaxed;human.LookTarget=attendLook;}
            human.Gesture=gesture;
            bool showUmbrella=raining&&me.Umbrella&&leader==null&&follower==null&&human.Renderer.enabled;
            if(showUmbrella&&umbrella==null)umbrella=BuildUmbrella(me.Accent);
            if(umbrella!=null&&umbrella.activeSelf!=showUmbrella)umbrella.SetActive(showUmbrella);
        }
        private void Update()
        {
            if(controller==null||human==null||Incapacitated||Away)return;
            if(Time.time>=nextPerceive){nextPerceive=Time.time+.2f+(identity%5)*.01f;Perceive();}
            Vector3 move=Vector3.zero;float speed=(jogging?pace*1.75f:pace)*(raining?1.2f:1);
            if(follower!=null)speed=Mathf.Min(speed,follower.pace*1.05f);
            if(Time.time<dodgeUntil){move=dodge*Mathf.Max(2.6f,pace*1.8f);Mood=CitizenMood.Dodging;}
            else if(leader!=null)move=FollowLeader();
            else if(partner!=null)
            {
                Mood=CitizenMood.Chatting;var face=partner.transform.position-transform.position;face.y=0;
                if(face.sqrMagnitude>.01f&&visual!=null)visual.rotation=Quaternion.Slerp(visual.rotation,Quaternion.LookRotation(face),1-Mathf.Exp(-4*Time.deltaTime));
            }
            else if(route!=null&&next<route.Length)move=Travel(speed);
            else Mood=CitizenMood.Idle;
            velocity=Vector3.MoveTowards(velocity,move,Time.deltaTime*(move.sqrMagnitude<.01f?8:Time.time<dodgeUntil?14:4));
            vertical=controller.isGrounded?-2:Mathf.Max(-25,vertical-18*Time.deltaTime);Vector3 before=transform.position;
            controller.Move((velocity+Vector3.up*vertical)*Time.deltaTime);
            Vector3 actual=transform.position-before;actual.y=0;
            if(visual!=null&&actual.sqrMagnitude>.000001f&&Time.time>=dodgeUntil)visual.rotation=Quaternion.Slerp(visual.rotation,Quaternion.LookRotation(actual),1-Mathf.Exp(-7*Time.deltaTime));
            human.Animate(Time.deltaTime>0?actual.magnitude/Time.deltaTime:0,jogging);
            if(umbrella!=null&&umbrella.activeSelf){umbrella.transform.position=human.RightHand.position;umbrella.transform.rotation=Quaternion.Euler(0,visual.eulerAngles.y,0);}
        }
        private Vector3 Travel(float speed)
        {
            Vector3 delta=route[next]-transform.position;delta.y=0;float distance=delta.magnitude;
            if(distance<.55f){next++;ResetProgress();Mood=CitizenMood.Walking;return Vector3.zero;}
            if(StreetJunction.PedestrianMustWait(transform.position,route[next],speed)){Mood=CitizenMood.Waiting;return Vector3.zero;}
            var direction=delta/distance;
            if(Time.time>=nextThink){nextThink=Time.time+.14f+(identity%7)*.009f;Think(direction,speed);}
            SolveBlockage(distance);
            // A small personal lane offset fades out at corners, preserving crossing endpoints.
            float lane=Mathf.Sin(Time.time*.45f+(identity%997)*.01f)*.09f*Mathf.Clamp01(distance-2);
            Vector3 detour=Time.time<detourUntil?Axis(direction)*detourSide*1.2f-direction*.25f:Vector3.zero;
            if(Mood!=CitizenMood.Recovering||Time.time>=detourUntil)Mood=CitizenMood.Walking;
            return Vector3.ClampMagnitude(direction+steering*.85f+detour+Vector3.Cross(Vector3.up,direction)*lane,1)*speed;
        }
        /// <summary>Escalating, self-directed recovery whenever progress towards the next waypoint stalls.</summary>
        private void SolveBlockage(float distance)
        {
            if(distance<bestDistance-.2f){bestDistance=distance;stuckTime=0;stuckStage=0;return;}
            stuckTime+=Time.deltaTime;float patience=Mathf.Lerp(.7f,1.3f,human.Identity.Patience);
            Vector3 toward=route[next]-transform.position;toward.y=0;toward.Normalize();Vector3 right=Axis(toward);
            if(stuckStage==0&&stuckTime>1.6f*patience)
            {
                // Look along the obstacle both ways and head for the nearest gap, keeping any way already being tried.
                float lateral=Vector3.Dot(velocity,right);float preferred=Mathf.Abs(lateral)>.2f?Mathf.Sign(lateral):(identity+ProblemsSolved)%2==0?1:-1;
                stuckStage=1;detourSide=ClearSide(toward,right,preferred);detourUntil=Time.time+3f;Mood=CitizenMood.Recovering;ProblemsSolved++;
            }
            else if(stuckStage==1&&stuckTime>4.6f*patience){stuckStage=2;detourSide=ClearSide(toward,right,detourSide);detourUntil=Time.time+3f;Mood=CitizenMood.Recovering;}
            else if(stuckStage==2&&stuckTime>8f*patience){stuckStage=3;if(next<route.Length-1){next++;bestDistance=float.MaxValue;}detourSide=ClearSide(toward,right,detourSide);detourUntil=Time.time+3f;Mood=CitizenMood.Recovering;ProblemsSolved++;}
            else if(stuckTime>12f*patience)
            {
                // Last resort, never in view of the player: reposition at the waypoint.
                if(!Seen())Warp(route[Mathf.Min(next,route.Length-1)]);else{next=Mathf.Min(next+1,route.Length);ResetProgress();}
                ProblemsSolved++;
            }
        }
        /// <summary>Sideways axis for detours: along the face of the obstacle in the way, oriented to my right.</summary>
        private Vector3 Axis(Vector3 toward)
        {
            Vector3 right=Vector3.Cross(Vector3.up,toward);
            if(Time.time-wallSeen>1.5f||wallNormal.sqrMagnitude<.5f)return right;
            Vector3 along=Vector3.Cross(Vector3.up,-wallNormal);return Vector3.Dot(along,right)<0?-along:along;
        }
        private bool Blocked(Vector3 direction)
        {
            Vector3 origin=transform.position+Vector3.up*Mathf.Min(.85f,controller.height*.55f);
            return Physics.SphereCast(origin,.2f,direction,out var hit,1.3f,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore)&&!hit.transform.IsChildOf(transform);
        }
        /// <summary>Finds the side with the nearest opening: a reachable point from which the way ahead is clear.</summary>
        private float ClearSide(Vector3 toward,Vector3 right,float preferred)
        {
            Vector3 origin=transform.position+Vector3.up*Mathf.Min(.85f,controller.height*.55f)+(Time.time-wallSeen<1.5f?wallNormal*.12f:Vector3.zero);
            for(float d=1;d<=8;d+=1)for(int k=0;k<2;k++)
            {
                float s=k==0?preferred:-preferred;Vector3 side=right*s;
                if(Physics.SphereCast(origin,.2f,side,out var wall,d,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore)&&!wall.transform.IsChildOf(transform))continue;
                if(!Physics.SphereCast(origin+side*d,.22f,toward,out var ahead,1.8f,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore)||ahead.transform.IsChildOf(transform))return s;
            }
            return preferred;
        }
        private Vector3 FollowLeader()
        {
            Mood=CitizenMood.Following;
            Vector3 target=leader.transform.position+leader.Side*.62f-leader.velocity.normalized*.05f;Vector3 delta=target-transform.position;delta.y=0;float distance=delta.magnitude;
            if(distance>14&&!Seen()){Warp(target);ProblemsSolved++;return Vector3.zero;}
            if(distance<.25f)return leader.velocity;
            if(Time.time>=nextThink){nextThink=Time.time+.14f;Think(delta/distance,pace);}
            float catchUp=Mathf.Clamp(distance*2.2f,0,Mathf.Max(pace*1.9f,leader.velocity.magnitude*1.4f));
            return Vector3.ClampMagnitude(delta/distance+steering*.5f,1)*catchUp+leader.velocity*Mathf.Clamp01(1-distance);
        }
        private GameObject BuildUmbrella(Color canopy)
        {
            var go=new GameObject("Umbrella");go.transform.SetParent(transform,false);
            var vertices=new List<Vector3>();var colors=new List<Color>();var tris=new List<int>();const int n=10;
            void Add(Vector3 v,Color c){vertices.Add(v);colors.Add(c);}
            Color shaft=new Color(.12f,.12f,.12f);
            for(int i=0;i<n;i++){float a=i*Mathf.PI*2/n,b=(i+1)*Mathf.PI*2/n;int s=vertices.Count;Color c=i%2==0?canopy:Color.Lerp(canopy,Color.white,.15f);
                for(int face=0;face<2;face++){Add(new Vector3(0,1.08f,0),c);Add(new Vector3(Mathf.Cos(a)*.52f,.9f,Mathf.Sin(a)*.52f),c);Add(new Vector3(Mathf.Cos(b)*.52f,.9f,Mathf.Sin(b)*.52f),c);}
                tris.AddRange(new[]{s,s+2,s+1,s+3,s+4,s+5});}
            for(int i=0;i<6;i++){float a=i*Mathf.PI*2/6,b=(i+1)*Mathf.PI*2/6;int s=vertices.Count;
                Add(new Vector3(Mathf.Cos(a)*.012f,-.05f,Mathf.Sin(a)*.012f),shaft);Add(new Vector3(Mathf.Cos(b)*.012f,-.05f,Mathf.Sin(b)*.012f),shaft);Add(new Vector3(Mathf.Cos(a)*.012f,1.08f,Mathf.Sin(a)*.012f),shaft);Add(new Vector3(Mathf.Cos(b)*.012f,1.08f,Mathf.Sin(b)*.012f),shaft);
                tris.AddRange(new[]{s,s+2,s+1,s+1,s+2,s+3});}
            var mesh=new Mesh{name="Umbrella"};mesh.SetVertices(vertices);mesh.SetColors(colors);mesh.SetTriangles(tris,0);mesh.RecalculateNormals();
            go.AddComponent<MeshFilter>().sharedMesh=mesh;var r=go.AddComponent<MeshRenderer>();r.sharedMaterial=human.Renderer.sharedMaterial;r.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.On;
            return go;
        }
    }
}
