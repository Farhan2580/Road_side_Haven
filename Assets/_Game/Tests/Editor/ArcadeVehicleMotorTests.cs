using System;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace RoadsideHaven.Tests
{
    public sealed class ArcadeVehicleMotorTests
    {
        [TestCase("Road",false,false,false)]
        [TestCase("Grass playground",false,false,false)]
        [TestCase("High prefab pivot",true,false,false)]
        [TestCase("Reverse",false,true,false)]
        [TestCase("Solid wall",false,false,true)]
        [TestCase("Small curb",false,false,false,.12f)]
        [TestCase("Playground edge",false,false,false,.35f)]
        [TestCase("Reverse curb",false,true,false,.2f)]
        [TestCase("Too high barrier",false,false,true,.8f)]
        [TestCase("Long sports car curb",false,false,false,.35f,true)]
        public void MotorDrivesOnSolidSurfacesAndRespectsWalls(string label,bool highPivot,bool reverse,bool wall,float step=0,bool modern=false)
        {
            var scene=SceneManager.CreateScene("Vehicle regression "+Guid.NewGuid(),new CreateSceneParameters(LocalPhysicsMode.Physics3D));
            PhysicsMaterial groundMaterial=null;
            try
            {
                var ground=GameObject.CreatePrimitive(PrimitiveType.Cube);SceneManager.MoveGameObjectToScene(ground,scene);
                ground.name=label;ground.transform.position=new Vector3(0,-.5f,0);ground.transform.localScale=new Vector3(200,1,200);
                groundMaterial=new PhysicsMaterial("Ground contact"){staticFriction=.9f,dynamicFriction=.9f};ground.GetComponent<Collider>().sharedMaterial=groundMaterial;
                var car=new GameObject("Car");SceneManager.MoveGameObjectToScene(car,scene);car.transform.position=new Vector3(0,highPivot?-1.8f:.2f,0);
                var collider=car.AddComponent<BoxCollider>();collider.size=modern?new Vector3(2.18f,1.44f,4.5f):new Vector3(1.6f,1.4f,2.8f);collider.center=new Vector3(0,highPivot?2.7f:.7f,0);
                var body=car.AddComponent<Rigidbody>();body.mass=1100;var motor=car.AddComponent<ArcadeVehicleMotor>();motor.Prepare();
                if(wall&&step==0){var block=GameObject.CreatePrimitive(PrimitiveType.Cube);SceneManager.MoveGameObjectToScene(block,scene);block.transform.position=new Vector3(0,2,6);block.transform.localScale=new Vector3(20,4,.2f);}
                if(step>0){var curb=GameObject.CreatePrimitive(PrimitiveType.Cube);SceneManager.MoveGameObjectToScene(curb,scene);curb.transform.position=new Vector3(0,step*.5f,reverse?-(modern?30:27):(modern?30:27));curb.transform.localScale=new Vector3(100,step,50);}
                Physics.SyncTransforms();var physics=scene.GetPhysicsScene();
                for(int i=0;i<250;i++){motor.Drive(reverse?-1:1,0,false,9,13,4,80,18,6,1,.02f);physics.Simulate(.02f);}
                Assert.IsTrue(motor.Grounded);
                if(wall){Assert.That(body.position.z,Is.LessThan(step>0?1f:4.75f));Assert.That(body.position.z,Is.GreaterThan(.1f));}
                else if(reverse)Assert.That(body.position.z,Is.LessThan(-5));
                else Assert.That(body.position.z,Is.GreaterThan(5));
            }
            finally{EditorSceneManager.CloseScene(scene,true);if(groundMaterial!=null)UnityEngine.Object.DestroyImmediate(groundMaterial);}
        }
    }
}
