using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
namespace RoadsideHaven.Tests
{
    public sealed class PocketParkTests
    {
        [UnityTest]
        public IEnumerator ParkCitizensMoveAndFountainAnimatesWithoutAddingLights()
        {
            yield return new EnterPlayMode();
            var load=SceneManager.LoadSceneAsync("Assets/_Game/Generated/RoadsideHavenPrototype.unity");while(!load.isDone)yield return null;yield return null;yield return null;
            var park=Object.FindFirstObjectByType<PocketParkLife>();Assert.IsNotNull(park);Assert.AreEqual(3,park.CitizenCount);
            var people=park.GetComponentsInChildren<TownCitizenAgent>();Assert.AreEqual(3,people.Length);var positions=new Vector3[people.Length];for(int i=0;i<people.Length;i++)positions[i]=people[i].transform.position;
            Renderer water=null;foreach(var r in park.GetComponentsInChildren<MeshRenderer>())if(r.name=="Park Fountain water"){water=r;break;}Assert.IsNotNull(water);var offset=water.sharedMaterial.GetTextureOffset("_BaseMap");
            float deadline=Time.realtimeSinceStartup+4;while(Time.realtimeSinceStartup<deadline)yield return null;
            for(int i=0;i<people.Length;i++){Assert.Greater(Vector3.Distance(positions[i],people[i].transform.position),1f,"Park visitor stuck");Assert.Greater(people[i].transform.position.y,-.2f,"Visitor fell through the park");}
            Assert.AreNotEqual(offset,water.sharedMaterial.GetTextureOffset("_BaseMap"));
            int lights=0;foreach(var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))if(l.name.StartsWith("Pooled night light "))lights++;Assert.LessOrEqual(lights,20);
            LogAssert.NoUnexpectedReceived();yield return new ExitPlayMode();
        }
    }
}