using System.Collections.Generic;
using UnityEngine;
namespace RoadsideHaven
{
    /// <summary>Editor-baked splat weights; roads and buildings shape the blend without runtime painting.</summary>
    public static class GroundLayerPaint
    {
        public static float[,,] Bake(Rect world,int resolution,IReadOnlyList<Rect> roads,IReadOnlyList<Rect> buildings,IReadOnlyList<Rect> rocks)
        {
            var dry=new float[resolution,resolution];var dirt=new float[resolution,resolution];var gravel=new float[resolution,resolution];
            Paint(dry,world,roads,11,.82f,11);Paint(dirt,world,buildings,6,.88f,29);Paint(gravel,world,rocks,35,.95f,41);
            var result=new float[resolution,resolution,4];
            for(int z=0;z<resolution;z++)for(int x=0;x<resolution;x++)
            {
                float wx=Mathf.Lerp(world.xMin,world.xMax,x/(float)(resolution-1)),wz=Mathf.Lerp(world.yMin,world.yMax,z/(float)(resolution-1));
                float noise=Mathf.PerlinNoise(wx*.017f+61,wz*.017f+19);
                float north=Mathf.SmoothStep(0,1,Mathf.InverseLerp(620,790,wz))*(.72f+noise*.25f);
                float r=Mathf.Max(gravel[z,x],north),d=dirt[z,x]*(1-r),p=dry[z,x]*(1-r-d);
                result[z,x,0]=Mathf.Max(0,1-r-d-p);result[z,x,1]=p;result[z,x,2]=d;result[z,x,3]=r;
            }
            return result;
        }
        private static void Paint(float[,] map,Rect world,IReadOnlyList<Rect> areas,float reach,float strength,float seed)
        {
            int n=map.GetLength(0);if(areas==null)return;
            foreach(var area in areas)
            {
                int x0=Mathf.Clamp(Mathf.FloorToInt((area.xMin-reach-world.xMin)/world.width*(n-1)),0,n-1),x1=Mathf.Clamp(Mathf.CeilToInt((area.xMax+reach-world.xMin)/world.width*(n-1)),0,n-1);
                int z0=Mathf.Clamp(Mathf.FloorToInt((area.yMin-reach-world.yMin)/world.height*(n-1)),0,n-1),z1=Mathf.Clamp(Mathf.CeilToInt((area.yMax+reach-world.yMin)/world.height*(n-1)),0,n-1);
                for(int z=z0;z<=z1;z++)for(int x=x0;x<=x1;x++)
                {
                    float wx=Mathf.Lerp(world.xMin,world.xMax,x/(float)(n-1)),wz=Mathf.Lerp(world.yMin,world.yMax,z/(float)(n-1));
                    float dx=Mathf.Max(area.xMin-wx,0,wx-area.xMax),dz=Mathf.Max(area.yMin-wz,0,wz-area.yMax);float distance=Mathf.Sqrt(dx*dx+dz*dz);
                    float noise=Mathf.PerlinNoise(wx*.23f+seed,wz*.23f+seed);float value=(1-Mathf.SmoothStep(0,1,distance/reach))*strength*(.7f+.3f*noise);
                    map[z,x]=Mathf.Max(map[z,x],value);
                }
            }
        }
    }
}
