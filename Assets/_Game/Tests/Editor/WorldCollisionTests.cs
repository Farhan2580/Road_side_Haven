using NUnit.Framework;
using UnityEngine;
namespace RoadsideHaven.Tests
{
    public sealed class WorldCollisionTests
    {
        [Test] public void RepairedBuildingBlocksControllerAndRepairIsIdempotent()
        {
            var root=new GameObject("Collision test");root.transform.position=new Vector3(10000,100,10000);
            try
            {
                var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.name="Neighborhood building";wall.transform.SetParent(root.transform,false);
                wall.transform.localPosition=new Vector3(2,1,0);wall.transform.localScale=new Vector3(1,2,4);
                Object.DestroyImmediate(wall.GetComponent<Collider>());
                Assert.AreEqual(1,WorldCollisionSetup.Apply(root.transform,false));
                Assert.AreEqual(0,WorldCollisionSetup.Apply(root.transform,false));
                Assert.AreEqual(1,wall.GetComponents<Collider>().Length);
                var player=new GameObject("Test player");player.transform.SetParent(root.transform,false);
                var controller=player.AddComponent<CharacterController>();controller.radius=.3f;controller.height=1.8f;controller.center=Vector3.up*.9f;
                Physics.SyncTransforms();var flags=controller.Move(Vector3.right*5);
                Assert.Less(player.transform.localPosition.x,1.3f);
                Assert.IsTrue((flags&CollisionFlags.Sides)!=0);
            }
            finally{Object.DestroyImmediate(root);}
        }
        [Test] public void DoorwayRemainsPassableAndDecalsGetNoCollider()
        {
            var root=new GameObject("Door test");root.transform.position=new Vector3(10000,200,10000);
            try
            {
                foreach(float z in new[]{-1.25f,1.25f})
                {var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.name="Door side wall";wall.transform.SetParent(root.transform,false);wall.transform.localPosition=new Vector3(2,1,z);wall.transform.localScale=new Vector3(1,2,1);}
                var decal=GameObject.CreatePrimitive(PrimitiveType.Cube);decal.name="Road dash";decal.transform.SetParent(root.transform,false);Object.DestroyImmediate(decal.GetComponent<Collider>());
                WorldCollisionSetup.Apply(root.transform,false);Assert.IsNull(decal.GetComponent<Collider>());
                var player=new GameObject("Player");player.transform.SetParent(root.transform,false);var controller=player.AddComponent<CharacterController>();controller.radius=.3f;controller.height=1.8f;controller.center=Vector3.up*.9f;
                Physics.SyncTransforms();controller.Move(Vector3.right*5);Assert.Greater(player.transform.localPosition.x,4.5f);
            }
            finally{Object.DestroyImmediate(root);}
        }
    }
}
