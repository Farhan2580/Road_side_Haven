using NUnit.Framework;
using UnityEngine;
namespace RoadsideHaven.Tests
{
    public sealed class TownRoadNetworkTests
    {
        [Test] public void MidRoadTripKeepsItsOwnEndpointsAcrossRepeatedQueries()
        {
            var graph=new TownRoadNetwork();graph.Add(Vector3.zero,new Vector3(1000,.12f,22));graph.Build(null);
            for(int i=0;i<100;i++)
            {
                var from=new Vector3(-300+i,0,0);var to=new Vector3(100+i,0,0);var route=graph.Route(from,to,false,5);
                Assert.GreaterOrEqual(route.Length,2);Assert.AreEqual(from.x,route[0].x,.01f);Assert.AreEqual(to.x,route[route.Length-1].x,.01f);
                Assert.Less(Vector3.Distance(route[0],route[route.Length-1]),401);
            }
        }
        [Test] public void NarrowLegacyOverlayCannotBecomeASidewalkInsideAWideRoad()
        {
            var graph=new TownRoadNetwork();graph.Add(Vector3.zero,new Vector3(1000,.12f,22));graph.Add(Vector3.zero,new Vector3(34,.08f,4));graph.Build(null);
            Assert.AreEqual(1,graph.Roads.Count);Assert.AreEqual(11,graph.Nearest(Vector3.zero).HalfWidth);
            var route=graph.Route(new Vector3(-10,0,0),new Vector3(10,0,0),true);Assert.Greater(Mathf.Abs(route[0].z),11);
        }
        [Test] public void ADisconnectedCampusDoesNotProduceAStraightLineThroughBuildings()
        {
            var graph=new TownRoadNetwork();graph.Add(Vector3.zero,new Vector3(100,.12f,22));graph.Add(new Vector3(0,0,200),new Vector3(100,.12f,22));graph.Build(null);
            Assert.IsEmpty(graph.Route(Vector3.zero,new Vector3(0,0,200),false,5));
        }
        [Test] public void HighwayLanesStayInside12mAndBusCanReach8mCityStreet()
        {
            var graph=new TownRoadNetwork();graph.Add(new Vector3(0,0,-13),new Vector3(360,.12f,12));graph.Add(new Vector3(50,0,50),new Vector3(8,.12f,150));graph.Build(null);
            var from=new Vector3(-100,0,-13);var to=new Vector3(100,0,-13);
            var inner=graph.Route(from,to,false,2.2f,false);var outer=graph.Route(from,to,false,2.2f,true);
            Assert.AreEqual(-14.5f,inner[0].z,.01f);Assert.AreEqual(-17.5f,outer[0].z,.01f);
            Assert.Greater(graph.Route(from,new Vector3(50,0,100),false,3.5f).Length,1);
        }    }
}
