using UnityEngine;
using UnityEngine.Rendering;
namespace RoadsideHaven
{
    public sealed class LakeAmbientMotion : MonoBehaviour
    {
        [SerializeField] private Transform[] ducks;
        [SerializeField] private ReflectionProbe reflection;
        private float nextReflection;
        public void Configure(Transform[] birds,ReflectionProbe probe){ducks=birds;reflection=probe;}
        private void Update()
        {
            if(ducks!=null)for(int i=0;i<ducks.Length;i++)
            {
                if(ducks[i]==null)continue;float a=Time.time*(.025f+i*.002f)+i*1.17f;float radius=.35f+(i%3)*.10f;
                var target=new Vector3(Mathf.Cos(a)*40*radius,.035f+Mathf.Sin(Time.time*1.7f+i)*.018f,Mathf.Sin(a)*30*radius);
                ducks[i].localPosition=target;var direction=new Vector3(-Mathf.Sin(a)*40,0,Mathf.Cos(a)*30);ducks[i].localRotation=Quaternion.LookRotation(direction);
            }
            if(reflection==null||Time.unscaledTime<nextReflection)return;nextReflection=Time.unscaledTime+1;
            var camera=Camera.main;if(camera!=null&&(camera.transform.position-transform.position).sqrMagnitude<260*260){nextReflection=Time.unscaledTime+20;reflection.RenderProbe();}
        }
    }
}
