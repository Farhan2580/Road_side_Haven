using UnityEngine;
using UnityEngine.InputSystem;

namespace RoadsideHaven
{
    /// <summary>Local exploration controls for the prototype's car and CNG. Owns driving input and camera follow.</summary>
    public sealed class WorldDrivingController : MonoBehaviour
    {
        [Header("Vehicles and view")]
        [SerializeField] private Rigidbody[] vehicles;
        [SerializeField] private IsometricCameraController mapCamera;
        [Header("Arcade handling")]
        [SerializeField, Min(1)] private float acceleration = 9f;
        [SerializeField, Min(1)] private float topSpeed = 13f;
        [SerializeField, Min(1)] private float reverseSpeed = 4f;
        [SerializeField, Min(1)] private float steering = 80f;
        [SerializeField, Min(1)] private float braking = 18f;
        [SerializeField, Min(0)] private float lateralGrip = 6f;
        [SerializeField, Min(1)] private float washSeconds = 5f;
        [SerializeField] private Vector3 washCenter = new Vector3(55, 0, -24);
        [SerializeField] private Vector2 washHalfSize = new Vector2(3, 4);
        private OwnerSandboxController owner;
        public bool IsDriving => active >= 0;
        public void ConfigureOwner(OwnerSandboxController player) => owner = player;
        private int active = -1;
        private float throttle, turn, washProgress;
        private bool brake;
        private bool[] clean;
        private string status = "Explore the village and city. Choose CAR or CNG.";
        private bool forwardHeld, reverseHeld, leftHeld, rightHeld, brakeHeld;
        private ShopHUD shopHud;
        private MotelHUD motelHud;
        private bool motelHudWasVisible;
        private Vector3[] startPositions;
        private Quaternion[] startRotations;

        public void Configure(Rigidbody[] fleet, IsometricCameraController camera)
        {
            vehicles = fleet;
            mapCamera = camera;
            clean = new bool[fleet.Length];
        }

        private void Start()
        {
            owner = GetComponentInParent<MotelGameRoot>()?.GetComponentInChildren<OwnerSandboxController>(true);
            shopHud = GetComponentInParent<MotelGameRoot>()?.GetComponentInChildren<ShopHUD>(true);
            motelHud = GetComponentInParent<MotelGameRoot>()?.GetComponentInChildren<MotelHUD>(true);
            motelHudWasVisible = motelHud != null && motelHud.gameObject.activeSelf;
            if (vehicles == null) return;
            clean = new bool[vehicles.Length];
            startPositions = new Vector3[vehicles.Length];
            startRotations = new Quaternion[vehicles.Length];
            for (int i = 0; i < vehicles.Length; i++)
                if (vehicles[i] != null)
                {
                    startPositions[i] = vehicles[i].position;
                    startRotations[i] = vehicles[i].rotation;
                    PaintCleanliness(i, false);
                }
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.tabKey.wasPressedThisFrame)
                SelectVehicle(active + 1 >= (vehicles == null ? 0 : vehicles.Length) ? -1 : active + 1);
            throttle = (forwardHeld ? 1 : 0) - (reverseHeld ? 1 : 0);
            turn = (rightHeld ? 1 : 0) - (leftHeld ? 1 : 0);
            brake = brakeHeld;
            if (active < 0 || keyboard == null) return;
            throttle += (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed ? 1 : 0) -
                (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed ? 1 : 0);
            turn += (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed ? 1 : 0) -
                (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed ? 1 : 0);
            brake |= keyboard.spaceKey.isPressed;
        }

