using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
namespace MotelGame.World
{
    /// <summary>Lightweight keyboard collision inspection rig for the river terrain review scene.</summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class RiverTerrainWalker : MonoBehaviour
    {
        [Header("Movement")]
        [Tooltip("Walking speed in meters per second.")]
        [SerializeField] private float walkSpeed=5;
        [Tooltip("Speed while holding Shift.")]
        [SerializeField] private float runSpeed=12;
        [Tooltip("Gravity acceleration in meters per second squared.")]
        [SerializeField] private float gravity=20;
        [Header("View")]
        [Tooltip("Camera pivot for vertical mouse look.")]
        [SerializeField] private Transform view;
        [Tooltip("Mouse look degrees per pixel while holding the right mouse button.")]
        [SerializeField] private float sensitivity=.12f;
        private CharacterController controller;
        private Vector3 spawn;
        private float velocityY,pitch;
        /// <summary>Assigns the camera when generating the test scene.</summary>
        public void Configure(Transform cameraTransform){view=cameraTransform;}
        private void Awake(){controller=GetComponent<CharacterController>();spawn=transform.position;}
        private void Update()
        {
            Vector2 movement=Vector2.zero,look=Vector2.zero;bool run=false,reset=false;
#if ENABLE_INPUT_SYSTEM
            var keys=Keyboard.current;var mouse=Mouse.current;
            if(keys!=null){movement.x=(keys.dKey.isPressed?1:0)-(keys.aKey.isPressed?1:0);movement.y=(keys.wKey.isPressed?1:0)-(keys.sKey.isPressed?1:0);run=keys.leftShiftKey.isPressed;reset=keys.rKey.wasPressedThisFrame;}
            if(mouse!=null&&mouse.rightButton.isPressed)look=mouse.delta.ReadValue()*sensitivity;
#else
            movement=new Vector2(Input.GetAxisRaw("Horizontal"),Input.GetAxisRaw("Vertical"));run=Input.GetKey(KeyCode.LeftShift);reset=Input.GetKeyDown(KeyCode.R);
            if(Input.GetMouseButton(1))look=new Vector2(Input.GetAxis("Mouse X"),Input.GetAxis("Mouse Y"))*sensitivity*12;
#endif
            if(reset||transform.position.y< -12){controller.enabled=false;transform.position=spawn;controller.enabled=true;velocityY=0;return;}
            transform.Rotate(0,look.x,0);pitch=Mathf.Clamp(pitch-look.y,-80,80);if(view!=null)view.localRotation=Quaternion.Euler(pitch,0,0);
            if(controller.isGrounded&&velocityY<0)velocityY=-2;
            velocityY-=gravity*Time.deltaTime;
            movement=Vector2.ClampMagnitude(movement,1);
            var direction=transform.right*movement.x+transform.forward*movement.y;
            var delta=(direction*(run?runSpeed:walkSpeed)+Vector3.up*velocityY)*Time.deltaTime;
            delta.x=Mathf.Clamp(transform.position.x+delta.x,-998,998)-transform.position.x;delta.z=Mathf.Clamp(transform.position.z+delta.z,-998,998)-transform.position.z;controller.Move(delta);
        }
    }
}