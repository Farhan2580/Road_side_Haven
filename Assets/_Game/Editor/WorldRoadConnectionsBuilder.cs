using System;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
namespace RoadsideHaven.Editor
{
    /// <summary>Repairs the three disconnected public spurs identified by the whole-world routing audit.</summary>
    public static class WorldRoadConnectionsBuilder
    {
        public const string Layer="Audited Public Road Connections v1";
        public const string Folder="Assets/_Game/Generated/FrontageDetails";
        public static Transform Build(MotelGameRoot motel)
        {
            var old=motel.transform.Find(Layer);if(old!=null)return old;
            if(!AssetDatabase.IsValidFolder(Folder))AssetDatabase.CreateFolder("Assets/_Game/Generated","FrontageDetails");
            var root=new GameObject(Layer).transform;root.SetParent(motel.transform,false);root.gameObject.AddComponent<MotelDistrictArt>();
            var asphalt=AssetDatabase.LoadAssetAtPath<Material>(MainRoadFrontageBuilder.Folder+"/Frontage asphalt.mat");var white=AssetDatabase.LoadAssetAtPath<Material>(MainRoadFrontageBuilder.Folder+"/Frontage pearl.mat");
            var restaurant=Road(root,new Vector3(-430,0,170),new Vector3(8,.12f,22),asphalt);
            var bank=Road(root,new Vector3(-700,0,380.5f),new Vector3(60,.12f,8),asphalt);
            var cuts=new[]{Footprint(restaurant.GetComponent<Renderer>().bounds),Footprint(bank.GetComponent<Renderer>().bounds)};
            ClipRaisedSurfaces(motel.transform,root,cuts);
            MoveBankFixtures(motel.transform);MoveBankTree(motel.transform);
            foreach(var r in motel.GetComponentsInChildren<MeshRenderer>(true))
            {
                if(!r.gameObject.activeInHierarchy||!TownPopulationController.RoadName(r.name))continue;
                var b=r.bounds;if(Mathf.Abs(b.center.x+27)<.05f&&Mathf.Abs(b.min.z+43)<.05f&&Mathf.Abs(b.max.z+15)<.05f)
                {var scale=r.transform.localScale;scale.z*=30f/28;r.transform.localScale=scale;r.transform.position+=Vector3.forward;EditorUtility.SetDirty(r.transform);break;}
            }
            for(int i=0;i<3;i++)Paint(root,new Vector3(-430,.071f,164+i*6),new Vector3(.12f,.006f,2),white);
            for(int i=0;i<7;i++)Paint(root,new Vector3(-725+i*8,.071f,380.5f),new Vector3(3,.006f,.12f),white);
            var surface=RoadSurfacePhysics.Rebuild(motel.transform);string path=Folder+"/ConnectedWorldRoadSurface.asset";var saved=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(saved==null){saved=surface.SurfaceMesh;AssetDatabase.CreateAsset(saved,path);}else{saved.Clear();EditorUtility.CopySerialized(surface.SurfaceMesh,saved);saved.UploadMeshData(false);}surface.UseSavedMesh(saved);
            if(DisconnectedSegments(motel.transform)!=0)throw new InvalidOperationException("A public road remains disconnected from the main highway.");
            AssetDatabase.SaveAssets();return root;
        }
        public static int DisconnectedSegments(Transform root)
        {
            var graph=new TownRoadNetwork();foreach(var r in root.GetComponentsInChildren<MeshRenderer>(true))if(r.gameObject.activeInHierarchy&&TownPopulationController.RoadName(r.name)&&r.GetComponentInParent<Rigidbody>()==null)graph.Add(r.bounds.center,r.bounds.size);graph.Build(null);
            int count=0;foreach(var road in graph.Roads)if(graph.Route(new Vector3(0,.08f,-13),(road.A+road.B)*.5f).Length==0)count++;return count;
        }
        private static Rect Footprint(Bounds b)=>Rect.MinMaxRect(b.min.x,b.min.z,b.max.x,b.max.z);
        private static GameObject Road(Transform root,Vector3 p,Vector3 size,Material material)
        {var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name="Connected road";go.transform.SetParent(root,false);go.transform.position=p;go.transform.localScale=size;go.GetComponent<Renderer>().sharedMaterial=material;go.AddComponent<RoadSurfaceProfile>().Kind=RoadSurfaceKind.City;return go;}
        private static void Paint(Transform root,Vector3 p,Vector3 size,Material material)
        {var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name="Connection centre dash";go.transform.SetParent(root,false);go.transform.position=p;go.transform.localScale=size;go.GetComponent<Renderer>().sharedMaterial=material;UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());}
        private static void ClipRaisedSurfaces(Transform motel,Transform root,Rect[] cuts)
        {
            int index=0;var source=motel.GetComponentsInChildren<MeshRenderer>(true);
            foreach(var r in source)
            {
                if(!r.gameObject.activeInHierarchy||r.transform.IsChildOf(root))continue;var b=r.bounds;
                bool allowed=GroundSurfaceStyle.SurfaceFor(r.name)==5||r.name=="Low sidewalk curb"||r.name=="Downtown park lawn";
                if(!allowed||b.max.y<.061f||b.size.y>.5f||!cuts.Any(c=>c.Overlaps(Footprint(b))))continue;
                var material=r.sharedMaterial;r.gameObject.SetActive(false);
                foreach(var rect in StreetPavingLayout.ClearRoads(Footprint(b),cuts))
                {
                    var go=new GameObject("Clipped connection paving",typeof(MeshFilter),typeof(MeshRenderer),typeof(BoxCollider));go.transform.SetParent(root,false);
                    var v=new[]{new Vector3(rect.xMin,b.max.y,rect.yMin),new Vector3(rect.xMax,b.max.y,rect.yMin),new Vector3(rect.xMax,b.max.y,rect.yMax),new Vector3(rect.xMin,b.max.y,rect.yMax)};
                    var mesh=new Mesh{name="Connection paving "+index,vertices=v,triangles=new[]{0,2,1,0,3,2},uv=v.Select(p=>new Vector2(p.x*.25f,p.z*.25f)).ToArray()};mesh.RecalculateNormals();string path=Folder+"/ConnectionPaving"+(index++)+".asset";var saved=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(saved==null)AssetDatabase.CreateAsset(mesh,path);else{saved.Clear();EditorUtility.CopySerialized(mesh,saved);UnityEngine.Object.DestroyImmediate(mesh);mesh=saved;}go.GetComponent<MeshFilter>().sharedMesh=mesh;go.GetComponent<MeshRenderer>().sharedMaterial=material;var c=go.GetComponent<BoxCollider>();c.center=new Vector3(rect.center.x,b.center.y,rect.center.y);c.size=new Vector3(rect.width,b.size.y,rect.height);
                }
            }
        }
        private static void MoveBankFixtures(Transform motel)
        {
            var renderers=motel.GetComponentsInChildren<MeshRenderer>(true);var posts=renderers.Where(r=>r.gameObject.activeInHierarchy&&(r.name=="Streetlight post"||r.name=="Garden bollard post")&&r.bounds.center.x> -731&&r.bounds.center.x< -670&&r.bounds.center.z>376&&r.bounds.center.z<385).ToArray();
            foreach(var post in posts){var p=post.bounds.center;foreach(var part in renderers){if(part.transform.parent!=post.transform.parent||!part.gameObject.activeInHierarchy)continue;if(!part.name.EndsWith(" post")&&!part.name.Contains("bulb"))continue;var q=part.bounds.center;if(Mathf.Abs(q.x-p.x)<.025f&&Mathf.Abs(q.z-p.z)<.025f)part.transform.position+=Vector3.forward*(389-p.z);}}
        }
        private static void MoveBankTree(Transform motel)
        {
            foreach(var r in motel.GetComponentsInChildren<MeshRenderer>(true))if(r.gameObject.activeInHierarchy&&r.name=="Tree trunk"&&Mathf.Abs(r.bounds.center.x+687.6f)<.2f&&Mathf.Abs(r.bounds.center.z-383)<.2f){var group=r.transform.parent;if(group!=null&&group.name.StartsWith("Avenue "))group.position+=Vector3.forward*6;break;}
        }
    }
}