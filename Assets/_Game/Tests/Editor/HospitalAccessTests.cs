using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
namespace RoadsideHaven.Tests
{
    public sealed class HospitalAccessTests
    {
        [UnityTest] public IEnumerator F11LoadsAMissingHospitalFromMapModeAndFacesItsEntrance()
        {
            yield return new EnterPlayMode();Application.runInBackground=true;
            var load=SceneManager.LoadSceneAsync("Assets/_Game/Generated/RoadsideHavenPrototype.unity");while(!load.isDone)yield return null;
            float until=Time.realtimeSinceStartup+2;while(Time.realtimeSinceStartup<until)yield return null;
            var motel=Object.FindFirstObjectByType<MotelGameRoot>();var owner=motel.GetComponentInChildren<OwnerSandboxController>(true);
            Assert.IsNotNull(motel.GetComponent<HospitalTravel>(),"The shortcut must exist independently of the hospital object");
            Object.Destroy(motel.GetComponentInChildren<HospitalCampusLife>().gameObject);yield return null;yield return null;
            if(owner.IsWalking)owner.ToggleWalking();Assert.IsFalse(owner.IsWalking);
            var keyboard=InputSystem.AddDevice<Keyboard>();
            var focusBehavior=InputSystem.settings.editorInputBehaviorInPlayMode;var background=InputSystem.settings.backgroundBehavior;
            try{
                // Batch-mode tests have no focused Game window; route the virtual keyboard to the player.
                InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;InputSystem.EnableDevice(keyboard);
                InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.F11));yield return null;
                until=Time.realtimeSinceStartup+8;while(Time.realtimeSinceStartup<until&&(!owner.IsWalking||Vector3.Distance(owner.transform.position,HospitalCampusLayout.Entrance)>1))yield return null;
                InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return null;
                Assert.IsTrue(owner.IsWalking,motel.GetComponent<HospitalTravel>().Status);Assert.Less(Vector3.Distance(owner.transform.position,HospitalCampusLayout.Entrance),1);
                Assert.AreEqual(1,motel.GetComponentsInChildren<HospitalCampusLife>().Length);Assert.Greater(Vector3.Dot(Camera.main.transform.forward,Vector3.forward),.9f,"The hospital is behind the arrival camera");
                HospitalTravel.Visit(motel);HospitalTravel.Visit(motel);yield return null;yield return null;Assert.AreEqual(1,motel.GetComponentsInChildren<HospitalCampusLife>().Length);
            }finally{InputSystem.RemoveDevice(keyboard);InputSystem.settings.editorInputBehaviorInPlayMode=focusBehavior;InputSystem.settings.backgroundBehavior=background;}
            yield return new ExitPlayMode();
        }
        [UnityTest] public IEnumerator MenuTravelSafelyStopsDrivingAndWorksFromASavedHospital()
        {
            yield return new EnterPlayMode();Application.runInBackground=true;
            var load=SceneManager.LoadSceneAsync("Assets/_Game/Generated/RoadsideHavenPrototype.unity");while(!load.isDone)yield return null;
            float until=Time.realtimeSinceStartup+2;while(Time.realtimeSinceStartup<until)yield return null;
            var motel=Object.FindFirstObjectByType<MotelGameRoot>();var owner=motel.GetComponentInChildren<OwnerSandboxController>(true);var driving=motel.GetComponentInChildren<WorldDrivingController>(true);
            var car=new GameObject("Hospital travel vehicle probe");car.transform.SetParent(motel.transform);car.transform.position=new Vector3(850,.2f,-700);var body=car.AddComponent<Rigidbody>();body.useGravity=false;var box=car.AddComponent<BoxCollider>();box.center=Vector3.up*.75f;box.size=new Vector3(2,1.5f,4);car.AddComponent<EnterableVehicle>();driving.RegisterVehicle(body);
            owner.ExitVehicle(car.transform.position+Vector3.right*2.6f);Physics.SyncTransforms();Assert.IsTrue(driving.TryEnterOwner(owner.transform.position,5));body.linearVelocity=Vector3.forward*20;
            var menu=motel.GetComponent<CompactGameHUD>();menu.ToggleMenu();HospitalTravel.Visit(motel);
            until=Time.realtimeSinceStartup+8;while(Time.realtimeSinceStartup<until&&(driving.IsDriving||Vector3.Distance(owner.transform.position,HospitalCampusLayout.Entrance)>1))yield return null;
            Assert.IsFalse(driving.IsDriving);Assert.IsTrue(owner.IsWalking);Assert.IsTrue(body.isKinematic);Assert.IsFalse(menu.MenuOpen);Assert.Less(Vector3.Distance(owner.transform.position,HospitalCampusLayout.Entrance),1);Assert.AreEqual(1,motel.GetComponentsInChildren<HospitalCampusLife>().Length);
            Assert.IsTrue(menu.GetComponentsInChildren<Transform>(true).Any(t=>t.name=="HOSPITAL (F11)")||motel.GetComponentsInChildren<Transform>(true).Any(t=>t.name=="HOSPITAL (F11)"));
            yield return new ExitPlayMode();
        }
    }
}
