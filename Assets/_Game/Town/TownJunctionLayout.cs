using System.Collections.Generic;
using UnityEngine;
namespace RoadsideHaven
{
    /// <summary>Finds full and T-junctions, including short approaches ending inside a wider road.</summary>
    public static class TownJunctionLayout
    {
        public sealed class Junction
        {public Vector3 Center;public float X,Z;public bool North,South,East,West;}
        public static List<Junction> Find(IReadOnlyList<TownRoadNetwork.Road> roads)
        {
            var result=new List<Junction>();
            foreach(var h in roads)foreach(var v in roads)
            {
                if(h.B.x-h.A.x<8||v.B.z-v.A.z<8)continue;
                var p=new Vector3(v.A.x,0,h.A.z);
                // Short approach meshes often finish at the carriageway edge, rather than its centre.
                if(p.x<h.A.x-v.HalfWidth||p.x>h.B.x+v.HalfWidth||p.z<v.A.z-h.HalfWidth||p.z>v.B.z+h.HalfWidth)continue;
                Junction junction=null;foreach(var existing in result)if((existing.Center-p).sqrMagnitude<.25f){junction=existing;break;}
                if(junction==null){junction=new Junction{Center=p};result.Add(junction);}
                junction.X=Mathf.Max(junction.X,v.HalfWidth);junction.Z=Mathf.Max(junction.Z,h.HalfWidth);
            }
            foreach(var junction in result)foreach(var road in roads)
            {
                var c=junction.Center;
                if(road.B.x-road.A.x>=8&&Mathf.Abs(road.A.z-c.z)<.1f&&road.A.x<=c.x+junction.X&&road.B.x>=c.x-junction.X)
                {junction.West|=road.A.x<c.x-junction.X-.2f;junction.East|=road.B.x>c.x+junction.X+.2f;}
                if(road.B.z-road.A.z>=8&&Mathf.Abs(road.A.x-c.x)<.1f&&road.A.z<=c.z+junction.Z&&road.B.z>=c.z-junction.Z)
                {junction.South|=road.A.z<c.z-junction.Z-.2f;junction.North|=road.B.z>c.z+junction.Z+.2f;}
            }
            result.RemoveAll(j=> (j.North?1:0)+(j.South?1:0)+(j.East?1:0)+(j.West?1:0)<3);
            return result;
        }
        /// <summary>Keep signals at well-separated main junctions; minor close approaches remain unpainted.</summary>
        public static List<Junction> SelectControlled(IReadOnlyList<TownRoadNetwork.Road> roads)
        {
            var candidates=Find(roads);
            candidates.Sort((a,b)=>{int priority=Importance(b,roads).CompareTo(Importance(a,roads));if(priority!=0)return priority;int x=a.Center.x.CompareTo(b.Center.x);return x!=0?x:a.Center.z.CompareTo(b.Center.z);});
            var selected=new List<Junction>();
            foreach(var candidate in candidates)
            {
                bool crowded=false;
                foreach(var existing in selected)
                {
                    float separation=Mathf.Max(85,2*Mathf.Max(candidate.X,candidate.Z,existing.X,existing.Z)+36);
                    if(Vector3.Distance(existing.Center,candidate.Center)<separation){crowded=true;break;}
                }
                if(!crowded)selected.Add(candidate);
            }
            return selected;
        }
        private static float Importance(Junction junction,IReadOnlyList<TownRoadNetwork.Road> roads)
        {
            float horizontal=0,vertical=0;
            foreach(var road in roads)
            {
                if((TownRoadNetwork.Project(junction.Center,road)-junction.Center).sqrMagnitude>.05f)continue;
                horizontal=Mathf.Max(horizontal,road.B.x-road.A.x);vertical=Mathf.Max(vertical,road.B.z-road.A.z);
            }
            // The motel entrance is the primary pedestrian/player approach, even though its spur is short.
            float landmark=(junction.Center-new Vector3(0,0,-13)).sqrMagnitude<1?100000:0;
            return landmark+Mathf.Max(horizontal,vertical)+Mathf.Min(horizontal,vertical)*2+(junction.X+junction.Z)*20;
        }    }
}
