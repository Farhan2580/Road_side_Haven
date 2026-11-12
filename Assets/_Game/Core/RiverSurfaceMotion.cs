using UnityEngine;
namespace RoadsideHaven
{
    /// <summary>Small moving surface waves near the camera; distant water uses shader flow only.</summary>
    [RequireComponent(typeof(MeshFilter))]
    public sealed class RiverSurfaceMotion : MonoBehaviour
    {
        private Mesh instance;
        private Vector3[] rest,vertices;
        private float next;
        private void Update()
        {
            if(Time.time<next)return;next=Time.time+.083f;
            var camera=Camera.main;var renderer=GetComponent<Renderer>();
            if(camera==null||renderer.bounds.SqrDistance(camera.transform.position)>120*120)return;
            if(instance==null)
            {
                var filter=GetComponent<MeshFilter>();instance=Instantiate(filter.sharedMesh);instance.name="Animated river surface";instance.MarkDynamic();rest=instance.vertices;vertices=new Vector3[rest.Length];filter.sharedMesh=instance;
            }
            float time=Time.time;
            for(int i=0;i<rest.Length;i++)
            {
                var p=rest[i];float shore=Mathf.Sin(Mathf.PI*(i%13)/12f);
                p.y+=(Mathf.Sin(p.x*.43f+p.z*.21f-time*.9f)*.018f+Mathf.Sin(p.z*.64f+time*.65f)*.009f)*shore;vertices[i]=p;
            }
            instance.vertices=vertices;instance.RecalculateNormals();instance.RecalculateBounds();
        }
        private void OnDestroy(){if(instance!=null)Destroy(instance);}
    }
}