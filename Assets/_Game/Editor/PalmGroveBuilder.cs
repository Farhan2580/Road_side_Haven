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
    /// <summary>Bakes a compact residential precinct beyond the existing clinic road, with explicit road and pedestrian clearances.</summary>
    public static class PalmGroveBuilder
    {
        public const string Layer="Palm Grove Residences v1";
        public const string Folder="Assets/_Game/Generated/PalmGrove";
        private static Material cream,coral,mint,ink,wood,glass,leaf,stone,lawn,asphalt,glow,rubber;
        private static readonly List<Renderer> bulbs=new List<Renderer>();
        private static readonly List<Transform> lamps=new List<Transform>();
        private static List<Rect> roads;
        [MenuItem("Roadside Haven/Design/Build Palm Grove Residences")]
        public static void BuildCurrent()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)return;var motel=UnityEngine.Object.FindFirstObjectByType<MotelGameRoot>();if(motel==null)return;
            string path=motel.gameObject.scene.path;if(File.Exists(path)){string backup="Backups/PalmGrove/"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");Directory.CreateDirectory(backup);File.Copy(path,backup+"/"+Path.GetFileName(path));}
            Build(motel);EditorSceneManager.MarkSceneDirty(motel.gameObject.scene);EditorSceneManager.SaveScene(motel.gameObject.scene);AssetDatabase.SaveAssets();
        }
        [MenuItem("Roadside Haven/Design/Focus Palm Grove Residences")]
        public static void Focus()
        {
            var motel=UnityEngine.Object.FindFirstObjectByType<MotelGameRoot>();var area=motel==null?null:motel.transform.Find(Layer);if(area==null)return;Selection.activeGameObject=area.gameObject;
            if(SceneView.lastActiveSceneView!=null)SceneView.lastActiveSceneView.Frame(new Bounds(new Vector3(290,3,90),new Vector3(120,18,88)),false);
        }
        public static Transform Build(MotelGameRoot motel)
        {
            var existing=motel.transform.Find(Layer);if(existing!=null)return existing;
            if(!AssetDatabase.IsValidFolder(Folder))AssetDatabase.CreateFolder("Assets/_Game/Generated","PalmGrove");
            cream=Load("Warm stucco");coral=Load("Sunset coral");mint=Load("Seafoam stucco");ink=Load("Blue graphite");wood=Load("Oiled cedar");glass=Load("Teal window glass");leaf=Load("Palm foliage");
            stone=Mat("Residential paving",new Color(.8f,.76f,.66f),"Paving");lawn=Mat("Residential lawn",new Color(.6f,.78f,.55f),"BaseGrass");asphalt=Mat("Local asphalt",new Color(.5f,.53f,.55f),"RoadParking");glow=Mat("Residential lantern glass",new Color(1,.8f,.5f),null);rubber=Mat("Playground rubber",new Color(.15f,.42f,.4f),null);
            var root=new GameObject(Layer).transform;root.SetParent(motel.transform,false);root.gameObject.AddComponent<MotelDistrictArt>();bulbs.Clear();lamps.Clear();
            RelocateApproachLamps(motel.transform);
            // Continue the existing 18m clinic approach with an 8m local street at the same physical elevation.
            Box(root,"Connected road",new Vector3(290,.0f,90),new Vector3(98,.12f,8),asphalt,true);
            roads=motel.GetComponentsInChildren<MeshRenderer>(true).Where(r=>r.gameObject.activeInHierarchy&&TownPopulationController.RoadName(r.name)).Select(r=>Rect.MinMaxRect(r.bounds.min.x,r.bounds.min.z,r.bounds.max.x,r.bounds.max.z)).ToList();
            foreach(var plot in StreetPavingLayout.ClearRoads(Rect.MinMaxRect(242.4f,48,342,128),roads))
            {Box(root,"Residential foundation",new Vector3(plot.center.x,-.04f,plot.center.y),new Vector3(plot.width,.2f,plot.height),stone,true);Plane(root,"Residential textured lawn",plot,.063f,lawn);}
            var cuts=new List<Rect>(roads);foreach(float x in new[]{255f,285f})cuts.Add(Rect.MinMaxRect(x-2.7f,74,x+2.7f,106));cuts.Add(Rect.MinMaxRect(333,94,339,110));
            foreach(float z in new[]{84.2f,95.8f})foreach(var piece in StreetPavingLayout.ClearRoads(Rect.MinMaxRect(241,z-1.2f,339,z+1.2f),cuts))Paving(root,piece,.12f,stone);
            foreach(float z in new[]{85.7f,94.3f})foreach(var piece in StreetPavingLayout.ClearRoads(Rect.MinMaxRect(241,z-.08f,339,z+.08f),cuts))Box(root,"Residential low curb",new Vector3(piece.center.x,.065f,piece.center.y),new Vector3(piece.width,.13f,piece.height),cream,true);
            // Short connecting paving reaches the existing clinic sidewalks without covering the approach road.
            foreach(var patch in new[]{Rect.MinMaxRect(238.9f,99.05f,244,102.2f),Rect.MinMaxRect(241.05f,95,244,102.2f),Rect.MinMaxRect(238.9f,77.8f,244,80.95f),Rect.MinMaxRect(241.05f,77.8f,244,85)})
                foreach(var piece in StreetPavingLayout.ClearRoads(patch,roads))Paving(root,piece,.12f,stone);
            House(root,new Vector3(255,0,111),0,0,coral);House(root,new Vector3(285,0,111),0,1,mint);
            House(root,new Vector3(255,0,69),180,2,cream);House(root,new Vector3(285,0,69),180,3,coral);
            Apartment(root,new Vector3(322,0,112));
            foreach(float x in new[]{255f,285f})
            {
                Paving(root,Rect.MinMaxRect(x-2.5f,94.04f,x+2.5f,103),.065f,asphalt);Paving(root,Rect.MinMaxRect(x-2.5f,77,x+2.5f,85.96f),.065f,asphalt);
                for(int side=-1;side<=1;side+=2){Lot(root,x,side<0?69:111,side<0);}
            }
            Paving(root,Rect.MinMaxRect(333,94.04f,339,115),.065f,asphalt);
            for(int i=0;i<4;i++)Box(root,"Apartment parking stripe",new Vector3(336,.073f,99+i*4),new Vector3(5,.008f,.08f),cream,false);
            Playground(root);
            foreach(var p in new[]{new Vector3(263,0,120),new Vector3(293,0,120),new Vector3(263,0,56),new Vector3(293,0,56),new Vector3(337,0,74)})CoastalCityBlockBuilder.AddPromenadePalm(root,p);
            foreach(var p in new[]{new Vector3(246,0,98.5f),new Vector3(273,0,98.5f),new Vector3(300,0,98.5f),new Vector3(246,0,81.5f),new Vector3(273,0,81.5f),new Vector3(301,0,81.5f)})CoastalCityBlockBuilder.AddPromenadePalm(root,p);
            foreach(var p in new[]{new Vector3(250,0,98.3f),new Vector3(277,0,98.3f),new Vector3(307,0,98.3f),new Vector3(250,0,81.7f),new Vector3(277,0,81.7f),new Vector3(307,0,81.7f)})Lantern(root,p);
            Box(root,"Neighbourhood entrance plaque",new Vector3(247,1.4f,102.8f),new Vector3(5.5f,1.3f,.2f),ink,false);Sign(root,"PALM GROVE",new Vector3(247,1.6f,102.65f),.35f,5,cream.color);Sign(root,"RESIDENCES",new Vector3(247,1.14f,102.65f),.2f,5,cream.color);
            Combine(root);RegisterLights(motel);
            var surface=RoadSurfacePhysics.Rebuild(motel.transform);string path=Folder+"/NeighbourhoodRoadSurface.asset";var saved=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(saved==null){saved=surface.SurfaceMesh;AssetDatabase.CreateAsset(saved,path);}else{EditorUtility.CopySerialized(surface.SurfaceMesh,saved);saved.UploadMeshData(false);}surface.UseSavedMesh(saved);
            foreach(var t in root.GetComponentsInChildren<TMP_Text>())t.ForceMeshUpdate(true,true);AssetDatabase.SaveAssets();return root;
        }
        private static void House(Transform root,Vector3 position,float yaw,int style,Material facade)
        {
            var group=new GameObject("Residence "+(style+1)).transform;group.SetParent(root,false);
            float height=style==0?3.6f:style==2?4.2f:6.8f;
            Box(group,"House physical shell",new Vector3(0,height*.5f+.1f,0),new Vector3(14,height,12),facade,true);
            Box(group,"House foundation trim",new Vector3(0,.3f,0),new Vector3(14.4f,.35f,12.4f),stone,false);
            Box(group,"Residence entry door",new Vector3(0,1.35f,-6.06f),new Vector3(1.45f,2.5f,.12f),wood,false);
            Box(group,"Entry door handle",new Vector3(.48f,1.35f,-6.14f),new Vector3(.055f,.3f,.065f),cream,false);
            Window(group,new Vector3(-4,1.85f,-6.12f),2.3f,1.65f);Window(group,new Vector3(4,1.85f,-6.12f),2.3f,1.65f);
            Box(group,"Porch terrace",new Vector3(0,.1f,-6.9f),new Vector3(5.2f,.16f,1.8f),stone,true);
            Box(group,"Porch canopy",new Vector3(0,3.1f,-6.8f),new Vector3(5.6f,.2f,2.2f),cream,false);
            foreach(int side in new[]{-1,1})Box(group,"Porch post",new Vector3(side*2.4f,1.55f,-7.5f),new Vector3(.14f,3,.14f),wood,true);
            if(style==0||style==2)
            {
                foreach(int side in new[]{-1,1}){var roof=Box(group,"Pitched roof plane",new Vector3(side*3.65f,height+.8f,0),new Vector3(7.8f,.22f,13.2f),style==0?coral:ink,false);roof.transform.localRotation=Quaternion.Euler(0,0,side*-14);}
                Box(group,"Roof ridge",new Vector3(0,height+1.7f,0),new Vector3(.3f,.22f,13.3f),cream,false);
            }
            else
            {
                Box(group,"Flat roof cornice",new Vector3(0,height+.15f,0),new Vector3(14.7f,.3f,12.7f),cream,false);
                Window(group,new Vector3(-4,5,-6.12f),2.3f,1.9f);Window(group,new Vector3(4,5,-6.12f),2.3f,1.9f);
                Balcony(group,new Vector3(0,3.55f,-6.8f),6);
                Box(group,"Upper balcony door",new Vector3(0,4.8f,-6.12f),new Vector3(1.9f,2.25f,.12f),glass,false);
                for(int i=0;i<3;i++)Box(group,"Roof vent",new Vector3(-4+i*4,height+.55f,2),new Vector3(1,.6f,1.5f),ink,false);
            }
            if(style==3){Box(group,"Raised corner wall",new Vector3(4,7.35f,2),new Vector3(6,1.1f,8),cream,true);Box(group,"Raised corner cornice",new Vector3(4,8,2),new Vector3(6.4f,.25f,8.4f),cream,false);}
            foreach(float x in new[]{-4f,4f})
            {
                Box(group,"Rear window surround",new Vector3(x,1.85f,6.06f),new Vector3(2.5f,1.85f,.18f),cream,false);
                Box(group,"Rear window",new Vector3(x,1.85f,6.2f),new Vector3(2.25f,1.6f,.06f),glass,false);
                if(style==1||style==3){Box(group,"Rear upper window surround",new Vector3(x,5,6.06f),new Vector3(2.5f,2.1f,.18f),cream,false);Box(group,"Rear upper window",new Vector3(x,5,6.2f),new Vector3(2.25f,1.85f,.06f),glass,false);}
            }
            Box(group,"Rear patio door",new Vector3(0,1.4f,6.1f),new Vector3(1.5f,2.6f,.12f),wood,false);
            Box(group,"Rear patio terrace",new Vector3(0,.1f,6.8f),new Vector3(4.5f,.16f,1.6f),stone,true);
            // Side windows make the buildings readable while walking between lots.
            foreach(int side in new[]{-1,1})foreach(float z in new[]{-2.8f,2.8f}){Box(group,"Side window surround",new Vector3(side*7.04f,1.8f,z),new Vector3(.13f,1.8f,2.25f),cream,false);Box(group,"Side window",new Vector3(side*7.12f,1.8f,z),new Vector3(.04f,1.5f,1.95f),glass,false);}
            Sign(group,"0"+(style+1),new Vector3(.95f,2.65f,-6.2f),.19f,.7f,cream.color);
            group.position=position;group.rotation=Quaternion.Euler(0,yaw,0);
        }
        private static void Apartment(Transform root,Vector3 p)
        {
            Box(root,"Small apartment physical shell",p+Vector3.up*5.3f,new Vector3(18,10.4f,14),mint,true);
            Box(root,"Apartment cornice",p+Vector3.up*10.65f,new Vector3(18.8f,.35f,14.8f),cream,false);
            for(int floor=0;floor<3;floor++)foreach(int side in new[]{-1,1}){var pos=p+new Vector3(side*4.5f,2+floor*3.1f,-7.12f);Window(root,pos,2.8f,2);Balcony(root,pos+new Vector3(0,-1.05f,-.7f),3.9f);}
            for(int floor=0;floor<3;floor++)
            {
                foreach(int side in new[]{-1,1})foreach(float z in new[]{-3f,3f}){Box(root,"Apartment side window frame",p+new Vector3(side*9.06f,2+floor*3.1f,z),new Vector3(.15f,2.1f,2.5f),cream,false);Box(root,"Apartment side window",p+new Vector3(side*9.17f,2+floor*3.1f,z),new Vector3(.05f,1.85f,2.25f),glass,false);}
                foreach(float x in new[]{-6f,0f,6f}){Box(root,"Apartment rear window frame",p+new Vector3(x,2+floor*3.1f,7.06f),new Vector3(2.6f,2.1f,.15f),cream,false);Box(root,"Apartment rear window",p+new Vector3(x,2+floor*3.1f,7.17f),new Vector3(2.35f,1.85f,.05f),glass,false);}
            }
            Box(root,"Apartment entry",p+new Vector3(0,1.6f,-7.08f),new Vector3(2.2f,3,.16f),wood,false);
            Box(root,"Apartment entry canopy",p+new Vector3(0,3.1f,-8),new Vector3(5.5f,.2f,2.4f),cream,false);
            Sign(root,"GROVE COURT",p+new Vector3(0,3.6f,-7.2f),.26f,8,cream.color);
        }
        private static void Window(Transform root,Vector3 p,float width,float height)
        {Box(root,"Window surround",p,new Vector3(width+.2f,height+.2f,.22f),cream,false);Box(root,"Window glass",p+Vector3.back*.14f,new Vector3(width,height,.06f),glass,false);Box(root,"Window sill",p+new Vector3(0,-height*.5f-.1f,-.08f),new Vector3(width+.35f,.13f,.35f),cream,false);Box(root,"Window mullion",p+Vector3.back*.19f,new Vector3(.05f,height,.04f),cream,false);}
        private static void Balcony(Transform root,Vector3 p,float width)
        {Box(root,"Balcony floor",p,new Vector3(width,.18f,1.8f),cream,false);Box(root,"Balcony handrail",p+new Vector3(0,1,-.85f),new Vector3(width,.075f,.075f),ink,false);for(int i=0;i<=12;i++)Box(root,"Balcony baluster",p+new Vector3(-width*.5f+i*width/12,.5f,-.85f),new Vector3(.035f,1,.035f),ink,false);}
        private static void Lot(Transform root,float x,float z,bool south)
        {
            float front=south?79.5f:99.5f,rear=south?52:124;float mid=(front+rear)*.5f;
            foreach(int side in new[]{-1,1}){Box(root,"Garden boundary wall",new Vector3(x+side*12.4f,.4f,mid),new Vector3(.22f,.65f,Mathf.Abs(rear-front)),cream,true);Box(root,"Front garden wall",new Vector3(x+side*7.6f,.4f,front),new Vector3(9.5f,.65f,.22f),cream,true);}
            Box(root,"Rear garden wall",new Vector3(x,.4f,rear),new Vector3(25,.65f,.22f),cream,true);
            foreach(int side in new[]{-1,1}){var p=new Vector3(x+side*9.2f,0,south?77.7f:101.5f);Box(root,"Garden raised bed",p+Vector3.up*.2f,new Vector3(3.8f,.25f,1.2f),stone,true);for(int i=0;i<5;i++)Box(root,"Garden shrub",p+new Vector3(-1.5f+i*.75f,.55f,0),new Vector3(.85f,.7f,.85f),leaf,false,PrimitiveType.Sphere);}
        }
        private static void Playground(Transform root)
        {
            Paving(root,Rect.MinMaxRect(308,50,339,78),.09f,rubber);
            foreach(float x in new[]{308f,339f})Box(root,"Playground low fence",new Vector3(x,.6f,64),new Vector3(.15f,1,28),cream,true);
            Box(root,"Playground rear fence",new Vector3(323.5f,.6f,50),new Vector3(31,1,.15f),cream,true);
            foreach(int side in new[]{-1,1})Box(root,"Playground front fence",new Vector3(323.5f+side*9.25f,.6f,78),new Vector3(12.5f,1,.15f),cream,true);
            Paving(root,Rect.MinMaxRect(321,78,326,85.96f),.09f,stone);
            foreach(float x in new[]{314f,321f})foreach(int side in new[]{-1,1}){var leg=Box(root,"Swing A-frame",new Vector3(x,1.7f,59+side*.9f),new Vector3(.13f,3.5f,.13f),coral,true);leg.transform.rotation=Quaternion.Euler(side*16,0,0);}
            Box(root,"Swing crossbar",new Vector3(317.5f,3.35f,59),new Vector3(7.5f,.14f,.14f),coral,true);
            foreach(float x in new[]{316f,319f}){foreach(int side in new[]{-1,1})Box(root,"Swing chain",new Vector3(x+side*.4f,1.95f,59),new Vector3(.035f,2.5f,.035f),ink,false);Box(root,"Swing seat",new Vector3(x,.7f,59),new Vector3(1.1f,.13f,.5f),wood,true);}
            Box(root,"Play tower",new Vector3(331,1,60),new Vector3(2.5f,1.8f,2.5f),mint,true);
            foreach(int side in new[]{-1,1})Box(root,"Play tower roof post",new Vector3(331+side,2.3f,60),new Vector3(.12f,1.5f,.12f),wood,true);
            Box(root,"Play tower canopy",new Vector3(331,3.1f,60),new Vector3(3.3f,.2f,3.3f),coral,false);
            var slide=Box(root,"Playground slide",new Vector3(331,1,63.3f),new Vector3(1.3f,.14f,5),cream,true);slide.transform.rotation=Quaternion.Euler(21,0,0);
            Bench(root,new Vector3(312,0,72));Bench(root,new Vector3(334,0,72));
            Box(root,"Playground sandbox",new Vector3(314,.2f,68),new Vector3(5.5f,.22f,4),wood,true);
            Box(root,"Sandbox sand",new Vector3(314,.32f,68),new Vector3(5.2f,.025f,3.7f),cream,false);
            for(int i=0;i<7;i++)Box(root,"Playground hedge",new Vector3(309,.55f,53+i*3.4f),new Vector3(.9f,.85f,1.8f),leaf,false,PrimitiveType.Sphere);
            for(int i=0;i<6;i++)Box(root,"Hopscotch marking",new Vector3(326,.1f,53+i*.8f),new Vector3(.85f,.008f,.65f),i%2==0?cream:coral,false);
            Sign(root,"PALM GROVE PLAYGROUND",new Vector3(323.5f,1.25f,77.8f),.28f,13,cream.color);
        }
        private static void Bench(Transform root,Vector3 p){Box(root,"Playground bench seat",p+Vector3.up*.6f,new Vector3(2.8f,.14f,.6f),wood,true);Box(root,"Playground bench back",p+new Vector3(0,1,.3f),new Vector3(2.8f,.65f,.1f),wood,true);foreach(int side in new[]{-1,1})Box(root,"Playground bench support",p+new Vector3(side, .3f,0),new Vector3(.12f,.6f,.5f),ink,true);}
        private static void Lantern(Transform root,Vector3 p)
        {Box(root,"Residential lantern post",p+Vector3.up*1.8f,new Vector3(.12f,3.5f,.12f),ink,true);var bulb=Box(root,"Residential lantern diffuser",p+Vector3.up*3.55f,new Vector3(.4f,.3f,.4f),glow,false);bulbs.Add(bulb.GetComponent<Renderer>());lamps.Add(bulb.transform);Box(root,"Lantern cap",p+Vector3.up*3.8f,new Vector3(.6f,.13f,.6f),ink,false);}
        private static void RelocateApproachLamps(Transform motel)
        {
            var renderers=motel.GetComponentsInChildren<MeshRenderer>(true);
            foreach(float oldZ in new[]{80.2f,99.8f})
            {
                var target=new Vector3(239.8f,0,oldZ<90?77:103);
                foreach(var part in renderers){if(!part.gameObject.activeInHierarchy||(part.name!="Streetlight post"&&part.name!="Streetlight bulb"))continue;var p=part.bounds.center;if(Mathf.Abs(p.x-243)<.02f&&Mathf.Abs(p.z-oldZ)<.02f)part.transform.position+=new Vector3(target.x-p.x,0,target.z-p.z);}
            }
        }
        private static void RegisterLights(MotelGameRoot motel)
        {var light=motel.GetComponentsInChildren<WorldNightLighting>().FirstOrDefault();if(light==null)return;var so=new SerializedObject(light);var points=so.FindProperty("fixtures");var surfaces=so.FindProperty("glowingSurfaces");int old=points.arraySize;points.arraySize+=lamps.Count;for(int i=0;i<lamps.Count;i++)points.GetArrayElementAtIndex(old+i).objectReferenceValue=lamps[i];old=surfaces.arraySize;surfaces.arraySize+=bulbs.Count;for(int i=0;i<bulbs.Count;i++)surfaces.GetArrayElementAtIndex(old+i).objectReferenceValue=bulbs[i];so.ApplyModifiedPropertiesWithoutUndo();}
        private static void Paving(Transform root,Rect r,float y,Material mat){Box(root,"Flat residential paving",new Vector3(r.center.x,y-.08f,r.center.y),new Vector3(r.width,.16f,r.height),mat,true);}
        private static void Plane(Transform root,string name,Rect r,float y,Material mat){var v=new[]{new Vector3(r.xMin,y,r.yMin),new Vector3(r.xMax,y,r.yMin),new Vector3(r.xMax,y,r.yMax),new Vector3(r.xMin,y,r.yMax)};var mesh=new Mesh{name=name,vertices=v,triangles=new[]{0,2,1,0,3,2},uv=v.Select(p=>new Vector2(p.x*.4f,p.z*.4f)).ToArray()};mesh.RecalculateNormals();Part(root,name,mesh,mat);}
        private static GameObject Box(Transform root,string name,Vector3 p,Vector3 size,Material mat,bool solid,PrimitiveType shape=PrimitiveType.Cube){var go=GameObject.CreatePrimitive(shape);go.name=name;go.transform.SetParent(root,false);go.transform.localPosition=p;go.transform.localScale=size;go.GetComponent<Renderer>().sharedMaterial=mat;UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());if(solid)go.AddComponent<BoxCollider>();return go;}
        private static void Sign(Transform root,string text,Vector3 p,float size,float width,Color color){var label=new GameObject(text).AddComponent<TextMeshPro>();label.transform.SetParent(root,false);label.transform.localPosition=p;label.font=TMP_Settings.GetFontAsset();label.text=text;label.fontSize=size*10;label.color=color;label.alignment=TextAlignmentOptions.Center;label.rectTransform.sizeDelta=new Vector2(width,size*3);label.raycastTarget=false;}
        private static Material Load(string name){var mat=AssetDatabase.LoadAssetAtPath<Material>(MotelDistrictArtBuilder.Folder+"/"+name+".mat");if(mat==null)throw new InvalidOperationException("Missing coastal material "+name);return mat;}
        private static Material Mat(string name,Color color,string texture){string path=Folder+"/"+name+".mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(path);if(mat==null){mat=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(mat,path);}mat.color=color;mat.SetFloat("_Smoothness",.2f);mat.enableInstancing=true;if(texture!=null){mat.mainTexture=AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_Game/Art/Surfaces/"+texture+"_Albedo.png");mat.SetTexture("_BumpMap",AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_Game/Art/Surfaces/"+texture+"_Normal.png"));mat.EnableKeyword("_NORMALMAP");mat.SetFloat("_BumpScale",.25f);}EditorUtility.SetDirty(mat);return mat;}
        private static void Part(Transform root,string name,Mesh mesh,Material mat){var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(root,false);go.GetComponent<MeshFilter>().sharedMesh=mesh;go.GetComponent<MeshRenderer>().sharedMaterial=mat;}
        private static void Combine(Transform root)
        {
            int index=0;var filters=root.GetComponentsInChildren<MeshFilter>().Where(f=>f.GetComponent<TMP_Text>()==null&&!TownPopulationController.RoadName(f.name)&&!bulbs.Contains(f.GetComponent<Renderer>())).ToArray();
            foreach(var group in filters.GroupBy(f=>f.GetComponent<MeshRenderer>().sharedMaterial))
            {var parts=group.ToArray();var mesh=new Mesh{name="Residences "+group.Key.name,indexFormat=IndexFormat.UInt32};mesh.CombineMeshes(parts.Select(f=>new CombineInstance{mesh=f.sharedMesh,transform=root.worldToLocalMatrix*f.transform.localToWorldMatrix}).ToArray(),true,true);mesh.RecalculateBounds();string path=Folder+"/Residences"+(index++)+".asset";var saved=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(saved==null)AssetDatabase.CreateAsset(mesh,path);else{EditorUtility.CopySerialized(mesh,saved);saved.UploadMeshData(false);UnityEngine.Object.DestroyImmediate(mesh);mesh=saved;}Part(root,mesh.name,mesh,group.Key);foreach(var f in parts){UnityEngine.Object.DestroyImmediate(f.GetComponent<MeshRenderer>());UnityEngine.Object.DestroyImmediate(f);}}
        }
    }
}