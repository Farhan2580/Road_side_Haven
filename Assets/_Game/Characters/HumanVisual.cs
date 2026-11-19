using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
namespace RoadsideHaven
{
    /// <summary>Body-language poses the citizen mind can ask for; blended over the walk cycle.</summary>
    public enum HumanGesture { None, Phone, Talk, Shelter, Umbrella, HoldLeft, HoldRight, Wave, Relaxed, LookAround }
    /// <summary>One skinned renderer/material per person, with deterministic faces, modern clothes, child proportions and distance-based detail.</summary>
    public sealed class HumanVisual : MonoBehaviour
    {
        public HumanIdentity Identity {get;private set;}
        public SkinnedMeshRenderer Renderer {get;private set;}
        public bool AutomaticAnimation=true;
        /// <summary>Sitting pose: thighs forward, shins down, pelvis lowered by one thigh length (seat ≈ 0.49 m × scale).</summary>
        public bool Seated;
        /// <summary>Pose requested by the citizen mind.</summary>
        public HumanGesture Gesture;
        /// <summary>World point the person turns their head towards (a passing car, a friend, the player).</summary>
        public Vector3? LookTarget;
        public HumanGesture ShownGesture => gestureWeight>.5f?shownGesture:HumanGesture.None;
        private Transform[] bones;
        private Mesh lowMesh,highMesh;
        private float phase,shownSpeed,nextDetail,gestureWeight,lookWeight;
        private HumanGesture shownGesture;
        private Vector3 previous,lastLook;
        private static Material shared;
        private static Camera view;
        private static readonly Vector3[] joints={new Vector3(0,.91f,0),new Vector3(0,1.23f,0),new Vector3(0,1.51f,0),new Vector3(0,1.66f,0),new Vector3(-.245f,1.39f,0),new Vector3(-.285f,1.12f,0),new Vector3(-.30f,.87f,0),new Vector3(.245f,1.39f,0),new Vector3(.285f,1.12f,0),new Vector3(.30f,.87f,0),new Vector3(-.105f,.9f,0),new Vector3(-.105f,.48f,0),new Vector3(-.105f,.105f,0),new Vector3(.105f,.9f,0),new Vector3(.105f,.48f,0),new Vector3(.105f,.105f,0)};
        private static readonly int[] parents={-1,0,1,2,1,4,5,1,7,8,0,10,11,0,13,14};
        private static readonly string[] names={"Hips","Chest","Neck","Head","Left shoulder","Left elbow","Left hand","Right shoulder","Right elbow","Right hand","Left hip","Left knee","Left foot","Right hip","Right knee","Right foot"};
        public static HumanVisual Create(Transform parent,int seed,HumanRole role=HumanRole.Civilian,bool automatic=true)=>Create(parent,HumanIdentity.Create(seed,role),automatic);
        public static HumanVisual Create(Transform parent,HumanIdentity identity,bool automatic)
        {
            var go=new GameObject("Human character");go.transform.SetParent(parent,false);var human=go.AddComponent<HumanVisual>();human.AutomaticAnimation=automatic;human.Initialize(identity);return human;
        }
        public static void HideOldVisuals(Transform root)
        {foreach(var renderer in root.GetComponentsInChildren<Renderer>(true))if(renderer.GetComponent<TMPro.TMP_Text>()==null&&renderer.GetComponentInParent<HumanVisual>()==null&&!(renderer is ParticleSystemRenderer))renderer.enabled=false;}
        public Transform RightHand=>bones!=null?bones[9]:transform;
        public Transform Head=>bones!=null?bones[3]:transform;
        public void Initialize(HumanIdentity identity)
        {
            Identity=identity;transform.localScale=Vector3.one*(identity.Height/1.8f);phase=(identity.Seed%997)*.01f;
            bones=new Transform[joints.Length];for(int i=0;i<bones.Length;i++){bones[i]=new GameObject(names[i]).transform;bones[i].SetParent(parents[i]<0?transform:bones[parents[i]],false);bones[i].localPosition=parents[i]<0?joints[i]:joints[i]-joints[parents[i]];}
            if(shared==null){var shader=Resources.Load<Shader>("HumanVertexLit");if(shader==null)shader=Shader.Find("Universal Render Pipeline/Lit");shared=new Material(shader){name="Shared human vertex colours"};}
            Renderer=gameObject.AddComponent<SkinnedMeshRenderer>();Renderer.sharedMaterial=shared;Renderer.bones=bones;Renderer.rootBone=bones[0];Renderer.quality=SkinQuality.Bone1;Renderer.updateWhenOffscreen=false;
            lowMesh=Build(false);Renderer.sharedMesh=lowMesh;Renderer.localBounds=new Bounds(new Vector3(0,1,0),new Vector3(1.3f,2.4f,1));
            previous=transform.position;nextDetail=Time.unscaledTime+(identity.Seed%31)*.015f;
        }
        public void Animate(float metersPerSecond,bool jogging=false)
        {
            if(bones==null||Renderer==null||!Renderer.enabled)return;
            shownSpeed=Mathf.MoveTowards(shownSpeed,metersPerSecond,Time.deltaTime*6);float moving=Mathf.Clamp01(shownSpeed/.9f);
            // Shorter legs take quicker steps, so children trot beside their parents.
            float cadence=Mathf.Clamp(1.75f/Identity.Height,.9f,1.65f);
            phase+=shownSpeed*Time.deltaTime*(jogging?6.5f:5.2f)*cadence;float stride=(jogging?35:23)*(Identity.Age==HumanAge.Senior?.72f:1)*moving;
            float swing=Mathf.Sin(phase)*stride;
            bones[4].localRotation=Quaternion.Euler(swing*.8f,0,5);bones[7].localRotation=Quaternion.Euler(-swing*.8f,0,-5);
            bones[5].localRotation=Quaternion.Euler(-8-Mathf.Max(0,-Mathf.Sin(phase))*moving*18,0,0);bones[8].localRotation=Quaternion.Euler(-8-Mathf.Max(0,Mathf.Sin(phase))*moving*18,0,0);
            bones[6].localRotation=Quaternion.identity;bones[9].localRotation=Quaternion.identity;
            bones[10].localRotation=Quaternion.Euler(-swing,0,0);bones[13].localRotation=Quaternion.Euler(swing,0,0);
            bones[11].localRotation=Quaternion.Euler(Mathf.Max(0,Mathf.Sin(phase))*stride*.95f,0,0);bones[14].localRotation=Quaternion.Euler(Mathf.Max(0,-Mathf.Sin(phase))*stride*.95f,0,0);
            bones[12].localRotation=Quaternion.Euler(-Mathf.Max(0,Mathf.Sin(phase))*stride*.35f,0,0);bones[15].localRotation=Quaternion.Euler(-Mathf.Max(0,-Mathf.Sin(phase))*stride*.35f,0,0);
            bones[0].localPosition=joints[0]+Vector3.up*(Mathf.Abs(Mathf.Sin(phase))*.018f*moving);
            if(Seated){bones[10].localRotation=Quaternion.Euler(-86,0,4);bones[13].localRotation=Quaternion.Euler(-86,0,-4);bones[11].localRotation=bones[14].localRotation=Quaternion.Euler(84,0,0);bones[12].localRotation=bones[15].localRotation=Quaternion.identity;bones[0].localPosition=joints[0]-Vector3.up*(joints[10].y-joints[11].y);}
            float individuality=(Identity.Seed%997)*.013f;
            // Idle breathing so standing people never look frozen.
            float breath=Mathf.Sin(Time.time*1.6f+individuality)*(1-moving);
            bones[1].localRotation=Quaternion.Euler((Identity.Age==HumanAge.Senior?6:1)+breath*.8f,Mathf.Sin(phase)*3*moving,Mathf.Sin(phase)*1.3f*moving);
            float blink=Mathf.Repeat(Time.time+individuality,3.5f+Identity.Seed%19*.1f);
            Renderer.SetBlendShapeWeight(0,blink<.14f?Mathf.Sin(blink/.14f*Mathf.PI)*100:0);
            bones[3].localRotation=Quaternion.Euler(Mathf.Sin(Time.time*1.5f+individuality)*1.1f,Mathf.Sin(Time.time*.55f+individuality)*6*(1-moving),0);
            if(Gesture!=HumanGesture.None&&Gesture!=shownGesture&&gestureWeight<.05f)shownGesture=Gesture;
            gestureWeight=Mathf.MoveTowards(gestureWeight,Gesture!=HumanGesture.None&&Gesture==shownGesture?1:0,Time.deltaTime*4);
            if(gestureWeight>0)Pose(shownGesture,Mathf.SmoothStep(0,1,gestureWeight),moving,individuality);
            Look();
        }
        private void Set(int bone,float x,float y,float z,float weight){bones[bone].localRotation=Quaternion.Slerp(bones[bone].localRotation,Quaternion.Euler(x,y,z),weight);}
        private void Pose(HumanGesture gesture,float w,float moving,float individuality)
        {
            float t=Time.time+individuality;
            switch(gesture)
            {
                case HumanGesture.Phone:
                    Set(7,-38,0,-14,w);Set(8,-92,0,0,w);Set(9,-10,0,0,w);Set(3,22,0,0,w*.8f);break;
                case HumanGesture.Talk:
                    float a=Mathf.Sin(t*3.1f),b=Mathf.Sin(t*2.3f+1);
                    Set(4,-18-a*8,0,8,w);Set(5,-70-a*18,0,0,w);Set(6,0,a*25,0,w);
                    Set(7,-22-b*10,0,-8,w);Set(8,-62-b*22,0,0,w);Set(9,0,-b*25,0,w);
                    Set(3,Mathf.Sin(t*4.2f)*4,0,Mathf.Sin(t*1.3f)*5,w*.6f);break;
                case HumanGesture.Shelter:
                    Set(4,-165,0,24,w);Set(5,-55,0,0,w);Set(3,12,0,0,w*.7f);break;
                case HumanGesture.Umbrella:
                    Set(7,-38,0,-6,w);Set(8,-78,0,0,w);break;
                case HumanGesture.HoldLeft:
                    Set(4,-12,0,-16,w);Set(5,-18,0,0,w);break;
                case HumanGesture.HoldRight:
                    Set(7,-12,0,16,w);Set(8,-18,0,0,w);break;
                case HumanGesture.Wave:
                    Set(7,-172,0,-14,w);Set(8,-28,0,Mathf.Sin(t*9)*28,w);break;
                case HumanGesture.Relaxed:
                    Set(4,6,0,-2,w);Set(7,6,0,2,w);Set(5,-22,0,0,w);Set(8,-22,0,0,w);break;
                case HumanGesture.LookAround:
                    Set(3,Mathf.Sin(t*.8f)*4,Mathf.Sin(t*.6f)*55,0,w*(1-moving*.6f));break;
            }
        }
        private void Look()
        {
            lookWeight=Mathf.MoveTowards(lookWeight,LookTarget.HasValue?1:0,Time.deltaTime*2.5f);
            if(LookTarget.HasValue)lastLook=LookTarget.Value;
            if(lookWeight<=0)return;
            Vector3 local=transform.InverseTransformPoint(lastLook)-joints[3];
            float yaw=Mathf.Atan2(local.x,local.z)*Mathf.Rad2Deg;if(Mathf.Abs(yaw)>115)return;
            yaw=Mathf.Clamp(yaw,-70,70);float pitch=Mathf.Clamp(-Mathf.Atan2(local.y,new Vector2(local.x,local.z).magnitude)*Mathf.Rad2Deg,-25,30);
            bones[3].localRotation=Quaternion.Slerp(bones[3].localRotation,Quaternion.Euler(pitch,yaw*.75f,0),lookWeight);
            bones[1].localRotation=Quaternion.Slerp(bones[1].localRotation,bones[1].localRotation*Quaternion.Euler(0,yaw*.25f,0),lookWeight);
        }
        private void LateUpdate()
        {
            if(Renderer==null)return;
            if(Time.unscaledTime>=nextDetail){nextDetail=Time.unscaledTime+.45f;if(view==null)view=Camera.main;float d=view==null?0:Vector3.Distance(view.transform.position,transform.position);bool phone=MobileTouchInput.Enabled||Application.isMobilePlatform;
                Renderer.enabled=d<(phone?150:260);Renderer.shadowCastingMode=d<(phone?30:65)?ShadowCastingMode.On:ShadowCastingMode.Off;
                bool detail=d<(phone?16:25);if(detail&&highMesh==null)highMesh=Build(true);Renderer.sharedMesh=detail?highMesh:lowMesh;}
            if(AutomaticAnimation){float speed=Time.deltaTime>0?Vector3.Distance(previous,transform.position)/Time.deltaTime:0;Animate(speed>8?0:speed);previous=transform.position;}
        }
        private Mesh Build(bool detailed)
        {
            var p=Identity;var b=new Geometry(detailed?18:8,detailed?12:6);float build=p.Build;
            Color skin=p.Skin,hair=p.Hair,shirt=p.Shirt,pants=p.Trousers,shoe=p.Shoes,accent=p.Accent;
            bool dress=p.Top==HumanTop.Dress,skirt=p.Bottom==HumanBottom.Skirt,shorts=p.Bottom==HumanBottom.Shorts;
            bool longSleeves=p.Top==HumanTop.Blazer||p.Top==HumanTop.Hoodie||p.Top==HumanTop.Jacket||p.Top==HumanTop.Uniform;
            bool shortSleeves=p.Top==HumanTop.TShirt||dress;
            bool curvy=p.Feminine&&!p.IsChild&&p.Age!=HumanAge.Teen;
            Color legTop=dress||skirt?skin:pants,legLow=dress||skirt||shorts?skin:pants;
            if((dress||skirt)&&p.Age==HumanAge.Senior)legTop=legLow=Color.Lerp(skin,new Color(.2f,.17f,.15f),.55f);
            b.HeadScale=p.HeadScale;
            // Torso, hips and shape.
            b.Torso(build,shirt,1,p.Role==HumanRole.Doctor?.81f:.94f,curvy);
            b.Ellipsoid(new Vector3(0,1.417f,0),new Vector3(.15f*build,.027f,.13f),shirt,1);
            b.Ellipsoid(new Vector3(0,.92f,0),new Vector3(.17f*build*(curvy?1.07f:1),.125f,.12f),dress?shirt:skirt?pants:pants,0);
            if(p.Belly>0)b.Ellipsoid(new Vector3(0,1.07f,.04f),new Vector3(.165f*build,.15f,.11f+.035f*p.Belly),shirt,1);
            if(curvy)for(int side=-1;side<=1;side+=2)b.Ellipsoid(new Vector3(side*.062f,1.285f,.083f),new Vector3(.068f,.062f,.055f),shirt,1);
            b.Tube(new Vector3(0,1.43f,0),new Vector3(0,1.565f,0),.052f,.047f,skin,2);
            b.Ellipsoid(new Vector3(0,1.66f,0),new Vector3(.098f*p.FaceWidth,.137f,.101f),skin,3,true);
            for(int side=-1;side<=1;side+=2)
            {
                int arm=side<0?4:7,leg=side<0?10:13;float x=side*.105f;
                Vector3 shoulder=new Vector3(side*.235f,1.375f,0),elbow=new Vector3(side*.285f,1.12f,0),wrist=new Vector3(side*.30f,.87f,.006f);
                b.Ellipsoid(shoulder,new Vector3(.074f*build,.065f,.079f),shirt,arm);
                Vector3 sleeve=Vector3.Lerp(shoulder,elbow,.59f);
                b.Tube(shoulder,sleeve,.072f*build,.060f,shirt,arm);
                b.Tube(sleeve,elbow,.060f,.049f,shortSleeves?skin:shirt,arm);
                b.Ellipsoid(elbow,new Vector3(.049f,.045f,.05f),longSleeves?shirt:skin,arm+1);
                b.Tube(elbow,wrist,.049f,.029f,longSleeves?shirt:skin,arm+1);
                if(longSleeves)b.Ellipsoid(wrist+Vector3.up*.012f,new Vector3(.033f,.012f,.033f),Color.Lerp(shirt,Color.black,.18f),arm+1);
                b.Ellipsoid(new Vector3(side*.30f,.835f,.012f),new Vector3(.035f,.05f,.022f),skin,arm+2);
                // Legs: shorts end above the knee, skirts and dresses show the legs.
                if(shorts){b.Tube(new Vector3(x,.92f,0),new Vector3(x,.6f,0),.09f*build,.075f*build,pants,leg);b.Tube(new Vector3(x,.6f,0),new Vector3(x,.48f,0),.066f*build,.060f*build,skin,leg);}
                else b.Tube(new Vector3(x,.92f,0),new Vector3(x,.48f,0),.087f*build,.060f*build,legTop,leg);
                b.Ellipsoid(new Vector3(x,.48f,0),new Vector3(.061f*build,.055f,.064f*build),legLow,leg+1);
                b.Tube(new Vector3(x,.48f,0),new Vector3(x,.105f,.004f),.060f*build,.043f,legLow,leg+1);
                if(shorts||skirt||dress)b.Tube(new Vector3(x,.17f,.004f),new Vector3(x,.1f,.004f),.046f,.045f,shoe.r>.5f?new Color(.12f,.12f,.12f):new Color(.92f,.92f,.9f),leg+1);
                b.Ellipsoid(new Vector3(x,.058f,.055f),new Vector3(.073f,.054f,.145f),shoe,leg+2);
                b.Ellipsoid(new Vector3(x,.028f,.061f),new Vector3(.075f,.018f,.146f),Color.Lerp(shoe,Color.white,.55f),leg+2);
                // Ears, eyes and brows.
                b.Ellipsoid(new Vector3(side*.101f*p.FaceWidth,1.658f,-.005f),new Vector3(.019f,.032f,.018f),skin,3);
                if(p.Earrings)b.Ellipsoid(new Vector3(side*.104f*p.FaceWidth,1.624f,-.003f),Vector3.one*.008f,new Color(.86f,.7f,.32f),3);
                float eyeX=side*.042f*p.EyeSpacing;
                int eyeStart=b.Count;
                b.Ellipsoid(new Vector3(eyeX,1.687f,.088f),new Vector3(.024f,.0105f,.017f),Color.Lerp(skin,Color.white,.7f),3);
                b.Ellipsoid(new Vector3(eyeX,1.687f,.107f),new Vector3(.0085f,.0085f,.003f),p.Iris,3);
                b.Ellipsoid(new Vector3(eyeX,1.687f,.110f),new Vector3(.0045f,.006f,.002f),new Color(.018f,.018f,.02f),3);
                b.MarkEye(eyeStart,new Vector3(eyeX,1.687f,.1f));
                b.Ellipsoid(new Vector3(eyeX,1.715f,.088f),new Vector3(p.Feminine?.026f:.029f,p.Feminine?.004f:.0055f,.012f),hair,3);
                if(detailed)
                {
                    for(int finger=0;finger<4;finger++)b.Ellipsoid(new Vector3(side*.30f+(finger-1.5f)*.013f,.779f+Mathf.Abs(finger-1.5f)*.006f,.014f),new Vector3(.006f,.028f,.007f),skin,arm+2);
                    b.Ellipsoid(new Vector3(side*.331f,.81f,.024f),new Vector3(.009f,.025f,.009f),skin,arm+2);
                    b.Ellipsoid(new Vector3(eyeX-.003f,1.691f,.113f),Vector3.one*.0025f,Color.white,3);
                    b.Ellipsoid(new Vector3(side*.055f,1.645f,.082f),new Vector3(.026f,.018f,.012f),Color.Lerp(skin,new Color(.8f,.35f,.33f),p.IsChild?.22f:.12f),3);
                    if(p.Glasses)b.Ring(new Vector3(eyeX,1.688f,.118f),.032f,.022f,.0025f,new Color(.08f,.07f,.06f),3);
                    if(p.Age==HumanAge.Senior){b.Ellipsoid(new Vector3(eyeX,1.667f,.101f),new Vector3(.022f,.0014f,.002f),skin*.76f,3);b.Ellipsoid(new Vector3(side*.032f,1.737f,.083f),new Vector3(.028f,.0015f,.003f),skin*.82f,3);b.Ellipsoid(new Vector3(side*.05f,1.625f,.085f),new Vector3(.0015f,.02f,.003f),skin*.8f,3);}
                    for(int lace=0;lace<3;lace++)b.Ellipsoid(new Vector3(x,.106f,.06f+lace*.018f),new Vector3(.041f,.003f,.004f),new Color(.7f,.7f,.65f),leg+2);
                }
            }
            if(p.Glasses&&detailed)b.Ellipsoid(new Vector3(0,1.69f,.118f),new Vector3(.012f,.0025f,.003f),new Color(.08f,.07f,.06f),3);
            // Nose, lips and jaw.
            b.Ellipsoid(new Vector3(0,1.663f,.099f),new Vector3(.014f,.026f,.021f*p.NoseLength),skin,3);
            b.Ellipsoid(new Vector3(0,1.644f,.117f),new Vector3(.017f,.011f,.014f*p.NoseLength),skin,3);
            b.Ellipsoid(new Vector3(0,1.611f,.09f),new Vector3(.033f,.0055f,.009f),p.Lips,3);
            b.Ellipsoid(new Vector3(0,1.606f,.087f),new Vector3(.027f,.004f,.009f),Color.Lerp(p.Lips,skin,.4f),3);
            if(p.Beard)b.Ellipsoid(new Vector3(0,1.582f,.045f),new Vector3(.072f,.033f,.059f),hair,3);
            if(p.Stubble)b.Ellipsoid(new Vector3(0,1.586f,.043f),new Vector3(.07f,.03f,.057f),Color.Lerp(skin,hair,.38f),3);
            Hair(b,p,hair,accent);
            if(p.Cap){b.Ellipsoid(new Vector3(0,1.765f,-.012f),new Vector3(.108f*p.FaceWidth,.06f,.106f),accent,3);b.Ellipsoid(new Vector3(0,1.738f,.095f),new Vector3(.075f,.007f,.065f),Color.Lerp(accent,Color.black,.2f),3);}
            // Skirts and dresses flare from the hips.
            if(dress)b.Tube(new Vector3(0,1.0f,0),new Vector3(0,.5f,0),.172f*build*(curvy?1.07f:1),.25f*build,shirt,0);
            else if(skirt)b.Tube(new Vector3(0,.99f,0),new Vector3(0,.6f,0),.18f*build*(curvy?1.07f:1),.23f*build,pants,0);
            Color trim=Color.Lerp(shirt,Color.black,.22f);
            switch(p.Top)
            {
                case HumanTop.Blazer:
                {
                    b.Patch(new Vector3(-.05f,1.42f,.138f),new Vector3(-.05f,1.1f,.142f),new Vector3(.05f,1.1f,.142f),new Vector3(.05f,1.42f,.138f),new Color(.92f,.92f,.9f),1);
                    b.Patch(new Vector3(-.155f,1.41f,.139f),new Vector3(-.058f,1.28f,.146f),new Vector3(-.025f,1.36f,.147f),new Vector3(-.06f,1.43f,.132f),trim,1);
                    b.Patch(new Vector3(.06f,1.43f,.132f),new Vector3(.025f,1.36f,.147f),new Vector3(.058f,1.28f,.146f),new Vector3(.155f,1.41f,.139f),trim,1);
                    if(!p.Feminine||p.Role==HumanRole.Owner){Color tie=p.Role==HumanRole.Owner?new Color(.56f,.17f,.13f):accent;b.Patch(new Vector3(-.012f,1.40f,.149f),new Vector3(.012f,1.40f,.149f),new Vector3(.017f,1.26f,.149f),new Vector3(0,1.24f,.149f),tie,1);}
                    break;
                }
                case HumanTop.Uniform:
                {
                    if(p.Role==HumanRole.Doctor){b.Patch(new Vector3(-.155f,1.41f,.139f),new Vector3(-.058f,1.28f,.146f),new Vector3(-.025f,1.36f,.147f),new Vector3(-.06f,1.43f,.132f),trim,1);b.Patch(new Vector3(.06f,1.43f,.132f),new Vector3(.025f,1.36f,.147f),new Vector3(.058f,1.28f,.146f),new Vector3(.155f,1.41f,.139f),trim,1);b.Patch(new Vector3(-.012f,1.40f,.149f),new Vector3(.012f,1.40f,.149f),new Vector3(.017f,1.26f,.149f),new Vector3(0,1.24f,.149f),new Color(.19f,.34f,.47f),1);}
                    break;
                }
                case HumanTop.Hoodie:
                {
                    b.Ellipsoid(new Vector3(0,1.445f,-.105f),new Vector3(.125f,.085f,.075f),trim,1);
                    b.Patch(new Vector3(-.1f,1.09f,.137f),new Vector3(-.115f,.98f,.135f),new Vector3(.115f,.98f,.135f),new Vector3(.1f,1.09f,.137f),trim,1);
                    for(int side=-1;side<=1;side+=2)b.Ellipsoid(new Vector3(side*.03f,1.33f,.142f),new Vector3(.004f,.05f,.004f),new Color(.9f,.9f,.88f),1);
                    break;
                }
                case HumanTop.Jacket:
                {
                    b.Patch(new Vector3(-.055f,1.4f,.139f),new Vector3(-.055f,.97f,.137f),new Vector3(.055f,.97f,.137f),new Vector3(.055f,1.4f,.139f),accent,1);
                    b.Ellipsoid(new Vector3(0,1.43f,-.015f),new Vector3(.155f*build,.04f,.142f),trim,1);
                    break;
                }
                case HumanTop.TShirt:
                    if(p.Print)b.Patch(new Vector3(-.06f,1.33f,.134f),new Vector3(-.06f,1.2f,.134f),new Vector3(.06f,1.2f,.134f),new Vector3(.06f,1.33f,.134f),accent,1);
                    break;
            }
            if(detailed)
            {
                if(p.Top==HumanTop.Shirt||p.Top==HumanTop.Uniform||p.Top==HumanTop.Blazer)
                {
                    Color collar=p.Top==HumanTop.Blazer?new Color(.95f,.95f,.93f):Color.Lerp(shirt,Color.white,.3f);
                    b.Ellipsoid(new Vector3(-.041f,1.424f,.102f),new Vector3(.043f,.012f,.032f),collar,1);b.Ellipsoid(new Vector3(.041f,1.424f,.102f),new Vector3(.043f,.012f,.032f),collar,1);
                    if(p.Top!=HumanTop.Blazer)for(int i=0;i<4;i++)b.Ellipsoid(new Vector3(0,1.35f-i*.075f,.137f),Vector3.one*.0045f,Color.Lerp(shirt,Color.white,.5f),1);
                }
                if(p.Top==HumanTop.Jacket)for(int i=0;i<8;i++)b.Ellipsoid(new Vector3(.058f,1.38f-i*.05f,.14f),new Vector3(.003f,.012f,.003f),new Color(.7f,.7f,.68f),1);
                if(!dress&&!skirt){b.Ellipsoid(new Vector3(0,.985f,0),new Vector3(.179f*build,.014f,.126f),new Color(.12f,.09f,.07f),0);b.Ellipsoid(new Vector3(0,.986f,.126f),new Vector3(.025f,.017f,.009f),new Color(.55f,.5f,.36f),0);}
                if(p.Role==HumanRole.Doctor||p.Role==HumanRole.Police)b.Ellipsoid(new Vector3(-.108f,1.30f,.127f),new Vector3(.023f,.032f,.005f),p.Role==HumanRole.Police?new Color(.84f,.68f,.26f):new Color(.47f,.77f,.84f),1);
                if(!p.IsChild)b.Ellipsoid(new Vector3(-.30f,.885f,.006f),new Vector3(.032f,.011f,.03f),new Color(.1f,.1f,.11f),5);
            }
            // Accessories.
            if(p.Backpack||p.Role==HumanRole.Student)
            {
                Color bag=p.Role==HumanRole.Student?new Color(.18f,.25f,.37f):Color.Lerp(accent,Color.black,.25f);
                b.Ellipsoid(new Vector3(0,1.2f,-.18f),new Vector3(.15f,.19f,.075f),bag,1);
                b.Ellipsoid(new Vector3(0,1.12f,-.24f),new Vector3(.1f,.08f,.03f),Color.Lerp(bag,Color.black,.2f),1);
                for(int side=-1;side<=1;side+=2)b.Tube(new Vector3(side*.09f,1.41f,-.02f),new Vector3(side*.1f,1.12f,.13f),.014f,.014f,Color.Lerp(bag,Color.black,.3f),1);
            }
            if(p.Handbag)
            {
                Color leather=Color.Lerp(new Color(.33f,.18f,.1f),accent,.35f);
                b.Ellipsoid(new Vector3(-.33f,.74f,.012f),new Vector3(.034f,.075f,.12f),leather,6);
                b.Tube(new Vector3(-.305f,.86f,.01f),new Vector3(-.325f,.8f,.01f),.006f,.006f,leather*.7f,6);
            }
            if(p.Phone)b.Ellipsoid(new Vector3(.305f,.8f,.036f),new Vector3(.009f,.042f,.022f),new Color(.05f,.05f,.06f),9);
            var mesh=b.Mesh("Human "+p.Seed+(detailed?" detail":" crowd"));var bind=new Matrix4x4[bones.Length];for(int i=0;i<bind.Length;i++)bind[i]=Matrix4x4.TRS(joints[i],Quaternion.identity,Vector3.one).inverse;mesh.bindposes=bind;return mesh;
        }
        private static void Hair(Geometry b,HumanIdentity p,Color hair,Color accent)
        {
            float fw=p.FaceWidth;
            switch(p.HairStyle)
            {
                case 3:
                    if(p.Age==HumanAge.Senior&&!p.Feminine)b.Ellipsoid(new Vector3(0,1.655f,-.04f),new Vector3(.101f*fw,.05f,.088f),hair,3);
                    else b.Ellipsoid(new Vector3(0,1.715f,-.02f),new Vector3(.1f*fw,.089f,.094f),Color.Lerp(hair,p.Skin,.35f),3);
                    return;
                case 6:
                    b.Ellipsoid(new Vector3(0,1.735f,-.02f),new Vector3(.118f*fw,.108f,.11f),hair,3);
                    for(int k=0;k<7;k++){float a=k*Mathf.PI*2/7;b.Ellipsoid(new Vector3(Mathf.Cos(a)*.085f*fw,1.775f+Mathf.Sin(a*2)*.012f,Mathf.Sin(a)*.075f-.025f),Vector3.one*.042f,hair*1.08f,3);}
                    if(p.Feminine)b.Ellipsoid(new Vector3(0,1.6f,-.07f),new Vector3(.125f*fw,.12f,.07f),hair,3);
                    return;
            }
            b.Ellipsoid(new Vector3(0,1.727f,-.026f),new Vector3(.103f*fw,.094f,.091f),hair,3);
            switch(p.HairStyle)
            {
                case 1:b.Ellipsoid(new Vector3(.035f,1.784f,-.002f),new Vector3(.082f,.033f,.08f),hair,3);break;
                case 2:
                    b.Ellipsoid(new Vector3(0,1.57f,-.075f),new Vector3(.112f*fw,.165f,.06f),hair,3);
                    for(int side=-1;side<=1;side+=2)b.Ellipsoid(new Vector3(side*.088f*fw,1.62f,0),new Vector3(.026f,.11f,.05f),hair,3);
                    break;
                case 4:
                    b.Ellipsoid(new Vector3(0,1.70f,-.11f),Vector3.one*.022f,accent,3);
                    b.Tube(new Vector3(0,1.70f,-.118f),new Vector3(0,1.5f,-.16f),.04f,.012f,hair,3);
                    break;
                case 5:b.Ellipsoid(new Vector3(0,1.785f,-.075f),new Vector3(.052f,.048f,.052f),hair,3);break;
                case 7:b.Ellipsoid(new Vector3(0,1.655f,-.03f),new Vector3(.116f*fw,.105f,.1f),hair,3);break;
            }
        }
        private void OnDestroy(){if(lowMesh!=null){if(Application.isPlaying)Destroy(lowMesh);else DestroyImmediate(lowMesh);}if(highMesh!=null){if(Application.isPlaying)Destroy(highMesh);else DestroyImmediate(highMesh);}}
        private sealed class Geometry
        {
            readonly List<Vector3> vertices=new List<Vector3>(),normals=new List<Vector3>();readonly List<Color> colors=new List<Color>();readonly List<int> triangles=new List<int>();readonly List<BoneWeight> weights=new List<BoneWeight>();readonly int segments,rings;
            public float HeadScale=1;
            static readonly Vector3 neck=new Vector3(0,1.555f,0);
            public Geometry(int segments,int rings){this.segments=segments;this.rings=rings;}
            readonly List<Vector3> blinkCenters=new List<Vector3>();readonly List<int> blinkStarts=new List<int>(),blinkEnds=new List<int>();
            public int Count=>vertices.Count;
            public void MarkEye(int start,Vector3 center){blinkStarts.Add(start);blinkEnds.Add(vertices.Count);blinkCenters.Add(center);}
            void Vertex(Vector3 point,Vector3 normal,Color color,int bone){vertices.Add(point);normals.Add(normal);colors.Add(color);weights.Add(new BoneWeight{boneIndex0=bone,weight0=1});}
            void Grid(int start,int rows,int columns){for(int y=0;y<rows;y++)for(int x=0;x<columns;x++){int a=start+y*(columns+1)+x,c=a+columns+1;triangles.Add(a);triangles.Add(a+1);triangles.Add(c);triangles.Add(a+1);triangles.Add(c+1);triangles.Add(c);}}
            public void Ellipsoid(Vector3 center,Vector3 size,Color color,int bone,bool face=false)
            {
                int start=vertices.Count;for(int y=0;y<=rings;y++){float lat=Mathf.PI*y/rings;for(int x=0;x<=segments;x++){float lon=Mathf.PI*2*x/segments;var n=new Vector3(Mathf.Sin(lat)*Mathf.Cos(lon),Mathf.Cos(lat),Mathf.Sin(lat)*Mathf.Sin(lon));var offset=Vector3.Scale(n,size);if(face&&n.y<-.15f)offset.x*=Mathf.Lerp(.74f,1,(n.y+1)/.85f);Vertex(center+offset,new Vector3(n.x/size.x,n.y/size.y,n.z/size.z).normalized,color,bone);}}Grid(start,rings,segments);
            }
            public void Torso(float build,Color color,int bone,float hem,bool curvy)
            {
                int start=vertices.Count;float[] heights={hem,1.03f,1.21f,1.36f,1.43f};float[] widths=curvy?new[]{.185f,.158f,.19f,.205f,.142f}:new[]{.17f,.175f,.205f,.225f,.15f};
                for(int y=0;y<heights.Length;y++)for(int x=0;x<=segments;x++){float a=Mathf.PI*2*x/segments;Vertex(new Vector3(Mathf.Cos(a)*widths[y]*build,heights[y],Mathf.Sin(a)*.133f),new Vector3(Mathf.Cos(a),0,Mathf.Sin(a)),color,bone);}int first=triangles.Count;Grid(start,heights.Length-1,segments);for(int i=first;i<triangles.Count;i+=3){int swap=triangles[i];triangles[i]=triangles[i+1];triangles[i+1]=swap;}
            }
            public void Patch(Vector3 a,Vector3 b,Vector3 c,Vector3 d,Color color,int bone)
            {
                int start=vertices.Count;Vertex(a,Vector3.forward,color,bone);Vertex(b,Vector3.forward,color,bone);Vertex(c,Vector3.forward,color,bone);Vertex(d,Vector3.forward,color,bone);
                bool front=Vector3.Cross(b-a,c-a).z>=0;
                triangles.Add(start);triangles.Add(start+(front?1:2));triangles.Add(start+(front?2:1));triangles.Add(start);triangles.Add(start+(front?2:3));triangles.Add(start+(front?3:2));
            }
            public void Tube(Vector3 from,Vector3 to,float fromRadius,float toRadius,Color color,int bone)
            {
                int start=vertices.Count;Vector3 axis=(to-from).normalized;Vector3 right=Vector3.Cross(axis,Vector3.forward).normalized;if(right.sqrMagnitude<.1f)right=Vector3.right;Vector3 front=Vector3.Cross(right,axis).normalized;
                for(int y=0;y<=3;y++)for(int x=0;x<=segments;x++){float a=x*Mathf.PI*2/segments;Vector3 n=right*Mathf.Cos(a)+front*Mathf.Sin(a);Vertex(Vector3.Lerp(from,to,y/3f)+n*Mathf.Lerp(fromRadius,toRadius,y/3f),n,color,bone);}
                int first=triangles.Count;Grid(start,3,segments);Reverse(first);
            }
            void Reverse(int first){for(int i=first;i<triangles.Count;i+=3){int swap=triangles[i];triangles[i]=triangles[i+1];triangles[i+1]=swap;}}
            public void Ring(Vector3 center,float width,float height,float thickness,Color color,int bone)
            {
                int start=vertices.Count;for(int y=0;y<=24;y++)for(int x=0;x<=5;x++){float a=y*Mathf.PI*2/24,b=x*Mathf.PI*2/5;Vector3 n=new Vector3(Mathf.Cos(a),Mathf.Sin(a),0);Vector3 normal=n*Mathf.Cos(b)+Vector3.forward*Mathf.Sin(b);Vertex(center+new Vector3(Mathf.Cos(a)*width,Mathf.Sin(a)*height,0)+normal*thickness,normal,color,bone);}int first=triangles.Count;Grid(start,24,5);Reverse(first);
            }
            public Mesh Mesh(string name)
            {
                // Children have proportionally larger heads: grow everything on the head bone from the top of the neck.
                if(!Mathf.Approximately(HeadScale,1))
                {
                    for(int i=0;i<vertices.Count;i++)if(weights[i].boneIndex0==3)vertices[i]=neck+(vertices[i]-neck)*HeadScale;
                    for(int i=0;i<blinkCenters.Count;i++)blinkCenters[i]=neck+(blinkCenters[i]-neck)*HeadScale;
                }
                var mesh=new Mesh{name=name};if(vertices.Count>65535)mesh.indexFormat=IndexFormat.UInt32;mesh.SetVertices(vertices);mesh.SetNormals(normals);mesh.SetColors(colors);mesh.SetTriangles(triangles,0);mesh.boneWeights=weights.ToArray();
                var delta=new Vector3[vertices.Count];for(int eye=0;eye<blinkStarts.Count;eye++)for(int i=blinkStarts[eye];i<blinkEnds[eye];i++)delta[i]=new Vector3(0,(blinkCenters[eye].y-vertices[i].y)*.95f,0);
                mesh.AddBlendShapeFrame("Blink",100,delta,null,null);mesh.RecalculateBounds();return mesh;
            }
        }
    }
}
