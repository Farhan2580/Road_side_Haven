using NUnit.Framework;
using UnityEngine;
namespace RoadsideHaven.Tests
{
    public sealed class StreetSignalTests
    {
        [Test] public void CrosswalkRouteDoesNotCutThroughIntersectionCentre()
        {
            var graph=new TownRoadNetwork();graph.Add(Vector3.zero,new Vector3(400,.12f,24));graph.Add(Vector3.zero,new Vector3(24,.12f,400));graph.Build(null);
            var route=graph.Route(new Vector3(-90,0,-13.5f),new Vector3(90,0,13.5f),true);Assert.Greater(route.Length,3);
            for(int i=1;i<route.Length;i++)for(int sample=0;sample<=20;sample++)
            {var p=Vector3.Lerp(route[i-1],route[i],sample/20f);Assert.IsFalse(Mathf.Abs(p.x)<12&&Mathf.Abs(p.z)<12,"Route cuts through junction");}
        }
        [Test] public void PedestriansWaitForTheirPhaseAndClearRoadIfAlreadyCrossing()
        {
            var obj=new GameObject("Signal test");try
            {
                var signal=obj.AddComponent<StreetJunction>();signal.Configure(12,12,0);
                var curb=new Vector3(13.5f,0,-13.5f);var other=new Vector3(13.5f,0,13.5f);
                Assert.IsTrue(signal.WaitForWalk(curb,other,1.65f,0));Assert.IsFalse(signal.WaitForWalk(curb,other,1.65f,38));
                Assert.IsTrue(signal.WaitForWalk(curb,other,1.65f,54));Assert.IsFalse(signal.WaitForWalk(new Vector3(13.5f,0,0),other,1.65f,0));
            }finally{Object.DestroyImmediate(obj);}
        }
        [Test] public void VehicleApproachingRedIsLimitedButCommittedVehicleClearsIntersection()
        {
            var obj=new GameObject("Signal test");try
            {
                var signal=obj.AddComponent<StreetJunction>();signal.Configure(12,12,0);
                Assert.Less(signal.LimitVehicle(new Vector3(-24,0,-3),Vector3.right,2,4,0),10);
                Assert.IsTrue(float.IsPositiveInfinity(signal.LimitVehicle(new Vector3(-12,0,-3),Vector3.right,2,4,0)));
            }finally{Object.DestroyImmediate(obj);}
        }
    }
}
