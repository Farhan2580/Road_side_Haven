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
            var scene=EditorSceneManager.NewPreviewScene();
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
            finally{EditorSceneManager.ClosePreviewScene(scene);if(groundMaterial!=null)UnityEngine.Object.DestroyImmediate(groundMaterial);}
        }
        [Test] public void HandbrakeTurnDriftsWhileStraightBrakeStops()
        {
            float Run(bool steer)
            {
                var scene=EditorSceneManager.NewPreviewScene();
                try
                {
                    var ground=GameObject.CreatePrimitive(PrimitiveType.Cube);SceneManager.MoveGameObjectToScene(ground,scene);ground.transform.position=new Vector3(0,-.5f,0);ground.transform.localScale=new Vector3(400,1,400);
                    var car=new GameObject("Car");SceneManager.MoveGameObjectToScene(car,scene);car.transform.position=new Vector3(0,.02f,0);
                    var collider=car.AddComponent<BoxCollider>();collider.size=new Vector3(1.6f,1.4f,2.8f);collider.center=new Vector3(0,.7f,0);
                    var body=car.AddComponent<Rigidbody>();body.mass=1100;var motor=car.AddComponent<ArcadeVehicleMotor>();motor.Prepare();
                    Physics.SyncTransforms();var physics=scene.GetPhysicsScene();
                    for(int i=0;i<150;i++){motor.Drive(1,0,false,9,16,4,80,18,6,1,.02f);physics.Simulate(.02f);}
                    float yaw0=body.rotation.eulerAngles.y;
                    for(int i=0;i<40;i++){motor.Drive(0,steer?1:0,true,9,16,4,80,18,6,1,.02f);physics.Simulate(.02f);}
                    return steer?Mathf.Abs(Mathf.DeltaAngle(yaw0,body.rotation.eulerAngles.y)):Vector3.ProjectOnPlane(body.linearVelocity,Vector3.up).magnitude;
                }
                finally{EditorSceneManager.ClosePreviewScene(scene);}
            }
            Assert.That(Run(false),Is.LessThan(1.5f),"Straight braking should bring the car almost to a stop");
            Assert.That(Run(true),Is.GreaterThan(45f),"Handbrake turn should swing the car round");
        }

        // Junction seams and curbs must be ridden over like a short ramp, not with a visible one-frame hop.
        [TestCase(.006f,4f)][TestCase(.07f,4f)][TestCase(.17f,4f)][TestCase(.07f,14f)][TestCase(.17f,14f)][TestCase(.17f,1.5f)]
        public void SmallStepsAreClimbedSmoothlyWithoutHopping(float step,float speed)
        {
            var scene=EditorSceneManager.NewPreviewScene();
            try
            {
                var ground=GameObject.CreatePrimitive(PrimitiveType.Cube);SceneManager.MoveGameObjectToScene(ground,scene);ground.transform.position=new Vector3(0,-.5f,0);ground.transform.localScale=new Vector3(200,1,200);
                var curb=GameObject.CreatePrimitive(PrimitiveType.Cube);SceneManager.MoveGameObjectToScene(curb,scene);curb.transform.position=new Vector3(0,step*.5f,40);curb.transform.localScale=new Vector3(100,step,50);
                var car=new GameObject("Car");SceneManager.MoveGameObjectToScene(car,scene);car.transform.position=new Vector3(0,.02f,8);
                var collider=car.AddComponent<BoxCollider>();collider.size=new Vector3(1.6f,1.4f,2.8f);collider.center=new Vector3(0,.7f,0);
                var body=car.AddComponent<Rigidbody>();body.mass=1100;var motor=car.AddComponent<ArcadeVehicleMotor>();motor.Prepare();
                Physics.SyncTransforms();var physics=scene.GetPhysicsScene();
                for(int i=0;i<20;i++)physics.Simulate(.02f);
                float last=body.position.y,largest=0;
                for(int i=0;i<450&&body.position.z<20;i++){motor.Drive(1,0,false,9,speed,4,80,18,6,1,.02f);physics.Simulate(.02f);largest=Mathf.Max(largest,body.position.y-last);last=body.position.y;}
                Assert.That(body.position.z,Is.GreaterThan(17),"Car could not get over the step");
                Assert.That(body.position.y,Is.EqualTo(step).Within(.04f),"Car is not resting on the raised surface");
                Assert.That(largest,Is.LessThan(Mathf.Max(.035f,speed*.02f*.65f)),"Car hopped instead of riding up the step");
            }
            finally{EditorSceneManager.ClosePreviewScene(scene);}
        }
    }
}
