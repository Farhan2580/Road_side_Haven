using NUnit.Framework;
using UnityEngine;
namespace RoadsideHaven.Tests
{
    public sealed class TownJunctionLayoutTests
    {
        [Test] public void ShortNarrowRoadEndingAtCarriagewayEdgeGetsTJunction()
        {
            var graph=new TownRoadNetwork();graph.Add(Vector3.zero,new Vector3(400,.12f,24));graph.Add(new Vector3(50,0,-20),new Vector3(6,.12f,16));graph.Build(null);
            var junctions=TownJunctionLayout.Find(graph.Roads);Assert.AreEqual(1,junctions.Count);var j=junctions[0];
            Assert.IsTrue(j.South);Assert.IsTrue(j.East);Assert.IsTrue(j.West);Assert.IsFalse(j.North);
        }
        [Test] public void SeparatedRoadsDoNotGetPhantomSignals()
        {
            var graph=new TownRoadNetwork();graph.Add(Vector3.zero,new Vector3(400,.12f,24));graph.Add(new Vector3(50,0,-40),new Vector3(6,.12f,16));graph.Build(null);
            Assert.IsEmpty(TownJunctionLayout.Find(graph.Roads));
        }
        [Test] public void FullCrossroadsHasFourApproaches()
        {
            var graph=new TownRoadNetwork();graph.Add(Vector3.zero,new Vector3(400,.12f,24));graph.Add(Vector3.zero,new Vector3(6,.12f,100));graph.Build(null);
            var junctions=TownJunctionLayout.Find(graph.Roads);Assert.AreEqual(1,junctions.Count);var j=junctions[0];Assert.IsTrue(j.North&&j.South&&j.East&&j.West);
        }
        [Test] public void CrowdedVillageKeepsOnlySpacedMainCrossings()
        {
            var graph=new TownRoadNetwork();graph.Add(new Vector3(0,0,-13),new Vector3(1960,.12f,22));
            foreach(float x in new[]{-113f,-65,-45,0,35,65,105,113})graph.Add(new Vector3(x,0,14),new Vector3(6,.12f,148));graph.Build(null);
            var selected=TownJunctionLayout.SelectControlled(graph.Roads);Assert.That(selected.Count,Is.InRange(2,3));
            for(int a=0;a<selected.Count;a++)for(int b=a+1;b<selected.Count;b++)Assert.GreaterOrEqual(Vector3.Distance(selected[a].Center,selected[b].Center),85);
        }    }
}
