using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
namespace RoadsideHaven.Tests
{
    public sealed class AsterQuarterTests
    {
        private static bool StaticObstacle(Collider c)=>c.enabled&&c.gameObject.activeInHierarchy&&!c.isTrigger&&c.GetComponentInParent<Rigidbody>()==null&&c.GetComponentInParent<CharacterController>()==null&&c.GetComponentInParent<NavMeshAgent>()==null&&c.bounds.max.y>.3f;
        [UnityTest]
        public IEnumerator MarketRemainsWalkableAndTerraceCanBeReachedAfterStartup()
        {
            yield return new EnterPlayMode();var load=SceneManager.LoadSceneAsync("Assets/_Game/Generated/RoadsideHavenPrototype.unity");while(!load.isDone)yield return null;yield return null;yield return null;
            var motel=Object.FindFirstObjectByType<MotelGameRoot>();Assert.IsNotNull(motel.transform.Find("Aster Quarter Modern Market v1"));
            foreach(var shop in new[]{new Vector2(252,175),new Vector2(276,176),new Vector2(302,175),new Vector2(328,174)})for(float z=shop.y-1;z<=shop.y+6;z+=.5f)foreach(var c in Physics.OverlapCapsule(new Vector3(shop.x,.55f,z),new Vector3(shop.x,1.65f,z),.25f))Assert.IsFalse(StaticObstacle(c),"Store entrance blocked: "+c.name);
            for(float z=97;z<145;z+=1)foreach(var c in Physics.OverlapCapsule(new Vector3(270,.55f,z),new Vector3(270,1.65f,z),.25f))Assert.IsFalse(StaticObstacle(c),"Residential connection blocked: "+c.name);
            var probe=new GameObject("Market terrace movement probe");
            try
            {
                var cc=probe.AddComponent<CharacterController>();cc.height=1.8f;cc.center=Vector3.up*.9f;cc.radius=.25f;cc.skinWidth=.03f;cc.stepOffset=.22f;cc.slopeLimit=45;probe.transform.position=new Vector3(338.8f,.4f,166.3f);Physics.SyncTransforms();
                for(int i=0;i<300&&probe.transform.position.z<184.9f;i++)cc.Move(new Vector3(0,-.1f,.1f));Assert.GreaterOrEqual(probe.transform.position.z,184.8f);
                for(int i=0;i<16;i++)cc.Move(new Vector3(-.1f,-.06f,0));Assert.Greater(probe.transform.position.y,3.9f,"Unable to step from ramp onto terrace");
            }
            finally{Object.DestroyImmediate(probe);}
            LogAssert.NoUnexpectedReceived();yield return new ExitPlayMode();
        }
    }
}