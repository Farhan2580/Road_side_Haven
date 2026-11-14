using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
namespace RoadsideHaven.Editor
{
    /// <summary>Bakes the walkable frontage between the motel and the east block, clipping all solids against roads and access routes.</summary>
    public static class CoastalPromenadeBuilder
    {
        public const string Layer="Coastal Promenade v1";
        public const string Folder="Assets/_Game/Generated/CoastalPromenade";
        private static Material paving,drive,cream,ink,wood,leaf,soil,pink,amber,glow;
        private static List<Rect> roads,cuts,entrances;
        private static readonly Rect Frontage=Rect.MinMaxRect(-24,-1.8f,169,2);
        [MenuItem("Roadside Haven/Design/Build Coastal Promenade")]
        public static void BuildCurrent()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode){Debug.LogWarning("Stop Play Mode before applying the promenade.");return;}
            var motel=UnityEngine.Object.FindFirstObjectByType<MotelGameRoot>();if(motel==null)return;
            string path=motel.gameObject.scene.path;if(File.Exists(path)){string backup="Backups/Promenade/"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");Directory.CreateDirectory(backup);File.Copy(path,backup+"/"+Path.GetFileName(path));}
            Build(motel);EditorSceneManager.MarkSceneDirty(motel.gameObject.scene);EditorSceneManager.SaveScene(motel.gameObject.scene);AssetDatabase.SaveAssets();
        }
        [MenuItem("Roadside Haven/Design/Focus Coastal Promenade")]
        public static void Focus()
        {
            var motel=UnityEngine.Object.FindFirstObjectByType<MotelGameRoot>();var root=motel==null?null:motel.transform.Find(Layer);if(root==null)return;
            Selection.activeGameObject=root.gameObject;if(SceneView.lastActiveSceneView!=null)SceneView.lastActiveSceneView.Frame(new Bounds(new Vector3(72,2,6),new Vector3(205,14,35)),false);
        }
        public static Transform Build(MotelGameRoot motel)
        {
            var existing=motel.transform.Find(Layer);if(existing!=null)return existing;
            if(!AssetDatabase.IsValidFolder(Folder))AssetDatabase.CreateFolder("Assets/_Game/Generated","CoastalPromenade");
            cream=Load("Warm stucco");ink=Load("Blue graphite");wood=Load("Oiled cedar");leaf=Load("Palm foliage");pink=Load("Rose neon");amber=Load("Warm accent");
            paving=Mat("Promenade concrete",new Color(.78f,.77f,.7f),"Paving");drive=Mat("Driveway asphalt",new Color(.55f,.57f,.58f),"RoadParking");soil=Mat("Garden soil",new Color(.21f,.13f,.075f),"DirtPebbles");
            glow=Mat("Lantern trim",new Color(.62f,.47f,.24f),null);
            CoastalCityBlockBuilder.Build(motel);
            var root=new GameObject(Layer).transform;root.SetParent(motel.transform,false);root.gameObject.AddComponent<MotelDistrictArt>();
            roads=motel.GetComponentsInChildren<MeshRenderer>(true).Where(r=>r.gameObject.activeInHierarchy&&TownPopulationController.RoadName(r.name)).Select(r=>RectOf(r.bounds)).ToList();
            // Keep explicit public access to the motel, older shop entrances and the east parking aisle.
            entrances=new List<Rect>();
            foreach(var r in motel.GetComponentsInChildren<MeshRenderer>(true))if(r.gameObject.activeInHierarchy&&r.name=="Main entrance"&&r.bounds.center.x>0&&r.bounds.center.x<115&&r.bounds.center.z<10)entrances.Add(Rect.MinMaxRect(r.bounds.center.x-1.6f,-2,r.bounds.center.x+1.6f,10));
            entrances.Add(Rect.MinMaxRect(-2.6f,-2,2.6f,9));entrances.Add(Rect.MinMaxRect(116.15f,-2,120.3f,29));
            cuts=new List<Rect>(roads);cuts.AddRange(entrances);
            HideDuplicatePaving(motel.transform);
            RelocateLaneFixtures(motel.transform,root);
            foreach(var piece in StreetPavingLayout.ClearRoads(Frontage,cuts))Paving(root,piece,.12f,paving,true);
            // Flush access surfaces are cut against the public roads too; no raised paint or stop-line colliders.
            foreach(var access in new[]{Rect.MinMaxRect(-2.6f,-1.94f,2.6f,2),Rect.MinMaxRect(116.15f,-1.94f,120.3f,29)})
                foreach(var piece in StreetPavingLayout.ClearRoads(access,roads))Paving(root,piece,.06f,drive,true);
            foreach(var entry in entrances.Where(p=>p.width<3.5f))foreach(var piece in StreetPavingLayout.ClearRoads(Rect.MinMaxRect(entry.xMin,-1.8f,entry.xMax,Mathf.Min(4,entry.yMax)),roads))Paving(root,piece,.12f,paving,true);
            foreach(var piece in StreetPavingLayout.ClearRoads(Rect.MinMaxRect(-24,-1.94f,169,-1.8f),cuts))Box(root,"Promenade curb",new Vector3(piece.center.x,.06f,piece.center.y),new Vector3(piece.width,.12f,piece.height),cream,true);
            // Thin inlaid borders read as paving details and have no physics representation.
            foreach(var piece in StreetPavingLayout.ClearRoads(Rect.MinMaxRect(-24,1.28f,169,1.36f),cuts))Paving(root,piece,.122f,cream,false);
            Physics.SyncTransforms();
            foreach(float x in new[]{20f,27f,41f,53f,73f,88f,98f})
            {
                var bed=Rect.MinMaxRect(x-1.35f,2.15f,x+1.35f,3.55f);
                if(Clear(bed)&&!Physics.CheckBox(new Vector3(x,.55f,2.85f),new Vector3(1.4f,.3f,.75f)))Garden(root,x);
            }
            foreach(float x in new[]{18.2f,55.5f,72f,99.8f})
            {
                var footprint=Rect.MinMaxRect(x-.4f,2.25f,x+.4f,3.05f);
                if(!Clear(footprint)||Physics.CheckCapsule(new Vector3(x,.5f,2.65f),new Vector3(x,5.9f,2.65f),.4f))continue;
                CoastalCityBlockBuilder.AddPromenadePalm(root,new Vector3(x,0,2.65f));
            }
            foreach(float x in new[]{22f,75f})if(Clear(Rect.MinMaxRect(x-2.5f,2.2f,x+2.5f,3.2f)))Wayfinding(root,new Vector3(x,0,2.75f),"CITY EAST  >","<  ROADSIDE HAVEN");
            if(Clear(Rect.MinMaxRect(121.1f,2.5f,124.9f,3.3f)))Wayfinding(root,new Vector3(123,0,2.9f),"PARKING  >","SUNSET KITCHEN");
            DressLegacyShops(motel.transform,root);
            Combine(root);foreach(var t in root.GetComponentsInChildren<TMP_Text>(true))t.ForceMeshUpdate(true,true);
            AssetDatabase.SaveAssets();return root;
        }
        private static Rect RectOf(Bounds b)=>Rect.MinMaxRect(b.min.x,b.min.z,b.max.x,b.max.z);
        private static bool Clear(Rect footprint)=>!cuts.Any(r=>footprint.Overlaps(Rect.MinMaxRect(r.xMin-.15f,r.yMin-.15f,r.xMax+.15f,r.yMax+.15f)));
        private static void HideDuplicatePaving(Transform motel)
        {
            foreach(var r in motel.GetComponentsInChildren<MeshRenderer>(true))
            {
                if(!r.gameObject.activeInHierarchy||(r.name!="Public sidewalk"&&r.name!="Low sidewalk curb"))continue;
                var b=r.bounds;if(b.center.z>1.1f||b.center.z< -2||b.min.x< -24.1f||b.max.x>169.1f)continue;r.gameObject.SetActive(false);
            }
        }
        private static void RelocateLaneFixtures(Transform motel,Transform root)
        {
            var renderers=motel.GetComponentsInChildren<MeshRenderer>(true);
            // Flat legacy fixture assemblies store post and bulb as siblings. Move matching pairs together,
            // retaining all serialized point-light anchors and the existing global light budget.
            var moved=new HashSet<Transform>();
            foreach(var post in renderers)
            {
                if(!post.gameObject.activeInHierarchy||!post.name.EndsWith(" post")||(!post.name.Contains("bollard")&&!post.name.Contains("Streetlight")))continue;
                var p=post.bounds.center;if(p.x< -24||p.x>169||p.z< -1.7f||p.z>1.5f)continue;
                var target=new Vector3(p.x,p.y,2.8f);foreach(var entry in entrances)if(entry.Contains(new Vector2(target.x,target.z)))target.x=entry.xMax+.8f;
                foreach(var part in renderers)
                {
                    if(part.transform.parent!=post.transform.parent||moved.Contains(part.transform)||(!part.name.Contains("bulb")&&!part.name.EndsWith(" post")))continue;
                    var q=part.bounds.center;if(Mathf.Abs(q.x-p.x)>.025f||Mathf.Abs(q.z-p.z)>.025f)continue;
                    part.transform.position+=new Vector3(target.x-p.x,0,target.z-p.z);moved.Add(part.transform);
                }
                post.sharedMaterial=ink;
            }
            foreach(var post in renderers)
            {
                if(!post.gameObject.activeInHierarchy||post.name!="Streetlight post")continue;var p=post.bounds.center;
                if(p.x<0||p.x>169||p.z<2||p.z>4||post.bounds.size.y<3)continue;
                float height=post.bounds.max.y;Box(root,"Lantern cap",new Vector3(p.x,height+.16f,p.z),new Vector3(.65f,.13f,.65f),ink,false);
                Box(root,"Lantern collar",new Vector3(p.x,height-.05f,p.z),new Vector3(.42f,.09f,.42f),glow,false);
            }
            // The frontage park formerly had a trunk in the narrow walking line.
            foreach(var tree in renderers)
            {
                if(!tree.gameObject.activeInHierarchy||tree.name!="Tree trunk")continue;var p=tree.bounds.center;if(p.x< -24||p.x>118||p.z< -1.5f||p.z>.3f)continue;
                foreach(var part in renderers)if(part.transform.parent==tree.transform.parent&&(part.name.StartsWith("Tree crown")||part.name=="Tree trunk")&&Mathf.Abs(part.bounds.center.x-p.x)<1.6f&&Mathf.Abs(part.bounds.center.z-p.z)<1.3f)part.gameObject.SetActive(false);
            }
        }
        private static void Garden(Transform root,float x)
        {
            Box(root,"Garden bed",new Vector3(x,.2f,2.85f),new Vector3(2.7f,.24f,1.4f),cream,true);
            Box(root,"Garden soil",new Vector3(x,.33f,2.85f),new Vector3(2.48f,.025f,1.18f),soil,false);
            for(int i=0;i<12;i++)
            {
                float px=x-1.05f+(i%6)*.42f,pz=2.55f+i/6*.55f;float height=.22f+(i%3)*.07f;
                Box(root,"Flower stem",new Vector3(px,.34f+height*.5f,pz),new Vector3(.025f,height,.025f),leaf,false);
                Blossom(root,new Vector3(px,.35f+height,pz),i%2==0?pink:amber);
                Box(root,"Garden leaf",new Vector3(px,.42f,pz),new Vector3(.2f,.06f,.1f),leaf,false);
            }
        }
        private static void Blossom(Transform root,Vector3 p,Material material)
        {
            var v=new[]{new Vector3(0,.08f,0),new Vector3(-.1f,0,0),new Vector3(0,0,.1f),new Vector3(.1f,0,0),new Vector3(0,0,-.1f),new Vector3(0,-.05f,0)};
            var mesh=new Mesh{name="Garden blossom",vertices=v,triangles=new[]{0,2,1,0,3,2,0,4,3,0,1,4,5,1,2,5,2,3,5,3,4,5,4,1}};mesh.RecalculateNormals();MeshPart(root,"Garden blossom",p,mesh,material);
        }
        private static void Wayfinding(Transform root,Vector3 p,string upper,string lower)
        {
            Box(root,"Wayfinding post",p+new Vector3(-1.6f,1.65f,0),new Vector3(.12f,3.3f,.12f),ink,true);
            Box(root,"Wayfinding plaque",p+new Vector3(0,2.8f,-.08f),new Vector3(3.8f,1.05f,.14f),ink,false);
            Box(root,"Wayfinding trim",p+new Vector3(0,3.32f,-.08f),new Vector3(3.8f,.045f,.16f),cream,false);
            Sign(root,upper,p+new Vector3(0,3.02f,-.16f),.25f,3.5f,cream.color);
            Sign(root,lower,p+new Vector3(0,2.58f,-.16f),.18f,3.5f,amber.color);
        }
        private static void DressLegacyShops(Transform motel,Transform root)
        {
            var buildings=motel.GetComponentsInChildren<MeshRenderer>(true).Where(r=>r.gameObject.activeInHierarchy&&r.name=="CITY SHOPS"&&r.bounds.center.x>35&&r.bounds.center.x<105&&r.bounds.center.z<15).ToArray();
            foreach(var building in buildings)
            {
                var b=building.bounds;float x=b.center.x,z=b.min.z-.12f;building.sharedMaterial=Load("Warm stucco");
                Box(root,"Shop frontage fascia",new Vector3(x,3.15f,z),new Vector3(b.size.x-.4f,.7f,.22f),ink,false);
                Sign(root,"HAVEN LOCAL SHOPS",new Vector3(x,3.18f,z-.13f),.28f,b.size.x-1,cream.color);
                Box(root,"Shop frontage cornice",new Vector3(x,3.55f,z),new Vector3(b.size.x,.12f,.4f),cream,false);
            }
            foreach(var text in motel.GetComponentsInChildren<TextMeshPro>(true))
                if(!text.transform.IsChildOf(root)&&text.gameObject.activeInHierarchy&&text.transform.position.x>35&&text.transform.position.x<105&&(text.text=="CITY SHOPS"||text.text=="HAVEN CITY"))text.gameObject.SetActive(false);
        }
        private static void Paving(Transform root,Rect r,float y,Material mat,bool solid)
        {
            var vertices=new[]{new Vector3(r.xMin,y,r.yMin),new Vector3(r.xMax,y,r.yMin),new Vector3(r.xMax,y,r.yMax),new Vector3(r.xMin,y,r.yMax)};
            var mesh=new Mesh{name="World tiled paving",vertices=vertices,triangles=new[]{0,2,1,0,3,2},uv=vertices.Select(v=>new Vector2(v.x*.5f,v.z*.5f)).ToArray()};mesh.RecalculateNormals();MeshPart(root,solid?"Promenade paving":"Paving inlay",Vector3.zero,mesh,mat);
            if(solid){var c=root.GetChild(root.childCount-1).gameObject.AddComponent<BoxCollider>();c.center=new Vector3(r.center.x,y-.08f,r.center.y);c.size=new Vector3(r.width,.16f,r.height);}
        }
        private static GameObject Box(Transform root,string name,Vector3 p,Vector3 size,Material mat,bool solid)
        {var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(root,false);go.transform.position=p;go.transform.localScale=size;go.GetComponent<Renderer>().sharedMaterial=mat;UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());if(solid)go.AddComponent<BoxCollider>();return go;}
        private static void MeshPart(Transform root,string name,Vector3 p,Mesh mesh,Material mat){var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(root,false);go.transform.position=p;go.GetComponent<MeshFilter>().sharedMesh=mesh;go.GetComponent<MeshRenderer>().sharedMaterial=mat;}
        private static void Sign(Transform root,string text,Vector3 p,float size,float width,Color color){var label=new GameObject(text).AddComponent<TextMeshPro>();label.transform.SetParent(root,false);label.transform.position=p;label.font=TMP_Settings.GetFontAsset();label.text=text;label.fontSize=size*10;label.alignment=TextAlignmentOptions.Center;label.color=color;label.rectTransform.sizeDelta=new Vector2(width,size*3);label.raycastTarget=false;}
        private static Material Load(string name){var mat=AssetDatabase.LoadAssetAtPath<Material>(MotelDistrictArtBuilder.Folder+"/"+name+".mat");if(mat==null)throw new InvalidOperationException("Missing coastal art material: "+name);return mat;}
        private static Material Mat(string name,Color color,string surface)
        {string path=Folder+"/"+name+".mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(path);if(mat==null){mat=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(mat,path);}mat.color=color;mat.SetFloat("_Smoothness",.15f);mat.enableInstancing=true;if(surface!=null){mat.mainTexture=AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_Game/Art/Surfaces/"+surface+"_Albedo.png");mat.SetTexture("_BumpMap",AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_Game/Art/Surfaces/"+surface+"_Normal.png"));mat.SetFloat("_BumpScale",.25f);mat.EnableKeyword("_NORMALMAP");}EditorUtility.SetDirty(mat);return mat;}
        private static void Combine(Transform root)
        {
            int index=0;var filters=root.GetComponentsInChildren<MeshFilter>().Where(f=>f.GetComponent<TMP_Text>()==null).ToArray();
            foreach(var group in filters.GroupBy(f=>f.GetComponent<MeshRenderer>().sharedMaterial))
            {
                var parts=group.ToArray();var mesh=new Mesh{name="Promenade "+group.Key.name,indexFormat=IndexFormat.UInt32};mesh.CombineMeshes(parts.Select(f=>new CombineInstance{mesh=f.sharedMesh,transform=root.worldToLocalMatrix*f.transform.localToWorldMatrix}).ToArray(),true,true);mesh.RecalculateBounds();string path=Folder+"/Promenade"+(index++)+".asset";var saved=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(saved==null)AssetDatabase.CreateAsset(mesh,path);else{EditorUtility.CopySerialized(mesh,saved);UnityEngine.Object.DestroyImmediate(mesh);mesh=saved;}MeshPart(root,mesh.name,Vector3.zero,mesh,group.Key);
                foreach(var f in parts){UnityEngine.Object.DestroyImmediate(f.GetComponent<MeshRenderer>());UnityEngine.Object.DestroyImmediate(f);}
            }
        }
    }
}