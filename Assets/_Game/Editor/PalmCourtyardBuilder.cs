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
    /// <summary>Fills the vacant rear-east plot with a walkable courtyard park, preserving its neighbouring residence and public roads.</summary>
    public static class PalmCourtyardBuilder
    {
        public const string Layer="Palm Courtyard Park v1";
        public const string Folder="Assets/_Game/Generated/PalmCourtyard";
        public static readonly Vector3 Center=new Vector3(149.5f,0,61);
        private static Material cream,coral,mint,ink,wood,leaf,stone,grass,water,glow,skin;
        private static readonly List<Renderer> bulbs=new List<Renderer>();
        private static readonly List<Transform> lamps=new List<Transform>();
        [MenuItem("Roadside Haven/Design/Build Palm Courtyard Park")]
        public static void BuildCurrent()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)return;var motel=UnityEngine.Object.FindFirstObjectByType<MotelGameRoot>();if(motel==null)return;
            string path=motel.gameObject.scene.path;if(File.Exists(path)){string backup="Backups/PalmCourtyard/"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");Directory.CreateDirectory(backup);File.Copy(path,backup+"/"+System.IO.Path.GetFileName(path));}
            Build(motel);EditorSceneManager.MarkSceneDirty(motel.gameObject.scene);EditorSceneManager.SaveScene(motel.gameObject.scene);AssetDatabase.SaveAssets();
        }
        [MenuItem("Roadside Haven/Design/Focus Palm Courtyard Park")]
        public static void Focus()
        {
            var motel=UnityEngine.Object.FindFirstObjectByType<MotelGameRoot>();var park=motel==null?null:motel.transform.Find(Layer);if(park==null)return;
            Selection.activeGameObject=park.gameObject;if(SceneView.lastActiveSceneView!=null)SceneView.lastActiveSceneView.Frame(new Bounds(Center+Vector3.up*2,new Vector3(65,14,57)),false);
        }
        public static Transform Build(MotelGameRoot motel)
        {
            var existing=motel.transform.Find(Layer);if(existing!=null)return existing;
            if(!AssetDatabase.IsValidFolder(Folder))AssetDatabase.CreateFolder("Assets/_Game/Generated","PalmCourtyard");
            cream=Load("Warm stucco");coral=Load("Sunset coral");mint=Load("Seafoam stucco");ink=Load("Blue graphite");wood=Load("Oiled cedar");leaf=Load("Palm foliage");
            stone=Mat("Park travertine",new Color(.76f,.7f,.56f),"Paving");grass=Mat("Park lawn",new Color(.62f,.78f,.55f),"BaseGrass");glow=Mat("Park lantern glass",new Color(1,.8f,.5f),null);skin=Mat("Park citizen skin",new Color(.63f,.42f,.29f),null);
            water=Mat("Fountain water",new Color(.12f,.6f,.67f,.75f),null);water.SetFloat("_Smoothness",.9f);water.SetFloat("_Surface",1);water.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);water.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);water.SetFloat("_ZWrite",0);water.SetFloat("_Cull",0);water.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");water.renderQueue=3000;water.SetShaderPassEnabled("ShadowCaster",false);water.mainTexture=null;water.SetTexture("_BumpMap",AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_Game/Art/Water/LakeWaveNormal.png"));water.SetFloat("_BumpScale",.3f);water.EnableKeyword("_NORMALMAP");
            var root=new GameObject(Layer).transform;root.SetParent(motel.transform,false);root.gameObject.AddComponent<MotelDistrictArt>();var life=root.gameObject.AddComponent<PocketParkLife>();bulbs.Clear();lamps.Clear();
            Box(root,"Park foundation",new Vector3(149.5f,-.045f,61),new Vector3(31,.2f,40),stone,true);
            Plane(root,"Textured park lawn",Rect.MinMaxRect(134,41,165,81),.058f,grass,true);
            Ring(root,"Curved walking loop",Center,8.7f,12.2f,11.3f,14.8f,.066f,stone);
            Ring(root,"Fountain courtyard",Center,4.6f,4.6f,6.2f,6.2f,.068f,stone);
            Path(root,Rect.MinMaxRect(148,34.9f,151,49),true);
            Path(root,Rect.MinMaxRect(148,73,151,81),false);
            Path(root,Rect.MinMaxRect(116.05f,59.5f,144,62.5f),true);
            Path(root,Rect.MinMaxRect(155,59.5f,168.9f,62.5f),true);
            Path(root,Rect.MinMaxRect(148.2f,47,150.8f,55),false);Path(root,Rect.MinMaxRect(148.2f,67,150.8f,75),false);
            Fountain(root);
            Perimeter(root);Pergola(root);Kiosk(root);
            foreach(var p in new[]{new Vector3(136.3f,0,44.3f),new Vector3(162.6f,0,44.3f),new Vector3(136.3f,0,67),new Vector3(162.6f,0,69),new Vector3(162.2f,0,77.8f)}){CoastalCityBlockBuilder.AddPromenadePalm(root,p);Garden(root,p+new Vector3(0,0,1.3f),2.5f,1.2f);}
            foreach(var p in new[]{new Vector3(136.3f,0,54),new Vector3(162.5f,0,54),new Vector3(143.2f,0,44),new Vector3(155.8f,0,44)})Garden(root,p,3.6f,1.5f);
            Bench(root,new Vector3(136.8f,0,56.5f),90);Bench(root,new Vector3(162.3f,0,56.5f),-90);Bench(root,new Vector3(142.5f,0,46),0);Bench(root,new Vector3(156.5f,0,46),0);
            foreach(var p in new[]{new Vector3(137.4f,0,49),new Vector3(161.6f,0,49),new Vector3(137.4f,0,72),new Vector3(161.6f,0,72),new Vector3(145.8f,0,42.4f),new Vector3(153.2f,0,42.4f)})Lantern(root,p);
            Box(root,"Park entrance plaque",new Vector3(155,1.45f,41.2f),new Vector3(5.4f,1.3f,.18f),ink,false);
            Sign(root,"PALM COURTYARD",new Vector3(155,1.63f,41.08f),.29f,5,cream.color);Sign(root,"PARK  /  CAFE  /  GARDENS",new Vector3(155,1.16f,41.08f),.14f,5,cream.color);
            var route=new Vector3[32];for(int i=0;i<route.Length;i++){float a=i*Mathf.PI*2/route.Length;route[i]=Center+new Vector3(Mathf.Cos(a)*10,.1f,Mathf.Sin(a)*13.5f);}
            var people=new TownCitizenAgent[3];for(int i=0;i<people.Length;i++){var go=new GameObject(i==2?"Park jogger":"Park visitor "+i);go.transform.SetParent(root,false);go.transform.position=route[i*10];var cc=go.AddComponent<CharacterController>();cc.enabled=false;people[i]=go.AddComponent<TownCitizenAgent>();}
            var waterRenderer=Combine(root);life.Configure(waterRenderer,people,route,new[]{coral,mint,ink},skin);RegisterNightFixtures(motel);
            foreach(var text in root.GetComponentsInChildren<TMP_Text>())text.ForceMeshUpdate(true,true);AssetDatabase.SaveAssets();return root;
        }
        private static void Path(Transform root,Rect rect,bool support)
        {
            if(support)Box(root,"Park access foundation",new Vector3(rect.center.x,-.045f,rect.center.y),new Vector3(rect.width,.2f,rect.height),stone,true);
            Plane(root,"Connected park paving",rect,.07f,stone,false);
        }
        private static void Fountain(Transform root)
        {
            var profile=new[]{new Vector2(3.65f,.35f),new Vector2(3.65f,.68f),new Vector2(3.8f,.8f),new Vector2(4.1f,.8f),new Vector2(4.25f,.65f),new Vector2(4.25f,.22f),new Vector2(4.1f,.16f),new Vector2(3.65f,.16f)};
            var vertices=new List<Vector3>();var triangles=new List<int>();const int sides=72;
            for(int i=0;i<=sides;i++){float a=i*Mathf.PI*2/sides;foreach(var p in profile)vertices.Add(new Vector3(Mathf.Cos(a)*p.x,p.y,Mathf.Sin(a)*p.x));}
            for(int i=0;i<sides;i++)for(int j=0;j<profile.Length;j++){int a=i*profile.Length+j,b=i*profile.Length+(j+1)%profile.Length;triangles.AddRange(new[]{a,a+profile.Length,b,b,a+profile.Length,b+profile.Length});}
            Mesh(root,"Rounded stone fountain rim",Center,vertices,triangles,cream);
            for(int i=0;i<24;i++){float a=i*Mathf.PI/12;var rim=Box(root,"Fountain rim collision",Center+new Vector3(Mathf.Cos(a)*3.98f,.48f,Mathf.Sin(a)*3.98f),new Vector3(1.1f,.65f,.5f),stone,true);UnityEngine.Object.DestroyImmediate(rim.GetComponent<Renderer>());UnityEngine.Object.DestroyImmediate(rim.GetComponent<MeshFilter>());rim.transform.rotation=Quaternion.Euler(0,90-i*15,0);}
            var basin=Box(root,"Fountain basin floor",Center+Vector3.up*.18f,new Vector3(7.4f,.22f,7.4f),stone,false,PrimitiveType.Cylinder);basin.AddComponent<MeshCollider>().sharedMesh=basin.GetComponent<MeshFilter>().sharedMesh;
            Ring(root,"Shallow fountain water",Center,.0f,.0f,3.65f,3.65f,.59f,water);
            foreach(var data in new[]{new Vector3(1.6f,.4f,1.6f),new Vector3(1.05f,1.1f,1.05f),new Vector3(1.8f,.2f,1.8f)})
            {
                float y=data.y==1.1f?.8f:data.y==.2f?1.4f:.4f;Box(root,"Fountain pedestal",Center+Vector3.up*y,new Vector3(data.x,data.y*.5f,data.z),cream,false,PrimitiveType.Cylinder);
            }
            for(int jet=0;jet<8;jet++)
            {
                float a=jet*Mathf.PI/4;var direction=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));var v=new List<Vector3>();var t=new List<int>();
                for(int row=0;row<=20;row++){float f=row/20f;var p=direction*(.4f+f*2.7f)+Vector3.up*(1.55f+Mathf.Sin(f*Mathf.PI)*1.8f-f*.95f);for(int side=0;side<8;side++){float angle=side*Mathf.PI/4;v.Add(p+new Vector3(Mathf.Cos(angle)*.065f,Mathf.Sin(angle)*.065f,0));if(row>0){int n=row*8+side,q=row*8+(side+1)%8;t.AddRange(new[]{n-8,q-8,n,q-8,q,n});}}}
                Mesh(root,"Fountain flowing jet",Center,v,t,water);
            }
        }
        private static void Perimeter(Transform root)
        {
            foreach(float x in new[]{134.2f,164.8f})foreach(var span in new[]{new Vector2(41,58.6f),new Vector2(63.4f,81)})Box(root,"Park low perimeter wall",new Vector3(x,.32f,(span.x+span.y)*.5f),new Vector3(.35f,.5f,span.y-span.x),cream,true);
            foreach(float z in new[]{41.2f,80.8f})foreach(var span in new[]{new Vector2(134,146.8f),new Vector2(152.2f,165)})Box(root,"Park low perimeter wall",new Vector3((span.x+span.y)*.5f,.32f,z),new Vector3(span.y-span.x,.5f,.35f),cream,true);
            foreach(var p in new[]{new Vector3(146.4f,0,41.2f),new Vector3(152.6f,0,41.2f),new Vector3(134.2f,0,58.2f),new Vector3(134.2f,0,63.8f),new Vector3(164.8f,0,58.2f),new Vector3(164.8f,0,63.8f)}){Box(root,"Park gate pier",p+Vector3.up*.65f,new Vector3(.65f,1.2f,.65f),coral,true);Box(root,"Pier cap",p+Vector3.up*1.3f,new Vector3(.8f,.12f,.8f),cream,false);}
        }
        private static void Pergola(Transform root)
        {
            var p=new Vector3(149.5f,0,78);Plane(root,"Pergola terrace",Rect.MinMaxRect(145,75.8f,154,80),.075f,stone,false);
            foreach(int side in new[]{-1,1})foreach(int end in new[]{-1,1})Box(root,"Pergola post",p+new Vector3(side*3.8f,1.65f,end*1.5f),new Vector3(.22f,3.2f,.22f),wood,true);
            foreach(int side in new[]{-1,1})Box(root,"Pergola beam",p+new Vector3(0,3.15f,side*1.5f),new Vector3(8.8f,.25f,.18f),wood,false);
            for(int i=0;i<13;i++)Box(root,"Pergola shade slat",p+new Vector3(-4.2f+i*.7f,3.4f,0),new Vector3(.16f,.18f,4.2f),wood,false);
            Bench(root,p+Vector3.left*2.6f,90);Bench(root,p+Vector3.right*2.6f,-90);
        }
        private static void Kiosk(Transform root)
        {
            var p=new Vector3(137.8f,0,76.5f);
            Box(root,"Cafe kiosk shell",p+Vector3.up*1.65f,new Vector3(5.5f,3.2f,4),mint,true);
            Box(root,"Cafe kiosk roof",p+Vector3.up*3.35f,new Vector3(6,.22f,4.5f),cream,false);
            Box(root,"Kiosk serving window",p+new Vector3(0,1.9f,-2.03f),new Vector3(3.8f,1.2f,.1f),ink,false);
            Box(root,"Kiosk counter",p+new Vector3(0,1.15f,-2.2f),new Vector3(4.1f,.13f,.6f),wood,false);
            Sign(root,"PARK CAFE",p+new Vector3(0,2.95f,-2.15f),.35f,4.5f,cream.color);
            for(int i=0;i<10;i++){var awning=Box(root,"Kiosk striped awning",p+new Vector3(-2.25f+i*.5f,2.6f,-2.7f),new Vector3(.5f,.08f,1.5f),i%2==0?coral:cream,false);awning.transform.rotation=Quaternion.Euler(-12,0,0);}
        }
        private static void Bench(Transform root,Vector3 p,float yaw)
        {
            var group=new GameObject("Park bench").transform;group.SetParent(root,false);group.position=p;group.rotation=Quaternion.Euler(0,yaw,0);
            foreach(int side in new[]{-1,1}){LocalBox(group,"Bench support",new Vector3(side*.85f,.35f,0),new Vector3(.12f,.6f,.65f),ink,true);LocalBox(group,"Bench arm",new Vector3(side*1.1f,.9f,0),new Vector3(.1f,.08f,.65f),ink,false);}
            LocalBox(group,"Bench seat collision",new Vector3(0,.65f,0),new Vector3(2.4f,.12f,.65f),wood,true);
            for(int i=0;i<4;i++)LocalBox(group,"Bench back slat",new Vector3(0,.83f+i*.13f,.3f),new Vector3(2.4f,.09f,.07f),wood,i==0);
        }
        private static void Garden(Transform root,Vector3 p,float width,float depth)
        {
            Box(root,"Garden planter border",p+Vector3.up*.2f,new Vector3(width,.28f,depth),cream,true);
            for(int i=0;i<8;i++){float x=-width*.4f+(i%4)*width*.27f,z=-depth*.3f+(i/4)*depth*.6f;Box(root,"Low garden foliage",p+new Vector3(x,.55f,z),new Vector3(width*.28f,.55f,depth*.55f),leaf,false,PrimitiveType.Sphere);}
        }
        private static void Lantern(Transform root,Vector3 p)
        {
            Box(root,"Park lantern post",p+Vector3.up*1.25f,new Vector3(.1f,2.4f,.1f),ink,true);
            var bulb=Box(root,"Park lantern diffuser",p+Vector3.up*2.5f,new Vector3(.32f,.38f,.32f),glow,false);bulbs.Add(bulb.GetComponent<Renderer>());lamps.Add(bulb.transform);
            Box(root,"Park lantern cap",p+Vector3.up*2.76f,new Vector3(.5f,.12f,.5f),wood,false);
        }
        private static void RegisterNightFixtures(MotelGameRoot motel)
        {
            var lighting=motel.GetComponentsInChildren<WorldNightLighting>().FirstOrDefault();if(lighting==null)return;
            var so=new SerializedObject(lighting);var points=so.FindProperty("fixtures");var surfaces=so.FindProperty("glowingSurfaces");int old=points.arraySize;points.arraySize+=lamps.Count;for(int i=0;i<lamps.Count;i++)points.GetArrayElementAtIndex(old+i).objectReferenceValue=lamps[i];old=surfaces.arraySize;surfaces.arraySize+=bulbs.Count;for(int i=0;i<bulbs.Count;i++)surfaces.GetArrayElementAtIndex(old+i).objectReferenceValue=bulbs[i];so.ApplyModifiedPropertiesWithoutUndo();EditorUtility.SetDirty(lighting);
        }
        private static void Plane(Transform root,string name,Rect rect,float y,Material mat,bool worldUv)
        {
            var v=new List<Vector3>{new Vector3(rect.xMin,y,rect.yMin),new Vector3(rect.xMax,y,rect.yMin),new Vector3(rect.xMax,y,rect.yMax),new Vector3(rect.xMin,y,rect.yMax)};Mesh(root,name,Vector3.zero,v,new List<int>{0,2,1,0,3,2},mat);
        }
        private static void Ring(Transform root,string name,Vector3 center,float innerX,float innerZ,float outerX,float outerZ,float y,Material mat)
        {
            var v=new List<Vector3>();var t=new List<int>();for(int i=0;i<=96;i++){float a=i*Mathf.PI/48;v.Add(new Vector3(Mathf.Cos(a)*innerX,y,Mathf.Sin(a)*innerZ));v.Add(new Vector3(Mathf.Cos(a)*outerX,y,Mathf.Sin(a)*outerZ));if(i>0){int n=i*2;t.AddRange(new[]{n-2,n,n-1,n-1,n,n+1});}}Mesh(root,name,center,v,t,mat);
        }
        private static void Mesh(Transform root,string name,Vector3 p,List<Vector3> v,List<int> t,Material mat)
        {
            var mesh=new UnityEngine.Mesh{name=name};mesh.SetVertices(v);mesh.SetTriangles(t,0);mesh.SetUVs(0,v.Select(a=>new Vector2((a.x+p.x)*.4f,(a.z+p.z)*.4f)).ToList());mesh.RecalculateNormals();mesh.RecalculateBounds();Part(root,name,p,mesh,mat);
        }
        private static GameObject Box(Transform root,string name,Vector3 p,Vector3 size,Material mat,bool solid,PrimitiveType shape=PrimitiveType.Cube){var go=GameObject.CreatePrimitive(shape);go.name=name;go.transform.SetParent(root,false);go.transform.position=p;go.transform.localScale=size;go.GetComponent<Renderer>().sharedMaterial=mat;UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());if(solid)go.AddComponent<BoxCollider>();return go;}
        private static void LocalBox(Transform root,string name,Vector3 p,Vector3 size,Material mat,bool solid){var go=Box(root,name,Vector3.zero,size,mat,solid);go.transform.localPosition=p;}
        private static void Part(Transform root,string name,Vector3 p,UnityEngine.Mesh mesh,Material mat){var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(root,false);go.transform.position=p;go.GetComponent<MeshFilter>().sharedMesh=mesh;go.GetComponent<MeshRenderer>().sharedMaterial=mat;}
        private static void Sign(Transform root,string text,Vector3 p,float size,float width,Color color){var label=new GameObject(text).AddComponent<TextMeshPro>();label.transform.SetParent(root,false);label.transform.position=p;label.font=TMP_Settings.GetFontAsset();label.text=text;label.fontSize=size*10;label.color=color;label.alignment=TextAlignmentOptions.Center;label.rectTransform.sizeDelta=new Vector2(width,size*3);label.raycastTarget=false;}
        private static Material Load(string name){var mat=AssetDatabase.LoadAssetAtPath<Material>(MotelDistrictArtBuilder.Folder+"/"+name+".mat");if(mat==null)throw new InvalidOperationException("Missing coastal art material "+name);return mat;}
        private static Material Mat(string name,Color color,string texture)
        {string path=Folder+"/"+name+".mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(path);if(mat==null){mat=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(mat,path);}mat.color=color;mat.SetFloat("_Smoothness",.2f);mat.enableInstancing=true;if(texture!=null){mat.mainTexture=AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_Game/Art/Surfaces/"+texture+"_Albedo.png");mat.SetTexture("_BumpMap",AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_Game/Art/Surfaces/"+texture+"_Normal.png"));mat.EnableKeyword("_NORMALMAP");mat.SetFloat("_BumpScale",.3f);}EditorUtility.SetDirty(mat);return mat;}
        private static Renderer Combine(Transform root)
        {
            Renderer waterRenderer=null;int index=0;var filters=root.GetComponentsInChildren<MeshFilter>().Where(f=>f.GetComponent<TMP_Text>()==null&&!bulbs.Contains(f.GetComponent<Renderer>())&&f.GetComponent<Renderer>().enabled).ToArray();
            foreach(var group in filters.GroupBy(f=>f.GetComponent<MeshRenderer>().sharedMaterial))
            {
                var parts=group.ToArray();var mesh=new UnityEngine.Mesh{name="Park "+group.Key.name,indexFormat=IndexFormat.UInt32};mesh.CombineMeshes(parts.Select(f=>new CombineInstance{mesh=f.sharedMesh,transform=root.worldToLocalMatrix*f.transform.localToWorldMatrix}).ToArray(),true,true);mesh.RecalculateBounds();string path=Folder+"/Park"+(index++)+".asset";var saved=AssetDatabase.LoadAssetAtPath<UnityEngine.Mesh>(path);if(saved==null)AssetDatabase.CreateAsset(mesh,path);else{EditorUtility.CopySerialized(mesh,saved);UnityEngine.Object.DestroyImmediate(mesh);mesh=saved;}Part(root,mesh.name,Vector3.zero,mesh,group.Key);if(group.Key==water)waterRenderer=root.GetChild(root.childCount-1).GetComponent<Renderer>();
                foreach(var f in parts){UnityEngine.Object.DestroyImmediate(f.GetComponent<MeshRenderer>());UnityEngine.Object.DestroyImmediate(f);}
            }
            return waterRenderer;
        }
    }
}