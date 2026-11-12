using UnityEngine;
namespace RoadsideHaven
{
    public sealed class VegetationWind : MonoBehaviour
    {
        [SerializeField] private BiomePlant[] plants;
        private MotelAtmosphere atmosphere;
        private WorldSeason season=(WorldSeason)(-1);
        private float nextWind,nextSeason;
        public void Configure(BiomePlant[] values){plants=values;ApplySeason();}
        private void Start(){ApplySeason();}
        private void Update()
        {
            if(plants==null)return;
            if(Time.time>=nextSeason){nextSeason=Time.time+1;if(atmosphere==null)atmosphere=GetComponentInParent<MotelAtmosphere>();if(atmosphere!=null&&season!=atmosphere.Season)ApplySeason();}
            if(Time.time<nextWind)return;nextWind=Time.time+.1f;var camera=Camera.main;if(camera==null)return;
            for(int i=0;i<plants.Length;i++)
            {var plant=plants[i];if(plant==null||plant.Canopy==null)continue;if((plant.transform.position-camera.transform.position).sqrMagnitude>190*190)continue;float wind=Mathf.Sin(Time.time*.72f+i*1.73f)*Mathf.Sin(Time.time*.23f+i*.51f);plant.Canopy.localRotation=Quaternion.Euler(wind*.6f,0,wind*(plant.Kind==VegetationKind.Bamboo?1.8f:.9f));}
        }
        private void ApplySeason()
        {
            atmosphere=GetComponentInParent<MotelAtmosphere>();if(atmosphere!=null)season=atmosphere.Season;if(plants==null)return;
            var block=new MaterialPropertyBlock();
            foreach(var plant in plants)
            {
                if(plant==null||plant.Canopy==null)continue;Color tint=Color.white;
                if(!plant.Evergreen&&plant.Kind!=VegetationKind.FlowerBed)
                {if(season==WorldSeason.Autumn)tint=new Color(1,.77f,.36f);else if(season==WorldSeason.LateAutumn)tint=new Color(.86f,.69f,.39f);else if(season==WorldSeason.Winter)tint=new Color(.78f,.87f,.76f);else if(season==WorldSeason.Spring)tint=new Color(.94f,1,.85f);}
                foreach(var r in plant.Canopy.GetComponentsInChildren<Renderer>()){r.GetPropertyBlock(block);var baseColor=r.sharedMaterial.HasProperty("_BaseColor")?r.sharedMaterial.GetColor("_BaseColor"):Color.white;block.SetColor("_BaseColor",baseColor*tint);r.SetPropertyBlock(block);block.Clear();}
            }
        }
    }
}
