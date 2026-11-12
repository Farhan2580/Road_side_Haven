using System.IO;
using UnityEditor;
using UnityEngine;
namespace RoadsideHaven.Editor
{
    /// <summary>Deterministic, seamless surface maps. Generated once; no runtime texture synthesis.</summary>
    public static class GroundTextureAuthoring
    {
        public const string Folder="Assets/_Game/Art/Surfaces";
        public static readonly string[] Names={"Grass","Asphalt","Earth","Sand","Rock","Paving"};
        public static void Generate(string folder,bool force=false)
        {
            Directory.CreateDirectory(folder);
            GenerateGrassLayers(folder,force);
            RoadTextureAuthoring.Generate(folder);
            for(int kind=0;kind<Names.Length;kind++)
            {
                string path=Path.Combine(folder,Names[kind]);if(!force&&File.Exists(path+"_Albedo.png")&&File.Exists(path+"_Normal.png"))continue;
                const int size=512;var colors=new Color[size*size];var heights=new float[size*size];
                for(int y=0;y<size;y++)for(int x=0;x<size;x++)
                {
                    float u=x/(float)(size-1),v=y/(float)(size-1),large=Noise(u,v,4,kind*13+5),detail=Noise(u,v,29,kind*7+31),grain=Hash(x,y,kind);
                    Color color;float h;
                    if(kind==0)
                    {float blades=Noise(u,v,110,7);color=Color.Lerp(new Color(.15f,.26f,.075f),new Color(.31f,.43f,.14f),large);color=Color.Lerp(color,new Color(.32f,.27f,.12f),Mathf.SmoothStep(0,.45f,Mathf.Clamp01((.39f-large)*5)));color*=.78f+detail*.34f+blades*.2f;h=blades*.6f+detail*.25f+grain*.15f;}
                    else if(kind==1)
                    {float aggregate=Mathf.Pow(grain,7);color=Color.Lerp(new Color(.135f,.145f,.15f),new Color(.165f,.175f,.18f),large);color*=.94f+detail*.08f;color+=new Color(.10f,.10f,.095f)*aggregate;h=detail*.25f+grain*.22f;}
                    else if(kind==2)
                    {color=Color.Lerp(new Color(.24f,.16f,.09f),new Color(.43f,.31f,.18f),large);color*=.8f+detail*.32f+grain*.12f;h=detail*.65f+grain*.25f;}
                    else if(kind==3)
                    {float ripple=Mathf.Sin((u*24+detail*.18f)*Mathf.PI*2)*.025f;color=Color.Lerp(new Color(.65f,.53f,.32f),new Color(.85f,.75f,.51f),large);color*=.93f+detail*.12f+grain*.06f+ripple;h=detail*.25f+grain*.08f+ripple;}
                    else if(kind==4)
                    {color=Color.Lerp(new Color(.28f,.30f,.27f),new Color(.51f,.51f,.43f),large);color*=.76f+detail*.45f;h=large*.5f+detail*.5f;}
                    else
                    {float joint=Mathf.Min(Mathf.Min(Mathf.Repeat(u*4,1),1-Mathf.Repeat(u*4,1)),Mathf.Min(Mathf.Repeat(v*4,1),1-Mathf.Repeat(v*4,1)));float grout=1-Mathf.SmoothStep(.005f,.018f,joint);color=Color.Lerp(new Color(.46f,.48f,.45f),new Color(.61f,.62f,.57f),large);color*=.93f+grain*.12f;color=Color.Lerp(color,new Color(.30f,.32f,.29f),grout*.5f);h=detail*.1f+grain*.04f-grout*.15f;}
                    color.a=1;colors[y*size+x]=color;heights[y*size+x]=h;
                }
                Write(path+"_Albedo.png",colors,size);
                var normals=new Color[size*size];float strength=kind==0?3:kind==1?1.1f:kind==4?3:1.8f;
                for(int y=0;y<size;y++)for(int x=0;x<size;x++)
                {float dx=heights[y*size+(x+1)%size]-heights[y*size+(x+size-1)%size],dy=heights[((y+1)%size)*size+x]-heights[((y+size-1)%size)*size+x];var n=new Vector3(-dx*strength,-dy*strength,1).normalized;normals[y*size+x]=new Color(n.x*.5f+.5f,n.y*.5f+.5f,n.z*.5f+.5f,1);}
                Write(path+"_Normal.png",normals,size);
            }
            string macro=Path.Combine(folder,"GroundMacro.png");
            if(force||!File.Exists(macro))
            {
                const int n=256;var pixels=new Color[n*n];for(int y=0;y<n;y++)for(int x=0;x<n;x++)
                {float value=.5f+(Noise(x/(float)(n-1),y/(float)(n-1),4,97)-.5f)*.45f;pixels[y*n+x]=new Color(value,value,value,1);}Write(macro,pixels,n);
            }
            string shoulder=Path.Combine(folder,"Shoulder_Albedo.png");
            if(force||!File.Exists(shoulder))
            {
                const int n=256;var pixels=new Color[n*n];for(int y=0;y<n;y++)for(int x=0;x<n;x++)
                {float u=x/(float)(n-1),v=y/(float)(n-1),noise=Noise(u,v,8,31);var c=Color.Lerp(new Color(.30f,.23f,.13f),new Color(.44f,.35f,.20f),noise);c.a=Mathf.Sin(v*Mathf.PI)*(.20f+noise*.42f);pixels[y*n+x]=c;}Write(shoulder,pixels,n);
            }
        }
        private static void GenerateGrassLayers(string folder,bool force)
        {
            string[] names={"BaseGrass","DryGrass","DirtPebbles","RockyGravel"};
            Color[] targets={new Color(74/255f,124/255f,63/255f),new Color(196/255f,169/255f,81/255f),new Color(139/255f,99/255f,64/255f),new Color(.49f,.51f,.48f)};
            const int n=512;
            for(int kind=0;kind<4;kind++)
            {
                string path=Path.Combine(folder,names[kind]);if(!force&&File.Exists(path+"_Albedo.png")&&File.Exists(path+"_Normal.png"))continue;
                var colors=new Color[n*n];var heights=new float[n*n];
                for(int y=0;y<n;y++)for(int x=0;x<n;x++)
                {
                    float u=x/(float)(n-1),v=y/(float)(n-1),noise=Noise(u,v,9,71+kind*13),grain=Hash(x,y,kind+9);
                    float fibers=Noise(u,v,kind<2?110:65,17);var c=targets[kind]*(.88f+noise*.24f);float height=fibers*.3f+grain*.08f;
                    if(kind<2){c*=.94f+fibers*.12f;height=fibers*.45f+grain*.08f;}
                    else
                    {
                        float frequency=kind==2?48:90;int cellX=Mathf.FloorToInt(u*frequency),cellY=Mathf.FloorToInt(v*frequency);
                        float cx=.3f+Hash(cellX,cellY,3)*.4f,cy=.3f+Hash(cellX,cellY,5)*.4f;
                        float dx=Mathf.Repeat(u*frequency,1)-cx,dy=Mathf.Repeat(v*frequency,1)-cy;float radius=kind==2?.12f:.30f;
                        float pebble=1-Mathf.SmoothStep(radius*.55f,radius,Mathf.Sqrt(dx*dx+dy*dy));
                        c=Color.Lerp(c,new Color(.48f,.48f,.43f)*(.75f+Hash(cellX,cellY,11)*.5f),pebble);height+=pebble*.5f;
                    }
                    c.a=1;colors[y*n+x]=c;heights[y*n+x]=height;
                }
                Write(path+"_Albedo.png",colors,n);var normals=new Color[n*n];
                for(int y=0;y<n;y++)for(int x=0;x<n;x++)
                {float dx=heights[y*n+(x+1)%n]-heights[y*n+(x+n-1)%n],dy=heights[((y+1)%n)*n+x]-heights[((y+n-1)%n)*n+x];var normal=new Vector3(-dx*2,-dy*2,1).normalized;normals[y*n+x]=new Color(normal.x*.5f+.5f,normal.y*.5f+.5f,normal.z*.5f+.5f,1);}
                Write(path+"_Normal.png",normals,n);
            }
        }        public static void ImportMaps()
        {
            AssetDatabase.Refresh();
            foreach(string path in Directory.GetFiles(Folder,"*.png"))
            {
                var importer=AssetImporter.GetAtPath(path.Replace('\\','/')) as TextureImporter;if(importer==null)continue;
                bool normal=path.Contains("_Normal");if(importer.textureType==(normal?TextureImporterType.NormalMap:TextureImporterType.Default)&&importer.anisoLevel==8&&importer.mipmapEnabled)continue;
                importer.textureType=normal?TextureImporterType.NormalMap:TextureImporterType.Default;importer.sRGBTexture=!normal&&!path.Contains("GroundMacro");importer.wrapMode=TextureWrapMode.Repeat;importer.mipmapEnabled=true;importer.anisoLevel=8;importer.maxTextureSize=512;importer.textureCompression=TextureImporterCompression.Compressed;importer.alphaIsTransparency=path.Contains("Shoulder")||path.Contains("RoadOil")||path.Contains("ParkingPaint");importer.SaveAndReimport();
            }
        }
        private static void Write(string path,Color[] pixels,int size)
        {var texture=new Texture2D(size,size,TextureFormat.RGBA32,false);texture.SetPixels(pixels);texture.Apply();File.WriteAllBytes(path,texture.EncodeToPNG());Object.DestroyImmediate(texture);}
        private static float Hash(int x,int y,int seed){unchecked{uint h=(uint)(x*374761393+y*668265263+seed*144269);h=(h^(h>>13))*1274126177;return (h^(h>>16))/(float)uint.MaxValue;}}
        private static float Noise(float u,float v,float scale,float seed)
        {float a=Mathf.PerlinNoise(seed+u*scale,seed+v*scale),b=Mathf.PerlinNoise(seed+(u-1)*scale,seed+v*scale),c=Mathf.PerlinNoise(seed+u*scale,seed+(v-1)*scale),d=Mathf.PerlinNoise(seed+(u-1)*scale,seed+(v-1)*scale);return Mathf.Lerp(Mathf.Lerp(a,b,u),Mathf.Lerp(c,d,u),v);}
    }
}
