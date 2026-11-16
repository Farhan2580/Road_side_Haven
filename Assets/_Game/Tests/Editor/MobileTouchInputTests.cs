using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.InputSystem;

namespace RoadsideHaven.Tests
{
    public sealed class MobileTouchInputTests
    {
        [Test] public void TouchAxesAreClampedAndCancelClearsEveryHeldInput()
        {
            var go = new GameObject("Touch input test"); var input = go.AddComponent<MobileTouchInput>();
            try
            {
                input.EnableTouch("1"); input.SetMove("3,4"); Assert.That(MobileTouchInput.Move.magnitude, Is.EqualTo(1).Within(.001f));
                input.SetHeld("gas:1"); input.SetHeld("brake:1"); input.SetHeld("sprint:1"); input.AddLook("12,-8"); input.Action("jump");
                input.ResetInput(""); Assert.AreEqual(Vector2.zero, MobileTouchInput.Move); Assert.AreEqual(Vector2.zero, MobileTouchInput.ConsumeLook());
                Assert.AreEqual(0, MobileTouchInput.Throttle); Assert.IsFalse(MobileTouchInput.Brake); Assert.IsFalse(MobileTouchInput.Sprint); Assert.IsFalse(MobileTouchInput.ConsumeJump());
                input.SetMove("NaN,2"); Assert.AreEqual(Vector2.zero, MobileTouchInput.Move);
                input.SetMove("Infinity,0"); Assert.AreEqual(Vector2.zero, MobileTouchInput.Move);
            }
            finally { input.EnableTouch("0"); Object.DestroyImmediate(go); }
        }

        [UnityTest] public IEnumerator TouchControlsMoveJumpDriveBrakeAndExitThroughExistingPhysics()
        {
            yield return new EnterPlayMode();
            Application.runInBackground = true; float until;
            var oldKeyboard = Keyboard.current;
            if(oldKeyboard != null) InputSystem.DisableDevice(oldKeyboard);
            var testKeyboard = InputSystem.AddDevice<Keyboard>(); testKeyboard.MakeCurrent();
            var load = SceneManager.LoadSceneAsync("Assets/_Game/Generated/RoadsideHavenPrototype.unity"); while (!load.isDone) yield return null;
            yield return null; yield return null; yield return null;
            var owner = Object.FindFirstObjectByType<OwnerSandboxController>();
            var driving = Object.FindFirstObjectByType<WorldDrivingController>();
            var input = Object.FindFirstObjectByType<MobileTouchInput>();
            // A headless Editor has no application focus. Remove only its view component for this physics integration test.
            var pc = Object.FindFirstObjectByType<PcThirdPersonCamera>(); if (pc != null) Object.DestroyImmediate(pc);
            yield return null;
            owner.Configure(owner.GetComponentInParent<MotelGameRoot>(), driving, null);
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube); ground.transform.position = new Vector3(12000, 1.9f, 12000); ground.transform.localScale = new Vector3(80, 1, 80);
            owner.ExitVehicle(new Vector3(12000, 2.5f, 12000)); input.EnableTouch("1"); Physics.SyncTransforms();
            until=Time.realtimeSinceStartup+.35f; while(Time.realtimeSinceStartup<until) yield return null;
            Vector3 before = owner.transform.position; input.SetMove("0,1");
            Assert.Greater(MobileTouchInput.Move.y, .9f, "Touch movement was rejected; menu=" + owner.GetComponentInParent<MotelGameRoot>().GetComponent<CompactGameHUD>().MenuOpen);
            until=Time.realtimeSinceStartup+.7f; while(Time.realtimeSinceStartup<until) yield return null;
            input.SetMove("0,0");
            Assert.Greater(Vector3.Distance(before, owner.transform.position), 1.4f, "Joystick did not move the owner");
            until=Time.realtimeSinceStartup+.2f; while(Time.realtimeSinceStartup<until) yield return null; float height = owner.transform.position.y; input.Action("jump"); until=Time.realtimeSinceStartup+.15f; while(Time.realtimeSinceStartup<until) yield return null;
            Assert.Greater(owner.transform.position.y, height + .2f, "Touch jump did not use the character physics"); until=Time.realtimeSinceStartup+.7f; while(Time.realtimeSinceStartup<until) yield return null;
            var car = new GameObject("Touch drive integration car"); car.transform.position = new Vector3(12008, 2.5f, 12000);
            var box = car.AddComponent<BoxCollider>(); box.size = new Vector3(1.8f, 1.2f, 4); box.center = Vector3.up * .7f;
            var body = car.AddComponent<Rigidbody>(); body.isKinematic = true; car.AddComponent<EnterableVehicle>().Configure(1); driving.RegisterVehicle(body);
            owner.ExitVehicle(car.transform.position - Vector3.right * 2.5f); Physics.SyncTransforms(); input.Action("vehicle"); Assert.IsTrue(driving.IsDriving, "Touch enter did not find the nearby car");
            Vector3 carBefore = body.position; input.SetHeld("gas:1"); input.SetMove(".3,0"); until=Time.realtimeSinceStartup+1f; while(Time.realtimeSinceStartup<until) yield return null;
            Assert.Greater(driving.SpeedKph, 3, "Touch gas did not accelerate"); Assert.Greater(Vector3.Distance(carBefore, body.position), 1, "Vehicle did not move");
            input.SetHeld("gas:0"); input.SetMove("0,0"); input.SetHeld("brake:1"); until=Time.realtimeSinceStartup+1.1f; while(Time.realtimeSinceStartup<until) yield return null;
            Assert.Less(driving.SpeedKph, 3.6f, "Touch brake did not stop the vehicle"); input.Action("vehicle"); Assert.IsFalse(driving.IsDriving); Assert.IsTrue(owner.IsWalking);
            input.EnableTouch("0"); Object.Destroy(car); Object.Destroy(ground); InputSystem.RemoveDevice(testKeyboard); if(oldKeyboard != null) InputSystem.EnableDevice(oldKeyboard); LogAssert.NoUnexpectedReceived();
            yield return new ExitPlayMode();
        }
    }
}
