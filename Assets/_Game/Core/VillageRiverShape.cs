using System.Collections.Generic;
using UnityEngine;
namespace RoadsideHaven
{
    public static class VillageRiverShape
    {
        public const float North=1015,South=-1015,WaterLevel=-1.25f;
        // Surveyed corridor: narrow urban passage, broad meanders across the outer districts.
        private static readonly Vector2[] Route={
            new Vector2(-90,-1015),new Vector2(-145,-850),new Vector2(65,-670),
            new Vector2(105,-490),new Vector2(-55,-310),new Vector2(72,-155),
            new Vector2(20.5f,-45),new Vector2(20,-13),new Vector2(21.5f,27),
            new Vector2(19.5f,60),new Vector2(21,95),new Vector2(-65,160),
            new Vector2(-260,280),new Vector2(-305,410),new Vector2(-110,590),
            new Vector2(-180,760),new Vector2(-310,900),new Vector2(-220,1015)};
        public static Vector3 Center(float z)
        {
            z=Mathf.Clamp(z,South,North);int i=0;while(i<Route.Length-2&&z>Route[i+1].y)i++;
            float t=Mathf.InverseLerp(Route[i].y,Route[i+1].y,z);
            float prev=i==0?0:(Route[i+1].x-Route[i-1].x)/(Route[i+1].y-Route[i-1].y);
            float next=i+2>=Route.Length?0:(Route[i+2].x-Route[i].x)/(Route[i+2].y-Route[i].y);
            if(Route[i].y>=-45&&Route[i].y<=95)prev=0;if(Route[i+1].y>=-45&&Route[i+1].y<=95)next=0;
            float dz=Route[i+1].y-Route[i].y;
            float x=(2*t*t*t-3*t*t+1)*Route[i].x+(t*t*t-2*t*t+t)*prev*dz+(-2*t*t*t+3*t*t)*Route[i+1].x+(t*t*t-t*t)*next*dz;
            return new Vector3(x,WaterLevel,z);
        }
        public static float HalfWidth(float z)
        {
            float urban=Mathf.SmoothStep(0,1,Mathf.InverseLerp(-95,-45,z))*(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(95,145,z)));
            return Mathf.Lerp(9.5f+1.5f*Mathf.Sin(z*.018f)+.7f*Mathf.Sin(z*.041f),3.1f+.18f*Mathf.Sin(z*.13f)+.12f*Mathf.Sin(z*.31f),urban);
        }
        public static bool Contains(Vector2 p,float margin=0)
        {if(p.y<South-margin||p.y>North+margin)return false;return Mathf.Abs(p.x-Center(p.y).x)<HalfWidth(p.y)+margin;}
        public static Mesh Ribbon(float north,float south,int mode=0)
        {
            int rows=Mathf.Max(2,Mathf.CeilToInt((north-south)/2));const int columns=12;var vertices=new List<Vector3>();var uv=new List<Vector2>();var colors=new List<Color>();var triangles=new List<int>();
            for(int row=0;row<=rows;row++)
            {
                float z=Mathf.Lerp(north,south,row/(float)rows);var center=Center(z);float half=HalfWidth(z);
                for(int col=0;col<=columns;col++)
                {float across=col/(float)columns*2-1,depth=1-Mathf.Abs(across),x=center.x+across*(half+(mode==2?10f:0));float y=mode==0?WaterLevel:mode==1?WaterLevel-.10f-depth*2.8f:Mathf.Lerp(-.09f,WaterLevel-.05f,Mathf.SmoothStep(0,1,Mathf.Clamp01((1-Mathf.Abs(across))/.28f)));if(mode==2){bool left=col<=3;float bankT=left?col/3f:(12-col)/3f;bankT=Mathf.Clamp01(bankT);x=center.x+(left?-1:1)*Mathf.Lerp(half+10,half-.2f,bankT);y=Mathf.Lerp(-.09f,WaterLevel-.10f,bankT*bankT);}vertices.Add(new Vector3(x,y,z));uv.Add(mode==0?new Vector2(col/(float)columns,(North-z)/80):new Vector2(x/4,z/4));colors.Add(new Color(depth,0,0,1));}
            }
            for(int row=0;row<rows;row++)for(int col=0;col<columns;col++){if(mode==2&&col>=3&&col<9)continue;int a=row*(columns+1)+col,b=a+1,c=a+columns+1,d=c+1;triangles.AddRange(new[]{a,b,c,b,d,c});}
            var mesh=new Mesh{name="River ribbon "+mode+" "+Mathf.RoundToInt(north)};mesh.SetVertices(vertices);mesh.SetUVs(0,uv);mesh.SetColors(colors);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }
        public static Mesh CulvertArch(float width,float height)
        {
            const int segments=18;var vertices=new Vector3[(segments+1)*2];var triangles=new List<int>();
            for(int i=0;i<=segments;i++){float a=i*Mathf.PI/segments;vertices[i*2]=new Vector3(Mathf.Cos(a)*width,Mathf.Sin(a)*height,0);vertices[i*2+1]=new Vector3(Mathf.Cos(a)*(width+.18f),Mathf.Sin(a)*(height+.18f),0);if(i<segments){int p=i*2;triangles.AddRange(new[]{p,p+2,p+1,p+1,p+2,p+3});}}
            var mesh=new Mesh{name="Twin culvert arch"};mesh.vertices=vertices;mesh.triangles=triangles.ToArray();mesh.RecalculateNormals();return mesh;
        }
    }
}
