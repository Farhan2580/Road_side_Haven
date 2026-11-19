using UnityEngine;
using UnityEngine.Rendering;
namespace RoadsideHaven
{
    /// <summary>Small shared neighbourhood animation budget and independent sidewalk residents.</summary>
    public sealed class HeritageNeighborhoodLife : MonoBehaviour
    {
        [SerializeField] private Transform[] swings,seesaws,roundabouts;
        private sealed class Resident {public TownCitizenAgent Agent;public Vector3[] Forward,Backward;public bool Reverse;public float Wait;}
        private struct Visual {public MeshRenderer Renderer;public float RangeSquared;public ShadowCastingMode Shadows;}
        private Visual[] visuals;
        private Camera view;
        private float nextVisibility,nextAnimation;
        private bool mobile;
        private Resident[] residents;
        private OwnerSandboxController owner;
        private int visit;
        public string Status {get;private set;}
        public void Configure(Transform[] movingSwings,Transform[] movingSeesaws,Transform[] movingRoundabouts)
        {swings=movingSwings;seesaws=movingSeesaws;roundabouts=movingRoundabouts;}
        private void Start()
        {
            mobile=MobileTouchInput.Enabled||Application.isMobilePlatform;view=Camera.main;
            var renderers=GetComponentsInChildren<MeshRenderer>();visuals=new Visual[renderers.Length];
            for(int i=0;i<renderers.Length;i++)
            {
                var r=renderers[i];string material=r.sharedMaterial==null?"":r.sharedMaterial.name;
                float range=mobile?210:420;
                if(material=="Garden rose petals"||material=="Marigold petals"||material=="Jasmine petals")range=mobile?40:70;
                if(r.GetComponent<TMPro.TMP_Text>()!=null)range=r.bounds.size.x<4?30:95;
                // Road surfaces must remain visible at the same distance as the world road network.
                if(TownPopulationController.RoadName(r.name))range=2500;
                visuals[i]=new Visual{Renderer=r,RangeSquared=range*range,Shadows=r.shadowCastingMode};
            }
            var motel=GetComponentInParent<MotelGameRoot>();owner=motel==null?null:motel.GetComponentInChildren<OwnerSandboxController>(true);
            int count=MobileTouchInput.Enabled||Application.isMobilePlatform?4:8;residents=new Resident[count];
            for(int i=0;i<count;i++)
            {
                int site=i%2;float x=HeritageNeighborhoodLayout.House(site,(i/2)%6).x,z=HeritageNeighborhoodLayout.Centers[site].y+6;
                var go=new GameObject("Heritage garden neighbour "+i);go.transform.SetParent(transform,false);var person=go.AddComponent<TownCitizenAgent>();person.Initialize(null,null,false);person.SetJogging(i==3);
                var route=i<2?HeritageNeighborhoodLayout.ParkWalk(site,x):new[]{new Vector3(x-7,.12f,z),new Vector3(x+16,.12f,z)};
                var back=(Vector3[])route.Clone();System.Array.Reverse(back);
                person.Warp(route[0]);go.AddComponent<WorldNpc>().Configure("heritage-neighbour-"+i,i%3==0?"Garden neighbour":"Local resident");
                residents[i]=new Resident{Agent=person,Forward=route,Backward=back,Wait=i*1.3f+1};
            }
        }
        public void VisitNext()
        {
            if(owner==null)return;
            if(!owner.IsWalking){Status="Exit your vehicle before visiting the gardens.";return;}
            if(owner.HasSupplyCargo){Status="Deliver your supplies before visiting another neighbourhood.";return;}
            owner.ExitVehicle(HeritageNeighborhoodLayout.Arrival(visit));Status="Welcome to "+HeritageNeighborhoodLayout.Names[visit]+". Explore the houses, back gardens and playground.";visit=(visit+1)%2;
        }
        private void Update()
        {
            float time=Time.time;
            if(time>=nextVisibility){nextVisibility=time+.35f;RefreshVisibility();}
            if(time>=nextAnimation)
            {
                nextAnimation=time+(mobile?1f/20:1f/30);
                if(swings!=null)for(int i=0;i<swings.Length;i++)if(Nearby(swings[i]))swings[i].localRotation=Quaternion.Euler(Mathf.Sin(time*.85f+i*1.7f)*12,0,0);
                if(seesaws!=null)for(int i=0;i<seesaws.Length;i++)if(Nearby(seesaws[i]))seesaws[i].localRotation=Quaternion.Euler(0,0,Mathf.Sin(time*.6f+i)*7);
                if(roundabouts!=null)for(int i=0;i<roundabouts.Length;i++)if(Nearby(roundabouts[i]))roundabouts[i].localRotation=Quaternion.Euler(0,time*7+i*30,0);
            }
            if(residents==null)return;
            foreach(var person in residents)
            {
                if(person.Agent==null||!person.Agent.Arrived)continue;person.Wait-=Time.deltaTime;if(person.Wait>0)continue;
                person.Agent.Walk(person.Reverse?person.Backward:person.Forward);person.Reverse=!person.Reverse;person.Wait=3+(HumanIdentity.StableSeed(person.Agent.name)%11)*.6f;
            }
        }
        private bool Nearby(Transform item)=>item!=null&&(view==null||(item.position-view.transform.position).sqrMagnitude<(mobile?10000:22500));
        private void RefreshVisibility()
        {
            if(view==null)view=Camera.main;if(view==null||visuals==null)return;
            Vector3 position=view.transform.position;float shadowRange=mobile?35*35:85*85;
            foreach(var visual in visuals)
            {
                var r=visual.Renderer;if(r==null)continue;float distance=r.bounds.SqrDistance(position);
                r.enabled=distance<=visual.RangeSquared;r.shadowCastingMode=distance<=shadowRange?visual.Shadows:ShadowCastingMode.Off;
            }
        }
        private void OnDisable()
        {
            if(visuals==null)return;foreach(var visual in visuals)if(visual.Renderer!=null){visual.Renderer.enabled=true;visual.Renderer.shadowCastingMode=visual.Shadows;}
        }
    }
}
