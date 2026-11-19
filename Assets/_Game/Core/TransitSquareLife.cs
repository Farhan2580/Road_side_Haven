using UnityEngine;
using UnityEngine.Rendering;
namespace RoadsideHaven
{
    /// <summary>Shared visual budget, a town-clock, fountain ripples and a few independent plaza pedestrians.</summary>
    public sealed class TransitSquareLife : MonoBehaviour
    {
        [SerializeField] private Transform hourHand,minuteHand;
        [SerializeField] private Transform[] fans,ripples,shopRoofs;
        private struct Visual {public MeshRenderer Renderer;public float Range;public ShadowCastingMode Shadows;}
        private Visual[] visuals;
        private TownCitizenAgent[] walkers;
        private Vector3[][] routes;
        private MaterialPropertyBlock[] rippleProperties;
        private Renderer[] rippleRenderers;
        private Renderer[][] roofRenderers;
        private OwnerSandboxController owner;
        private MotelAtmosphere atmosphere;
        private Camera view;
        private bool mobile;
        private float refresh,nextAnimation;
        public int WalkerCount=>walkers==null?0:walkers.Length;
        public void Configure(Transform hour,Transform minute,Transform[] ceilingFans,Transform[] fountainRipples,Transform[] roofs)
        {hourHand=hour;minuteHand=minute;fans=ceilingFans;ripples=fountainRipples;shopRoofs=roofs;}
        private void Start()
        {
            mobile=MobileTouchInput.Enabled||Application.isMobilePlatform;view=Camera.main;var motel=GetComponentInParent<MotelGameRoot>();atmosphere=motel.GetComponent<MotelAtmosphere>();owner=motel.GetComponentInChildren<OwnerSandboxController>(true);
            var renderers=GetComponentsInChildren<MeshRenderer>();visuals=new Visual[renderers.Length];
            for(int i=0;i<renderers.Length;i++)
            {
                var r=renderers[i];float range=r.name.StartsWith("Transit near batch")?(mobile?38:65):(mobile?190:330);
                if(r.GetComponent<TMPro.TMP_Text>()!=null)range=80;
                visuals[i]=new Visual{Renderer=r,Range=range*range,Shadows=r.shadowCastingMode};
            }
            rippleProperties=new MaterialPropertyBlock[ripples.Length];rippleRenderers=new Renderer[ripples.Length];for(int i=0;i<ripples.Length;i++){rippleProperties[i]=new MaterialPropertyBlock();rippleRenderers[i]=ripples[i].GetComponent<Renderer>();}
            roofRenderers=new Renderer[shopRoofs.Length][];for(int i=0;i<shopRoofs.Length;i++)roofRenderers[i]=shopRoofs[i].GetComponentsInChildren<Renderer>();
            int count=mobile?2:4;walkers=new TownCitizenAgent[count];routes=new Vector3[count][];
            for(int i=0;i<count;i++)
            {
                var source=i%2==0?TransitSquareLayout.ShopWalk:TransitSquareLayout.GardenWalk;int offset=(i/2)*2;int points=source.Length-1;
                routes[i]=new Vector3[source.Length];for(int p=0;p<source.Length;p++)routes[i][p]=source[(p+offset)%points];
                var go=new GameObject("Transit square neighbour "+i);go.transform.SetParent(transform,false);var person=go.AddComponent<TownCitizenAgent>();person.Initialize(null,null,false);person.SetJogging(i==3);person.Warp(routes[i][0]);person.Walk(routes[i]);walkers[i]=person;
                go.AddComponent<WorldNpc>().Configure("transit-walker-"+i,i%2==0?"Market visitor":"Garden neighbour");
            }
        }
        private void Update()
        {
            if(Time.unscaledTime>=refresh)
            {
                refresh=Time.unscaledTime+.35f;if(view==null)view=Camera.main;
                if(view!=null&&visuals!=null)foreach(var visual in visuals)
                {
                    var r=visual.Renderer;if(r==null)continue;float d=r.bounds.SqrDistance(view.transform.position);r.enabled=d<visual.Range;r.shadowCastingMode=d<(mobile?35*35:65*65)?visual.Shadows:ShadowCastingMode.Off;
                }
                float hour=atmosphere==null?15:atmosphere.Hour;
                if(hourHand!=null)hourHand.localRotation=Quaternion.Euler(0,0,-hour*30);
                if(minuteHand!=null)minuteHand.localRotation=Quaternion.Euler(0,0,-Mathf.Repeat(hour,1)*360);
                if(roofRenderers!=null)for(int i=0;i<roofRenderers.Length;i++)
                {
                    bool inside=owner!=null&&owner.IsWalking&&owner.transform.position.y<3&&TransitSquareLayout.Building(i).Contains(new Vector2(owner.transform.position.x,owner.transform.position.z));
                    foreach(var r in roofRenderers[i])if(r!=null)r.enabled=!inside&&(view==null||r.bounds.SqrDistance(view.transform.position)<(mobile?190*190:330*330));
                }
            }
            if(Time.time>=nextAnimation)
            {
                nextAnimation=Time.time+(mobile ? .05f : 1f/30);
                if(view==null||(view.transform.position-new Vector3(-150,1,-47)).sqrMagnitude<200*200)
                {
                    for(int i=0;i<fans.Length;i++)if(fans[i]!=null)fans[i].localRotation=Quaternion.Euler(0,Time.time*120+i*40,0);
                    for(int i=0;i<ripples.Length;i++)
                    {
                        var ripple=ripples[i];if(ripple==null)continue;float phase=Mathf.Repeat(Time.time*.23f+i*.5f,1);ripple.localScale=Vector3.one*(.55f+phase*.72f);
                        var r=rippleRenderers[i];if(r==null)continue;rippleProperties[i].SetColor("_BaseColor",new Color(.8f,.96f,1,(1-phase)*.55f));r.SetPropertyBlock(rippleProperties[i]);
                    }
                }
            }
            if(walkers!=null)for(int i=0;i<walkers.Length;i++)if(walkers[i]!=null&&walkers[i].Arrived)walkers[i].Walk(routes[i]);
        }
        private void OnDisable(){if(visuals==null)return;foreach(var visual in visuals)if(visual.Renderer!=null){visual.Renderer.enabled=true;visual.Renderer.shadowCastingMode=visual.Shadows;}}
    }
}
