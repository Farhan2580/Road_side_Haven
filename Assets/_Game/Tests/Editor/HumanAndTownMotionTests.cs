using NUnit.Framework;
using System.Collections;
using UnityEngine.TestTools;
using UnityEngine;
namespace RoadsideHaven.Tests
{
    public sealed class HumanAndTownMotionTests
    {
        [Test] public void IdentitiesAreRepeatableVariedAndDoNotUseGlobalRandom()
        {
            Random.InitState(91);var state=Random.state;int seniors=0,young=0;float low=10,high=0;var faces=new System.Collections.Generic.HashSet<string>();
            for(int i=0;i<192;i++){var a=HumanIdentity.Create(i);var b=HumanIdentity.Create(i);Assert.AreEqual(a.Skin,b.Skin);Assert.AreEqual(a.Height,b.Height);Assert.AreEqual(a.FaceWidth,b.FaceWidth);if(a.Age==HumanAge.Senior)seniors++;if(a.Age==HumanAge.YoungAdult)young++;low=Mathf.Min(low,a.Pace);high=Mathf.Max(high,a.Pace);faces.Add(a.FaceWidth.ToString("F4")+a.NoseLength.ToString("F4"));}
            Assert.Greater(seniors,20);Assert.Greater(young,20);Assert.Greater(faces.Count,185);Assert.Greater(high-low,.5f);Assert.AreEqual(state,Random.state);
        }
        [Test] public void UniformsAndStudentAgeAreRoleAppropriate()
        {
            var doctor=HumanIdentity.Create(10,HumanRole.Doctor);var police=HumanIdentity.Create(10,HumanRole.Police);var student=HumanIdentity.Create(10,HumanRole.Student);
            Assert.Greater(doctor.Shirt.r,.85f);Assert.Less(police.Shirt.r,.1f);Assert.IsTrue(student.Age==HumanAge.Teen||student.Age==HumanAge.YoungAdult);Assert.Less(student.Trousers.r,.15f);
        }
        [Test] public void ApproachingPeoplePassOnTheirOwnRightAndCrowdedPeopleSeparate()
        {
            Vector3 a=TownMotion.PersonalSpace(Vector3.zero,Vector3.forward*1.5f,Vector3.forward*1.6f,Vector3.back*1.5f,0);
            Vector3 b=TownMotion.PersonalSpace(Vector3.forward*1.6f,Vector3.back*1.5f,Vector3.zero,Vector3.forward*1.5f,1);
            Assert.Greater(a.x,0);Assert.Less(b.x,0);
            Assert.Less(TownMotion.PersonalSpace(Vector3.zero,Vector3.forward,Vector3.right*.3f,Vector3.zero,0).x,0);
            Assert.LessOrEqual(TownMotion.PersonalSpace(Vector3.zero,Vector3.forward,Vector3.zero,Vector3.zero,1).magnitude,1.2f);
        }
        [Test] public void TrafficAnticipatesQueueWithoutAnInstantStopAtTheSensorRange()
        {
            Assert.AreEqual(0,TownMotion.FollowingSpeed(.6f,0,12));Assert.Greater(TownMotion.FollowingSpeed(8,0,12),0);Assert.Less(TownMotion.FollowingSpeed(8,0,12),12);
            Assert.Greater(TownMotion.FollowingSpeed(18,0,12),TownMotion.FollowingSpeed(8,0,12));Assert.Greater(TownMotion.FollowingSpeed(8,5,12),TownMotion.FollowingSpeed(8,0,12));
        }
        [UnityTest] public IEnumerator TrafficMovesBrakesBeforeAnObstacleAndResumesWhenClear()
        {
            yield return new EnterPlayMode();Application.runInBackground=true;
            var car=new GameObject("Traffic physics test");car.transform.position=new Vector3(14000,.09f,14000);
            var box=car.AddComponent<BoxCollider>();box.size=new Vector3(2,1.4f,4);box.center=Vector3.up*.7f;var body=car.AddComponent<Rigidbody>();var driver=car.AddComponent<TownVehicleAgent>();
            var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.transform.position=new Vector3(14000,1,14012);wall.transform.localScale=new Vector3(5,2,2);
            Physics.SyncTransforms();driver.SetRoute(new[]{new Vector3(14000,.09f,14030)},9);
            float until=Time.realtimeSinceStartup+5;while(Time.realtimeSinceStartup<until)yield return null;
            Assert.Greater(body.position.z,14001,"Traffic never moved");Assert.Less(body.position.z,14009,"Traffic crossed the obstacle");float stopped=body.position.z;
            Object.Destroy(wall);yield return null;until=Time.realtimeSinceStartup+1.5f;while(Time.realtimeSinceStartup<until)yield return null;
            Assert.Greater(body.position.z,stopped+.5f,"Traffic did not resume after the obstruction cleared");Object.Destroy(car);LogAssert.NoUnexpectedReceived();yield return new ExitPlayMode();
        }
        [Test] public void HumanMeshHasOneRendererSkinBonesFacesClothesAndNoDecorativeColliders()
        {
            var root=new GameObject("Test human");try
            {
                var visual=HumanVisual.Create(root.transform,2580,HumanRole.Owner,false);var mesh=visual.Renderer.sharedMesh;
                Assert.AreEqual(1,root.GetComponentsInChildren<Renderer>().Length);Assert.AreEqual(0,root.GetComponentsInChildren<Collider>().Length);
                Assert.AreEqual(16,visual.Renderer.bones.Length);Assert.AreEqual(mesh.vertexCount,mesh.boneWeights.Length);Assert.AreEqual(mesh.vertexCount,mesh.colors.Length);Assert.Greater(mesh.vertexCount,1000);Assert.AreEqual(1,mesh.blendShapeCount);
                Assert.IsNotNull(visual.Renderer.sharedMaterial.shader);foreach(var v in mesh.vertices){Assert.IsFalse(float.IsNaN(v.x));Assert.Less(v.magnitude,3);}
                Assert.Greater(TownMotion.PeopleBudget(false),96);Assert.Greater(TownMotion.PeopleBudget(true),96);Assert.Less(TownMotion.PeopleBudget(true),TownMotion.PeopleBudget(false));
            }finally{Object.DestroyImmediate(root);}
        }
    }
}
