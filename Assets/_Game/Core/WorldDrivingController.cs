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
        private const float MaximumForwardSpeed = 170f / 3.6f;
        [SerializeField, Min(1)] private float reverseSpeed = 4f;
        [SerializeField, Min(1)] private float steering = 80f;
        [SerializeField, Min(1)] private float braking = 18f;
        [SerializeField, Min(0)] private float lateralGrip = 6f;
        [SerializeField, Min(1)] private float washSeconds = 5f;
        [SerializeField] private Vector3 washCenter = new Vector3(55, 0, -24);
        [SerializeField] private Vector2 washHalfSize = new Vector2(3, 4);
        private OwnerSandboxController owner;
        private OwnerLifeController life;
        public void ConfigureLife(OwnerLifeController controller) => life = controller;
        public bool IsDriving => active >= 0;
        public float SpeedKph => IsDriving && vehicles[active]!=null ? Vector3.ProjectOnPlane(vehicles[active].linearVelocity,Vector3.up).magnitude*3.6f : 0;
        public Transform ActiveVehicleTransform => active>=0 && vehicles!=null && active<vehicles.Length && vehicles[active]!=null ? vehicles[active].transform : null;
        public float ActiveVehicleCameraDistance { get { var box=ActiveVehicleTransform==null?null:ActiveVehicleTransform.GetComponent<BoxCollider>(); return box==null?9:Mathf.Max(9,box.size.z*ActiveVehicleTransform.lossyScale.z*.5f+6); } }
        public float ActiveVehicleCameraHeight { get { var box=ActiveVehicleTransform==null?null:ActiveVehicleTransform.GetComponent<BoxCollider>(); return box==null?1.7f:Mathf.Max(1.7f,box.size.y*ActiveVehicleTransform.lossyScale.y*.75f); } }
        public float ActiveVehicleYaw => ActiveVehicleTransform==null?0:ActiveVehicleTransform.eulerAngles.y+(ActiveVehicleTransform.GetComponent<EnterableVehicle>()?.ForwardSign<0?180:0);
        public void ConfigureOwner(OwnerSandboxController player) => owner = player;
        private int active = -1;
        private float throttle, turn, washProgress;
        private bool brake;
        private bool[] clean;
        public string Status => status;
        private string status = "Explore the village and city. Choose CAR or CNG.";
        private PcThirdPersonCamera pcCamera;
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
            if(GetComponentInParent<MotelGameRoot>()?.GetComponentInChildren<BusinessEmpireController>(true)?.PanelOpen ?? false){throttle=turn=0;brake=true;return;}
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.tabKey.wasPressedThisFrame)
                SelectVehicle(active + 1 >= (vehicles == null ? 0 : vehicles.Length) ? -1 : active + 1);
            if (keyboard != null && keyboard.rKey.wasPressedThisFrame) ResetActiveVehicle();
            if ((GetComponentInParent<MotelGameRoot>()?.GetComponent<CompactGameHUD>()?.MenuOpen ?? false) || (life != null && life.PanelOpen)) { throttle=turn=0; brake=true; return; }
            if(pcCamera==null && Camera.main!=null)pcCamera=Camera.main.GetComponent<PcThirdPersonCamera>();
            if(pcCamera!=null&&pcCamera.InputBlocked){throttle=turn=0;brake=true;return;}
            throttle = 0;
            turn = 0;
            brake = false;
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
            var trafficDriver=vehicle.GetComponent<TownVehicleAgent>();if(trafficDriver!=null&&trafficDriver.enabled)trafficDriver.enabled=false;
            var access=vehicle.GetComponent<EnterableVehicle>();
            float scale=(access==null?1:access.SpeedScale)*(life==null?1:life.VehicleMultiplier(active));
            var motor=vehicle.GetComponent<ArcadeVehicleMotor>();if(motor==null){motor=vehicle.gameObject.AddComponent<ArcadeVehicleMotor>();motor.Prepare();}
            motor.Drive(throttle,turn,brake,acceleration*scale,Mathf.Min(MaximumForwardSpeed,MaximumForwardSpeed*scale),reverseSpeed,steering,braking,lateralGrip,access==null?1:access.ForwardSign,Time.fixedDeltaTime);
            var horizontalVelocity=Vector3.ProjectOnPlane(vehicle.linearVelocity,Vector3.up);
            if(horizontalVelocity.sqrMagnitude>MaximumForwardSpeed*MaximumForwardSpeed)vehicle.linearVelocity=horizontalVelocity.normalized*MaximumForwardSpeed+Vector3.up*vehicle.linearVelocity.y;
            var velocity=vehicle.linearVelocity;
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
            { status = "Walk beside your vehicle and press E to enter."; return; }
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
            if (motelHud != null && motelHudWasVisible) motelHud.gameObject.SetActive(true);
            washProgress = 0;
            throttle=turn=0; brake=false;
            if (active >= 0 && vehicles[active] != null)
            {
                if (owner != null) owner.EnteredVehicle();
                if (mapCamera != null) mapCamera.SetModalInputBlocked(true);
                var access=vehicles[active].GetComponent<EnterableVehicle>();
                if(access!=null)access.TakeControl();else{vehicles[active].isKinematic=false;vehicles[active].useGravity=true;}
                status = "WASD / arrows: drive | SPACE: brake | TAB: switch | R: reset";
            }
            else
            {
                if (mapCamera != null) mapCamera.FollowWorldPoint(Vector3.zero);
                status = "Motel management view. Choose CAR or CNG to explore.";
            }
        }

        public void ResetActiveVehicle()
        {
            if(active<0 || vehicles==null)return;
            if(active==2 && owner!=null && owner.HasSupplyCargo){status="Unload cargo before resetting the supply truck.";return;}
            vehicles[active].position=startPositions[active];vehicles[active].rotation=startRotations[active];
            vehicles[active].linearVelocity=Vector3.zero;vehicles[active].angularVelocity=Vector3.zero;
        }
        private void OnDisable()
        {
            if (active >= 0) SelectVehicle(-1);
        }

        public bool SupplyTruckNear(Vector3 point, float radius)
        {
            if (vehicles == null || vehicles.Length < 3 || vehicles[2] == null || life != null && !life.Owns(2)) return false;
            return Vector2.Distance(new Vector2(point.x,point.z),new Vector2(vehicles[2].position.x,vehicles[2].position.z)) <= radius;
        }
        public bool IsRegistered(Rigidbody body)=>vehicles!=null&&System.Array.IndexOf(vehicles,body)>=0;
        public void RegisterVehicle(Rigidbody body)
        {
            if(body==null||IsRegistered(body))return;
            if(vehicles==null)vehicles=new Rigidbody[3];
            if(vehicles.Length<3)System.Array.Resize(ref vehicles,3);
            int index=vehicles.Length;System.Array.Resize(ref vehicles,index+1);vehicles[index]=body;
            System.Array.Resize(ref clean,index+1);System.Array.Resize(ref startPositions,index+1);System.Array.Resize(ref startRotations,index+1);
            startPositions[index]=body.position;startRotations[index]=body.rotation;
        }
        public void AppendSupplyTruck(Rigidbody truck)
        {
            if(vehicles==null||truck==null)return;
            if(vehicles.Length<3)System.Array.Resize(ref vehicles,3);
            if(vehicles[2]!=null)return;
            vehicles[2]=truck;System.Array.Resize(ref clean,vehicles.Length);
            if(startPositions!=null){System.Array.Resize(ref startPositions,vehicles.Length);startPositions[2]=truck.position;}
            if(startRotations!=null){System.Array.Resize(ref startRotations,vehicles.Length);startRotations[2]=truck.rotation;}
        }
        public static float EntryDistance(Rigidbody vehicle,Vector3 ownerPosition)
        {
            Vector3 from=ownerPosition+Vector3.up*.8f;float nearest=float.MaxValue;
            foreach(var collider in vehicle.GetComponentsInChildren<Collider>())
            {
                if(!collider.enabled||collider.isTrigger)continue;
                nearest=Mathf.Min(nearest,Vector3.Distance(from,collider.ClosestPoint(from)));
            }
            return nearest;
        }
        public bool TryEnterOwner(Vector3 position, float radius)
        {
            if (vehicles == null || IsDriving) return false;
            int nearest=-1;float distance=radius;
            for(int i=0;i<vehicles.Length;i++)
            {
                if(vehicles[i]==null||!vehicles[i].gameObject.activeInHierarchy)continue;
                float candidate=EntryDistance(vehicles[i],position);if(candidate>distance)continue;
                Vector3 from=position+Vector3.up;Vector3 to=vehicles[i].worldCenterOfMass;
                if(Physics.Linecast(from,to,out var hit,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore)&&!hit.transform.IsChildOf(vehicles[i].transform))continue;
                nearest=i;distance=candidate;
            }
            if(nearest>=0){SelectVehicle(nearest);return IsDriving;}
            return false;
        }

        public bool TryExitOwner()
        {
            if (!IsDriving || owner == null || vehicles[active] == null) return false;
            var vehicle = vehicles[active];
            if (vehicle.linearVelocity.magnitude > 1) return false;
            for (int side = -1; side <= 1; side += 2)
            {
                float halfWidth=0;
                foreach(var collider in vehicle.GetComponentsInChildren<Collider>())if(collider.enabled&&!collider.isTrigger)
                {
                    var box=collider as BoxCollider;Vector3 right=vehicle.transform.right;
                    if(box!=null)
                    {
                        float extent=Mathf.Abs(Vector3.Dot(box.transform.TransformVector(Vector3.right*box.size.x*.5f),right))+
                            Mathf.Abs(Vector3.Dot(box.transform.TransformVector(Vector3.up*box.size.y*.5f),right))+
                            Mathf.Abs(Vector3.Dot(box.transform.TransformVector(Vector3.forward*box.size.z*.5f),right));
                        halfWidth=Mathf.Max(halfWidth,Mathf.Abs(Vector3.Dot(box.transform.TransformPoint(box.center)-vehicle.position,right))+extent);
                    }
                    else halfWidth=Mathf.Max(halfWidth,Mathf.Abs(Vector3.Dot(collider.bounds.center-vehicle.position,right))+Mathf.Abs(right.x)*collider.bounds.extents.x+Mathf.Abs(right.z)*collider.bounds.extents.z);
                }
                var point = vehicle.worldCenterOfMass + vehicle.transform.right * side * Mathf.Max(2.5f,halfWidth+.8f);
                point.y=vehicle.position.y;
                if (!Physics.Raycast(point + Vector3.up * 3, Vector3.down, out var ground, 8, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) continue;
                point = ground.point + Vector3.up * .1f;
                if (Physics.CheckCapsule(point + Vector3.up * .4f, point + Vector3.up * 1.5f, .32f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) continue;
                vehicle.linearVelocity = Vector3.zero; vehicle.angularVelocity = Vector3.zero; vehicle.isKinematic = true;
                active = -1;
                var spray = transform.Find("Car Wash Spray"); if (spray != null) spray.gameObject.SetActive(false);
                if (motelHud != null) motelHud.gameObject.SetActive(motelHudWasVisible);
                owner.ExitVehicle(point);
                vehicle.GetComponent<EnterableVehicle>()?.ReleaseControl();
                status = "On foot. E: enter nearby vehicle | G: work / interact";
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
