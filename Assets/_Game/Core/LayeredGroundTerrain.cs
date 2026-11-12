using UnityEngine;
namespace RoadsideHaven
{
    /// <summary>Terrain is a visual skin; the existing world and welded road colliders remain authoritative.</summary>
    [ExecuteAlways]
    public sealed class LayeredGroundTerrain : MonoBehaviour
    {
        private void OnEnable(){Apply();}
        private void Start(){Apply();}
        public void Apply()
        {
            var root=transform.parent;if(root==null)return;
            foreach(var renderer in root.GetComponentsInChildren<MeshRenderer>(true))if(GroundSurfaceStyle.SurfaceFor(renderer.name)==0)renderer.enabled=false;
            if(root.Find("Village City River v3")!=null){RiverGroundPhysics.Apply(root);return;}
            foreach(var collider in GetComponents<Collider>()){collider.enabled=false;}
        }
    }
}
