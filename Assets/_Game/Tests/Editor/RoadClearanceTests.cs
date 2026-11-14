using NUnit.Framework;
using UnityEngine;
namespace RoadsideHaven.Tests
{
    public sealed class RoadClearanceTests
    {
        private static GameObject Box(Transform root,string name,Vector3 p,Vector3 size)
        {var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(root,false);go.transform.position=p;go.transform.localScale=size;return go;}
        [Test]
        public void BuildingRoofAndInteractionPointMoveTogetherAndStayClear()
        {
            var root=new GameObject("Clearance test");
            try
            {
                Box(root.transform,"Connected road",Vector3.zero,new Vector3(100,.12f,12));
                var shop=new GameObject("Shop assembly");shop.transform.SetParent(root.transform,false);
                var wall=Box(shop.transform,"Shop building",Vector3.up*1.5f,new Vector3(4,3,4));
                var roof=Box(shop.transform,"Roof",Vector3.up*3.1f,new Vector3(4.5f,.2f,4.5f));
                var interaction=new GameObject("Door interaction");interaction.transform.SetParent(shop.transform,false);interaction.transform.localPosition=Vector3.back*2;
                var oldDoor=interaction.transform.position;var oldRoof=roof.transform.position;
                Assert.Greater(RoadClearance.Resolve(root.transform),0);
                Assert.AreEqual(roof.transform.position-oldRoof,interaction.transform.position-oldDoor);
                var b=wall.GetComponent<Renderer>().bounds;Assert.IsTrue(b.min.z>6||b.max.z< -6||b.min.x>50||b.max.x< -50);
                var after=shop.transform.position;Assert.AreEqual(0,RoadClearance.Resolve(root.transform));Assert.AreEqual(after,shop.transform.position);
            }
            finally{Object.DestroyImmediate(root);}
        }
        [Test]
        public void VehiclesAndCrossingPaintAreNotRelocated()
        {
            var root=new GameObject("Clearance test");
            try
            {
                Box(root.transform,"Connected road",Vector3.zero,new Vector3(100,.12f,12));
                var vehicle=Box(root.transform,"Vehicle",Vector3.up,new Vector3(2,1,4));vehicle.AddComponent<Rigidbody>().isKinematic=true;
                var paint=Box(root.transform,"Crossing stripe",new Vector3(0,.07f,0),new Vector3(4,.001f,1));
                Assert.AreEqual(0,RoadClearance.Resolve(root.transform));Assert.AreEqual(Vector3.up,vehicle.transform.position);Assert.AreEqual(new Vector3(0,.07f,0),paint.transform.position);
            }
            finally{Object.DestroyImmediate(root);}
        }
        [Test]
        public void UngroupedTreeTrunkAndCrownRemainAligned()
        {
            var root=new GameObject("Clearance test");
            try
            {
                Box(root.transform,"Connected road",Vector3.zero,new Vector3(100,.12f,12));
                var trunk=Box(root.transform,"Tree trunk",Vector3.up,new Vector3(.3f,2,.3f));
                var crown=Box(root.transform,"Tree crown",Vector3.up*3,new Vector3(3,2,3));
                RoadClearance.Resolve(root.transform);
                Assert.AreEqual(trunk.transform.position.x,crown.transform.position.x);Assert.AreEqual(trunk.transform.position.z,crown.transform.position.z);
                Assert.That(Mathf.Abs(trunk.transform.position.z),Is.GreaterThan(7));
            }
            finally{Object.DestroyImmediate(root);}
        }
    }
}