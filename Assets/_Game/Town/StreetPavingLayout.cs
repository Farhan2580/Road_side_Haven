using System.Collections.Generic;
using UnityEngine;
namespace RoadsideHaven
{
    /// <summary>Clips paving against every road, including short T-junction approaches.</summary>
    public static class StreetPavingLayout
    {
        public static List<Rect> ClearRoads(Rect source,IReadOnlyList<Rect> roads)
        {
            var pieces=new List<Rect>{source};
            foreach(var road in roads)
            {
                var cut=Rect.MinMaxRect(road.xMin-.04f,road.yMin-.04f,road.xMax+.04f,road.yMax+.04f);var next=new List<Rect>();
                foreach(var p in pieces)
                {
                    if(!p.Overlaps(cut)){next.Add(p);continue;}
                    float x0=Mathf.Max(p.xMin,cut.xMin),x1=Mathf.Min(p.xMax,cut.xMax),z0=Mathf.Max(p.yMin,cut.yMin),z1=Mathf.Min(p.yMax,cut.yMax);
                    Add(next,p.xMin,p.yMin,x0,p.yMax);Add(next,x1,p.yMin,p.xMax,p.yMax);Add(next,x0,p.yMin,x1,z0);Add(next,x0,z1,x1,p.yMax);
                }
                pieces=next;if(pieces.Count==0)break;
            }
            return pieces;
        }
        private static void Add(List<Rect> list,float x0,float z0,float x1,float z1){if(x1-x0>.06f&&z1-z0>.06f)list.Add(Rect.MinMaxRect(x0,z0,x1,z1));}
    }
}
