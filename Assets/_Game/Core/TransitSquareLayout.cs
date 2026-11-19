using UnityEngine;
namespace RoadsideHaven
{
    /// <summary>Reserved parcels south of the residential pickup, outside every public carriageway.</summary>
    public static class TransitSquareLayout
    {
        public const string Layer="Residential Transit Square v1";
        public static readonly Rect Retail=Rect.MinMaxRect(-108,-47,-50,-30.3f);
        public static readonly Rect ClockGarden=Rect.MinMaxRect(-176,-74,-123,-31);
        public static readonly Rect WeekendCourt=Rect.MinMaxRect(-216,-62,-181,-31);
        public static readonly Vector3[] Shops={new Vector3(-98,0,-42),new Vector3(-78,0,-42),new Vector3(-59,0,-42)};
        public static readonly string[] ShopNames={"PLATFORM COFFEE","EVERYDAY MARKET","CYCLE & GO"};
        public static readonly Vector3[] ShopWalk={new Vector3(-103,.12f,-32.2f),new Vector3(-72,.12f,-32.2f),new Vector3(-72,.12f,-35.5f),new Vector3(-103,.12f,-35.5f),new Vector3(-103,.12f,-32.2f)};
        public static readonly Vector3[] GardenWalk={new Vector3(-166,.12f,-36),new Vector3(-132,.12f,-36),new Vector3(-132,.12f,-69),new Vector3(-166,.12f,-69),new Vector3(-166,.12f,-36)};
        public static Rect Building(int index)
        {var p=Shops[index];float half=index==0?9:index==1?8:6;return Rect.MinMaxRect(p.x-half,p.z-3.5f,p.x+half,p.z+3.5f);}
        public static Vector3 Entrance(int index)=>Shops[index]+new Vector3(0,.12f,3.5f);
    }
}
