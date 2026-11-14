using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
namespace RoadsideHaven.Tests
{
    public sealed class AirportTests
    {
        [Test]
        public void FlightPhasesJoinWithoutTeleporting()
        {
            int[] order={0,1,2,3,4,5,6,7,0};
            for(int i=0;i<order.Length-1;i++)Assert.Less(Vector3.Distance(AirportOperations.JetPosition(order[i],1),AirportOperations.JetPosition(order[i+1],0)),.01f);
            for(int phase=0;phase<8;phase++)for(int i=0;i<=100;i++){var p=AirportOperations.JetPosition(phase,i/100f);Assert.IsFalse(float.IsNaN(p.x)||float.IsNaN(p.y)||float.IsNaN(p.z));Assert.GreaterOrEqual(p.y,0);}
        }
        private static float PhaseProgress(int phase) { float[] values={1f/28,2f/14,3f/22,4f/10,16f/65,3f/8,8f/22,8f/20};return values[phase]; }
        [UnityTest]
        public IEnumerator AirportRunsAllFlightAndBoardingPhases()
        {
            yield return new EnterPlayMode();
            var load=SceneManager.LoadSceneAsync("Assets/_Game/Generated/RoadsideHavenPrototype.unity");while(!load.isDone)yield return null;yield return null;
            var airport=Object.FindFirstObjectByType<AirportOperations>();Assert.IsNotNull(airport);
            var clock=typeof(AirportOperations).GetField("clock",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
            float[] times={1,30,45,68,90,142,155,175};
            for(int phase=0;phase<times.Length;phase++){clock.SetValue(airport,times[phase]);airport.SendMessage("Update");Assert.AreEqual(phase,airport.Phase);
                var jet=airport.transform.Find("HV101 scheduled passenger jet");Assert.IsNotNull(jet);Assert.Less(Vector3.Distance(jet.position,airport.transform.TransformPoint(AirportOperations.JetPosition(phase,PhaseProgress(phase)))),25f);}
            Assert.AreEqual(1800f,airport.transform.Find("Airport runway").lossyScale.x,.01f);
            Assert.AreEqual(1500f,airport.transform.Find("Secondary runway 1500m").lossyScale.x,.01f);
            Assert.Greater(airport.transform.position.x,1000);
            var owner=Object.FindFirstObjectByType<OwnerSandboxController>();Assert.IsNotNull(owner);
            if(!owner.IsWalking)owner.ToggleWalking();airport.VisitAirport();
            Assert.Less(Vector3.Distance(owner.transform.position,airport.transform.TransformPoint(AirportOperations.VisitPoint)),.1f);
            Assert.IsTrue(Physics.Raycast(owner.transform.position+Vector3.up*2,Vector3.down,5),"Terminal arrival needs solid ground");
            LogAssert.NoUnexpectedReceived();
            yield return new ExitPlayMode();
        }
    }
}