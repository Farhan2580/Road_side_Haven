using System.Collections.Generic;
namespace RoadsideHaven
{
    /// <summary>Small binary min-heap for nonblocking road/footpath shortest-path queries.</summary>
    public sealed class TownRouteHeap
    {
        private struct Entry {public int Node;public float Cost;}
        private readonly List<Entry> entries=new List<Entry>();
        public void Push(int node,float cost)
        {
            var item=new Entry{Node=node,Cost=cost};int i=entries.Count;entries.Add(item);
            while(i>0){int parent=(i-1)/2;if(entries[parent].Cost<=cost)break;entries[i]=entries[parent];i=parent;}entries[i]=item;
        }
        public bool Pop(out int node,out float cost)
        {
            if(entries.Count==0){node=-1;cost=0;return false;}
            var first=entries[0];var last=entries[entries.Count-1];entries.RemoveAt(entries.Count-1);int i=0;
            while(i*2+1<entries.Count){int child=i*2+1;if(child+1<entries.Count&&entries[child+1].Cost<entries[child].Cost)child++;if(entries[child].Cost>=last.Cost)break;entries[i]=entries[child];i=child;}
            if(entries.Count>0)entries[i]=last;node=first.Node;cost=first.Cost;return true;
        }
    }
}
