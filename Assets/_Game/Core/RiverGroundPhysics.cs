using UnityEngine;
namespace RoadsideHaven
{
    /// <summary>The visual terrain holes are also physical holes; only bridges span the channel.</summary>
    public static class RiverGroundPhysics
    {
        public static void Apply(Transform root)
        {
            if(root.Find("Village City River v3")==null)return;
            foreach(var terrain in root.GetComponentsInChildren<Terrain>(true))
            {
                if(!terrain.gameObject.activeInHierarchy||terrain.terrainData==null)continue;
                var collider=terrain.GetComponent<TerrainCollider>();if(collider==null)collider=terrain.gameObject.AddComponent<TerrainCollider>();
                collider.terrainData=terrain.terrainData;collider.enabled=true;
            }
            foreach(var renderer in root.GetComponentsInChildren<MeshRenderer>(true))
                if(GroundSurfaceStyle.SurfaceFor(renderer.name)==0)
                    foreach(var collider in renderer.GetComponents<Collider>())collider.enabled=false;
            foreach(var renderer in root.GetComponentsInChildren<MeshRenderer>(true))if(renderer.name=="Lake bed water"){var filter=renderer.GetComponent<MeshFilter>();if(filter!=null&&renderer.GetComponent<Collider>()==null)renderer.gameObject.AddComponent<MeshCollider>().sharedMesh=filter.sharedMesh;}
            Physics.SyncTransforms();
        }
        public static bool Submerged(Vector3 p)=>p.y<VillageRiverShape.WaterLevel+.15f&&VillageRiverShape.Contains(new Vector2(p.x,p.z),-.3f);
    }
}