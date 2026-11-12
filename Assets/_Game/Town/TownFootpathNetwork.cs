using System.Collections.Generic;
using UnityEngine;
namespace RoadsideHaven
{
    /// <summary>Sidewalk graph with corner-to-corner crossings; never cuts diagonally across junctions.</summary>
    public sealed class TownFootpathNetwork
    {
        private sealed class Edge {public int To;public float Cost;}
        private sealed class Side {public TownRoadNetwork.Road Road;public List<int> Nodes=new List<int>();}
        private readonly List<Vector3> points=new List<Vector3>();
        private readonly List<List<Edge>> edges=new List<List<Edge>>();
        private readonly List<Side> sides=new List<Side>();
        private int Node(Vector3 p){for(int i=0;i<points.Count;i++)if((points[i]-p).sqrMagnitude<.02f)return i;points.Add(p);edges.Add(new List<Edge>());return points.Count-1;}
        private void Join(int a,int b){if(a==b)return;float cost=Vector3.Distance(points[a],points[b]);edges[a].Add(new Edge{To=b,Cost=cost});edges[b].Add(new Edge{To=a,Cost=cost});}
        public TownFootpathNetwork(IReadOnlyList<TownRoadNetwork.Road> roads,IReadOnlyList<Vector3> centers)
        {
            foreach(var road in roads)for(int side=-1;side<=1;side+=2)
            {
                var branch=new Side{Road=road};sides.Add(branch);var axis=(road.B-road.A).normalized;var right=Vector3.Cross(Vector3.up,axis);
                foreach(int node in road.Nodes)
                {
                    Vector3 p=centers[node];float along=0,width=road.HalfWidth;
                    foreach(var other in roads)
                    {
                        if((TownRoadNetwork.Project(p,other)-p).sqrMagnitude>.05f)continue;
                        var otherAxis=(other.B-other.A).normalized;
                        if(Mathf.Abs(Vector3.Dot(axis,otherAxis))<.5f)along=Mathf.Max(along,other.HalfWidth+1.5f);
                        else width=Mathf.Max(width,other.HalfWidth);
                    }
                    branch.Nodes.Add(Node(p-axis*along+right*side*(width+1.5f)));
                    if(along>0)branch.Nodes.Add(Node(p+axis*along+right*side*(width+1.5f)));
                }
                branch.Nodes.Sort((a,b)=>Vector3.Dot(points[a]-road.A,axis).CompareTo(Vector3.Dot(points[b]-road.A,axis)));
                for(int i=1;i<branch.Nodes.Count;i++)Join(branch.Nodes[i-1],branch.Nodes[i]);
            }
        }
        private int Endpoint(Vector3 p)
        {
            Side selected=null;Vector3 projection=default;float best=float.MaxValue;
            foreach(var side in sides)
            {
                if(side.Nodes.Count<2)continue;
                for(int i=1;i<side.Nodes.Count;i++)
                {
                    var a=points[side.Nodes[i-1]];var delta=points[side.Nodes[i]]-a;if(delta.sqrMagnitude<.01f)continue;
                    var q=a+delta*Mathf.Clamp01(Vector3.Dot(p-a,delta)/delta.sqrMagnitude);float d=(q-p).sqrMagnitude;
                    if(d<best){best=d;projection=q;selected=side;}
                }
            }
            if(selected==null)return -1;
            int id=Node(projection);var axis=(selected.Road.B-selected.Road.A).normalized;float position=Vector3.Dot(projection,axis);int before=-1,after=-1;float lo=float.MinValue,hi=float.MaxValue;
            foreach(int n in selected.Nodes){float t=Vector3.Dot(points[n],axis);if(t<=position&&t>lo){lo=t;before=n;}if(t>=position&&t<hi){hi=t;after=n;}}
            if(before>=0)Join(id,before);if(after>=0)Join(id,after);
            return id;
        }
        public Vector3[] Route(Vector3 from,Vector3 to)
        {
            int original=points.Count;var counts=new int[original];for(int i=0;i<original;i++)counts[i]=edges[i].Count;
            try
            {
                int a=Endpoint(from),b=Endpoint(to);if(a<0||b<0)return System.Array.Empty<Vector3>();
                // Direct connection on the same sidewalk edge avoids a detour via the far endpoint.
                foreach(var side in sides)
                    for(int i=1;i<side.Nodes.Count;i++)
                    {
                        var x=points[side.Nodes[i-1]];var y=points[side.Nodes[i]];
                        if(OnSegment(points[a],x,y)&&OnSegment(points[b],x,y))Join(a,b);
                    }
                var distance=new float[points.Count];var previous=new int[points.Count];var visited=new bool[points.Count];
                for(int i=0;i<points.Count;i++){distance[i]=float.MaxValue;previous[i]=-1;}distance[a]=0;
                var queue=new TownRouteHeap();queue.Push(a,0);
                while(queue.Pop(out int n,out float cost))
                {
                    if(visited[n]||cost>distance[n])continue;if(n==b)break;visited[n]=true;
                    foreach(var edge in edges[n])if(distance[n]+edge.Cost<distance[edge.To])
                    {distance[edge.To]=distance[n]+edge.Cost;previous[edge.To]=n;queue.Push(edge.To,distance[edge.To]);}
                }
                if(distance[b]==float.MaxValue)return System.Array.Empty<Vector3>();
                var route=new List<Vector3>();for(int n=b;n>=0;n=previous[n]){route.Add(points[n]);if(n==a)break;}route.Reverse();route.Add(to);return route.ToArray();
            }
            finally
            {
                for(int i=0;i<original;i++)if(edges[i].Count>counts[i])edges[i].RemoveRange(counts[i],edges[i].Count-counts[i]);
                points.RemoveRange(original,points.Count-original);edges.RemoveRange(original,edges.Count-original);
            }
        }
        private static bool OnSegment(Vector3 p,Vector3 a,Vector3 b){var d=b-a;if(d.sqrMagnitude<.01f)return false;float t=Vector3.Dot(p-a,d)/d.sqrMagnitude;return t>=0&&t<=1&&(a+d*t-p).sqrMagnitude<.02f;}
    }
}
