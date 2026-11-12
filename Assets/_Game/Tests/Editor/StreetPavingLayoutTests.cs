using NUnit.Framework;
using UnityEngine;
namespace RoadsideHaven.Tests
{
    public sealed class StreetPavingLayoutTests
    {
        [Test] public void PavingCrossingAShortApproachIsClippedOutOfBothRoads()
        {
            var roads=new[]{new Rect(-200,-12,400,24),new Rect(51,-25,18,30)};
            var pieces=StreetPavingLayout.ClearRoads(new Rect(59,-29.5f,2,35),roads);
            Assert.IsNotEmpty(pieces);
            foreach(var piece in pieces)foreach(var road in roads)Assert.IsFalse(piece.Overlaps(road));
        }
        [Test] public void CompletelyBlockedPavementIsRemoved()
        {Assert.IsEmpty(StreetPavingLayout.ClearRoads(new Rect(-2,-2,4,4),new[]{new Rect(-12,-12,24,24)}));}
        [Test] public void QueueReturnsLowestCostDespiteOutOfOrderInsertions()
        {
            var queue=new TownRouteHeap();for(int i=1000;i>=0;i--)queue.Push(i,i);
            for(int i=0;i<=1000;i++){Assert.IsTrue(queue.Pop(out int node,out float cost));Assert.AreEqual(i,node);Assert.AreEqual(i,cost);}
            Assert.IsFalse(queue.Pop(out _,out _));
        }
    }
}
