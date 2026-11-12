using System.IO;
using UnityEditor;
using UnityEngine;
namespace RoadsideHaven.Editor
{
    public static class LakeTextureAuthoring
    {
        public const string Folder="Assets/_Game/Art/Water";
        public static void Generate(bool force=false)
        {
            Directory.CreateDirectory(Folder);string path=Folder+"/LakeWaveNormal.png";
            if(force||!File.Exists(path))
            {
                const int n=256;var colors=new Color[n*n];
                var heights=new float[n*n];
                for(int y=0;y<n;y++)for(int x=0;x<n;x++)
                {float u=x/(float)(n-1),v=y/(float)(n-1);float a=Mathf.PerlinNoise(u*7+17,v*7+31),b=Mathf.PerlinNoise((u-1)*7+17,v*7+31),c=Mathf.PerlinNoise(u*7+17,(v-1)*7+31),d=Mathf.PerlinNoise((u-1)*7+17,(v-1)*7+31);heights[y*n+x]=Mathf.Lerp(Mathf.Lerp(a,b,u),Mathf.Lerp(c,d,u),v);}
                for(int y=0;y<n;y++)for(int x=0;x<n;x++)
                {float dx=(heights[y*n+(x+1)%n]-heights[y*n+(x+n-1)%n])*9,dy=(heights[((y+1)%n)*n+x]-heights[((y+n-1)%n)*n+x])*9;var normal=new Vector3(-dx,-dy,1).normalized;colors[y*n+x]=new Color(normal.x*.5f+.5f,normal.y*.5f+.5f,normal.z*.5f+.5f,1);}
                var texture=new Texture2D(n,n,TextureFormat.RGBA32,false);texture.SetPixels(colors);texture.Apply();File.WriteAllBytes(path,texture.EncodeToPNG());Object.DestroyImmediate(texture);
            }
            string ripple=Folder+"/LakeRipple.png";
            if(!File.Exists(ripple))
            {
                const int n=64;var colors=new Color[n*n];for(int y=0;y<n;y++)for(int x=0;x<n;x++){float r=new Vector2((x/(float)(n-1)-.5f)*2,(y/(float)(n-1)-.5f)*2).magnitude;float alpha=Mathf.Exp(-Mathf.Pow((r-.7f)/.055f,2))*.55f;colors[y*n+x]=new Color(.85f,.96f,1,alpha);}var texture=new Texture2D(n,n,TextureFormat.RGBA32,false);texture.SetPixels(colors);texture.Apply();File.WriteAllBytes(ripple,texture.EncodeToPNG());Object.DestroyImmediate(texture);
            }
            AssetDatabase.Refresh();var importer=AssetImporter.GetAtPath(path) as TextureImporter;
            if(importer!=null&&importer.textureType!=TextureImporterType.NormalMap){importer.textureType=TextureImporterType.NormalMap;importer.sRGBTexture=false;importer.wrapMode=TextureWrapMode.Repeat;importer.mipmapEnabled=true;importer.anisoLevel=4;importer.SaveAndReimport();}
        }
    }
}
