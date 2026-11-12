using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
namespace RoadsideHaven
{
    /// <summary>One welded road surface prevents internal box edges from catching vehicle chassis at intersections.</summary>
    public sealed class RoadSurfacePhysics : MonoBehaviour
    {
        [SerializeField] private MeshCollider drivingSurface;
        private Mesh transient;
        public bool HasSurface=>drivingSurface!=null&&drivingSurface.enabled&&drivingSurface.sharedMesh!=null;
        public Mesh SurfaceMesh=>drivingSurface==null?null:drivingSurface.sharedMesh;
        private IEnumerator Start(){yield return null;yield return null;if(!HasSurface)Rebuild(transform);}
        public void UseSavedMesh(Mesh mesh){AssignMesh(mesh);transient=null;}
        public void AssignMesh(Mesh mesh)
        {
            drivingSurface.sharedMesh=null;drivingSurface.sharedMesh=mesh;
            if(transient!=null&&transient!=mesh){if(Application.isPlaying)Destroy(transient);else DestroyImmediate(transient);transient=null;}
        }
        public static RoadSurfacePhysics Rebuild(Transform root)
        {
            var component=root.GetComponent<RoadSurfacePhysics>();if(component==null)component=root.gameObject.AddComponent<RoadSurfacePhysics>();
            var rectangles=new List<Rect>();var colliders=new List<Collider>();var xs=new List<float>();var zs=new List<float>();
            foreach(var surface in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                if(!surface.gameObject.activeInHierarchy||!TownPopulationController.RoadName(surface.name)||surface.GetComponentInParent<Rigidbody>()!=null)continue;
                var b=surface.bounds;var r=Rect.MinMaxRect(b.min.x,b.min.z,b.max.x,b.max.z);rectangles.Add(r);xs.Add(r.xMin);xs.Add(r.xMax);zs.Add(r.yMin);zs.Add(r.yMax);colliders.AddRange(surface.GetComponents<Collider>());
            }
            if(rectangles.Count==0)return component;
            SortUnique(xs);SortUnique(zs);var vertices=new List<Vector3>();var indices=new List<int>();var lookup=new Dictionary<long,int>();
            for(int x=0;x<xs.Count-1;x++)for(int z=0;z<zs.Count-1;z++)
            {
                var midpoint=new Vector2((xs[x]+xs[x+1])*.5f,(zs[z]+zs[z+1])*.5f);bool inside=false;
                foreach(var rect in rectangles)if(rect.Contains(midpoint)){inside=true;break;}if(!inside)continue;
                int a=Vertex(x,z,xs,zs,vertices,lookup),b=Vertex(x+1,z,xs,zs,vertices,lookup),c=Vertex(x,z+1,xs,zs,vertices,lookup),d=Vertex(x+1,z+1,xs,zs,vertices,lookup);
                indices.Add(a);indices.Add(c);indices.Add(b);indices.Add(b);indices.Add(c);indices.Add(d);
            }
            var mesh=new Mesh{name="Welded road driving surface",indexFormat=IndexFormat.UInt32};mesh.SetVertices(vertices);mesh.SetTriangles(indices,0);mesh.RecalculateNormals();mesh.RecalculateBounds();
            if(component.drivingSurface==null)
            {var obj=new GameObject("Continuous road physics");obj.transform.SetParent(root,false);obj.transform.position=Vector3.zero;obj.transform.rotation=Quaternion.identity;obj.transform.localScale=Vector3.one;component.drivingSurface=obj.AddComponent<MeshCollider>();}
            component.AssignMesh(mesh);component.transient=mesh;
            component.drivingSurface.cookingOptions=MeshColliderCookingOptions.CookForFasterSimulation|MeshColliderCookingOptions.EnableMeshCleaning|MeshColliderCookingOptions.WeldColocatedVertices|MeshColliderCookingOptions.UseFastMidphase;
            component.drivingSurface.sharedMesh=null;component.drivingSurface.sharedMesh=mesh;component.drivingSurface.enabled=true;
            foreach(var collider in colliders)if(collider!=null)collider.enabled=false;
            Physics.SyncTransforms();return component;
        }
        private static int Vertex(int x,int z,List<float> xs,List<float> zs,List<Vector3> vertices,Dictionary<long,int> lookup)
        {long key=((long)x<<32)|(uint)z;if(lookup.TryGetValue(key,out int index))return index;index=vertices.Count;vertices.Add(new Vector3(xs[x],.06f,zs[z]));lookup.Add(key,index);return index;}
        private static void SortUnique(List<float> values)
        {values.Sort();for(int i=values.Count-1;i>0;i--)if(Mathf.Abs(values[i]-values[i-1])<.0001f)values.RemoveAt(i);}
        private void OnDestroy(){if(transient!=null){if(Application.isPlaying)Destroy(transient);else DestroyImmediate(transient);}}
    }
}
