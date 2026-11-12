using UnityEngine;
namespace RoadsideHaven
{
    /// <summary>Removes retired stop-line objects from saved and generated street layers.</summary>
    public static class RoadMarkingCleanup
    {
        public static int RemoveStopLines(Transform root)
        {
            int removed=0;
            foreach(var marker in root.GetComponentsInChildren<Transform>(true))
            {
                if(marker.name!="Signal stop line marking")continue;
                foreach(var collider in marker.GetComponentsInChildren<Collider>(true))collider.enabled=false;
                marker.gameObject.SetActive(false);
                if(Application.isPlaying)Object.Destroy(marker.gameObject);else Object.DestroyImmediate(marker.gameObject);
                removed++;
            }
            return removed;
        }
        private static Mesh paintMesh;
        private static Mesh PaintMesh()
        {
            if(paintMesh!=null)return paintMesh;
            var temporary=GameObject.CreatePrimitive(PrimitiveType.Quad);paintMesh=temporary.GetComponent<MeshFilter>().sharedMesh;
            temporary.SetActive(false);if(Application.isPlaying)Object.Destroy(temporary);else Object.DestroyImmediate(temporary);return paintMesh;
        }
        public static GameObject CrossingStripe(Transform root,Vector3 position,Vector3 size,Material material)
        {
            var obj=new GameObject("Zebra crossing marking");obj.transform.SetParent(root,false);obj.transform.position=new Vector3(position.x,.0615f,position.z);
            obj.transform.rotation=Quaternion.Euler(90,0,0);obj.transform.localScale=new Vector3(size.x,size.z,1);obj.layer=2;
            obj.AddComponent<MeshFilter>().sharedMesh=PaintMesh();var renderer=obj.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;
            renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;renderer.receiveShadows=false;return obj;
        }
        public static int FlattenCrossings(Transform root)
        {
            int repaired=0;
            foreach(var renderer in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                if(renderer.name!="Zebra crossing marking"||!renderer.gameObject.activeInHierarchy)continue;
                foreach(var collider in renderer.GetComponentsInChildren<Collider>(true))
                {collider.enabled=false;if(Application.isPlaying)Object.Destroy(collider);else Object.DestroyImmediate(collider);}
                var filter=renderer.GetComponent<MeshFilter>();if(filter==null)continue;
                var bounds=renderer.bounds;
                if(filter.sharedMesh!=PaintMesh())
                {filter.sharedMesh=PaintMesh();renderer.transform.rotation=Quaternion.Euler(90,0,0);renderer.transform.localScale=new Vector3(bounds.size.x,bounds.size.z,1);repaired++;}
                var p=renderer.transform.position;p.y=.0615f;renderer.transform.position=p;renderer.gameObject.layer=2;
                renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;renderer.receiveShadows=false;
            }
            return repaired;
        }    }
}
