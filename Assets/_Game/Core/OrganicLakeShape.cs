using System.Collections.Generic;
using UnityEngine;
namespace RoadsideHaven
{
    public static class OrganicLakeShape
    {
        public const float Width=80,Length=60;
        public static Vector2[] Outline(int count=96)
        {
            var points=new Vector2[count];var min=Vector2.one*1000;var max=-min;
            for(int i=0;i<count;i++){float a=i*Mathf.PI*2/count;float r=1+.11f*Mathf.Sin(a*3+.4f)+.065f*Mathf.Cos(a*5)+.035f*Mathf.Sin(a*7);var p=new Vector2(Mathf.Cos(a)*40*r,Mathf.Sin(a)*30*r);points[i]=p;min=Vector2.Min(min,p);max=Vector2.Max(max,p);}
            for(int i=0;i<count;i++)points[i]=new Vector2((points[i].x-min.x)/(max.x-min.x)*Width-Width*.5f,(points[i].y-min.y)/(max.y-min.y)*Length-Length*.5f);return points;
        }
        public static bool Contains(Vector2 point,Vector2[] polygon)
        {bool inside=false;for(int i=0,j=polygon.Length-1;i<polygon.Length;j=i++)if((polygon[i].y>point.y)!=(polygon[j].y>point.y)&&point.x<(polygon[j].x-polygon[i].x)*(point.y-polygon[i].y)/(polygon[j].y-polygon[i].y)+polygon[i].x)inside=!inside;return inside;}
        public static Mesh Surface(bool bed=false)
        {
            var outline=Outline();int count=outline.Length,rings=14;var vertices=new List<Vector3>{new Vector3(0,bed?-3.2f:0,0)};var uv=new List<Vector2>{new Vector2(.5f,.5f)};var colors=new List<Color>{new Color(1,0,0,1)};var triangles=new List<int>();
            for(int ring=1;ring<=rings;ring++)for(int i=0;i<count;i++)
            {float r=ring/(float)rings;var p=outline[i]*r;float depth=1-r;vertices.Add(new Vector3(p.x,bed?-.04f-Mathf.Pow(depth,1.4f)*3.16f:0,p.y));uv.Add(new Vector2(p.x/Width+.5f,p.y/Length+.5f));colors.Add(new Color(depth,0,0,1));}
            for(int i=0;i<count;i++){triangles.Add(0);triangles.Add(1+(i+1)%count);triangles.Add(1+i);}
            for(int ring=0;ring<rings-1;ring++)for(int i=0;i<count;i++)
            {int a=1+ring*count+i,b=1+ring*count+(i+1)%count,c=a+count,d=b+count;triangles.AddRange(new[]{a,b,c,b,d,c});}
            var mesh=new Mesh{name=bed?"Organic lake depth bowl":"Organic 80x60 lake water"};mesh.SetVertices(vertices);mesh.SetUVs(0,uv);mesh.SetColors(colors);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }
        public static Mesh Bank()
        {
            var outline=Outline();int n=outline.Length;var vertices=new Vector3[n*2];var uv=new Vector2[n*2];var triangles=new List<int>();
            for(int i=0;i<n;i++){var p=outline[i];vertices[i]=new Vector3(p.x*.99f,-.04f,p.y*.99f);vertices[i+n]=new Vector3(p.x*1.13f,-.075f,p.y*1.13f);uv[i]=p/4;uv[i+n]=p*1.13f/4;}
            for(int i=0;i<n;i++){int next=(i+1)%n;triangles.AddRange(new[]{i,next,i+n,next,next+n,i+n});}
            var mesh=new Mesh{name="Natural lake shore"};mesh.vertices=vertices;mesh.uv=uv;mesh.triangles=triangles.ToArray();mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }
    }
}
