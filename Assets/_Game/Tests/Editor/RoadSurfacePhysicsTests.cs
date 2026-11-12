using NUnit.Framework;
using UnityEngine;
namespace RoadsideHaven.Tests
{
    public sealed class RoadSurfacePhysicsTests
    {
        [Test] public void CrossroadsUseOneFlatSurfaceWhileWallsKeepTheirCollision()
        {
            var root=new GameObject("Road surface test");Mesh mesh=null;
            try
            {
                var horizontal=Cube(root.transform,"Connected road",new Vector3(300,.12f,22));
                var vertical=Cube(root.transform,"Connected road",new Vector3(22,.12f,300));
                var wall=Cube(root.transform,"Wall",new Vector3(1,4,10));wall.transform.position=Vector3.right*40;
                var surface=RoadSurfacePhysics.Rebuild(root.transform);mesh=surface.SurfaceMesh;
                Assert.IsTrue(surface.HasSurface);Assert.IsFalse(horizontal.GetComponent<Collider>().enabled);Assert.IsFalse(vertical.GetComponent<Collider>().enabled);Assert.IsTrue(wall.GetComponent<Collider>().enabled);
                var vertices=mesh.vertices;foreach(var v in vertices)Assert.AreEqual(.06f,v.y,.0001f);
                var triangles=mesh.triangles;
                for(int i=0;i<triangles.Length;i+=3)
                {var center=(vertices[triangles[i]]+vertices[triangles[i+1]]+vertices[triangles[i+2]])/3;Assert.IsTrue(Mathf.Abs(center.x)<=11||Mathf.Abs(center.z)<=11,"Driving surface leaked into grass");}
            }
            finally{Object.DestroyImmediate(root);if(mesh!=null)Object.DestroyImmediate(mesh);}
        }
        private static GameObject Cube(Transform root,string name,Vector3 size)
        {var obj=GameObject.CreatePrimitive(PrimitiveType.Cube);obj.name=name;obj.transform.SetParent(root,false);obj.transform.localScale=size;return obj;}
    }
}
