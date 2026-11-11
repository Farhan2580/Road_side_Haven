using TMPro;
using UnityEngine;
namespace RoadsideHaven
{
    /// <summary>Synchronizes sign faces while reducing distant text clutter.</summary>
    public sealed class PhysicalWorldSign : MonoBehaviour
    {
        private TextMeshPro front,back;
        private Camera cameraView;
        private float range;
        public void Configure(TextMeshPro label,TextMeshPro rear,float visibility){front=label;back=rear;range=visibility;cameraView=Camera.main;}
        private void LateUpdate()
        {
            if(front==null||back==null)return;
            back.text=front.text;back.color=front.color;
            if(cameraView==null)cameraView=Camera.main;
            bool visible=cameraView==null||(cameraView.transform.position-transform.position).sqrMagnitude<range*range || cameraView.orthographic;
            front.renderer.enabled=visible;back.renderer.enabled=visible;
        }
    }
}
