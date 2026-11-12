using NUnit.Framework;
using UnityEngine;
namespace RoadsideHaven.Tests
{
    public sealed class OrganicLakeShapeTests
    {
        [Test] public void LakeHasExactFootprintUpwardSurfaceAndShoreDepthMask()
        {
            var mesh=OrganicLakeShape.Surface();try
            {
                Assert.AreEqual(80,mesh.bounds.size.x,.01f);Assert.AreEqual(60,mesh.bounds.size.z,.01f);
                foreach(var n in mesh.normals)Assert.Greater(n.y,.9f);
                Assert.AreEqual(1,mesh.colors[0].r);Assert.AreEqual(0,mesh.colors[mesh.vertexCount-1].r);
            }finally{Object.DestroyImmediate(mesh);}
        }
        [Test] public void IrregularBoundaryContainsCentreAndRejectsOutsidePoints()
        {
            var points=OrganicLakeShape.Outline();Assert.IsTrue(OrganicLakeShape.Contains(Vector2.zero,points));Assert.IsFalse(OrganicLakeShape.Contains(new Vector2(50,50),points));
            float min=10,max=0;foreach(var p in points){float r=new Vector2(p.x/40,p.y/30).magnitude;min=Mathf.Min(min,r);max=Mathf.Max(max,r);}Assert.Greater(max-min,.1f);
        }
    }
}
