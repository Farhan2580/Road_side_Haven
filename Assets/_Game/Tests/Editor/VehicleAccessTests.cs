using NUnit.Framework;
using UnityEngine;
namespace RoadsideHaven.Tests
{
    public sealed class VehicleAccessTests
    {
        [Test] public void EntryDistanceUsesLargeVehicleSurfaceInsteadOfItsCenter()
        {
            var car=new GameObject("Test truck");car.transform.position=new Vector3(10000,100,10000);
            try
            {
                var body=car.AddComponent<Rigidbody>();body.isKinematic=true;
                var box=car.AddComponent<BoxCollider>();box.size=new Vector3(8,5,30);box.center=Vector3.up*2.5f;
                Physics.SyncTransforms();
                float near=WorldDrivingController.EntryDistance(body,car.transform.position+new Vector3(4.5f,0,12));
                float far=WorldDrivingController.EntryDistance(body,car.transform.position+new Vector3(15,0,12));
                Assert.That(near,Is.LessThan(1));Assert.That(far,Is.GreaterThan(5));
            }
            finally{Object.DestroyImmediate(car);}
        }
        [Test] public void TakingTrafficVehicleStopsAutopilotAndEnablesDrivingPhysics()
        {
            var car=new GameObject("Traffic test");
            try
            {
                var body=car.AddComponent<Rigidbody>();body.isKinematic=true;body.useGravity=false;
                var traffic=car.AddComponent<RoadTraffic>();traffic.enabled=true;
                var access=car.AddComponent<EnterableVehicle>();access.Configure(-1);
                access.TakeControl();Assert.IsFalse(traffic.enabled);Assert.IsFalse(body.isKinematic);Assert.IsTrue(body.useGravity);
                Assert.AreEqual(-1,access.ForwardSign);
            }
            finally{Object.DestroyImmediate(car);}
        }
        [Test] public void RegisteringSameVehicleTwiceKeepsSupplyTruckSlotAvailable()
        {
            var root=new GameObject("Fleet test");
            try
            {
                var controller=root.AddComponent<WorldDrivingController>();controller.Configure(new Rigidbody[2],null);
                var car=new GameObject("Extra vehicle");car.transform.SetParent(root.transform);var body=car.AddComponent<Rigidbody>();
                controller.RegisterVehicle(body);controller.RegisterVehicle(body);Assert.IsTrue(controller.IsRegistered(body));
                var truck=new GameObject("Supply truck");truck.transform.SetParent(root.transform);var truckBody=truck.AddComponent<Rigidbody>();
                controller.AppendSupplyTruck(truckBody);Assert.IsTrue(controller.IsRegistered(truckBody));
            }
            finally{Object.DestroyImmediate(root);}
        }
    }
}
