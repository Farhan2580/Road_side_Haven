using UnityEngine;
using UnityEngine.InputSystem;

namespace RoadsideHaven
{
    /// <summary>Local motel owner: collision-based walking, nearby vehicle entry and location-gated jobs.</summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class OwnerSandboxController : MonoBehaviour
    {
        [Header("Services")]
        [SerializeField] private MotelGameRoot motel;
        [SerializeField] private ShopController shop;
        [SerializeField] private WorldDrivingController driving;
        [SerializeField] private IsometricCameraController mapCamera;
        [Header("Movement")]
        [SerializeField, Min(1)] private float walkSpeed = 4f;
        [SerializeField, Min(1)] private float runSpeed = 7f;
        [SerializeField, Min(1)] private float movementAcceleration = 14f;
        private Vector3 planarVelocity;
        [SerializeField, Min(1)] private float interactionRadius = 4f;
        [Header("Supplier job")]
        [SerializeField] private Vector3 supplier = new Vector3(500, 0, -230);
        [SerializeField] private Vector3 delivery = new Vector3(7, 0, -8);
        [SerializeField] private string cargoProduct = "water";
        [SerializeField, Min(1)] private int cargoQuantity = 4;
        [SerializeField, Min(1)] private int cargoCost = 8;
        [SerializeField, Min(0)] private int jobReward = 20;
        [Header("Fictional rival interaction")]
        [SerializeField] private Vector3 rival = new Vector3(180, 0, 710);
        [SerializeField, Min(1)] private float prankCooldown = 120f;
        private CharacterController character;
        private Transform visual;
        private Transform[] limbs;
        private float fallSpeed, gait, prankReadyAt, rivalClosedUntil;
        private bool walking, cargo;
        private string message = "P: walk as owner | F: enter/exit nearby vehicle | G: interact";
        private Material skin, clothes;
        private PcThirdPersonCamera pcCamera;
        [SerializeField,Min(.1f)] private float jumpHeight=1.1f;
        public string Status => message;
        public void ToggleWalking() { if (!driving.IsDriving) SetWalking(!walking); }
        public void UseNearby() { if (walking) Interact(); }
        public bool IsWalking => walking;
        public bool HasSupplyCargo => cargo;

        public void Configure(MotelGameRoot root, WorldDrivingController vehicles, IsometricCameraController camera)
        { motel = root; shop = root.GetComponent<ShopController>(); driving = vehicles; mapCamera = camera; }

        private void Start()
        {
            character = GetComponent<CharacterController>();
            character.detectCollisions=true;character.enableOverlapRecovery=true;character.skinWidth=.035f;
            character.stepOffset=.22f;character.slopeLimit=45f;character.minMoveDistance=0;
            if(Vector2.Distance(new Vector2(transform.position.x,transform.position.z),new Vector2(-5,-6))<.5f)
            { character.enabled=false;transform.position=new Vector3(-1.8f,.2f,-5.8f);character.enabled=true; }
            visual = new GameObject("Owner visual").transform;
            visual.SetParent(transform, false);
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader != null)
            {
                skin = new Material(shader) { color = new Color(.68f, .45f, .30f) };
                clothes = new Material(shader) { color = new Color(.16f, .27f, .48f) };
                Part(visual, "Torso", new Vector3(0, 1.05f, 0), new Vector3(.48f, .6f, .3f), clothes);
                Part(visual, "Head", new Vector3(0, 1.55f, 0), new Vector3(.32f, .36f, .32f), skin);
                limbs = new Transform[4];
                for (int i = 0; i < 4; i++)
                {
                    limbs[i] = new GameObject("Owner limb").transform;
                    limbs[i].SetParent(visual, false);
                    limbs[i].localPosition = new Vector3((i % 2 == 0 ? -1 : 1) * (i < 2 ? .30f : .13f), i < 2 ? 1.25f : .75f, 0);
                    Part(limbs[i], "Limb mesh", Vector3.down * .3f, new Vector3(.15f, .6f, .15f), i < 2 ? skin : clothes);
                }
            }
            if (mapCamera != null) { pcCamera=mapCamera.GetComponent<PcThirdPersonCamera>(); if(pcCamera==null)pcCamera=mapCamera.gameObject.AddComponent<PcThirdPersonCamera>(); pcCamera.Configure(motel,this,driving,mapCamera); }
            SetWalking(true);
        }

