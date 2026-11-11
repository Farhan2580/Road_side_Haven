using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
namespace RoadsideHaven
{
    /// <summary>PC orbit/chase view with cursor handoff, bounded zoom and collision shortening.</summary>
    [RequireComponent(typeof(Camera))]
    [DefaultExecutionOrder(100)]
    public sealed class PcThirdPersonCamera : MonoBehaviour
    {
        [Header("Mouse and orbit")]
        [SerializeField,Range(.02f,.5f)] private float sensitivity=.12f;
        [SerializeField] private Vector2 pitchLimits=new Vector2(-12,65);
        [SerializeField] private Vector2 walkingZoom=new Vector2(2,8);
        [SerializeField] private Vector2 drivingZoom=new Vector2(5,15);
        [SerializeField,Min(.1f)] private float collisionRadius=.22f;
        [SerializeField,Min(1)] private float followSharpness=12;
        [SerializeField,Range(45,90)] private float fieldOfView=65;
        [SerializeField,Min(.2f)] private float vehicleRecenterDelay=1.5f;
        private float lastLookTime;
        private MotelGameRoot motel;
        private OwnerSandboxController owner;
        private WorldDrivingController driving;
        private IsometricCameraController map;
        private Camera lens;
        private Transform target,lastTarget;
        private Vector3 smoothedPivot;
        private Quaternion mapRotation;
        private float originalNearClip;
        private float mapSize,yaw,pitch=18,distance=4.5f;
        private bool activeView,released;
        private CursorLockMode originalLock;
        private bool originalVisible;
        private readonly RaycastHit[] hits=new RaycastHit[32];
        public bool InputBlocked => activeView && (ModalOpen || !Application.isFocused);
        private bool LookBlocked => InputBlocked || released;
        private bool ModalOpen => (motel.GetComponent<CompactGameHUD>()?.MenuOpen ?? false) ||
            (motel.GetComponentInChildren<OwnerLifeController>(true)?.PanelOpen ?? false) ||
            (motel.GetComponentInChildren<MultiverseController>(true)?.IsPortalPanelOpen ?? false) ||
            (motel.GetComponentInChildren<ShopHUD>(true)?.IsOpen ?? false);
        public void Configure(MotelGameRoot root,OwnerSandboxController player,WorldDrivingController vehicles,IsometricCameraController mapController)
        {motel=root;owner=player;driving=vehicles;map=mapController;lens=GetComponent<Camera>();}
        private void Awake(){originalLock=Cursor.lockState;originalVisible=Cursor.visible;originalNearClip=GetComponent<Camera>().nearClipPlane;}
        private void Update()
        {
            if(motel==null)return;
            bool shouldFollow=driving.IsDriving||owner.IsWalking;
            if(!shouldFollow)return;
            if(Keyboard.current!=null&&Keyboard.current.escapeKey.wasPressedThisFrame)released=true;
            if(Mouse.current!=null&&Mouse.current.leftButton.wasPressedThisFrame&&!ModalOpen&&
                (EventSystem.current==null||!EventSystem.current.IsPointerOverGameObject()))released=false;
        }
        private void LateUpdate()
        {
            if(motel==null||map==null)return;
            target=driving.IsDriving?driving.ActiveVehicleTransform:owner.IsWalking?owner.transform:null;
            if(target==null)
            {
                if(activeView)
                {
                    activeView=false;map.SetExternalView(false);lens.orthographic=true;lens.orthographicSize=mapSize;lens.nearClipPlane=originalNearClip;
                    transform.rotation=mapRotation;map.FollowWorldPoint(Vector3.zero);lastTarget=null;
                }
                Cursor.lockState=CursorLockMode.None;Cursor.visible=true;return;
            }
            if(!activeView)
            {
                mapRotation=transform.rotation;mapSize=lens.orthographicSize;activeView=true;released=false;
                yaw=target.eulerAngles.y;pitch=18;distance=driving.IsDriving?9:4.5f;
            }
            map.SetExternalView(true);lens.orthographic=false;lens.fieldOfView=fieldOfView;lens.nearClipPlane=.08f;
            bool blocked=LookBlocked;
            Cursor.lockState=blocked?CursorLockMode.None:CursorLockMode.Locked;Cursor.visible=blocked;
            if(lastTarget!=target)
            {lastTarget=target;smoothedPivot=target.position+Vector3.up*(driving.IsDriving?1.7f:1.35f);distance=driving.IsDriving?9:4.5f;}
            var mouse=Mouse.current;
            if(!blocked&&mouse!=null)
            {
                var delta=mouse.delta.ReadValue();if(delta.sqrMagnitude>.01f)lastLookTime=Time.unscaledTime;yaw+=delta.x*sensitivity;pitch=Mathf.Clamp(pitch-delta.y*sensitivity,pitchLimits.x,pitchLimits.y);
                var zoom=driving.IsDriving?drivingZoom:walkingZoom;
                distance=Mathf.Clamp(distance-mouse.scroll.ReadValue().y*.005f,zoom.x,zoom.y);
            }
            if(driving.IsDriving && !blocked && Time.unscaledTime-lastLookTime>vehicleRecenterDelay)
                yaw=Mathf.LerpAngle(yaw,target.eulerAngles.y,1-Mathf.Exp(-2f*Time.unscaledDeltaTime));
            float blend=1-Mathf.Exp(-followSharpness*Time.unscaledDeltaTime);
            Vector3 pivot=target.position+Vector3.up*(driving.IsDriving?1.7f:1.35f);
            smoothedPivot=Vector3.Distance(smoothedPivot,pivot)>20?pivot:Vector3.Lerp(smoothedPivot,pivot,blend);
            Quaternion orbit=Quaternion.Euler(pitch,yaw,0);Vector3 back=orbit*Vector3.back;
            float clearDistance=distance;
            int count=Physics.SphereCastNonAlloc(smoothedPivot,collisionRadius,back,hits,distance,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore);
            for(int i=0;i<count;i++)
            {
                var collider=hits[i].collider;if(collider==null||collider.transform.IsChildOf(target))continue;
                clearDistance=Mathf.Min(clearDistance,Mathf.Max(.2f,hits[i].distance-.08f));
            }
            transform.SetPositionAndRotation(smoothedPivot+back*clearDistance,orbit);
        }
        private void OnDisable()
        {
            if(map!=null)
            {
                map.SetExternalView(false);
                if(activeView && lens!=null){lens.orthographic=true;lens.orthographicSize=mapSize;lens.nearClipPlane=originalNearClip;transform.rotation=mapRotation;map.FollowWorldPoint(Vector3.zero);}
            }
            Cursor.lockState=originalLock;Cursor.visible=originalVisible;
        }
        private void OnApplicationFocus(bool focus){if(!focus){released=true;Cursor.lockState=CursorLockMode.None;Cursor.visible=true;}}
    }
}
