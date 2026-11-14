using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using UnityEditor.SceneManagement;
namespace RoadsideHaven.Editor
{
    /// <summary>Connects Aster's retail frontage to both existing avenues without widening the residential walking gap.</summary>
    public static class AsterAccessBuilder
    {
        public const string Layer="Aster Quarter Connected Boulevard v1";
        public const string Folder="Assets/_Game/Generated/AsterAccess";
        private static Material asphalt,stone,white,yellow,metal,glow,leaf;
        private static int meshIndex;
        private static readonly List<Renderer> lamps=new List<Renderer>();
        [MenuItem("Roadside Haven/Design/Connect Aster Quarter to Main Road")]
        public static void BuildCurrent()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)return;
            var motel=UnityEngine.Object.FindFirstObjectByType<MotelGameRoot>();if(motel==null)return;
            var scene=motel.gameObject.scene;if(File.Exists(scene.path)){Directory.CreateDirectory("Backups/AsterAccess");File.Copy(scene.path,"Backups/AsterAccess/Before-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")+".unity");}
            Build(motel);EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
        }
        [MenuItem("Roadside Haven/Design/Focus Aster Boulevard Connection")]
        public static void Focus()
        {
            var root=UnityEngine.Object.FindFirstObjectByType<MotelGameRoot>();if(root==null)return;Selection.activeGameObject=root.transform.Find(Layer)?.gameObject;
            SceneView.lastActiveSceneView?.Frame(new Bounds(new Vector3(290,0,103),new Vector3(200,30,210)),false);
        }
        public static Transform Build(MotelGameRoot motel)
        {
            var old=motel.transform.Find(Layer);if(old!=null)return old;
            if(motel.transform.Find(AsterQuarterBuilder.Layer)==null)throw new InvalidOperationException("Build Aster Quarter first.");
            if(!AssetDatabase.IsValidFolder(Folder))AssetDatabase.CreateFolder("Assets/_Game/Generated","AsterAccess");
            meshIndex=0;lamps.Clear();
            asphalt=Material("Boulevard fine asphalt",new Color(.29f,.32f,.35f),"Asphalt",.18f);
            stone=Material("Boulevard limestone",new Color(.71f,.73f,.7f),"Paving",.18f);
            white=Material("Reflective ivory paint",new Color(.9f,.9f,.8f),null,.2f);
            yellow=Material("Warm centre paint",new Color(.96f,.72f,.25f),null,.15f);
            metal=AssetDatabase.LoadAssetAtPath<Material>(AsterQuarterBuilder.Folder+"/Graphite aluminium.mat");
            leaf=AssetDatabase.LoadAssetAtPath<Material>(MotelDistrictArtBuilder.Folder+"/Palm foliage.mat");
            glow=Material("Boulevard warm diffuser",new Color(1,.83f,.56f),null,.3f);glow.EnableKeyword("_EMISSION");glow.SetColor("_EmissionColor",glow.color*1.2f);
            var root=new GameObject(Layer).transform;root.SetParent(motel.transform,false);root.gameObject.AddComponent<MotelDistrictArt>();
            var cuts=new[]{Rect.MinMaxRect(180,128,352,136),Rect.MinMaxRect(344,-13,352,94),Rect.MinMaxRect(341.96f,-2.04f,354.04f,10)};
            ClipExistingPaving(motel.transform,root,cuts);MoveEntranceFixtures(motel.transform);
            Road(root,Rect.MinMaxRect(180,128,348,136));Road(root,Rect.MinMaxRect(344,-13,352,90));
            Polygon(root,"Flared highway entrance",new[]{new Vector3(342,.06f,-2),new Vector3(344,.06f,10),new Vector3(352,.06f,10),new Vector3(354,.06f,-2)},asphalt,true);
            var allRoads=motel.GetComponentsInChildren<MeshRenderer>().Where(r=>TownPopulationController.RoadName(r.name)).Select(r=>Rect.MinMaxRect(r.bounds.min.x,r.bounds.min.z,r.bounds.max.x,r.bounds.max.z)).Concat(cuts).ToArray();
            Paved(root,Rect.MinMaxRect(195,125.7f,342,127.96f),allRoads);
            Paved(root,Rect.MinMaxRect(195,136.04f,342,138.5f),allRoads);
            Paved(root,Rect.MinMaxRect(341.1f,10,343.96f,94.05f),allRoads);
            Paved(root,Rect.MinMaxRect(352.04f,10,354.9f,136),allRoads);
            // All paint and drainage are visual-only; the continuous road collider stays at y=.06.
            foreach(float z in new[]{128.12f,135.88f})foreach(var span in new[]{new Vector2(195,267.9f),new Vector2(272.1f,340)})Paint(root,new Vector3((span.x+span.y)*.5f,.067f,z),new Vector3(span.y-span.x,.003f,.09f),white);
            for(float x=201;x<339;x+=8)if(Mathf.Abs(x-270)>5)Paint(root,new Vector3(x,.068f,132),new Vector3(3,.003f,.10f),yellow);
            for(float z=12;z<87;z+=8)Paint(root,new Vector3(348,.068f,z),new Vector3(.10f,.003f,3),yellow);
            foreach(float x in new[]{344.12f,351.88f})Paint(root,new Vector3(x,.067f,48),new Vector3(.09f,.003f,72),white);
            for(int i=0;i<5;i++)Paint(root,new Vector3(268.6f+i*.7f,.07f,132),new Vector3(.4f,.003f,7.5f),white);
            // Tactile pads remain flush with the walking floor and never add road collision.
            foreach(float z in new[]{127.5f,136.5f})Paint(root,new Vector3(270,.125f,z),new Vector3(3.5f,.003f,.55f),yellow);
            foreach(float z in new[]{127.9f,136.1f})foreach(var r in StreetPavingLayout.ClearRoads(Rect.MinMaxRect(195,z-.065f,342,z+.065f),new[]{Rect.MinMaxRect(267.9f,125,272.1f,139)}))Box(root,"Low bevel curb",new Vector3(r.center.x,.075f,r.center.y),new Vector3(r.width,.15f,r.height),stone,true);
            foreach(float x in new[]{343.9f,352.1f})foreach(var r in StreetPavingLayout.ClearRoads(Rect.MinMaxRect(x-.065f,10,x+.065f,89),allRoads))Box(root,"Boulevard edge curb",new Vector3(r.center.x,.075f,r.center.y),new Vector3(r.width,.15f,r.height),stone,true);
            foreach(float x in new[]{204f,234f,300f,332f})Drain(root,new Vector3(x,.069f,128.4f));
            foreach(float z in new[]{22f,58f,110f})Drain(root,new Vector3(351.5f,.069f,z));
            foreach(var p in new[]{new Vector3(357.3f,0,20),new Vector3(357.3f,0,53),new Vector3(357.3f,0,115),new Vector3(228,0,139.8f)}){Planter(root,p);CoastalCityBlockBuilder.AddPromenadePalm(root,p);}
            foreach(var p in new[]{new Vector3(356,0,12),new Vector3(356,0,42),new Vector3(356,0,74),new Vector3(356,0,116),new Vector3(221,0,139.5f),new Vector3(299,0,139.5f)})Lantern(root,p);
            Board(root,new Vector3(359,0,8),"ASTER QUARTER\nMARKET  /  FOOD  /  PARKING",0);
            Board(root,new Vector3(338,0,139.2f),"ASTER BOULEVARD\n< CITY AVENUE  |  MAIN ROAD >",0);
            SaveAndCombine(root);RegisterLights(motel);
            var physics=RoadSurfacePhysics.Rebuild(motel.transform);var saved=Save(physics.SurfaceMesh,Folder+"/AsterAccessRoadSurface.asset");physics.UseSavedMesh(saved);
            foreach(var text in root.GetComponentsInChildren<TMP_Text>())text.ForceMeshUpdate(true,true);AssetDatabase.SaveAssets();return root;
        }
        private static void ClipExistingPaving(Transform motel,Transform target,Rect[] cuts)
        {
            int index=0;
            foreach(var filter in motel.GetComponentsInChildren<MeshFilter>())
            {
                var r=filter.GetComponent<MeshRenderer>();if(r==null||!cuts.Any(c=>c.Overlaps(Footprint(r.bounds))))continue;
                bool ground=r.name=="Public sidewalk"||r.name=="Low sidewalk curb"||r.name=="Retained public sidewalk";
                bool aster=r.transform.IsChildOf(motel.Find(AsterQuarterBuilder.Layer))&&r.sharedMaterial!=null&&r.sharedMaterial.name=="Limestone plaza";
                bool frontage=r.transform.IsChildOf(motel.Find(MainRoadFrontageBuilder.Layer))&&r.sharedMaterial!=null&&r.sharedMaterial.name=="Frontage limestone";
                if(ground||aster||frontage)AsterAccessMeshClipper.ClipLowGround(filter,cuts,Folder+"/ClippedGround"+(index++)+".asset");
            }
            foreach(var collider in motel.GetComponentsInChildren<BoxCollider>())
            {
                var b=collider.bounds;if(!collider.enabled||collider.isTrigger||b.max.y>.25f||!cuts.Any(c=>c.Overlaps(Footprint(b))))continue;
                bool ground=collider.name=="Public sidewalk"||collider.name=="Low sidewalk curb"||collider.name=="Retained public sidewalk"||collider.name=="Residential walking connection"||collider.name=="Market east sidewalk"||collider.name=="Public plaza walking floor";
                if(!ground)continue;collider.enabled=false;
                foreach(var piece in StreetPavingLayout.ClearRoads(Footprint(b),cuts)){var go=new GameObject("Clipped access paving collision",typeof(BoxCollider));go.transform.SetParent(target,false);var box=go.GetComponent<BoxCollider>();box.center=new Vector3(piece.center.x,b.center.y,piece.center.y);box.size=new Vector3(piece.width,b.size.y,piece.height);}
            }
        }
        private static void MoveEntranceFixtures(Transform root)
        {
            foreach(var trunk in root.GetComponentsInChildren<MeshRenderer>())if(trunk.name=="Tree trunk"&&Mathf.Abs(trunk.bounds.center.x-346)<.1f&&Mathf.Abs(trunk.bounds.center.z-3.6f)<.1f){trunk.transform.parent.position+=Vector3.right*12;break;}
            var renderers=root.GetComponentsInChildren<MeshRenderer>();foreach(var part in renderers)if((part.name=="Garden bollard post"||part.name.Contains("bulb"))&&Mathf.Abs(part.bounds.center.x-347)<.1f&&Mathf.Abs(part.bounds.center.z-4.6f)<.1f)part.transform.position+=Vector3.right*11;
        }
        private static void Road(Transform root,Rect r)
        {
            // Preserve full semantic bounds for routing/welded physics, but let existing asphalt own junction pixels.
            var go=Quad(root,"Connected road",r,.06f,asphalt);go.GetComponent<MeshRenderer>().enabled=false;
            go.AddComponent<RoadSurfaceProfile>().Kind=RoadSurfaceKind.City;var c=go.AddComponent<BoxCollider>();c.center=new Vector3(r.center.x,0,r.center.y);c.size=new Vector3(r.width,.12f,r.height);
            var existing=root.parent.GetComponentsInChildren<MeshRenderer>().Where(v=>TownPopulationController.RoadName(v.name)&&!v.transform.IsChildOf(root)).Select(v=>{var b=v.bounds;return Rect.MinMaxRect(b.min.x+.04f,b.min.z+.04f,b.max.x-.04f,b.max.z-.04f);}).ToArray();
            foreach(var piece in StreetPavingLayout.ClearRoads(r,existing))Quad(root,"Boulevard asphalt finish",piece,.06f,asphalt);
        }
        private static void Paved(Transform root,Rect r,Rect[] cuts){foreach(var p in StreetPavingLayout.ClearRoads(r,cuts)){var go=Quad(root,"Boulevard pedestrian paving",p,.12f,stone);var c=go.AddComponent<BoxCollider>();c.center=new Vector3(p.center.x,.04f,p.center.y);c.size=new Vector3(p.width,.16f,p.height);}}
        private static Rect Footprint(Bounds b)=>Rect.MinMaxRect(b.min.x,b.min.z,b.max.x,b.max.z);
        private static GameObject Quad(Transform root,string name,Rect r,float y,Material m)=>Polygon(root,name,new[]{new Vector3(r.xMin,y,r.yMin),new Vector3(r.xMin,y,r.yMax),new Vector3(r.xMax,y,r.yMax),new Vector3(r.xMax,y,r.yMin)},m,false);
        private static GameObject Polygon(Transform root,string name,Vector3[] vertices,Material mat,bool solid)
        {
            var mesh=new Mesh{name=name,vertices=vertices,triangles=new[]{0,1,2,0,2,3},uv=vertices.Select(v=>new Vector2(v.x*.3f,v.z*.3f)).ToArray()};mesh.RecalculateNormals();mesh.RecalculateBounds();mesh=Save(mesh,Folder+"/AccessMesh"+(meshIndex++)+".asset");var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(root,false);go.GetComponent<MeshFilter>().sharedMesh=mesh;go.GetComponent<MeshRenderer>().sharedMaterial=mat;if(solid)go.AddComponent<MeshCollider>().sharedMesh=mesh;return go;
        }
        private static void Paint(Transform root,Vector3 p,Vector3 size,Material m)=>Box(root,"Flush road paint",p,size,m,false);
        private static void Drain(Transform root,Vector3 p){Box(root,"Flush drain",p,new Vector3(.65f,.003f,.3f),metal,false);for(int i=0;i<6;i++)Box(root,"Drain grille slat",p+new Vector3(-.25f+i*.1f,.003f,0),new Vector3(.035f,.002f,.25f),stone,false);}
        private static void Planter(Transform root,Vector3 p){Box(root,"Boulevard tree planter",p+Vector3.up*.23f,new Vector3(2.8f,.35f,1.8f),stone,true);for(int i=-1;i<=1;i++)Box(root,"Planter shrub",p+new Vector3(i*.75f,.5f,0),new Vector3(1,.65f,1.25f),leaf,false,PrimitiveType.Sphere);}
        private static void Lantern(Transform root,Vector3 p){Box(root,"Boulevard lighting column",p+Vector3.up*2.4f,new Vector3(.12f,4.7f,.12f),metal,true);var bulb=Box(root,"Boulevard diffuser",p+Vector3.up*4.8f,new Vector3(.65f,.12f,.65f),glow,false);lamps.Add(bulb.GetComponent<Renderer>());}
        private static void Board(Transform root,Vector3 p,string label,float yaw){var go=new GameObject("Mounted boulevard wayfinding").transform;go.SetParent(root,false);go.localPosition=p;go.localRotation=Quaternion.Euler(0,yaw,0);Box(go,"Sign mounting column",new Vector3(0,1.4f,0),new Vector3(.12f,2.7f,.12f),metal,true);Box(go,"Wayfinding board",new Vector3(0,2.2f,0),new Vector3(4.3f,1.2f,.14f),metal,false);var t=new GameObject(label).AddComponent<TextMeshPro>();t.transform.SetParent(go,false);t.transform.localPosition=new Vector3(0,2.2f,-.085f);t.font=TMP_Settings.GetFontAsset();t.text=label;t.fontSize=1.65f;t.color=white.color;t.alignment=TextAlignmentOptions.Center;t.rectTransform.sizeDelta=new Vector2(4,1);t.raycastTarget=false;}
        private static GameObject Box(Transform root,string name,Vector3 p,Vector3 size,Material m,bool solid,PrimitiveType shape=PrimitiveType.Cube){var go=GameObject.CreatePrimitive(shape);go.name=name;go.transform.SetParent(root,false);go.transform.localPosition=p;go.transform.localScale=size;go.GetComponent<Renderer>().sharedMaterial=m;UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());if(solid)go.AddComponent<BoxCollider>();return go;}
        private static Material Material(string name,Color color,string texture,float smooth){string path=Folder+"/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);if(m==null){m=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(m,path);}m.color=color;m.SetFloat("_Smoothness",smooth);m.enableInstancing=true;if(texture!=null){m.mainTexture=AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_Game/Art/Surfaces/"+texture+"_Albedo.png");m.SetTexture("_BumpMap",AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_Game/Art/Surfaces/"+texture+"_Normal.png"));m.EnableKeyword("_NORMALMAP");m.SetFloat("_BumpScale",.18f);}EditorUtility.SetDirty(m);return m;}
        private static Mesh Save(Mesh mesh,string path){var saved=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(saved==null){AssetDatabase.CreateAsset(mesh,path);return mesh;}saved.Clear();EditorUtility.CopySerialized(mesh,saved);saved.UploadMeshData(false);if(mesh!=saved)UnityEngine.Object.DestroyImmediate(mesh);return saved;}
        private static void SaveAndCombine(Transform root){int i=0;foreach(var g in root.GetComponentsInChildren<MeshFilter>().Where(f=>!TownPopulationController.RoadName(f.name)&&f.GetComponent<TMP_Text>()==null&&!lamps.Contains(f.GetComponent<Renderer>())).GroupBy(f=>f.GetComponent<MeshRenderer>().sharedMaterial).ToArray()){var parts=g.ToArray();var mesh=new Mesh{name="Boulevard "+g.Key.name,indexFormat=IndexFormat.UInt32};mesh.CombineMeshes(parts.Select(f=>new CombineInstance{mesh=f.sharedMesh,transform=root.worldToLocalMatrix*f.transform.localToWorldMatrix}).ToArray(),true,true);mesh.RecalculateBounds();var go=new GameObject(mesh.name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(root,false);go.GetComponent<MeshFilter>().sharedMesh=Save(mesh,Folder+"/Boulevard"+(i++)+".asset");go.GetComponent<MeshRenderer>().sharedMaterial=g.Key;foreach(var f in parts){UnityEngine.Object.DestroyImmediate(f.GetComponent<MeshRenderer>());UnityEngine.Object.DestroyImmediate(f);}}}
        private static void RegisterLights(MotelGameRoot motel){var light=motel.GetComponentsInChildren<WorldNightLighting>().FirstOrDefault();if(light==null)return;var so=new SerializedObject(light);var a=so.FindProperty("fixtures");var b=so.FindProperty("glowingSurfaces");int ai=a.arraySize,bi=b.arraySize;a.arraySize+=lamps.Count;b.arraySize+=lamps.Count;for(int i=0;i<lamps.Count;i++){a.GetArrayElementAtIndex(ai+i).objectReferenceValue=lamps[i].transform;b.GetArrayElementAtIndex(bi+i).objectReferenceValue=lamps[i];}so.ApplyModifiedPropertiesWithoutUndo();}
    }
}
