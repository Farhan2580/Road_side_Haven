using UnityEngine;
namespace RoadsideHaven
{
    /// <summary>Shows room roofs on foot and exposes interiors in the overhead management view.</summary>
    public sealed class MotelDistrictArt : MonoBehaviour
    {
        [SerializeField] private Renderer[] roofRenderers;
        private Camera view;
        private float nextCheck;
        public void Configure(Renderer[] roofs) { roofRenderers=roofs; }
        private void LateUpdate()
        {
            if(roofRenderers==null||roofRenderers.Length==0||Time.unscaledTime<nextCheck)return;nextCheck=Time.unscaledTime+.2f;
            if(view==null)view=Camera.main;if(view==null)return;
            bool visible=!view.orthographic&&view.transform.position.y<12;
            if(roofRenderers!=null)foreach(var roof in roofRenderers)if(roof!=null)roof.enabled=visible;
        }
    }
}