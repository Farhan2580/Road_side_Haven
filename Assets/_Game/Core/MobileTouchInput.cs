using System.Globalization;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Scripting;

namespace RoadsideHaven
{
    /// <summary>Browser touch controls share the same movement and interaction paths as the PC controls.</summary>
    public sealed class MobileTouchInput : MonoBehaviour
    {
        public const string ObjectName = "Roadside Touch Controls";
        public static bool Enabled { get; private set; }
        public static Vector2 Move { get; private set; }
        public static bool Sprint { get; private set; }
        public static bool Brake { get; private set; }
        public static float Throttle => (gas ? 1f : 0f) - (reverse ? 1f : 0f);
        private static bool gas, reverse, jump;
        private static Vector2 look;
        private static MobileTouchInput instance;
        private MotelGameRoot motel;
        private OwnerSandboxController owner;
        private WorldDrivingController driving;
        private CompactGameHUD hud;
        private OwnerLifeController life;
        private MultiverseController portal;
        private BusinessEmpireController business;
        private ShopHUD shop;
        private Camera lens;
        private float originalFarClip, nextStatus;
        private int originalFrameRate;
        private int previousMode = -1, previousModal = -1, previousSpeed = -1;

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern void RoadsideTouchStatus(int mode, int modal, int speed);
#endif

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { instance = null; Enabled = false; Clear(); }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (instance != null) return;
            instance = new GameObject(ObjectName).AddComponent<MobileTouchInput>();
            DontDestroyOnLoad(instance.gameObject);
        }

        [Preserve] public void EnableTouch(string value)
        {
            if (!Enabled && value == "1") originalFrameRate = Application.targetFrameRate;
            Enabled = value == "1"; Clear(); previousMode = previousModal = previousSpeed = -1;
            if (Enabled) { Cache(); Application.targetFrameRate = 30; }
            else { Application.targetFrameRate = originalFrameRate; if (lens != null && originalFarClip > 0) lens.farClipPlane = originalFarClip; }
        }

        [Preserve] public void SetMove(string value) { if (Enabled && !ModalOpen) Move = Vector2.ClampMagnitude(Parse(value), 1); }
        [Preserve] public void AddLook(string value) { if (Enabled && !ModalOpen) look += Vector2.ClampMagnitude(Parse(value), 120); }
        [Preserve] public void SetHeld(string value)
        {
            if (!Enabled) return;
            int split = value == null ? -1 : value.IndexOf(':');
            if (split < 1) return;
            bool pressed = value.Substring(split + 1) == "1";
            if (ModalOpen && pressed) return;
            switch (value.Substring(0, split))
            {
                case "gas": gas = pressed; break;
                case "reverse": reverse = pressed; break;
                case "brake": Brake = pressed; break;
                case "sprint": Sprint = pressed; break;
            }
        }

        [Preserve] public void Action(string command)
        {
            if (!Enabled) return;
            Cache();
            if (command == "back")
            {
                Clear(); hud?.Close(); life?.ClosePanel(); portal?.ClosePortal(); business?.ClosePanel(); shop?.Close(); return;
            }
            if (ModalOpen) return;
            switch (command)
            {
                case "jump": jump = true; break;
                case "vehicle": Clear(); owner?.VehicleInteraction(); break;
                case "use": owner?.UseNearby(); break;
                case "talk": life?.OpenDialogue(); break;
                case "map": Clear(); owner?.ToggleWalking(); break;
                case "reset": Clear(); driving?.ResetActiveVehicle(); break;
                case "menu": Clear(); hud?.ToggleMenu(); break;
            }
        }

        [Preserve] public void ResetInput(string unused) => Clear();
        public static bool ConsumeJump() { bool value = Enabled && jump; jump = false; return value; }
        public static Vector2 ConsumeLook() { var value = Enabled ? look : Vector2.zero; look = Vector2.zero; return value; }
        public static void Clear() { Move = look = Vector2.zero; gas = reverse = jump = Sprint = Brake = false; }

        private static Vector2 Parse(string value)
        {
            if (string.IsNullOrEmpty(value)) return Vector2.zero;
            int split = value.IndexOf(',');
            if (split < 1 || !float.TryParse(value.Substring(0, split), NumberStyles.Float, CultureInfo.InvariantCulture, out float x) ||
                !float.TryParse(value.Substring(split + 1), NumberStyles.Float, CultureInfo.InvariantCulture, out float y) ||
                float.IsNaN(x) || float.IsInfinity(x) || float.IsNaN(y) || float.IsInfinity(y)) return Vector2.zero;
            return new Vector2(x, y);
        }

        private bool ModalOpen => (hud != null && hud.MenuOpen) || (life != null && life.PanelOpen) ||
            (portal != null && portal.IsPortalPanelOpen) || (business != null && business.PanelOpen) || (shop != null && shop.IsOpen);

        private void Cache()
        {
            if (motel == null) motel = FindFirstObjectByType<MotelGameRoot>();
            if (motel == null) return;
            if (owner == null) owner = motel.GetComponentInChildren<OwnerSandboxController>(true);
            if (driving == null) driving = motel.GetComponentInChildren<WorldDrivingController>(true);
            if (hud == null) hud = motel.GetComponent<CompactGameHUD>();
            if (life == null) life = motel.GetComponentInChildren<OwnerLifeController>(true);
            if (portal == null) portal = motel.GetComponentInChildren<MultiverseController>(true);
            if (business == null) business = motel.GetComponentInChildren<BusinessEmpireController>(true);
            if (shop == null) shop = motel.GetComponentInChildren<ShopHUD>(true);
            if (lens == null && Camera.main != null)
            {
                lens = Camera.main; originalFarClip = lens.farClipPlane;
                if (Enabled) lens.farClipPlane = Mathf.Min(originalFarClip, 600f);
            }
        }

        private void Update()
        {
            if (!Enabled || Time.unscaledTime < nextStatus) return;
            nextStatus = Time.unscaledTime + .15f; Cache();
            bool modal = ModalOpen;
            if (modal) Clear();
            int mode = driving != null && driving.IsDriving ? 2 : owner != null && owner.IsWalking ? 1 : 0;
            int speed = driving == null ? 0 : Mathf.RoundToInt(driving.SpeedKph);
            if (mode == previousMode && (modal ? 1 : 0) == previousModal && speed == previousSpeed) return;
            previousMode = mode; previousModal = modal ? 1 : 0; previousSpeed = speed;
#if UNITY_WEBGL && !UNITY_EDITOR
            RoadsideTouchStatus(mode, previousModal, speed);
#endif
        }

        private void OnApplicationFocus(bool focused) { if (!focused) Clear(); }
        private void OnApplicationPause(bool paused) { if (paused) Clear(); }
        private void OnDestroy() { if (instance == this) { instance = null; Enabled = false; Clear(); } }
    }
}
