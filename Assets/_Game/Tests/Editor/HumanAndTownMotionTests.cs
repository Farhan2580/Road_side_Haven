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
        [Test] public void PeopleHaveChildrenAndVariedModernLooks()
        {
            var tops=new System.Collections.Generic.HashSet<HumanTop>();var bottoms=new System.Collections.Generic.HashSet<HumanBottom>();var hair=new System.Collections.Generic.HashSet<int>();int caps=0,bags=0,phones=0,women=0;
            for(int i=0;i<300;i++){var p=HumanIdentity.Create(i);tops.Add(p.Top);bottoms.Add(p.Bottom);hair.Add(p.HairStyle);if(p.Cap)caps++;if(p.Backpack||p.Handbag)bags++;if(p.Phone)phones++;if(p.Feminine)women++;}
            Assert.GreaterOrEqual(tops.Count,5);Assert.GreaterOrEqual(bottoms.Count,5);Assert.GreaterOrEqual(hair.Count,7);Assert.Greater(caps,5);Assert.Greater(bags,30);Assert.Greater(phones,40);Assert.Greater(women,100);
            for(int i=0;i<40;i++){var child=HumanIdentity.Create(i,HumanRole.Civilian,HumanAge.Child);Assert.IsTrue(child.IsChild);Assert.Less(child.Height,1.45f);Assert.Greater(child.HeadScale,1.15f);Assert.IsFalse(child.Beard||child.Stubble||child.Glasses);}
            var root=new GameObject("Test child");try
            {
                var kid=HumanVisual.Create(root.transform,HumanIdentity.Create(7,HumanRole.Civilian,HumanAge.Child),false);var adult=HumanVisual.Create(root.transform,HumanIdentity.Create(7,HumanRole.Civilian,HumanAge.Adult),false);
                float kidTop=kid.Renderer.sharedMesh.bounds.max.y*kid.transform.localScale.y,adultTop=adult.Renderer.sharedMesh.bounds.max.y*adult.transform.localScale.y;
                Assert.Less(kidTop,adultTop-.1f,"Child is not smaller than an adult");
            }finally{Object.DestroyImmediate(root);}
        }
        private static TownCitizenAgent Person(string name,Vector3 at,HumanAge? age=null)
        {
            var go=new GameObject(name);var agent=go.AddComponent<TownCitizenAgent>();if(age.HasValue)agent.InitializeAs(age.Value);else agent.Initialize(null,null,false);agent.Warp(at);return agent;
        }
        private static string SociableName(string prefix){for(int i=0;;i++){var n=prefix+i;if(HumanIdentity.Create(HumanIdentity.StableSeed(n)).Sociability>.7f)return n;}}
        private static IEnumerator Seconds(float s){float u=Time.time+s;while(Time.time<u)yield return null;}
        [UnityTest] public IEnumerator CitizensSolveBlockagesDodgeCarsChatAndWalkAsFamilies()
        {
            yield return new EnterPlayMode();Application.runInBackground=true;
            var o=new Vector3(15000,0,15000);var ground=GameObject.CreatePrimitive(PrimitiveType.Plane);ground.transform.position=o;ground.transform.localScale=Vector3.one*12;
            // A wall right across the path: the walker must find its own way round and still arrive.
            var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.transform.position=o+new Vector3(0,1,6);wall.transform.localScale=new Vector3(9,2,.4f);
            var walker=Person("Problem solver",o);walker.Walk(new[]{o+new Vector3(0,0,14)});
            // A parent and child.
            var parent=Person("Family parent",o+new Vector3(20,0,0),HumanAge.Adult);var child=Person("Family child",o+new Vector3(20,0,0),HumanAge.Child);child.Follow(parent);parent.Walk(new[]{o+new Vector3(20,0,10)});
            // Two sociable neighbours standing together.
            var a=Person(SociableName("Chatty A"),o+new Vector3(-20,0,0));var b=Person(SociableName("Chatty B"),o+new Vector3(-20,0,1.6f));
            // A pedestrian in the path of a speeding car.
            var target=Person("Alert pedestrian",o+new Vector3(-40,0,0));
            Physics.SyncTransforms();yield return Seconds(1);
            var car=GameObject.CreatePrimitive(PrimitiveType.Cube);car.name="Speeding car";car.transform.position=o+new Vector3(-40,.8f,-22);car.transform.localScale=new Vector3(2,1.4f,4.4f);
            var body=car.AddComponent<Rigidbody>();body.useGravity=false;body.constraints=RigidbodyConstraints.FreezeRotation|RigidbodyConstraints.FreezePositionY;body.linearVelocity=Vector3.forward*12;
            float startX=target.transform.position.x;
            yield return Seconds(1.8f);
            Assert.Greater(target.DangersAvoided,0,"Pedestrian ignored the approaching car");
            Assert.Greater(Mathf.Abs(target.transform.position.x-startX),.8f,"Pedestrian did not step out of the way");
            Object.Destroy(car);
            Assert.IsNotNull(a.Partner,"Sociable neighbours did not start a conversation");Assert.AreEqual(CitizenMood.Chatting,a.Mood);Assert.AreEqual(b,a.Partner);
            yield return Seconds(19);
            Assert.Greater(walker.ProblemsSolved,0,"Walker never tried to solve the blockage");
            Assert.Less(Vector3.Distance(walker.transform.position,o+new Vector3(0,0,14)),1.2f,"Walker did not get round the wall");
            Assert.Less(Vector3.Distance(parent.transform.position,o+new Vector3(20,0,10)),1.2f,"Parent did not arrive");
            Assert.Less(Vector3.Distance(child.transform.position,parent.transform.position),1.6f,"Child did not stay with the parent");
            Assert.AreEqual(CitizenMood.Following,child.Mood);Assert.IsTrue(child.Human.Identity.IsChild);
            LogAssert.NoUnexpectedReceived();
            foreach(var go in new[]{ground,wall,walker.gameObject,parent.gameObject,child.gameObject,a.gameObject,b.gameObject,target.gameObject})Object.Destroy(go);
            yield return new ExitPlayMode();
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
