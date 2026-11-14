using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
namespace RoadsideHaven.Editor
{
    /// <summary>Bakes the first coastal art pass without changing room IDs, destinations or road geometry.</summary>
    public static class MotelDistrictArtBuilder
    {
        public const string Folder="Assets/_Game/Generated/MotelDistrict";
        public const string Layer="Coastal Motel Art v1";
        private static Material cream,coral,mint,ink,wood,stone,glass,leaf,leafLight,glow,rose;
        private static readonly List<Renderer> roofs=new List<Renderer>();
        [MenuItem("Roadside Haven/Design/Build Coastal Motel")]
        public static void BuildCurrent()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode){Debug.LogWarning("Stop Play Mode before building the motel art.");return;}
            var root=UnityEngine.Object.FindFirstObjectByType<MotelGameRoot>();if(root==null)return;
            string path=root.gameObject.scene.path;if(File.Exists(path)){string backup="Backups/MotelDesign/"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");Directory.CreateDirectory(backup);File.Copy(path,backup+"/"+Path.GetFileName(path));}
            Build(root);EditorSceneManager.MarkSceneDirty(root.gameObject.scene);EditorSceneManager.SaveScene(root.gameObject.scene);AssetDatabase.SaveAssets();
        }
        public static Transform Build(MotelGameRoot motel)
        {
            var existing=motel.transform.Find(Layer);if(existing!=null)return existing;
            if(!AssetDatabase.IsValidFolder(Folder))AssetDatabase.CreateFolder("Assets/_Game/Generated","MotelDistrict");
            cream=Mat("Warm stucco",new Color(.88f,.81f,.65f),.16f);
            coral=Mat("Sunset coral",new Color(.76f,.35f,.29f),.19f);
            mint=Mat("Seafoam stucco",new Color(.21f,.53f,.48f),.19f);
            ink=Mat("Blue graphite",new Color(.055f,.12f,.15f),.4f);
            wood=Mat("Oiled cedar",new Color(.27f,.14f,.08f),.26f);
            stone=Mat("Travertine paving",new Color(.62f,.57f,.46f),.12f);
            glass=Mat("Teal window glass",new Color(.08f,.28f,.32f),.88f);
            leaf=Mat("Palm foliage",new Color(.13f,.3f,.11f),.15f);leaf.SetFloat("_Cull",0);
            leafLight=Mat("Palm leaf tips",new Color(.29f,.43f,.13f),.15f);leafLight.SetFloat("_Cull",0);
            glow=Mat("Warm accent",new Color(1,.71f,.36f),.4f,true);
            rose=Mat("Rose neon",new Color(1,.24f,.47f),.4f,true);
            CreateSurfaceTextures();
            foreach(var mat in new[]{cream,coral,mint}){mat.mainTexture=AssetDatabase.LoadAssetAtPath<Texture2D>(Folder+"/Stucco.png");mat.SetTexture("_BumpMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Folder+"/StuccoNormal.png"));mat.SetFloat("_BumpScale",.25f);mat.EnableKeyword("_NORMALMAP");}
            wood.mainTexture=AssetDatabase.LoadAssetAtPath<Texture2D>(Folder+"/CedarGrain.png");
            Surface(stone,"Paving_Normal",.25f);stone.mainTexture=AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_Game/Art/Surfaces/Paving_Albedo.png");
            var root=new GameObject(Layer).transform;root.SetParent(motel.transform,false);var art=root.gameObject.AddComponent<MotelDistrictArt>();roofs.Clear();
            // Original walls and their colliders retain exactly the same footprints and navigation exclusions.
            foreach(var room in motel.GetComponentsInChildren<RoomSelectable>(true))
            {
                var floor=room.transform.Find("Floor");if(floor==null)continue;var c=floor.position;float x=c.x,z=c.z;
                foreach(var r in room.GetComponentsInChildren<MeshRenderer>(true))
                {
                    if(r.name=="Back wall"||r.name=="Side wall"){var scale=r.transform.localScale;scale.y=3.2f;r.transform.localScale=scale;var p=r.transform.position;p.y=1.7f;r.transform.position=p;r.sharedMaterial=cream;}
                    if(r.name.StartsWith("Door ")&&r.name.EndsWith("wall")){var scale=r.transform.localScale;scale.y=2.65f;r.transform.localScale=scale;var p=r.transform.position;p.y=1.5f;r.transform.position=p;r.sharedMaterial=mint;}
                    if(r.name=="Open door"){var scale=r.transform.localScale;scale.y=2.1f;r.transform.localScale=scale;var p=r.transform.position;p.y=1.25f;r.transform.position=p;r.sharedMaterial=wood;}
                    if(r.name=="Floor")r.sharedMaterial=stone;
                    if(r.name=="Bed frame"||r.name=="Nightstand")r.sharedMaterial=wood;
                }
                var group=new GameObject(room.name+" facade").transform;group.SetParent(root,false);
                Box(group,"Entry lintel",new Vector3(x,2.85f,z-3),new Vector3(5.35f,.35f,.24f),cream);
                Box(group,"Recessed doorway header",new Vector3(x,2.4f,z-3.02f),new Vector3(1.4f,.16f,.25f),wood);
                foreach(int side in new[]{-1,1})Box(group,"Door jamb",new Vector3(x+side*.69f,1.28f,z-3.04f),new Vector3(.09f,2.2f,.25f),wood);
                // Deep window recesses, projecting sills and cedar louvers give the frontage real depth.
                Window(group,new Vector3(x-1.75f,1.65f,z-3.12f),1.15f,1.2f);
                Window(group,new Vector3(x+1.75f,1.65f,z-3.12f),1.15f,1.2f);
                Box(group,"Entry light housing",new Vector3(x+.95f,2.35f,z-3.18f),new Vector3(.16f,.36f,.18f),ink);
                Box(group,"Entry light diffuser",new Vector3(x+.95f,2.35f,z-3.28f),new Vector3(.12f,.26f,.025f),glow);
                Sign(group,room.name.Replace("Room ",""),new Vector3(x,2.7f,z-3.17f),.16f,1.0f,cream.color);
                foreach(int side in new[]{-1,1}){Box(group,"Roof fascia",new Vector3(x,3.35f,z+side*3.23f),new Vector3(5.48f,.35f,.14f),coral);Box(group,"Roof fascia",new Vector3(x+side*2.67f,3.35f,z),new Vector3(.14f,.35f,6.6f),coral);}
                roofs.Add(Box(group,"Room roof",new Vector3(x,3.62f,z),new Vector3(5.5f,.22f,6.7f),cream).GetComponent<Renderer>());
                Box(group,"Courtyard awning",new Vector3(x,3.1f,z-3.65f),new Vector3(5.4f,.17f,1.4f),mint);
                Box(group,"Awning light ribbon",new Vector3(x,3.01f,z-4.23f),new Vector3(5.15f,.035f,.035f),glow);
                for(int slat=0;slat<8;slat++)Box(group,"Cedar soffit",new Vector3(x-2.3f+slat*.65f,3.0f,z-3.65f),new Vector3(.1f,.09f,1.2f),wood);
            }
            // Close the outer end wall; inner partitions already belonged to the original navigation bake.
            Solid(root,"East motel end wall",new Vector3(10.95f,1.7f,12),new Vector3(.18f,3.2f,6),cream);
            // Preserve the reception queue and shop interaction points below open-sided architectural canopies.
            Pavilion(root,new Vector3(-6,0,4.3f),6.4f,3.6f,"RECEPTION",coral);
            Pavilion(root,new Vector3(7,0,2.5f),6.6f,5,"HAVEN MARKET",mint);
            for(int i=0;i<9;i++)Box(root,"Reception cedar facing",new Vector3(-7.35f+i*.34f,.7f,3.77f),new Vector3(.22f,1.15f,.06f),wood);
            Box(root,"Reception stone counter",new Vector3(-6,1.46f,4.3f),new Vector3(3.4f,.12f,1.3f),stone);
            // A freestanding Art Deco blade sign stays behind the sidewalk, away from the highway.
            Solid(root,"Motel sign base",new Vector3(-12.1f,.3f,3.2f),new Vector3(1,.6f,1),stone);
            Solid(root,"Motel sign pier",new Vector3(-12.1f,3.5f,3.2f),new Vector3(.55f,6.4f,.6f),cream);
            Box(root,"Neon sign plaque",new Vector3(-12.1f,5.7f,3.1f),new Vector3(3.8f,1.7f,.3f),ink);
            Sign(root,"ROADSIDE",new Vector3(-12.1f,6,2.92f),.28f,3.5f,glow.color);
            Sign(root,"HAVEN",new Vector3(-12.1f,5.5f,2.92f),.4f,3.4f,rose.color);
            for(int i=0;i<3;i++)Box(root,"Neon blade trim",new Vector3(-12.1f,6.67f+i*.12f,3.1f),new Vector3(4-i*.3f,.045f,.35f),rose);
            foreach(var p in new[]{new Vector3(-12.5f,0,16.5f),new Vector3(12.5f,0,16.5f),new Vector3(-12.5f,0,7),new Vector3(12.6f,0,7)})Palm(root,p,5.6f);
            // Low garden beds use the rear corners, leaving the room entry corridor entirely clear.
            foreach(int side in new[]{-1,1})
            {
                var p=new Vector3(side*12.45f,.2f,12);
                Solid(root,"Garden planter",p,new Vector3(1.3f,.35f,4.8f),stone);
                for(int i=0;i<6;i++)Shrub(root,p+new Vector3(0,.1f,-2+i*.8f));
            }
            CleanLegacy(motel.transform,root);
            // Merge per material and by roof visibility: no per-frame mesh generation or extra real-time lights.
            Combine(root,false);Combine(root,true);art.Configure(roofs.ToArray());
            foreach(var text in root.GetComponentsInChildren<TMP_Text>())text.ForceMeshUpdate(true,true);
            EditorUtility.SetDirty(motel);AssetDatabase.SaveAssets();return root;
        }
        private static void Pavilion(Transform root,Vector3 p,float width,float depth,string title,Material color)
        {
            Box(root,"Pavilion roof",p+Vector3.up*3.4f,new Vector3(width,.25f,depth),cream);
            Box(root,"Pavilion fascia",p+new Vector3(0,3.1f,-depth*.5f),new Vector3(width,.55f,.2f),color);
            Box(root,"Pavilion warm accent",p+new Vector3(0,2.81f,-depth*.5f-.1f),new Vector3(width-.25f,.035f,.035f),glow);
            Sign(root,title,p+new Vector3(0,3.1f,-depth*.5f-.12f),.25f,width-.6f,cream.color);
            // Decorative posts occupy the existing side supports, not the central visitor approach.
            foreach(int side in new[]{-1,1})Solid(root,"Pavilion support",p+new Vector3(side*(width*.5f-.25f),1.5f,depth*.5f-.3f),new Vector3(.18f,3,.18f),wood);
            for(int i=0;i<12;i++)Box(root,"Pavilion cedar ceiling",p+new Vector3(-width*.45f+i*width*.08f,3.21f,0),new Vector3(.08f,.08f,depth-.3f),wood);
        }
        private static void Window(Transform root,Vector3 p,float width,float height)
        {
            Box(root,"Window reveal",p,new Vector3(width+.18f,height+.18f,.16f),ink);
            Box(root,"Window glass",p+Vector3.back*.1f,new Vector3(width,height,.04f),glass);
            Box(root,"Window sill",p+new Vector3(0,-height*.5f-.08f,-.13f),new Vector3(width+.3f,.12f,.32f),cream);
            Box(root,"Window mullion",p+Vector3.back*.14f,new Vector3(.045f,height,.035f),cream);
            foreach(int side in new[]{-1,1})for(int i=0;i<7;i++)Box(root,"Cedar shutter",p+new Vector3(side*(width*.5f+.18f),-height*.43f+i*height*.14f,0),new Vector3(.22f,.075f,.16f),wood);
        }
        private static void Palm(Transform parent,Vector3 p,float height)
        {
            var trunk=Solid(parent,"Palm trunk",p+Vector3.up*height*.5f,new Vector3(.25f,height,.25f),wood,PrimitiveType.Cylinder);trunk.transform.localScale=new Vector3(.3f,height*.5f,.3f);
            var vertices=new List<Vector3>();var triangles=new List<int>();
            for(int f=0;f<10;f++)
            {
                float a=f*Mathf.PI*2/10;var dir=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));var across=Vector3.Cross(dir,Vector3.up);int start=vertices.Count;
                for(int j=0;j<=8;j++){float t=j/8f;var center=dir*t*2.8f+Vector3.up*(height+.6f*Mathf.Sin(t*Mathf.PI)-t*t*1.2f);float w=.38f*Mathf.Sin(t*Mathf.PI);vertices.Add(center+across*w);vertices.Add(center-across*w);if(j>0){int k=start+j*2;triangles.AddRange(new[]{k-2,k,k-1,k-1,k,k+1});}}
            }
            var mesh=new Mesh{name="Curved palm fronds",vertices=vertices.ToArray(),triangles=triangles.ToArray()};mesh.RecalculateNormals();mesh.RecalculateBounds();
            var go=new GameObject("Curved palm fronds",typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(parent,false);go.transform.position=p;go.GetComponent<MeshFilter>().sharedMesh=mesh;go.GetComponent<MeshRenderer>().sharedMaterial=leaf;
            for(int i=0;i<3;i++)Box(parent,"Palm fruit",p+new Vector3(i*.15f-.15f,height-.15f,0),Vector3.one*.22f,wood);
        }
        private static void Shrub(Transform parent,Vector3 p)
        {
            for(int i=0;i<3;i++){var go=Box(parent,"Garden foliage",p+new Vector3((i-1)*.25f,.3f,i%2*.2f),new Vector3(.6f,.65f,.65f),i%2==0?leaf:leafLight,PrimitiveType.Sphere);}
        }
        private static void CleanLegacy(Transform motel,Transform art)
        {
            foreach(var r in motel.GetComponentsInChildren<MeshRenderer>(true))
            {
                if(r.transform.IsChildOf(art))continue;
                string n=r.name;var p=r.bounds.center;
                if((n=="Rear cornice"||n=="Window frame"||n=="Window pane"||n=="Window mullion")&&Mathf.Abs(p.x)<11&&p.z>8&&p.z<16)r.enabled=false;
                if((n=="Reception canopy"||n=="Canopy column"||n=="Shop fascia"||n=="Shop awning stripe")&&Mathf.Abs(p.x)<11){r.gameObject.SetActive(false);}
                if((n=="Tree crown"||n=="Tree crown highlight"||n=="Tree trunk")&&Mathf.Abs(p.x)>11&&Mathf.Abs(p.x)<14&&p.z>5&&p.z<19){r.gameObject.SetActive(false);}
            }
        }
        private static GameObject Box(Transform parent,string name,Vector3 p,Vector3 size,Material mat,PrimitiveType type=PrimitiveType.Cube)
        {
            var go=GameObject.CreatePrimitive(type);go.name=name;go.transform.SetParent(parent,false);go.transform.position=p;go.transform.localScale=size;go.GetComponent<Renderer>().sharedMaterial=mat;UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());return go;
        }
        private static GameObject Solid(Transform parent,string name,Vector3 p,Vector3 size,Material mat,PrimitiveType type=PrimitiveType.Cube)
        {var go=Box(parent,name,p,size,mat,type);go.AddComponent<BoxCollider>();return go;}
        private static void Sign(Transform parent,string title,Vector3 p,float height,float width,Color color)
        {
            var t=new GameObject(title).AddComponent<TextMeshPro>();t.transform.SetParent(parent,false);t.transform.position=p;t.font=TMP_Settings.GetFontAsset();t.text=title;t.fontSize=height*10;t.color=color;t.alignment=TextAlignmentOptions.Center;t.rectTransform.sizeDelta=new Vector2(width,height*2);t.raycastTarget=false;
        }
        private static Material Mat(string name,Color color,float smooth,bool emissive=false)
        {
            string path=Folder+"/"+name+".mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(path);if(mat==null){mat=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(mat,path);}mat.color=color;mat.SetFloat("_Smoothness",smooth);mat.enableInstancing=true;
            if(emissive){mat.EnableKeyword("_EMISSION");mat.SetColor("_EmissionColor",color*.8f);}EditorUtility.SetDirty(mat);return mat;
        }
        private static void CreateSurfaceTextures()
        {
            const int n=256;var height=new float[n*n];
            for(int y=0;y<n;y++)for(int x=0;x<n;x++){uint h=(uint)(x*374761393+y*668265263);h=(h^(h>>13))*1274126177;float grain=(h&65535)/65535f;height[y*n+x]=grain*.1f+.5f+.035f*Mathf.Sin(x*Mathf.PI/16)*Mathf.Sin(y*Mathf.PI/32);}
            foreach(string name in new[]{"Stucco","StuccoNormal","CedarGrain"})
            {
                string path=Folder+"/"+name+".png";if(File.Exists(path))continue;var texture=new Texture2D(n,n,TextureFormat.RGB24,false);var colors=new Color[n*n];
                for(int y=0;y<n;y++)for(int x=0;x<n;x++)
                {
                    int i=y*n+x;float v=height[i];
                    if(name=="StuccoNormal"){float dx=height[y*n+(x+1)%n]-height[y*n+(x+n-1)%n],dy=height[((y+1)%n)*n+x]-height[((y+n-1)%n)*n+x];var normal=new Vector3(-dx*2,-dy*2,1).normalized;colors[i]=new Color(normal.x*.5f+.5f,normal.y*.5f+.5f,normal.z*.5f+.5f);}
                    else {float value=name=="Stucco"?.84f+v*.23f:.66f+.19f*Mathf.Sin(x*.48f+Mathf.Sin(y*.024f)*2)+v*.15f;colors[i]=new Color(value,value,value);}
                }
                texture.SetPixels(colors);texture.Apply();File.WriteAllBytes(path,texture.EncodeToPNG());UnityEngine.Object.DestroyImmediate(texture);AssetDatabase.ImportAsset(path);
                var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.wrapMode=TextureWrapMode.Repeat;importer.mipmapEnabled=true;importer.anisoLevel=4;if(name=="StuccoNormal"){importer.textureType=TextureImporterType.NormalMap;importer.sRGBTexture=false;}importer.SaveAndReimport();
            }
        }
        private static void Surface(Material material,string normal,float strength)
        {
            var map=AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_Game/Art/Surfaces/"+normal+".png");if(map==null)return;material.SetTexture("_BumpMap",map);material.SetFloat("_BumpScale",strength);material.EnableKeyword("_NORMALMAP");
        }
        private static void Combine(Transform root,bool roofGroup)
        {
            var candidates=root.GetComponentsInChildren<MeshFilter>().Where(f=>f.GetComponent<TMP_Text>()==null&&roofs.Contains(f.GetComponent<Renderer>())==roofGroup).ToArray();
            var merged=new List<Renderer>();int index=0;
            foreach(var group in candidates.GroupBy(f=>f.GetComponent<MeshRenderer>().sharedMaterial))
            {
                var filters=group.ToArray();var parts=filters.Select(f=>new CombineInstance{mesh=f.sharedMesh,transform=root.worldToLocalMatrix*f.transform.localToWorldMatrix}).ToArray();
                var mesh=new Mesh{name=(roofGroup?"Roofs ":"District ")+group.Key.name,indexFormat=IndexFormat.UInt32};mesh.CombineMeshes(parts,true,true);mesh.RecalculateBounds();string path=Folder+"/"+(roofGroup?"Roof":"Static")+(index++)+".asset";var saved=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(saved==null)AssetDatabase.CreateAsset(mesh,path);else{EditorUtility.CopySerialized(mesh,saved);UnityEngine.Object.DestroyImmediate(mesh);mesh=saved;}
                var go=new GameObject(mesh.name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(root,false);go.GetComponent<MeshFilter>().sharedMesh=mesh;var renderer=go.GetComponent<MeshRenderer>();renderer.sharedMaterial=group.Key;merged.Add(renderer);
                foreach(var f in filters){UnityEngine.Object.DestroyImmediate(f.GetComponent<MeshRenderer>());UnityEngine.Object.DestroyImmediate(f);}
            }
            if(roofGroup){roofs.Clear();roofs.AddRange(merged);}
        }
    }
}