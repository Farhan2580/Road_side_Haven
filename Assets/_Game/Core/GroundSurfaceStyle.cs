using System;
using UnityEngine;
namespace RoadsideHaven
{
    /// <summary>Shared PBR ground materials with metre-scale tiling; renderer-only, never changes driving collision.</summary>
    [ExecuteAlways]
    public sealed class GroundSurfaceStyle : MonoBehaviour
    {
        [SerializeField] private Material[] surfaces;
        private MotelAtmosphere atmosphere;
        private WorldSeason season=(WorldSeason)(-1);
        private float nextSeasonCheck;
        public void Configure(Material[] materials){surfaces=materials;Apply();}
        private void OnEnable(){Apply();}
        private void Start(){Apply();}
        private void Update()
        {
            if(!Application.isPlaying||Time.time<nextSeasonCheck)return;nextSeasonCheck=Time.time+1;
            if(atmosphere==null)atmosphere=GetComponent<MotelAtmosphere>();if(atmosphere!=null&&season!=atmosphere.Season)Apply();
        }
        public static int SurfaceFor(string name)
        {
            if(string.IsNullOrEmpty(name))return -1;
            if(TownPopulationController.RoadName(name)||name=="Parking apron"||name=="Showroom apron"||name=="Wash apron"||name=="Truck rest apron")return 1;
            if(name=="Ground"||name=="District ground"||name=="World foundation"||name.StartsWith("Regional foundation",StringComparison.Ordinal))return 0;
            if(name=="Cultivated field")return 2;if(name=="Beach sand")return 3;if(name=="Mountain ridge")return 4;
            if(name=="Public sidewalk"||name=="Repaired block paving"||name=="Downtown district paving"||name=="City block sidewalk")return 5;
            return -1;
        }
        public void Apply()
        {
            if(surfaces==null||surfaces.Length!=6)return;
            atmosphere=GetComponent<MotelAtmosphere>();if(atmosphere!=null)season=atmosphere.Season;
            var block=new MaterialPropertyBlock();
            foreach(var renderer in GetComponentsInChildren<MeshRenderer>(true))
            {
                if(renderer.GetComponent<RoadSurfaceProfile>()!=null)continue;
                int kind=SurfaceFor(renderer.name);if(kind<0||surfaces[kind]==null)continue;
                renderer.sharedMaterial=surfaces[kind];var bounds=renderer.bounds;
                float metres=kind==0?3:kind==1?4:kind==5?4:6;
                var tiling=new Vector4(Mathf.Max(.1f,bounds.size.x/metres),Mathf.Max(.1f,bounds.size.z/metres),bounds.min.x/metres,bounds.min.z/metres);
                renderer.GetPropertyBlock(block);block.SetVector("_BaseMap_ST",tiling);block.SetVector("_BumpMap_ST",tiling);
                block.SetVector("_DetailAlbedoMap_ST",new Vector4(bounds.size.x/48,bounds.size.z/48,bounds.min.x/48,bounds.min.z/48));
                block.SetColor("_BaseColor",kind==0?GrassTint(season):Color.white);renderer.SetPropertyBlock(block);block.Clear();
            }
        }
        private static Color GrassTint(WorldSeason current)
        {
            if(current==WorldSeason.Monsoon)return new Color(.88f,1,.90f);
            if(current==WorldSeason.Autumn)return new Color(1,.94f,.73f);
            if(current==WorldSeason.LateAutumn)return new Color(.96f,.89f,.69f);
            if(current==WorldSeason.Winter)return new Color(.92f,.95f,.85f);
            if(current==WorldSeason.Spring)return new Color(.94f,1,.91f);return Color.white;
        }
    }
}
