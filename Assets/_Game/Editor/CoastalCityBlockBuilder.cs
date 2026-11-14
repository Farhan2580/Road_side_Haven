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
    /// <summary>First east-side mixed-use block. Bakes static art and explicit collision outside public road footprints.</summary>
    public static class CoastalCityBlockBuilder
    {
        public const string Layer="Coastal City Block v1";
        public const string Folder="Assets/_Game/Generated/CoastalCityBlock";
        private static Material cream,coral,mint,ink,wood,stone,glass,leaf,glow,rose;
        [MenuItem("Roadside Haven/Design/Build Coastal City Block")]
        public static void BuildCurrent()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)return;
            var motel=UnityEngine.Object.FindFirstObjectByType<MotelGameRoot>();if(motel==null)return;
            string path=motel.gameObject.scene.path;if(File.Exists(path)){string backup="Backups/CityBlock/"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");Directory.CreateDirectory(backup);File.Copy(path,backup+"/"+Path.GetFileName(path));}
            Build(motel);EditorSceneManager.MarkSceneDirty(motel.gameObject.scene);EditorSceneManager.SaveScene(motel.gameObject.scene);AssetDatabase.SaveAssets();
        }
        [MenuItem("Roadside Haven/Design/Focus Coastal City Block")]
        public static void Focus()
        {
            var motel=UnityEngine.Object.FindFirstObjectByType<MotelGameRoot>();var block=motel==null?null:motel.transform.Find(Layer);if(block==null)return;
            Selection.activeGameObject=block.gameObject;if(SceneView.lastActiveSceneView!=null)SceneView.lastActiveSceneView.Frame(new Bounds(new Vector3(142,5,16),new Vector3(55,18,40)),false);
        }
        public static Transform Build(MotelGameRoot motel)
        {
            var existing=motel.transform.Find(Layer);if(existing!=null)return existing;
            if(!AssetDatabase.IsValidFolder(Folder))AssetDatabase.CreateFolder("Assets/_Game/Generated","CoastalCityBlock");
            cream=Load("Warm stucco");coral=Load("Sunset coral");mint=Load("Seafoam stucco");ink=Load("Blue graphite");wood=Load("Oiled cedar");stone=Load("Travertine paving");glass=Load("Teal window glass");leaf=Load("Palm foliage");glow=Load("Warm accent");rose=Load("Rose neon");
            var root=new GameObject(Layer).transform;root.SetParent(motel.transform,false);root.gameObject.AddComponent<MotelDistrictArt>();
            // Replace only foliage inside the actual new building sites, never moving existing homes or services.
            foreach(var plant in motel.GetComponentsInChildren<BiomePlant>(true))
            {
                var rs=plant.GetComponentsInChildren<Renderer>();if(rs.Length==0)continue;var b=rs[0].bounds;foreach(var r in rs)b.Encapsulate(r.bounds);
                if(b.max.x>119&&b.min.x<165&&b.max.z>10.5f&&b.min.z<22)plant.gameObject.SetActive(false);
            }
            foreach(var renderer in motel.GetComponentsInChildren<MeshRenderer>(true))
            {
                if(!renderer.gameObject.activeInHierarchy||renderer.transform.IsChildOf(root))continue;
                var b=renderer.bounds;
                if((renderer.name.Contains("Tree trunk")||renderer.name.Contains("Tree crown"))&&b.max.x>119&&b.min.x<165&&b.max.z>10.5f&&b.min.z<22)
                {
                    var group=renderer.transform.parent;
                    if(group!=null&&group.name.StartsWith("Avenue "))group.gameObject.SetActive(false);
                }
            }
            Box(root,"Public plaza paving",new Vector3(142,.03f,6.5f),new Vector3(47,.12f,9),stone,false);
            Restaurant(root,126);
            Apartment(root,142,coral,"CORAL RECORDS",false);
            Apartment(root,158,mint,"PALM PHARMACY",true);
            // Private parking has flat, nonblocking painted stalls and a clear 4m access aisle at X=118.
            Box(root,"Private parking apron",new Vector3(141,.025f,29),new Vector3(50,.1f,12),ink,false);
            for(int i=0;i<10;i++)Box(root,"Parking stripe",new Vector3(122+i*4,.082f,30),new Vector3(.08f,.008f,6),cream,false);
            for(int i=0;i<3;i++)Box(root,"Parking aisle dash",new Vector3(124+i*14,.082f,25),new Vector3(4,.008f,.1f),cream,false);
            Sign(root,"P  /  VISITOR PARKING",new Vector3(118.8f,2.2f,30),.22f,3.6f,cream.color);
            Box(root,"Parking sign plaque",new Vector3(118.8f,2.2f,30.08f),new Vector3(4,.7f,.12f),ink,false);
            Box(root,"Parking sign post",new Vector3(118.8f,1.1f,30.15f),new Vector3(.1f,2.2f,.1f),ink,true);
            Shelter(root,new Vector3(149,0,5));
            foreach(float x in new[]{119.2f,134f,165f})Palm(root,new Vector3(x,0,7.5f));
            foreach(float x in new[]{119.2f,134f,165f})Planter(root,new Vector3(x,.1f,7.5f));
            foreach(float x in new[]{136f,164.4f})
            {
                Box(root,"Street litter bin",new Vector3(x,.55f,9),new Vector3(.5f,1.0f,.5f),ink,true);
                Box(root,"Bin lid",new Vector3(x,1.1f,9),new Vector3(.55f,.08f,.55f),wood,false);
            }
            Combine(root);foreach(var t in root.GetComponentsInChildren<TMP_Text>())t.ForceMeshUpdate(true,true);AssetDatabase.SaveAssets();return root;
        }
        private static void Restaurant(Transform root,float x)
        {
            // Open doorway, physical glazing and hollow interior instead of a solid building cube.
            Box(root,"Restaurant floor",new Vector3(x,.1f,16),new Vector3(12,.16f,10),stone,true);
            Box(root,"Restaurant back wall",new Vector3(x,2,21),new Vector3(12,4,.25f),cream,true);
            foreach(int side in new[]{-1,1})
            {
                Box(root,"Restaurant side wall",new Vector3(x+side*6,2,16),new Vector3(.25f,4,10),coral,true);
                Box(root,"Restaurant window wall",new Vector3(x+side*3.65f,1.6f,11),new Vector3(4.7f,3,.15f),glass,true);
                Box(root,"Restaurant doorway jamb",new Vector3(x+side*1.27f,1.5f,10.92f),new Vector3(.12f,3,.18f),wood,true);
            }
            Box(root,"Restaurant roof",new Vector3(x,4.1f,16),new Vector3(12.5f,.3f,10.5f),cream,false);
            Box(root,"Restaurant sign fascia",new Vector3(x,3.5f,10.85f),new Vector3(12.2f,.85f,.3f),ink,false);
            Sign(root,"SUNSET KITCHEN",new Vector3(x,3.55f,10.67f),.46f,11,glow.color);
            Sign(root,"COFFEE  /  GRILL  /  GOOD TIMES",new Vector3(x,2.95f,10.65f),.16f,9,cream.color);
            Box(root,"Restaurant warm ribbon",new Vector3(x,3.04f,10.6f),new Vector3(11.8f,.04f,.04f),rose,false);
            for(int i=0;i<20;i++)
            {
                var awning=Box(root,"Cafe striped awning",new Vector3(x-5.7f+i*.6f,2.85f,9.9f),new Vector3(.6f,.09f,2),i%2==0?coral:cream,false);awning.transform.rotation=Quaternion.Euler(-12,0,0);
            }
            Box(root,"Cafe serving counter",new Vector3(x,1,19.5f),new Vector3(8,1.6f,.9f),wood,true);
            Box(root,"Cafe stone worktop",new Vector3(x,1.85f,19.5f),new Vector3(8.2f,.12f,1.05f),stone,false);
            foreach(int side in new[]{-1,1})Table(root,new Vector3(x+side*3.5f,0,16),false);
            Table(root,new Vector3(x-3.8f,0,7),true);Table(root,new Vector3(x+3.8f,0,7),true);
            foreach(float dx in new[]{-4.5f,0,4.5f}){Box(root,"Cafe pendant stem",new Vector3(x+dx,3.4f,15),new Vector3(.035f,1,.035f),ink,false);Box(root,"Cafe pendant shade",new Vector3(x+dx,2.9f,15),new Vector3(.5f,.15f,.5f),glow,false,PrimitiveType.Sphere);}
        }
        private static void Apartment(Transform root,float x,Material facade,string shop,bool pharmacy)
        {
            Box(root,"Mixed use building shell",new Vector3(x,6,16.5f),new Vector3(12,12,10),facade,true);
            Box(root,"Building plinth",new Vector3(x,.3f,16.5f),new Vector3(12.2f,.45f,10.2f),stone,false);
            Box(root,"Roof cornice",new Vector3(x,12.15f,16.5f),new Vector3(12.5f,.35f,10.5f),cream,false);
            for(int floor=0;floor<3;floor++)
            {
                float y=5+floor*3;
                foreach(int side in new[]{-1,1})
                {
                    float wx=x+side*3;
                    Box(root,"Apartment window surround",new Vector3(wx,y,11.4f),new Vector3(2.45f,2.15f,.24f),cream,false);
                    Box(root,"Apartment window",new Vector3(wx,y,11.25f),new Vector3(2.15f,1.85f,.06f),glass,false);
                    Box(root,"Window mullion",new Vector3(wx,y,11.2f),new Vector3(.06f,1.85f,.06f),cream,false);
                    Box(root,"Balcony slab",new Vector3(wx,y-1.12f,10.6f),new Vector3(3.3f,.18f,1.6f),cream,false);
                    Box(root,"Balcony rail",new Vector3(wx,y-.25f,9.83f),new Vector3(3.25f,.07f,.08f),ink,false);
                    for(int i=0;i<9;i++)Box(root,"Balcony baluster",new Vector3(wx-1.5f+i*.375f,y-.67f,9.83f),new Vector3(.035f,.9f,.035f),ink,false);
                    foreach(int edge in new[]{-1,1})Box(root,"Balcony return rail",new Vector3(wx+edge*1.6f,y-.25f,10.5f),new Vector3(.07f,.07f,1.4f),ink,false);
                    Box(root,"Balcony planter",new Vector3(wx+1,y-.9f,10.5f),new Vector3(.55f,.3f,.5f),wood,false);
                    Box(root,"Balcony foliage",new Vector3(wx+1,y-.58f,10.5f),new Vector3(.7f,.6f,.65f),leaf,false,PrimitiveType.Sphere);
                    // Side windows keep buildings readable from the adjacent avenue.
                    Box(root,"Side window surround",new Vector3(x+side*6.03f,y,16.5f),new Vector3(.12f,2.1f,2.2f),cream,false);
                    Box(root,"Side window",new Vector3(x+side*6.11f,y,16.5f),new Vector3(.04f,1.8f,1.9f),glass,false);
                }
            }
            foreach(int side in new[]{-1,1})
            {
                Box(root,"Shopfront reveal",new Vector3(x+side*3.8f,1.65f,11.35f),new Vector3(3.8f,2.75f,.18f),ink,false);
                Box(root,"Shopfront glass",new Vector3(x+side*3.8f,1.65f,11.22f),new Vector3(3.55f,2.5f,.06f),glass,false);
            }
            Box(root,"Shop entrance",new Vector3(x,1.5f,11.25f),new Vector3(1.5f,2.8f,.1f),wood,false);
            Box(root,"Store canopy",new Vector3(x,3.3f,10.7f),new Vector3(12.2f,.18f,1.6f),ink,false);
            Sign(root,shop,new Vector3(x,3.02f,9.82f),.35f,10.6f,pharmacy?glow.color:rose.color);
            Box(root,"Store accent ribbon",new Vector3(x,3.38f,9.83f),new Vector3(11.8f,.04f,.04f),pharmacy?glow:rose,false);
            for(int i=0;i<3;i++)Box(root,"Roof vent",new Vector3(x-3+i*3,12.65f,17),new Vector3(1.2f,.7f,1.6f),ink,false);
            if(pharmacy){Box(root,"Pharmacy cross",new Vector3(x+5,4,10),new Vector3(.2f,1,.15f),glow,false);Box(root,"Pharmacy cross",new Vector3(x+5,4,10),new Vector3(.7f,.2f,.15f),glow,false);}
        }
        private static void Table(Transform root,Vector3 p,bool umbrella)
        {
            Box(root,"Cafe table pedestal",p+Vector3.up*.55f,new Vector3(.15f,1.1f,.15f),ink,true);
            Box(root,"Cafe table top",p+Vector3.up*1.1f,new Vector3(1.15f,.09f,1.15f),wood,false,PrimitiveType.Cylinder);
            foreach(int side in new[]{-1,1})
            {
                Box(root,"Cafe chair seat",p+new Vector3(side*.95f,.55f,0),new Vector3(.5f,.1f,.55f),mint,true);
                Box(root,"Cafe chair back",p+new Vector3(side*1.15f,.85f,0),new Vector3(.08f,.6f,.55f),mint,true);
                foreach(float z in new[]{-.2f,.2f})Box(root,"Cafe chair leg",p+new Vector3(side*.95f,.25f,z),new Vector3(.055f,.5f,.055f),ink,false);
            }
            if(!umbrella)return;
            Box(root,"Umbrella pole",p+Vector3.up*1.5f,new Vector3(.055f,3,.055f),wood,false);
            var mesh=new Mesh{name="Octagonal cafe parasol"};var v=new List<Vector3>{new Vector3(0,3.2f,0)};for(int i=0;i<=8;i++){float a=i*Mathf.PI/4;v.Add(new Vector3(Mathf.Cos(a)*1.5f,2.8f,Mathf.Sin(a)*1.5f));}var tri=new List<int>();for(int i=1;i<=8;i++)tri.AddRange(new[]{0,i+1,i,0,i,i+1});mesh.SetVertices(v);mesh.SetTriangles(tri,0);mesh.RecalculateNormals();MeshPart(root,"Cafe parasol",p,mesh,coral);
        }
        private static void Shelter(Transform root,Vector3 p)
        {
            Box(root,"Bus shelter roof",p+Vector3.up*2.8f,new Vector3(5.8f,.2f,2.8f),mint,false);
            foreach(int side in new[]{-1,1})Box(root,"Bus shelter post",p+new Vector3(side*2.7f,1.35f,.8f),new Vector3(.12f,2.7f,.12f),ink,true);
            Box(root,"Bus shelter rear glazing",p+new Vector3(0,1.6f,1),new Vector3(5.4f,1.9f,.08f),glass,true);
            Box(root,"Bus bench seat",p+new Vector3(0,.58f,.5f),new Vector3(3.5f,.12f,.55f),wood,true);
            Box(root,"Bus bench back",p+new Vector3(0,.95f,.8f),new Vector3(3.5f,.65f,.1f),wood,true);
            Sign(root,"HAVEN EAST  /  BUS STOP",p+new Vector3(0,2.56f,-1.42f),.23f,5.2f,cream.color);
            Box(root,"Bus route board",p+new Vector3(2.2f,1.55f,.87f),new Vector3(.7f,.9f,.1f),cream,false);
            Sign(root,"CITY\nSCHOOL\nCOLLEGE",p+new Vector3(2.2f,1.55f,.8f),.11f,.65f,ink.color);
        }
        private static void Planter(Transform root,Vector3 p)
        {
            Box(root,"Palm garden planter",p+Vector3.up*.2f,new Vector3(1.2f,.4f,1.2f),stone,true);
            for(int i=0;i<4;i++){float a=i*Mathf.PI*.5f;Box(root,"Garden shrub",p+new Vector3(Mathf.Cos(a)*.4f,.5f,Mathf.Sin(a)*.4f),new Vector3(.55f,.6f,.55f),leaf,false,PrimitiveType.Sphere);}
        }
        private static void Palm(Transform root,Vector3 p)
        {
            Box(root,"Street palm trunk",p+Vector3.up*3,new Vector3(.3f,3,.3f),wood,true,PrimitiveType.Cylinder);
            var v=new List<Vector3>();var tri=new List<int>();
            for(int f=0;f<10;f++){float a=f*Mathf.PI*.2f;var dir=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));var cross=Vector3.Cross(dir,Vector3.up);int start=v.Count;for(int j=0;j<=8;j++){float t=j/8f;var c=dir*t*2.5f+Vector3.up*(6+.7f*Mathf.Sin(t*Mathf.PI)-t*t);float w=.4f*Mathf.Sin(t*Mathf.PI);v.Add(c+cross*w);v.Add(c-cross*w);if(j>0){int k=start+j*2;tri.AddRange(new[]{k-2,k,k-1,k-1,k,k+1});}}}
            var mesh=new Mesh{name="Coastal palm fronds"};mesh.SetVertices(v);mesh.SetTriangles(tri,0);mesh.RecalculateNormals();MeshPart(root,"Coastal palm fronds",p,mesh,leaf);
        }
        private static Material Load(string name){var mat=AssetDatabase.LoadAssetAtPath<Material>(MotelDistrictArtBuilder.Folder+"/"+name+".mat");if(mat==null)throw new InvalidOperationException("Build the coastal motel art first: "+name);return mat;}
        private static GameObject Box(Transform parent,string name,Vector3 p,Vector3 size,Material material,bool solid,PrimitiveType type=PrimitiveType.Cube)
        {
            var go=GameObject.CreatePrimitive(type);go.name=name;go.transform.SetParent(parent,false);go.transform.position=p;go.transform.localScale=size;go.GetComponent<Renderer>().sharedMaterial=material;UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());if(solid)go.AddComponent<BoxCollider>();return go;
        }
        private static void Sign(Transform root,string title,Vector3 p,float size,float width,Color color)
        {
            var text=new GameObject(title).AddComponent<TextMeshPro>();text.transform.SetParent(root,false);text.transform.position=p;text.font=TMP_Settings.GetFontAsset();text.text=title;text.fontSize=size*10;text.color=color;text.alignment=TextAlignmentOptions.Center;text.rectTransform.sizeDelta=new Vector2(width,size*4);text.raycastTarget=false;
        }
        private static void MeshPart(Transform root,string name,Vector3 p,Mesh mesh,Material material){var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(root,false);go.transform.position=p;go.GetComponent<MeshFilter>().sharedMesh=mesh;go.GetComponent<MeshRenderer>().sharedMaterial=material;}
        private static void Combine(Transform root)
        {
            int index=0;var filters=root.GetComponentsInChildren<MeshFilter>().Where(f=>f.GetComponent<TMP_Text>()==null).ToArray();
            foreach(var group in filters.GroupBy(f=>f.GetComponent<MeshRenderer>().sharedMaterial))
            {
                var parts=group.ToArray();var mesh=new Mesh{name="City block "+group.Key.name,indexFormat=IndexFormat.UInt32};mesh.CombineMeshes(parts.Select(f=>new CombineInstance{mesh=f.sharedMesh,transform=root.worldToLocalMatrix*f.transform.localToWorldMatrix}).ToArray(),true,true);mesh.RecalculateBounds();string path=Folder+"/Block"+(index++)+".asset";var saved=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(saved==null)AssetDatabase.CreateAsset(mesh,path);else{EditorUtility.CopySerialized(mesh,saved);UnityEngine.Object.DestroyImmediate(mesh);mesh=saved;}MeshPart(root,mesh.name,Vector3.zero,mesh,group.Key);
                foreach(var f in parts){UnityEngine.Object.DestroyImmediate(f.GetComponent<MeshRenderer>());UnityEngine.Object.DestroyImmediate(f);}
            }
        }
    }
}