        private static void Part(Transform parent, string name, Vector3 position, Vector3 scale, Material material)
        {
            var part = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            part.name = name; part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localScale = new Vector3(scale.x, scale.y * .5f, scale.z);
            part.GetComponent<Renderer>().sharedMaterial = material;
            var collider = part.GetComponent<Collider>(); collider.enabled = false; Destroy(collider);
        }

        private void Update()
        {
            var shopView = motel == null ? null : motel.GetComponentInChildren<ShopHUD>(true);
            if (shopView != null && shopView.IsOpen) { planarVelocity=Vector3.zero; return; }
            if (walking && mapCamera != null) mapCamera.SetModalInputBlocked(true);
            var portalView = motel == null ? null : motel.GetComponentInChildren<MultiverseController>(true);
            if (portalView != null && portalView.IsPortalPanelOpen) { planarVelocity=Vector3.zero; return; }
            var lifePanel = motel.GetComponentInChildren<OwnerLifeController>(true);
            if (lifePanel != null && lifePanel.PanelOpen) { planarVelocity=Vector3.zero; return; }
            if (motel.GetComponent<CompactGameHUD>()?.MenuOpen ?? false) { planarVelocity=Vector3.zero; return; }
            if(motel.GetComponentInChildren<BusinessEmpireController>(true)?.PanelOpen ?? false){planarVelocity=Vector3.zero;return;}
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if ((keyboard.pKey.wasPressedThisFrame || keyboard.mKey.wasPressedThisFrame) && !driving.IsDriving) SetWalking(!walking);
                if (keyboard.fKey.wasPressedThisFrame) VehicleInteraction();
                if (keyboard.gKey.wasPressedThisFrame && walking) Interact();
            }
            if (!walking) return;
            if(pcCamera!=null&&pcCamera.InputBlocked)return;
            float x = 0;
            float z = 0;
            if (keyboard != null)
            {
                x += (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed ? 1 : 0) - (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed ? 1 : 0);
                z += (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed ? 1 : 0) - (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed ? 1 : 0);
            }
            Vector3 forward = mapCamera == null ? Vector3.forward : Vector3.ProjectOnPlane(mapCamera.transform.forward, Vector3.up).normalized;
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            Vector3 move = Vector3.ClampMagnitude(right * x + forward * z, 1);
            float speed = keyboard != null && keyboard.leftShiftKey.isPressed ? runSpeed : walkSpeed;
            if(character.isGrounded && fallSpeed<0)fallSpeed=-2f;
            if(character.isGrounded && keyboard!=null && keyboard.spaceKey.wasPressedThisFrame)fallSpeed=Mathf.Sqrt(40f*jumpHeight);
            fallSpeed=Mathf.Max(-35f,fallSpeed-20f*Time.deltaTime);
            planarVelocity=Vector3.MoveTowards(planarVelocity,move*speed,movementAcceleration*Time.deltaTime);
            character.Move((planarVelocity + Vector3.up * fallSpeed) * Time.deltaTime);
            if (move.sqrMagnitude > .01f) visual.rotation = Quaternion.Slerp(visual.rotation, Quaternion.LookRotation(move), Time.deltaTime * 12);
            gait += move.magnitude * speed * Time.deltaTime * 5;
            if (limbs != null)
                for (int i = 0; i < limbs.Length; i++) limbs[i].localRotation = Quaternion.Euler(Mathf.Sin(gait + (i == 0 || i == 3 ? 0 : Mathf.PI)) * 25 * move.magnitude, 0, 0);
            if (mapCamera != null) mapCamera.FollowWorldPoint(transform.position);
        }

        public bool CanEnter(Rigidbody vehicle) => walking && vehicle != null && Near(vehicle.position, interactionRadius + 1);
        public void EnteredVehicle() { SetWalking(false); }
        public void ExitVehicle(Vector3 position)
        {
            character.enabled = false; transform.position = position;
            SetWalking(true);
        }

        private void SetWalking(bool enabled)
        {
            var hud = motel == null ? null : motel.GetComponentInChildren<ShopHUD>(true);
            if (enabled && hud != null && hud.IsOpen) { message = "Close shop checkout before walking."; return; }
            walking = enabled;
            if (character != null) character.enabled = enabled;
            if (visual != null) visual.gameObject.SetActive(enabled);
            if (mapCamera != null) mapCamera.SetModalInputBlocked(enabled);
            fallSpeed=0;planarVelocity=Vector3.zero;
        }

        private bool Near(Vector3 point, float radius = -1)
        { var offset = transform.position - point; offset.y = 0; return offset.sqrMagnitude <= Mathf.Pow(radius < 0 ? interactionRadius : radius, 2); }

        public void VehicleInteraction()
        {
            if (driving.IsDriving)
            {
                if (!driving.TryExitOwner()) message = "Stop the vehicle; its side must be clear before exiting.";
            }
            else if (!driving.TryEnterOwner(transform.position, interactionRadius + 1)) message = "Walk beside your car or CNG, then press F.";
        }

        private void Interact()
        {
            if (motel == null || shop == null) return;
            var dimension = motel.GetComponentInChildren<MultiverseController>(true);
            if (dimension != null && dimension.Current != UniverseId.Normal && (Near(supplier) || Near(delivery)))
            { message = "Return to Normal for physical shop supplies and delivery jobs. Use the portal market here."; return; }
            if (Near(supplier))
            {
                if (!driving.SupplyTruckNear(supplier, 12)) { message = "Bring your owned supply truck to the supplier loading bay."; return; }
                if (cargo) { message = "Cargo already loaded. Return to your shop."; return; }
                if (!motel.TryDebit(cargoCost)) { message = "Not enough coins for supplies."; return; }
                cargo = true;
                message = $"Loaded {cargoQuantity} water for {cargoCost} coins. Deliver to your shop for +{jobReward}.";
                return;
            }
            if (Near(delivery))
            {
                if (!cargo) { var view = motel.GetComponentInChildren<ShopHUD>(true); if (view != null) view.Open(); message = "Shop checkout opened. Supplier is at (500, -230)."; return; }
                if (!driving.SupplyTruckNear(delivery, 16)) { message = "Park the loaded supply truck beside your shop before unloading."; return; }
                if (!shop.TryAcceptOwnerDelivery(cargoProduct, cargoQuantity)) { message = "Shelf full. Cargo retained; sell some stock first."; return; }
                cargo = false;
                motel.Credit(jobReward);
                motel.GetComponentInChildren<OwnerLifeController>(true)?.Delivered();
                message = $"Delivery complete. +{jobReward} coins!";
                return;
            }
            if (Near(rival))
            {
                if (Time.time < prankReadyAt) { message = "Rival sign prank is on cooldown."; return; }
                prankReadyAt = Time.time + prankCooldown; rivalClosedUntil = Time.time + 45;
                motel.PauseRivalAdvertising(45);
                var sign = motel.transform.Find("Owner Activities/Rival Status");
                if (sign != null) sign.GetComponent<TMPro.TMP_Text>().text = "RIVAL MOTEL\nSIGN OUT OF ORDER";
                message = "Fictional sign prank: rival advertising disabled for 45 seconds.";
                return;
            }
            if (motel.TryOwnerCleanNearest(transform.position, interactionRadius)) { message = "Room cleaning started."; return; }
            if (motel.TryOwnerCheckIn(transform.position, interactionRadius)) { message = "Guest assigned to an available room."; return; }
            message = "Go near reception, a dirty room, supplier, shop or rival sign; then press G.";
        }

        private void LateUpdate()
        {
            if (rivalClosedUntil > 0 && Time.time >= rivalClosedUntil)
            {
                rivalClosedUntil = 0;
                var sign = motel.transform.Find("Owner Activities/Rival Status");
                if (sign != null) sign.GetComponent<TMPro.TMP_Text>().text = "RIVAL MOTEL\nOPEN";
            }
        }

        private void OnDestroy()
        { if (skin != null) Destroy(skin); if (clothes != null) Destroy(clothes); }
    }
}
