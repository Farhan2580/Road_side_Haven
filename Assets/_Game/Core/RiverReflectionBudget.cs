using UnityEngine;
namespace RoadsideHaven
{
    public sealed class RiverReflectionBudget : MonoBehaviour
    {
        [SerializeField] private ReflectionProbe reflection;
        private float next;
        public void Configure(ReflectionProbe probe){reflection=probe;}
        private void Update()
        {
            if(reflection==null||Time.unscaledTime<next)return;next=Time.unscaledTime+1;var camera=Camera.main;if(camera==null)return;
            var p=VillageRiverShape.Center(camera.transform.position.z);if((camera.transform.position-p).sqrMagnitude>150*150)return;
            reflection.transform.position=p+Vector3.up*2;next=Time.unscaledTime+20;reflection.RenderProbe();
        }
    }
}
