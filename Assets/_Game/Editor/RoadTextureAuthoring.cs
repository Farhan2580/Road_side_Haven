using System.IO;
using UnityEngine;
namespace RoadsideHaven.Editor
{
    public static class RoadTextureAuthoring
    {
        public static readonly string[] Names={"RoadHighway","RoadCity","RoadDirt","RoadParking","RoadDirtHorizontal"};
        public static void Generate(string folder)
        {
            Directory.CreateDirectory(folder);GenerateWear(folder);const int n=512;
            for(int kind=0;kind<5;kind++)
            {
                string path=Path.Combine(folder,Names[kind]);if(File.Exists(path+"_Albedo.png")&&File.Exists(path+"_Normal.png"))continue;
                var pixels=new Color[n*n];var heights=new float[n*n];
                for(int y=0;y<n;y++)for(int x=0;x<n;x++)
                {
                    float u=x/(float)(n-1),v=y/(float)(n-1),noise=Mathf.PerlinNoise(u*12+17,v*12+17);float hash=Hash(x,y);
                    bool dirt=kind==2||kind==4;Color color;float h;
                    if(dirt)
                    {float across=kind==2?u:v,along=kind==2?v:u;float wheel=Mathf.Exp(-Mathf.Pow((across-.29f)/.055f,2))+Mathf.Exp(-Mathf.Pow((across-.71f)/.055f,2));float tread=Mathf.Pow(Mathf.Max(0,Mathf.Sin(along*Mathf.PI*48)),6)*wheel;color=Color.Lerp(new Color(.27f,.17f,.095f),new Color(.43f,.30f,.17f),noise);color*=1-wheel*.24f-tread*.10f;h=noise*.28f+hash*.07f-wheel*.12f-tread*.04f;}
                    else
                    {float shade=kind==0?.105f:kind==1?.18f:.235f;color=new Color(shade,shade*1.04f,shade*1.05f)*(.92f+noise*.12f+hash*.07f);float stone=Mathf.Pow(hash,9);color+=Color.gray*stone*.07f;h=hash*.12f+noise*.1f;}
                    color.a=1;pixels[y*n+x]=color;heights[y*n+x]=h;
                }
                Save(path+"_Albedo.png",pixels,n);var normal=new Color[n*n];
                for(int y=0;y<n;y++)for(int x=0;x<n;x++){float dx=heights[y*n+(x+1)%n]-heights[y*n+(x+n-1)%n],dy=heights[((y+1)%n)*n+x]-heights[((y+n-1)%n)*n+x];var direction=new Vector3(-dx*1.5f,-dy*1.5f,1).normalized;normal[y*n+x]=new Color(direction.x*.5f+.5f,direction.y*.5f+.5f,direction.z*.5f+.5f,1);}Save(path+"_Normal.png",normal,n);
            }
        }
        private static void GenerateWear(string folder)
        {
            foreach(bool oil in new[]{false,true})
            {
                string path=Path.Combine(folder,oil?"RoadOil.png":"ParkingPaint.png");if(File.Exists(path))continue;const int n=128;var pixels=new Color[n*n];
                for(int y=0;y<n;y++)for(int x=0;x<n;x++)
                {
                    float u=x/(float)(n-1),v=y/(float)(n-1),noise=Mathf.PerlinNoise(u*24+7,v*24+7);Color color;
                    if(oil){float dx=(u-.5f)*2,dy=(v-.5f)*2,r=Mathf.Sqrt(dx*dx+dy*dy)+(noise-.5f)*.25f;color=new Color(.045f,.05f,.045f,(1-Mathf.SmoothStep(.42f,.95f,r))*.58f);}
                    else{float edge=Mathf.Clamp01(Mathf.Min(u,1-u)*20)*Mathf.Clamp01(Mathf.Min(v,1-v)*20);color=new Color(.84f,.85f,.81f,(Hash(x,y)<.06f||noise<.34f?0:.30f+noise*.55f)*edge);}
                    pixels[y*n+x]=color;
                }
                Save(path,pixels,n);
            }
        }        private static float Hash(int x,int y){unchecked{uint h=(uint)(x*374761393+y*668265263);h=(h^(h>>13))*1274126177;return (h^(h>>16))/(float)uint.MaxValue;}}
        private static void Save(string path,Color[] pixels,int n){var t=new Texture2D(n,n,TextureFormat.RGBA32,false);t.SetPixels(pixels);t.Apply();File.WriteAllBytes(path,t.EncodeToPNG());Object.DestroyImmediate(t);}
    }
}
