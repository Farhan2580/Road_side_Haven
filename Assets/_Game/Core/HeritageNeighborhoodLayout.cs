using UnityEngine;
namespace RoadsideHaven
{
    /// <summary>Surveyed empty plots; road, garden and walking coordinates share the same layout.</summary>
    public static class HeritageNeighborhoodLayout
    {
        public const string Layer="Heritage Residential Gardens v1";
        public static readonly Vector2[] Centers={new Vector2(-220,-155),new Vector2(580,395)};
        public static readonly string[] Names={"OLD TOWN GARDENS","WILLOW HERITAGE"};
        public static Vector3 House(int site,int index)
        {int row=index/6;float first=site==0?-304:504;return new Vector3(first+index%6*30,0,Centers[site].y+(row==0?32:-32));}
        public static Rect Plot(int site,int index)
        {var p=House(site,index);return Rect.MinMaxRect(p.x-13.8f,p.z-(index<6?23:26),p.x+13.8f,p.z+(index<6?26:23));}
        public static Rect Driveway(int site,int index)
        {var p=House(site,index);float sign=index<6?1:-1;float x=p.x+sign*8.8f,z=Centers[site].y;return Rect.MinMaxRect(x-1.8f,index<6?z+4.01f:z-30,x+1.8f,index<6?z+30:z-4.01f);}
        public static Rect[] Roads(int site)
        {
            if(site==0)return new[]{Rect.MinMaxRect(-350,-159,-113,-151),Rect.MinMaxRect(-117,-155,-109,-60),Rect.MinMaxRect(-113,-159,-48,-151),Rect.MinMaxRect(-52,-159,-44,-60)};
            return new[]{Rect.MinMaxRect(420,391,708,399),Rect.MinMaxRect(472,310,480,480),Rect.MinMaxRect(704,310,712,480),Rect.MinMaxRect(420,306,708,314),Rect.MinMaxRect(420,476,708,484)};
        }
        // Routes follow the public sidewalk, one marked crossing and the open park gate.
        public static Vector3[] ParkWalk(int site,float homeX)
        {
            const float y=.12f;
            return site==0?new[]{new Vector3(homeX,y,-149.6f),new Vector3(-118.5f,y,-149.6f),new Vector3(-118.5f,y,-139),new Vector3(-105,y,-139),new Vector3(-82,y,-139)}:
                new[]{new Vector3(homeX,y,400.7f),new Vector3(690,y,400.7f),new Vector3(690,y,410)};
        }
        public static Vector3 Arrival(int site)=>site==0?new Vector3(-285,.25f,-149):new Vector3(522,.25f,401);
    }
}
