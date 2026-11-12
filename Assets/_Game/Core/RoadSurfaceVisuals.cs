using UnityEngine;
namespace RoadsideHaven
{
    [ExecuteAlways]
    public sealed class RoadSurfaceVisuals : MonoBehaviour
    {
        [SerializeField] private Material[] materials;
        public void Configure(Material[] values){materials=values;Apply();}
        private void OnEnable(){Apply();}
        private void Start(){Apply();}
        public void Apply()
        {
            if(materials==null||materials.Length!=5)return;
            foreach(var profile in GetComponentsInChildren<RoadSurfaceProfile>(true))
            {
                var r=profile.GetComponent<MeshRenderer>();if(r==null)continue;var b=r.bounds;bool horizontal=b.size.x>b.size.z;
                int index=(int)profile.Kind;if(profile.Kind==RoadSurfaceKind.Dirt&&horizontal)index=4;r.sharedMaterial=materials[index];
                var block=new MaterialPropertyBlock();r.GetPropertyBlock(block);
                var tiling=profile.Kind==RoadSurfaceKind.Dirt?(horizontal?new Vector4(b.size.x/5,1,0,0):new Vector4(1,b.size.z/5,0,0)):new Vector4(b.size.x/4,b.size.z/4,b.min.x/4,b.min.z/4);
                block.SetVector("_BaseMap_ST",tiling);block.SetVector("_BumpMap_ST",tiling);block.SetColor("_BaseColor",Color.white);r.SetPropertyBlock(block);
            }
        }
    }
}