        private void FixedUpdate()
        {
            if (vehicles == null || active < 0 || active >= vehicles.Length || vehicles[active] == null) return;
            var vehicle = vehicles[active];
            var velocity = vehicle.linearVelocity;
            Vector3 forward = vehicle.transform.forward;
            float speed = Vector3.Dot(velocity, forward);
            bool grounded = Physics.Raycast(vehicle.position + Vector3.up * .15f, Vector3.down, .65f,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            if (grounded)
            {
                float input = Mathf.Clamp(throttle, -1, 1);
                if ((input > 0 && speed < topSpeed) || (input < 0 && speed > -reverseSpeed))
                    vehicle.AddForce(forward * input * acceleration, ForceMode.Acceleration);
                Vector3 sideways = vehicle.transform.right * Vector3.Dot(velocity, vehicle.transform.right);
                vehicle.AddForce(-sideways * lateralGrip, ForceMode.Acceleration);
                if (brake || Mathf.Abs(input) < .01f)
                    vehicle.AddForce(-new Vector3(velocity.x, 0, velocity.z) * (brake ? braking : 1f), ForceMode.Acceleration);
                float rotation = Mathf.Clamp(turn, -1, 1) * steering * Mathf.Clamp(speed / 3f, -1, 1) * Time.fixedDeltaTime;
                vehicle.MoveRotation(vehicle.rotation * Quaternion.Euler(0, rotation, 0));
            }
            bool insideWash = Mathf.Abs(vehicle.position.x - washCenter.x) < washHalfSize.x &&
                              Mathf.Abs(vehicle.position.z - washCenter.z) < washHalfSize.y;
            if (insideWash && velocity.magnitude < .5f && !clean[active])
            {
                washProgress += Time.fixedDeltaTime;
                status = $"Washing... {Mathf.CeilToInt(Mathf.Max(0, washSeconds - washProgress))}s";
                if (washProgress >= washSeconds)
                {
                    clean[active] = true;
                    PaintCleanliness(active, true);
                    status = "Wash complete! Your vehicle is clean.";
                    var spray = transform.Find("Car Wash Spray");
                    if (spray != null) spray.gameObject.SetActive(false);
                }
                else
                {
                    var spray = transform.Find("Car Wash Spray");
                    if (spray != null) spray.gameObject.SetActive(true);
                }
            }
            else
            {
                if (washProgress > 0) status = "Wash interrupted. Stop inside the wash bay to try again.";
                washProgress = 0;
                var spray = transform.Find("Car Wash Spray");
                if (spray != null) spray.gameObject.SetActive(false);
            }
        }

        private void LateUpdate()
        {
            if (active >= 0 && vehicles != null && vehicles[active] != null && mapCamera != null)
                mapCamera.FollowWorldPoint(vehicles[active].position);
        }

        private void SelectVehicle(int index)
        {
            if (vehicles == null) return;
            if (owner != null && index >= 0 && (index >= vehicles.Length || !owner.CanEnter(vehicles[index])))
            { status = "Walk beside your vehicle and press F to enter."; return; }
            if (owner != null && active >= 0 && index < 0) { TryExitOwner(); return; }
            if (index >= 0 && shopHud != null && shopHud.IsOpen)
            { status = "Close the shop panel before driving."; return; }
            if (active >= 0 && active < vehicles.Length && vehicles[active] != null)
            {
                vehicles[active].linearVelocity = Vector3.zero;
                vehicles[active].angularVelocity = Vector3.zero;
                vehicles[active].isKinematic = true;
            }
            active = index >= 0 && index < vehicles.Length ? index : -1;
            if (mapCamera != null) mapCamera.SetModalInputBlocked(active >= 0);
            if (motelHud != null) motelHud.gameObject.SetActive(active < 0 && motelHudWasVisible);
            washProgress = 0;
            forwardHeld = reverseHeld = leftHeld = rightHeld = brakeHeld = false;
            if (active >= 0 && vehicles[active] != null)
            {
                if (owner != null) owner.EnteredVehicle();
                if (mapCamera != null) mapCamera.SetModalInputBlocked(true);
                vehicles[active].isKinematic = false;
                status = "WASD / arrows: drive | SPACE: brake | TAB: switch | R: reset";
            }
            else
            {
                if (mapCamera != null) mapCamera.FollowWorldPoint(Vector3.zero);
                status = "Motel management view. Choose CAR or CNG to explore.";
            }
        }

        private void OnGUI()
        {
            // Prototype controls also accept touch. Coordinates use a fixed logical landscape width.
            var previous = GUI.matrix;
            float scale = Mathf.Max(.5f, Screen.width / 1280f);
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));
            float height = Screen.height / scale;
            GUI.Box(new Rect(18, 82, 670, 90), string.Empty);
            GUI.Label(new Rect(28, 87, 650, 30), status);
            if (GUI.Button(new Rect(28, 120, 105, 40), "DRIVE CAR")) SelectVehicle(0);
            if (GUI.Button(new Rect(143, 120, 105, 40), "DRIVE CNG")) SelectVehicle(1);
            if (GUI.Button(new Rect(258, 120, 105, 40), "MOTEL")) SelectVehicle(-1);
            if (active >= 0)
            {
                forwardHeld = GUI.RepeatButton(new Rect(1120, height - 240, 120, 65), "ACCEL");
                reverseHeld = GUI.RepeatButton(new Rect(1120, height - 165, 120, 65), "REVERSE");
                leftHeld = GUI.RepeatButton(new Rect(25, height - 180, 100, 70), "LEFT");
                rightHeld = GUI.RepeatButton(new Rect(135, height - 180, 100, 70), "RIGHT");
                brakeHeld = GUI.RepeatButton(new Rect(990, height - 165, 110, 65), "BRAKE");
                if (GUI.Button(new Rect(378, 120, 105, 40), "RESET") ||
                    (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.R))
                {
                    vehicles[active].position = startPositions[active];
                    vehicles[active].rotation = startRotations[active];
                    vehicles[active].linearVelocity = Vector3.zero;
                    vehicles[active].angularVelocity = Vector3.zero;
                }
            }
            GUI.matrix = previous;
        }

        private void OnDisable()
        {
            if (active >= 0) SelectVehicle(-1);
        }

        public bool TryEnterOwner(Vector3 position, float radius)
        {
            if (vehicles == null || IsDriving) return false;
            for (int i = 0; i < vehicles.Length; i++)
                if (vehicles[i] != null && Vector3.Distance(position, vehicles[i].position) <= radius)
                { SelectVehicle(i); return IsDriving; }
            return false;
        }

        public bool TryExitOwner()
        {
            if (!IsDriving || owner == null || vehicles[active] == null) return false;
            var vehicle = vehicles[active];
            if (vehicle.linearVelocity.magnitude > 1) return false;
            for (int side = -1; side <= 1; side += 2)
            {
                var point = vehicle.position + vehicle.transform.right * side * 2.5f;
                if (!Physics.Raycast(point + Vector3.up * 3, Vector3.down, out var ground, 8, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) continue;
                point = ground.point + Vector3.up * .1f;
                if (Physics.CheckCapsule(point + Vector3.up * .4f, point + Vector3.up * 1.5f, .32f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) continue;
                vehicle.linearVelocity = Vector3.zero; vehicle.angularVelocity = Vector3.zero; vehicle.isKinematic = true;
                active = -1;
                var spray = transform.Find("Car Wash Spray"); if (spray != null) spray.gameObject.SetActive(false);
                if (motelHud != null) motelHud.gameObject.SetActive(motelHudWasVisible);
                owner.ExitVehicle(point);
                status = "On foot. F: enter nearby vehicle | G: work / interact";
                return true;
            }
            return false;
        }
        private void PaintCleanliness(int index, bool washed)
        {
            foreach (var renderer in vehicles[index].GetComponentsInChildren<Renderer>())
            {
                if (!renderer.name.Contains("body") && renderer.name != "Car roof") continue;
                if (renderer.sharedMaterial == null) continue;
                var block = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(block);
                var original = renderer.sharedMaterial.color;
                block.SetColor("_BaseColor", washed ? original : Color.Lerp(original, new Color(.28f, .22f, .14f), .6f));
                renderer.SetPropertyBlock(block);
            }
        }
    }
}
