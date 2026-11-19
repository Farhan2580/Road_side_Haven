using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
namespace RoadsideHaven.Tests
{
    public sealed class UltraGraphicsTests
    {
        private static IEnumerator Wait(float seconds){float until=Time.realtimeSinceStartup+seconds;while(Time.realtimeSinceStartup<until)yield return null;}

        [UnityTest] public IEnumerator UltraLayerInstallsGrowsGrassOffRoadsAndRainWetsTheStreets()
        {
            yield return new EnterPlayMode();Application.runInBackground=true;
            var loading=SceneManager.LoadSceneAsync("Assets/_Game/Generated/RoadsideHavenPrototype.unity");while(!loading.isDone)yield return null;
            yield return Wait(3);
            var motel=Object.FindFirstObjectByType<MotelGameRoot>();
            Assert.IsNotNull(motel.GetComponent<UltraGraphics>(),"Ultra graphics layer was not installed at runtime");
            var weather=motel.GetComponent<WeatherSystem>();var grass=motel.GetComponent<GrassField>();
            Assert.IsNotNull(weather);Assert.IsNotNull(grass);Assert.IsTrue(grass.Ready,"Grass field did not initialise (shader missing?)");
            Assert.AreEqual(AmbientMode.Trilight,RenderSettings.ambientMode,"Ambient light is still flat");

            // Grass: dense near the start, and never on public roads, inside buildings or in water.
            var owner=motel.GetComponentInChildren<OwnerSandboxController>(true);
            grass.GenerateAround(owner.transform.position,70);
            Assert.Greater(grass.InstanceCount,800,"Too little grass around the start area");
            var roads=motel.GetComponentsInChildren<MeshRenderer>().Where(r=>r.gameObject.activeInHierarchy&&TownPopulationController.RoadName(r.name)).Select(r=>Rect.MinMaxRect(r.bounds.min.x+.3f,r.bounds.min.z+.3f,r.bounds.max.x-.3f,r.bounds.max.z-.3f)).ToList();
            var buildings=new List<Rect>();
            foreach(var c in Object.FindObjectsByType<Collider>(FindObjectsSortMode.None))
            {
                if(!c.enabled||c.isTrigger||c.attachedRigidbody!=null)continue;var b=c.bounds;
                if(b.size.y>2.5f&&b.size.x>3&&b.size.z>3&&b.min.y<.5f)buildings.Add(Rect.MinMaxRect(b.min.x+.3f,b.min.z+.3f,b.max.x-.3f,b.max.z-.3f));
            }
            var water=Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None).Where(r=>{var n=r.name.ToLowerInvariant();return n.Contains("river")||n.Contains("water")||n.Contains("lake")||n.Contains("pond");}).Select(r=>Rect.MinMaxRect(r.bounds.min.x,r.bounds.min.z,r.bounds.max.x,r.bounds.max.z)).ToList();
            foreach(var p in grass.InstancePositions())
            {
                var xz=new Vector2(p.x,p.z);
                Assert.IsFalse(roads.Any(r=>r.Contains(xz)),"Grass grows on a public road at "+p);
                Assert.IsFalse(buildings.Any(r=>r.Contains(xz)),"Grass grows inside a building at "+p);
                Assert.IsFalse(water.Any(r=>r.Contains(xz)),"Grass grows in water at "+p);
            }

            // Weather: a storm brings rain particles, wind and wet, glossy streets; clearing stops the rain.
            weather.AutomaticChanges=false;weather.SetWeather(WeatherKind.Storm,true);
            yield return Wait(2);
            Assert.Greater(weather.RainParticles.particleCount,100,"Rain is not falling during a storm");
            Assert.Greater(weather.Wind,1f);Assert.Greater(weather.WetSurfaceCount,20,"No street surfaces were registered for wetness");
            int glossy=0;for(int i=0;i<weather.WetSurfaceCount;i++)if(weather.SmoothnessOf(i)>.7f)glossy++;
            Assert.Greater(glossy,weather.WetSurfaceCount/2,"Streets did not become wet in the rain");
            weather.SetWeather(WeatherKind.Clear,true);
            yield return Wait(.5f);
            Assert.AreEqual(0,weather.Rain,1e-3,"Rain continued after the weather cleared");
            LogAssert.NoUnexpectedReceived();
            yield return new ExitPlayMode();
        }
    }
}
