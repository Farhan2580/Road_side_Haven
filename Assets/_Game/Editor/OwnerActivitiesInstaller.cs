using TMPro;
using UnityEditor;
using UnityEngine;

namespace RoadsideHaven.Editor
{
    public static partial class VisualPolishInstaller
    {
        private static void InstallOwnerActivities(Transform motel, IsometricCameraController camera)
        {
            if (motel.Find("Owner Activities") != null) return;
            var driving = motel.GetComponentInChildren<WorldDrivingController>(true);
            var game = motel.GetComponent<MotelGameRoot>();
            if (driving == null || game == null) return;
            var activities = new GameObject("Owner Activities").transform;
            activities.SetParent(motel, false);
            if (!Application.isPlaying) Undo.RegisterCreatedObjectUndo(activities.gameObject, "Add owner gameplay");
            var owner = new GameObject("Motel owner");
            owner.transform.SetParent(activities, false);
            owner.transform.position = new Vector3(-5, .2f, -6);
            var controller = owner.AddComponent<CharacterController>();
            controller.height = 1.8f; controller.radius = .28f; controller.center = Vector3.up * .9f;
            controller.stepOffset = .3f;
            var ownerController = owner.AddComponent<OwnerSandboxController>();
            ownerController.Configure(game, driving, camera);
            driving.ConfigureOwner(ownerController);
            Campus(activities, new Vector3(500, 0, -217), "SUPPLIER - WATER CRATES", 16, 5, mint);
            Box(activities, "Supplier loading marker", new Vector3(500, .09f, -230), new Vector3(3, .04f, 3), gold);
            Box(activities, "Shop delivery marker", new Vector3(7, .09f, -8), new Vector3(2, .04f, 1), gold);
            var sign = new GameObject("Rival Status").AddComponent<TextMeshPro>();
            sign.transform.SetParent(activities, false); sign.transform.position = new Vector3(180, 4, 710);
            sign.transform.rotation = Quaternion.Euler(45, 0, 0);
            sign.font = TMP_Settings.GetFontAsset(); sign.fontSize = 8;
            sign.alignment = TextAlignmentOptions.Center;
            sign.text = "RIVAL MOTEL\nOPEN";
            sign.rectTransform.sizeDelta = new Vector2(18, 5);
        }
    }
}
