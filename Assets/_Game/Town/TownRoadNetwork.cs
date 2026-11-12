using System;
using System.Collections.Generic;
using UnityEngine;
namespace RoadsideHaven
{
    /// <summary>Axis-aligned road graph built from the generated road surfaces, including junctions and stop projections.</summary>
    public sealed class TownRoadNetwork
    {
        public sealed class Road { public Vector3 A,B; public float HalfWidth; public readonly List<int> Nodes=new List<int>(); }
        private sealed class Link {public int To;public float Cost,Width;}
        private readonly List<Road> roads=new List<Road>();
        private TownFootpathNetwork footpaths;
        private readonly List<Vector3> nodes=new List<Vector3>();
        private readonly List<List<Link>> links=new List<List<Link>>();
        public IReadOnlyList<Road> Roads=>roads;
        public void Add(Vector3 center,Vector3 size)
        {
            bool horizontal=size.x>=size.z;float length=horizontal?size.x:size.z;
            if(length<8)return;
            Vector3 delta=horizontal?Vector3.right*length*.5f:Vector3.forward*length*.5f;
            roads.Add(new Road{A=new Vector3(center.x,.08f,center.z)-delta,B=new Vector3(center.x,.08f,center.z)+delta,HalfWidth=(horizontal?size.z:size.x)*.5f});
        }
        private int Node(Vector3 point)
        {
            for(int i=0;i<nodes.Count;i++)if((nodes[i]-point).sqrMagnitude<.04f)return i;
            nodes.Add(point);links.Add(new List<Link>());return nodes.Count-1;
        }
        public void Build(Vector3[] stops)
        {
            // Discard old narrow overlays contained by a newer, wider road.
            for(int i=roads.Count-1;i>=0;i--)for(int j=0;j<roads.Count;j++)
            {if(i==j)continue;var a=roads[i];var b=roads[j];if(b.HalfWidth<a.HalfWidth||(b.HalfWidth==a.HalfWidth&&j>i))continue;if(OnSegment(a.A,b)&&OnSegment(a.B,b)){roads.RemoveAt(i);break;}}
            nodes.Clear();links.Clear();foreach(var road in roads)road.Nodes.Clear();
            foreach(var road in roads){road.Nodes.Add(Node(road.A));road.Nodes.Add(Node(road.B));}
            for(int a=0;a<roads.Count;a++)for(int b=a+1;b<roads.Count;b++)
            {
                var x=roads[a];var y=roads[b];bool hx=Mathf.Abs(x.B.x-x.A.x)>1,hy=Mathf.Abs(y.B.x-y.A.x)>1;
                if(hx!=hy)
                {
                    var h=hx?x:y;var v=hx?y:x;var point=new Vector3(v.A.x,.08f,h.A.z);
                    if(point.x>=h.A.x-.1f&&point.x<=h.B.x+.1f&&point.z>=v.A.z-.1f&&point.z<=v.B.z+.1f)
                    {int id=Node(point);x.Nodes.Add(id);y.Nodes.Add(id);}
                }
                else if((hx?Mathf.Abs(x.A.z-y.A.z):Mathf.Abs(x.A.x-y.A.x))<.1f)
                {
                    foreach(var point in new[]{x.A,x.B,y.A,y.B})
                        if(OnSegment(point,x)&&OnSegment(point,y)){int id=Node(point);x.Nodes.Add(id);y.Nodes.Add(id);}
                }
            }
            if(stops!=null)foreach(var stop in stops){var road=Nearest(stop);if(road!=null)road.Nodes.Add(Node(Project(stop,road)));}
            foreach(var road in roads)
            {
                road.Nodes.Sort((a,b)=>(nodes[a]-road.A).sqrMagnitude.CompareTo((nodes[b]-road.A).sqrMagnitude));
                for(int i=1;i<road.Nodes.Count;i++)
                {int a=road.Nodes[i-1],b=road.Nodes[i];if(a==b)continue;float cost=Vector3.Distance(nodes[a],nodes[b]);links[a].Add(new Link{To=b,Cost=cost,Width=road.HalfWidth});links[b].Add(new Link{To=a,Cost=cost,Width=road.HalfWidth});}
            }
            footpaths=new TownFootpathNetwork(roads,nodes);
        }
        private static bool OnSegment(Vector3 point,Road road)=>point.x>=road.A.x-.1f&&point.x<=road.B.x+.1f&&point.z>=road.A.z-.1f&&point.z<=road.B.z+.1f;
        public Road Nearest(Vector3 point)
        {Road nearest=null;float distance=float.MaxValue;foreach(var road in roads){float d=(Project(point,road)-point).sqrMagnitude;if(d>=distance)continue;distance=d;nearest=road;}return nearest;}
        public static Vector3 Project(Vector3 point,Road road){var delta=road.B-road.A;return road.A+delta*Mathf.Clamp01(Vector3.Dot(point-road.A,delta)/delta.sqrMagnitude);}
        public Vector3[] Route(Vector3 from,Vector3 to,bool pedestrian=false,float minimumWidth=0,bool outerLane=false)
        {
            if(pedestrian)return footpaths==null?Array.Empty<Vector3>():footpaths.Route(from,to);
            if(nodes.Count==0)return Array.Empty<Vector3>();
            int original=nodes.Count;
            try {
            int start=Endpoint(from,minimumWidth,original),end=Endpoint(to,minimumWidth,original);if(start<0||end<0)return Array.Empty<Vector3>();
            float[] distance=new float[nodes.Count];int[] previous=new int[nodes.Count];bool[] visited=new bool[nodes.Count];
            for(int i=0;i<nodes.Count;i++){distance[i]=float.MaxValue;previous[i]=-1;}distance[start]=0;
            var queue=new TownRouteHeap();queue.Push(start,0);
            while(queue.Pop(out int current,out float cost))
            {
                if(visited[current]||cost>distance[current])continue;if(current==end)break;visited[current]=true;
                foreach(var edge in links[current])if(edge.Width>=minimumWidth&&distance[current]+edge.Cost<distance[edge.To])
                {distance[edge.To]=distance[current]+edge.Cost;previous[edge.To]=current;queue.Push(edge.To,distance[edge.To]);}
            }
            if(distance[end]==float.MaxValue)return Array.Empty<Vector3>();
            var chain=new List<int>();for(int n=end;n>=0;n=previous[n]){chain.Add(n);if(n==start)break;}chain.Reverse();
            var result=new List<Vector3>();
            for(int i=0;i<chain.Count;i++)
            {
                Vector3 direction=i+1<chain.Count?nodes[chain[i+1]]-nodes[chain[i]]:i>0?nodes[chain[i]]-nodes[chain[i-1]]:Vector3.forward;
                if(direction.sqrMagnitude<.01f)continue;direction.Normalize();float halfWidth=3;
                int neighbor=i+1<chain.Count?chain[i+1]:i>0?chain[i-1]:-1;
                foreach(var edge in links[chain[i]])if(edge.To==neighbor){halfWidth=edge.Width;break;}
                float offset=pedestrian?halfWidth+1.5f:Mathf.Abs(halfWidth-6)<.05f?(outerLane?4.5f:1.5f):Mathf.Min(halfWidth*.5f,3f);
                result.Add(nodes[chain[i]]+Vector3.Cross(Vector3.up,direction)*offset);
            }
            if(pedestrian)result.Add(to);
            return result.ToArray();
            } finally {for(int i=0;i<original;i++)links[i].RemoveAll(e=>e.To>=original);nodes.RemoveRange(original,nodes.Count-original);links.RemoveRange(original,links.Count-original);}
        }
        private int Endpoint(Vector3 point,float width,int original)
        {
            Road selected=null;float best=float.MaxValue;
            foreach(var road in roads){if(road.HalfWidth<width)continue;float d=(Project(point,road)-point).sqrMagnitude;if(d<best){best=d;selected=road;}}
            if(selected==null)return -1;
            var projected=Project(point,selected);int id=Node(projected);if(id<original)return id;
            // Connect query projections to the surrounding edge without growing the saved graph.
            int before=-1,after=-1;float low=-1,high=float.MaxValue;var axis=(selected.B-selected.A).normalized;float along=Vector3.Dot(projected-selected.A,axis);
            foreach(int n in selected.Nodes){float t=Vector3.Dot(nodes[n]-selected.A,axis);if(t<=along&&t>low){low=t;before=n;}if(t>=along&&t<high){high=t;after=n;}}
            foreach(int n in new[]{before,after})if(n>=0&&n!=id){float cost=Vector3.Distance(nodes[n],projected);links[id].Add(new Link{To=n,Cost=cost,Width=selected.HalfWidth});links[n].Add(new Link{To=id,Cost=cost,Width=selected.HalfWidth});}
            // Two endpoints on one road may share the same unsplit edge.
            for(int n=original;n<id;n++)if(OnSegment(nodes[n],selected)){float cost=Vector3.Distance(nodes[n],projected);links[id].Add(new Link{To=n,Cost=cost,Width=selected.HalfWidth});links[n].Add(new Link{To=id,Cost=cost,Width=selected.HalfWidth});}
            return id;
        }
    }
}