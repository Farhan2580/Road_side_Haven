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
    /// <summary>Two surveyed residential pockets with heritage homes, usable back gardens and connected public streets.</summary>
    public static class HeritageResidentialBuilder
    {
        public const string Folder="Assets/_Game/Generated/HeritageResidential";
        private static Material cream,sage,ochre,rose,blue,brick,roof,roofDark,wood,ivory,iron,glass,lawn,leaf,leafLight,soil,paving,asphalt,flowerPink,flowerGold,flowerWhite,rubber,playBlue,playRed,warm;
        private static readonly List<Renderer> bulbs=new List<Renderer>();
        private static readonly List<Transform> swings=new List<Transform>(),seesaws=new List<Transform>(),roundabouts=new List<Transform>();
        private static readonly List<Mesh> temporary=new List<Mesh>();
        private static readonly Dictionary<string,Mesh> boxes=new Dictionary<string,Mesh>();
        private static Mesh sphere,blossom;
        private static int chunk;
        [MenuItem("Roadside Haven/Design/Build Heritage Homes and Gardens")]
        public static void BuildCurrent()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)return;var motel=UnityEngine.Object.FindFirstObjectByType<MotelGameRoot>();if(motel==null)return;
            if(File.Exists(motel.gameObject.scene.path)){Directory.CreateDirectory("Backups/ResidentialUpgrade");File.Copy(motel.gameObject.scene.path,"Backups/ResidentialUpgrade/Before-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")+".unity",true);}
            Build(motel);EditorSceneManager.MarkSceneDirty(motel.gameObject.scene);EditorSceneManager.SaveScene(motel.gameObject.scene);AssetDatabase.SaveAssets();Focus();
        }
        [MenuItem("Roadside Haven/Design/Focus Heritage Homes and Gardens")]
        public static void Focus()
        {
            var m=UnityEngine.Object.FindFirstObjectByType<MotelGameRoot>();var t=m==null?null:m.transform.Find(HeritageNeighborhoodLayout.Layer);if(t==null)return;Selection.activeGameObject=t.gameObject;
            if(SceneView.lastActiveSceneView!=null)SceneView.lastActiveSceneView.Frame(new Bounds(new Vector3(-205,4,-155),new Vector3(310,25,155)),false);
        }
        public static Transform Build(MotelGameRoot motel)
        {
            if(Application.isPlaying)throw new InvalidOperationException("Bake residential geometry in Edit Mode.");
            var old=motel.transform.Find(HeritageNeighborhoodLayout.Layer);if(old!=null)return old;
            if(!AssetDatabase.IsValidFolder(Folder))AssetDatabase.CreateFolder("Assets/_Game/Generated","HeritageResidential");
            SetupMaterials();bulbs.Clear();swings.Clear();seesaws.Clear();roundabouts.Clear();temporary.Clear();boxes.Clear();chunk=0;
            var root=Group(motel.transform,HeritageNeighborhoodLayout.Layer,Vector3.zero);root.gameObject.AddComponent<MotelDistrictArt>();
            var additions=new List<Rect>();for(int s=0;s<2;s++)additions.AddRange(HeritageNeighborhoodLayout.Roads(s));
            foreach(var r in additions)Box(root,"Connected road",new Vector3(r.center.x,0,r.center.y),new Vector3(r.width,.12f,r.height),asphalt,true);
            var allRoads=motel.GetComponentsInChildren<MeshRenderer>().Where(r=>r.gameObject.activeInHierarchy&&TownPopulationController.RoadName(r.name)).Select(r=>Rect.MinMaxRect(r.bounds.min.x,r.bounds.min.z,r.bounds.max.x,r.bounds.max.z)).ToList();
            RepairPaving(motel.transform,root,additions,allRoads);
            for(int site=0;site<2;site++)
            {
                var district=Group(root,HeritageNeighborhoodLayout.Names[site],Vector3.zero);Streets(district,site,allRoads);
                for(int index=0;index<12;index++)House(district,site,index);Park(district,site,allRoads);ParkConnections(district,site,allRoads);
                foreach(float offset in new[]{-55f,5f,65f})
                {float x=HeritageNeighborhoodLayout.Centers[site].x+offset,z=HeritageNeighborhoodLayout.Centers[site].y;Lantern(district,new Vector3(x,0,z+8.3f),3.2f);Lantern(district,new Vector3(x+13,0,z-8.3f),3.2f);}
                var center=HeritageNeighborhoodLayout.Centers[site];Plaque(district,HeritageNeighborhoodLayout.Names[site],new Vector3(site==0?-328:448,0,center.y+10),8);
            }
            root.gameObject.AddComponent<HeritageNeighborhoodLife>().Configure(swings.ToArray(),seesaws.ToArray(),roundabouts.ToArray());RegisterLights(motel);RoadClearance.Resolve(motel.transform);Combine(root);
            var surface=RoadSurfacePhysics.Rebuild(motel.transform);var saved=Save(surface.SurfaceMesh,Folder+"/ConnectedResidentialRoadSurface.asset");surface.UseSavedMesh(saved);
            foreach(var text in root.GetComponentsInChildren<TMP_Text>())text.ForceMeshUpdate(true,true);
            foreach(var mesh in temporary)if(mesh!=null)UnityEngine.Object.DestroyImmediate(mesh);temporary.Clear();boxes.Clear();sphere=blossom=null;AssetDatabase.SaveAssets();return root;
        }
        private static void SetupMaterials()
        {
            cream=Mat("Aged warm plaster",new Color(.80f,.72f,.55f),"Stucco");sage=Mat("Muted sage plaster",new Color(.51f,.61f,.47f),"Stucco");ochre=Mat("Honey ochre plaster",new Color(.75f,.52f,.29f),"Stucco");rose=Mat("Faded rose plaster",new Color(.73f,.47f,.38f),"Stucco");blue=Mat("Dusty blue plaster",new Color(.45f,.57f,.59f),"Stucco");
            brick=Mat("Weathered red brick",Color.white);roof=Mat("Terracotta roof tiles",Color.white);roofDark=Mat("Smoked terracotta tiles",new Color(.58f,.61f,.6f));brick.mainTexture=Pattern("Heritage brick",false);roof.mainTexture=Pattern("Heritage tile",true);roofDark.mainTexture=roof.mainTexture;
            wood=Mat("Old cedar timber",new Color(.35f,.19f,.085f),"CedarGrain");ivory=Mat("Painted ivory timber",new Color(.91f,.84f,.66f));iron=Mat("Aged iron",new Color(.085f,.11f,.105f));glass=Mat("Recessed vintage glass",new Color(.13f,.27f,.29f));glass.SetFloat("_Smoothness",.65f);
            lawn=Mat("Garden grass",new Color(.65f,.78f,.5f),"BaseGrass");leaf=Mat("Deep garden leaves",new Color(.18f,.34f,.13f));leafLight=Mat("Fruit tree leaves",new Color(.34f,.48f,.16f));soil=Mat("Garden earth",new Color(.62f,.43f,.25f),"DirtPebbles");paving=Mat("Old limestone paving",new Color(.73f,.68f,.56f),"Paving");asphalt=Mat("Quiet local asphalt",new Color(.25f,.255f,.26f));asphalt.mainTexture=null;asphalt.SetTexture("_BumpMap",null);asphalt.DisableKeyword("_NORMALMAP");asphalt.SetFloat("_Smoothness",.25f);/* match the town asphalt; the RoadCity albedo is near-black and made these streets read as voids */
            flowerPink=Mat("Garden rose petals",new Color(.79f,.24f,.34f));flowerGold=Mat("Marigold petals",new Color(1,.62f,.12f));flowerWhite=Mat("Jasmine petals",new Color(.95f,.91f,.72f));rubber=Mat("Soft play surface",new Color(.61f,.41f,.24f));playBlue=Mat("Playground teal",new Color(.12f,.43f,.45f));playRed=Mat("Playground coral",new Color(.77f,.29f,.16f));warm=Mat("Heritage lantern glass",new Color(1,.76f,.36f));
        }
        private static Material Mat(string name,Color color,string texture=null)
        {
            string path=Folder+"/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);if(m==null){m=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(m,path);}m.color=color;m.SetFloat("_Smoothness",.16f);m.enableInstancing=true;
            if(texture!=null){string directory=texture=="Stucco"||texture=="CedarGrain"?"Assets/_Game/Generated/MotelDistrict/":"Assets/_Game/Art/Surfaces/";string suffix=directory.Contains("Surfaces")?"_Albedo.png":".png";m.mainTexture=AssetDatabase.LoadAssetAtPath<Texture2D>(directory+texture+suffix);var normal=AssetDatabase.LoadAssetAtPath<Texture2D>(directory+texture+(directory.Contains("Surfaces")?"_Normal.png":"Normal.png"));if(normal!=null){m.SetTexture("_BumpMap",normal);m.SetFloat("_BumpScale",.22f);m.EnableKeyword("_NORMALMAP");}}
            EditorUtility.SetDirty(m);return m;
        }
        private static Texture2D Pattern(string name,bool tile)
        {
            string path=Folder+"/"+name+".asset";var saved=AssetDatabase.LoadAssetAtPath<Texture2D>(path);if(saved!=null)return saved;var tex=new Texture2D(128,128,TextureFormat.RGB24,true){name=name,wrapMode=TextureWrapMode.Repeat,filterMode=FilterMode.Trilinear,anisoLevel=4};
            for(int y=0;y<128;y++)for(int x=0;x<128;x++){float v=y/128f,u=x/128f;int row=Mathf.FloorToInt(v*(tile?7:12));float a=Mathf.Repeat(u*(tile?5:4)+(row%2)*.5f,1),b=Mathf.Repeat(v*(tile?7:12),1);bool grout=a<.035f||b<.08f;float grain=Mathf.PerlinNoise(x*.47f,y*.47f)*.12f+Mathf.PerlinNoise(x*.09f,y*.09f)*.08f;Color c=tile?new Color(.50f+grain,.215f+grain*.5f,.09f+grain*.3f):new Color(.45f+grain,.235f+grain*.45f,.13f+grain*.25f);if(grout)c=tile?new Color(.26f,.14f,.075f):new Color(.59f,.53f,.42f);else if(tile)c*=.8f+.2f*Mathf.Sin(a*Mathf.PI);tex.SetPixel(x,y,c);}tex.Apply(true,false);AssetDatabase.CreateAsset(tex,path);return tex;
        }
        private static Transform Group(Transform root,string name,Vector3 p,float yaw=0){var g=new GameObject(name).transform;g.SetParent(root,false);g.localPosition=p;g.localRotation=Quaternion.Euler(0,yaw,0);return g;}
        private static GameObject Box(Transform parent,string name,Vector3 position,Vector3 size,Material mat,bool solid=false)
        {var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(parent,false);go.transform.localPosition=position;go.GetComponent<MeshFilter>().sharedMesh=Cube(size);go.GetComponent<MeshRenderer>().sharedMaterial=mat;if(solid)go.AddComponent<BoxCollider>().size=size;return go;}
        private static Mesh Cube(Vector3 size)
        {
            string key=size.ToString("F3");if(boxes.TryGetValue(key,out var cached))return cached;var v=new List<Vector3>();var uv=new List<Vector2>();var t=new List<int>();Vector3 h=size*.5f;
            Action<Vector3,Vector3,Vector3,Vector3> face=(a,b,c,d)=>{int start=v.Count;v.AddRange(new[]{a,b,c,d});var n=Vector3.Cross(b-a,d-a).normalized;foreach(var q in new[]{a,b,c,d})uv.Add(Mathf.Abs(n.y)>.5f?new Vector2(q.x,q.z):Mathf.Abs(n.x)>.5f?new Vector2(q.z,q.y):new Vector2(q.x,q.y));t.AddRange(new[]{start,start+1,start+2,start,start+2,start+3});};
            face(new Vector3(-h.x,-h.y,-h.z),new Vector3(-h.x,h.y,-h.z),new Vector3(h.x,h.y,-h.z),new Vector3(h.x,-h.y,-h.z));face(new Vector3(h.x,-h.y,h.z),new Vector3(h.x,h.y,h.z),new Vector3(-h.x,h.y,h.z),new Vector3(-h.x,-h.y,h.z));face(new Vector3(-h.x,-h.y,h.z),new Vector3(-h.x,h.y,h.z),new Vector3(-h.x,h.y,-h.z),new Vector3(-h.x,-h.y,-h.z));face(new Vector3(h.x,-h.y,-h.z),new Vector3(h.x,h.y,-h.z),new Vector3(h.x,h.y,h.z),new Vector3(h.x,-h.y,h.z));face(new Vector3(-h.x,h.y,-h.z),new Vector3(-h.x,h.y,h.z),new Vector3(h.x,h.y,h.z),new Vector3(h.x,h.y,-h.z));face(new Vector3(-h.x,-h.y,h.z),new Vector3(-h.x,-h.y,-h.z),new Vector3(h.x,-h.y,-h.z),new Vector3(h.x,-h.y,h.z));
            var mesh=new Mesh{name="Heritage cuboid"};mesh.SetVertices(v);mesh.SetUVs(0,uv);mesh.SetTriangles(t,0);mesh.RecalculateNormals();mesh.RecalculateBounds();temporary.Add(mesh);boxes[key]=mesh;return mesh;
        }
        private static Mesh Sphere()
        {
            if(sphere!=null)return sphere;var v=new List<Vector3>();var uv=new List<Vector2>();var t=new List<int>();const int columns=10,rows=7;
            for(int y=0;y<=rows;y++)for(int x=0;x<=columns;x++){float a=y*Mathf.PI/rows,b=x*Mathf.PI*2/columns;v.Add(new Vector3(Mathf.Sin(a)*Mathf.Cos(b),Mathf.Cos(a),Mathf.Sin(a)*Mathf.Sin(b)));uv.Add(new Vector2(x/(float)columns,y/(float)rows));}for(int y=0;y<rows;y++)for(int x=0;x<columns;x++){int a=y*(columns+1)+x,b=a+columns+1;t.AddRange(new[]{a,a+1,b,a+1,b+1,b});}sphere=new Mesh{name="Shared garden foliage"};sphere.SetVertices(v);sphere.SetNormals(v);sphere.SetUVs(0,uv);sphere.SetTriangles(t,0);sphere.RecalculateBounds();temporary.Add(sphere);return sphere;
        }
        private static Mesh Blossom()
        {
            if(blossom!=null)return blossom;float t=(1+Mathf.Sqrt(5))*.5f;var v=new[]{new Vector3(-1,t,0),new Vector3(1,t,0),new Vector3(-1,-t,0),new Vector3(1,-t,0),new Vector3(0,-1,t),new Vector3(0,1,t),new Vector3(0,-1,-t),new Vector3(0,1,-t),new Vector3(t,0,-1),new Vector3(t,0,1),new Vector3(-t,0,-1),new Vector3(-t,0,1)}.Select(x=>x.normalized).ToArray();blossom=new Mesh{name="Low cost garden blossoms",vertices=v,normals=v,triangles=new[]{0,11,5,0,5,1,0,1,7,0,7,10,0,10,11,1,5,9,5,11,4,11,10,2,10,7,6,7,1,8,3,9,4,3,4,2,3,2,6,3,6,8,3,8,9,4,9,5,2,4,11,6,2,10,8,6,7,9,8,1},uv=v.Select(x=>new Vector2(x.x,x.z)).ToArray()};blossom.RecalculateBounds();temporary.Add(blossom);return blossom;
        }
        private static GameObject Ball(Transform parent,string name,Vector3 p,Vector3 scale,Material mat)
        {var g=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));g.transform.SetParent(parent,false);g.transform.localPosition=p;g.transform.localScale=scale;g.GetComponent<MeshFilter>().sharedMesh=name.IndexOf("flower",StringComparison.OrdinalIgnoreCase)>=0||name.IndexOf("geranium",StringComparison.OrdinalIgnoreCase)>=0||name.Contains("greens")||name.Contains("roses")||name.Contains("fruit")?Blossom():Sphere();g.GetComponent<MeshRenderer>().sharedMaterial=mat;return g;}
        private static void Beam(Transform parent,string name,Vector3 a,Vector3 b,float width,Material mat,bool solid=false){var g=Box(parent,name,(a+b)*.5f,new Vector3(width,(b-a).magnitude,width),mat,solid);g.transform.localRotation=Quaternion.FromToRotation(Vector3.up,b-a);}
        private static void Surface(Transform root,string name,Rect r,float top,Material mat,bool solid=false){float bottom=Mathf.Min(-.025f,top-.05f);Box(root,name,new Vector3(r.center.x,(top+bottom)*.5f,r.center.y),new Vector3(r.width,top-bottom,r.height),mat,solid);}
        private static void Paved(Transform root,string name,Rect rect,float height,List<Rect> roads,bool solid=true)
        {foreach(var piece in StreetPavingLayout.ClearRoads(rect,roads))Surface(root,name,piece,height,paving,solid);}
        private static void Streets(Transform root,int site,List<Rect> allRoads)
        {
            var roads=HeritageNeighborhoodLayout.Roads(site);
            var cuts=new List<Rect>(allRoads);for(int i=0;i<12;i++){cuts.Add(HeritageNeighborhoodLayout.Driveway(site,i));var p=HeritageNeighborhoodLayout.House(site,i);cuts.Add(Rect.MinMaxRect(p.x-1.3f,p.z-30,p.x+1.3f,p.z+30));}
            foreach(var road in roads)
            {
                bool horizontal=road.width>road.height;for(int side=-1;side<=1;side+=2){float edge=horizontal?(side<0?road.yMin:road.yMax):(side<0?road.xMin:road.xMax),center=edge+side*1.4f;Rect path=horizontal?Rect.MinMaxRect(road.xMin,center-1.4f,road.xMax,center+1.4f):Rect.MinMaxRect(center-1.4f,road.yMin,center+1.4f,road.yMax);foreach(var piece in StreetPavingLayout.ClearRoads(path,cuts))Surface(root,"Heritage public sidewalk",piece,.10f,paving,true);}
                float length=horizontal?road.width:road.height;for(float d=12;d<length-8;d+=13){Vector3 p=horizontal?new Vector3(road.xMin+d,.064f,road.center.y):new Vector3(road.center.x,.064f,road.yMin+d);Box(root,"Local lane paint",p,horizontal?new Vector3(3,.003f,.10f):new Vector3(.10f,.003f,3),ivory);}
            }
            for(int i=0;i<12;i++){var driveway=HeritageNeighborhoodLayout.Driveway(site,i);driveway.xMin-=.06f;driveway.xMax+=.06f;Paved(root,"Flush residential driveway",driveway,.065f,allRoads);var p=HeritageNeighborhoodLayout.House(site,i);float z=HeritageNeighborhoodLayout.Centers[site].y;Paved(root,"Open front garden approach",Rect.MinMaxRect(p.x-1.36f,i<6?z+4.01f:p.z,p.x+1.36f,i<6?p.z:z-4.01f),.067f,allRoads);}
        }
        private static void RepairPaving(Transform motel,Transform root,List<Rect> additions,List<Rect> allRoads)
        {
            foreach(var r in motel.GetComponentsInChildren<MeshRenderer>(true).ToArray())
            {if(r.transform.IsChildOf(root)||!r.enabled||!r.gameObject.activeInHierarchy)continue;string name=r.name.ToLowerInvariant();if(!name.Contains("sidewalk")&&!name.Contains("curb"))continue;var b=r.bounds;var f=r.GetComponent<MeshFilter>();if(f==null||f.sharedMesh==null||f.sharedMesh.vertexCount>24||b.max.y>.4f)continue;var area=Rect.MinMaxRect(b.min.x,b.min.z,b.max.x,b.max.z);if(!additions.Any(x=>x.Overlaps(area)))continue;r.gameObject.SetActive(false);foreach(var piece in StreetPavingLayout.ClearRoads(area,allRoads))Surface(root,"Repaired heritage road entry sidewalk",piece,b.max.y,r.sharedMaterial,true);}
        }
        private static void ParkConnections(Transform root,int site,List<Rect> roads)
        {
            if(site==0)
            {
                Paved(root,"Playground gate promenade",Rect.MinMaxRect(-109,-140.5f,-82,-137.5f),.10f,roads);
                Paved(root,"Flush playground crossing approach",Rect.MinMaxRect(-121,-141,-105,-137),.065f,roads);
                Paved(root,"Park north neighbourhood link",Rect.MinMaxRect(-83.5f,-98,-80.5f,-71),.10f,roads);
                Paved(root,"Park boulevard outer corner sidewalk",Rect.MinMaxRect(-48,-161.8f,-41.2f,-159),.10f,roads);
                // Decorative paint has no collider and sits only millimetres above the welded road.
                for(float x=-116.5f;x<-109;x+=1.1f)Box(root,"Playground pedestrian crossing paint",new Vector3(x,.063f,-139),new Vector3(.55f,.002f,4),ivory);
                for(float z=-158.5f;z<-151;z+=1.1f)Box(root,"Park boulevard pedestrian crossing paint",new Vector3(-82,.063f,z),new Vector3(4,.002f,.55f),ivory);
                foreach(float z in new[]{-124f,-88f})Lantern(root,new Vector3(-41,0,z),3.2f);
            }
            else
            {
                for(float z=391.5f;z<399;z+=1.1f)Box(root,"Willow park pedestrian crossing paint",new Vector3(690,.063f,z),new Vector3(4,.002f,.55f),ivory);
                Paved(root,"Willow park north link",Rect.MinMaxRect(688.5f,451,691.5f,476),.10f,roads);
            }
        }

        private static void House(Transform root,int site,int index)
        {
            var p=HeritageNeighborhoodLayout.House(site,index);var house=Group(root,"Heritage home "+site+"-"+index,p,index<6?0:180);int style=(site*7+index)%5;bool upper=index%4==0;Material wall=new[]{cream,sage,ochre,rose,blue}[style];float height=upper?6.7f:3.65f;
            Surface(house,"Private garden lawn",Rect.MinMaxRect(-13.6f,-22.5f,13.6f,25.5f),.045f,lawn);Box(house,"Old brick plinth",new Vector3(0,.28f,0),new Vector3(13,.48f,12.8f),brick,true);Box(house,"Heritage solid house",new Vector3(0,.45f+height*.5f,0),new Vector3(12.2f,height,12),wall,true);Box(house,"Brick lower wall course",new Vector3(0,.62f,0),new Vector3(12.35f,.48f,12.14f),brick);
            foreach(float y in new[]{1f,height+.42f})Box(house,"Traditional wall string course",new Vector3(0,y,0),new Vector3(12.55f,.13f,12.4f),ivory);Roof(house,new Vector3(0,height+.65f,0),6.85f,6.8f,2.1f,index%3==0?roofDark:roof,wall);Box(house,"Brick chimney",new Vector3(3.7f,height+1.6f,2.3f),new Vector3(.85f,3.0f,.8f),brick,true);Box(house,"Chimney crown",new Vector3(3.7f,height+3.1f,2.3f),new Vector3(1.1f,.15f,1.05f),paving);
            for(int side=-1;side<=1;side+=2){Box(house,"Corner dressed stone",new Vector3(side*6.18f,height*.5f+.45f,-5.95f),new Vector3(.25f,height,.26f),ivory);Window(house,new Vector3(side*3.65f,2.1f,-6.085f),style%2==0?sage:blue,true);var bay=Group(house,"Side shutter window",new Vector3(side*6.17f,0,0),side<0?90:-90);Window(bay,new Vector3(0,2.1f,0),style%2==0?blue:sage,false);if(upper)Window(house,new Vector3(side*3.65f,5.2f,-6.085f),sage,true);}
            Box(house,"House doorway",new Vector3(0,1.66f,-6.13f),new Vector3(1.35f,2.42f,.16f),wood,true);foreach(float x in new[]{-.4f,.4f})foreach(float y in new[]{.95f,1.85f})Box(house,"Raised door panel",new Vector3(x,y,-6.24f),new Vector3(.48f,.62f,.035f),brick);Ball(house,"Brass door handle",new Vector3(.44f,1.55f,-6.28f),new Vector3(.045f,.045f,.045f),flowerGold);
            var rear=Group(house,"Rear garden door and shutters",new Vector3(0,0,6.09f),180);Window(rear,new Vector3(-3.65f,2.1f,0),sage,true);Window(rear,new Vector3(3.65f,2.1f,0),blue,true);Box(rear,"Back garden doorway",new Vector3(0,1.66f,-.08f),new Vector3(1.35f,2.42f,.16f),wood,true);
            for(int step=0;step<2;step++)Box(house,"Back garden porch step",new Vector3(0,.08f+step*.16f,7.25f-step*.45f),new Vector3(2.1f,.16f,.8f),paving,true);
            if(upper){Window(rear,new Vector3(-3.65f,5.2f,0),sage,false);Window(rear,new Vector3(3.65f,5.2f,0),blue,false);}
            Porch(house,upper?ivory:wood,height,style);BackGarden(house,site,index);
            FenceLine(house,new Vector3(-13.7f,0,-21),new Vector3(-10.4f,0,-21));FenceLine(house,new Vector3(-8,0,-21),new Vector3(-1.8f,0,-21));FenceLine(house,new Vector3(1.8f,0,-21),new Vector3(6.8f,0,-21));FenceLine(house,new Vector3(10.8f,0,-21),new Vector3(13.7f,0,-21));FenceLine(house,new Vector3(-13.7f,0,-21),new Vector3(-13.7f,0,25.5f));FenceLine(house,new Vector3(13.7f,0,-21),new Vector3(13.7f,0,25.5f));FenceLine(house,new Vector3(-13.7f,0,25.5f),new Vector3(13.7f,0,25.5f));
            for(int k=0;k<5;k++)Surface(house,"Front stepping stone",Rect.MinMaxRect(-.95f,-19+k*1.65f,.95f,-17.65f+k*1.65f),.08f,paving);Surface(house,"Side garden access path",Rect.MinMaxRect(-10.2f,-21,-8.1f,19),.072f,paving);FlowerBed(house,new Vector3(-4.5f,0,-16),new Vector2(4.6f,2.6f),index);FlowerBed(house,new Vector3(4.5f,0,-16),new Vector2(3.2f,2.6f),index+1);Lantern(house,new Vector3(-1.7f,0,-8.2f),1.6f);Plaque(house,(site==0?"OAK":"WILLOW")+"  "+(index+1).ToString("00"),new Vector3(3,0,-21.15f),2.4f);
        }
        private static void Porch(Transform root,Material column,float height,int style)
        {
            Box(root,"Veranda raised deck",new Vector3(0,.37f,-8.3f),new Vector3(12.9f,.20f,4.3f),paving,true);for(int step=0;step<2;step++)Box(root,"Low porch step",new Vector3(0,.08f+step*.16f,-11+step*.45f),new Vector3(3.2f,.16f,1.5f),brick,true);
            foreach(float x in new[]{-5.7f,-2.2f,2.2f,5.7f}){Box(root,"Turned veranda column",new Vector3(x,1.98f,-9.9f),new Vector3(.18f,3.05f,.18f),column,true);Box(root,"Veranda column base",new Vector3(x,.63f,-9.9f),new Vector3(.39f,.29f,.39f),ivory);Box(root,"Veranda column capital",new Vector3(x,3.42f,-9.9f),new Vector3(.42f,.22f,.42f),ivory);Beam(root,"Porch decorative brace",new Vector3(x,2.95f,-9.9f),new Vector3(x+.75f,3.5f,-9.9f),.08f,column);}
            Box(root,"Veranda timber fascia",new Vector3(0,3.56f,-10),new Vector3(13.3f,.28f,.18f),column);var canopy=Box(root,"Weathered veranda tile canopy",new Vector3(0,height>5?3.62f:3.78f,-8.5f),new Vector3(13.6f,.16f,4.4f),roof);canopy.transform.localRotation=height>5?Quaternion.identity:Quaternion.Euler(-8,0,0);
            foreach(int side in new[]{-1,1}){Box(root,"Porch balustrade rail",new Vector3(side*3.8f,1.23f,-10),new Vector3(4.1f,.10f,.10f),ivory);for(int i=0;i<9;i++)Box(root,"Porch baluster",new Vector3(side*(1.9f+i*.46f),.91f,-10),new Vector3(.065f,.63f,.065f),ivory);}
            Bench(root,new Vector3(-3.8f,.49f,-7.4f),2.7f);Pot(root,new Vector3(4.2f,.48f,-8),.75f,style);
            if(height>5){Box(root,"Upper veranda balcony",new Vector3(0,3.82f,-7),new Vector3(11.8f,.18f,2.2f),wood);for(int i=0;i<20;i++)Box(root,"Upper balcony baluster",new Vector3(-5.5f+i*.58f,4.36f,-8.05f),new Vector3(.07f,1.0f,.07f),ivory);Box(root,"Upper balcony handrail",new Vector3(0,4.9f,-8.05f),new Vector3(11.8f,.09f,.12f),wood);}
        }
        private static void Window(Transform root,Vector3 p,Material shutter,bool flowers)
        {
            Box(root,"Deep window reveal",p,new Vector3(1.9f,1.65f,.12f),wood);Box(root,"Vintage window glazing",p+new Vector3(0,0,-.075f),new Vector3(1.6f,1.40f,.08f),glass);foreach(float x in new[]{-.86f,0,.86f})Box(root,"Window timber mullion",p+new Vector3(x,0,-.14f),new Vector3(.075f,1.72f,.065f),ivory);foreach(float y in new[]{-.8f,0,.8f})Box(root,"Window timber transom",p+new Vector3(0,y,-.14f),new Vector3(1.85f,.065f,.065f),ivory);Box(root,"Deep window sill",p+new Vector3(0,-.94f,-.15f),new Vector3(2.2f,.15f,.37f),paving);
            for(int side=-1;side<=1;side+=2){Box(root,"Painted wooden shutter",p+new Vector3(side*1.22f,0,-.04f),new Vector3(.55f,1.75f,.08f),shutter);for(int slat=0;slat<7;slat++)Box(root,"Shutter louvre",p+new Vector3(side*1.22f,-.66f+slat*.22f,-.1f),new Vector3(.45f,.065f,.08f),wood);}
            if(flowers){Box(root,"Window flower box",p+new Vector3(0,-1.11f,-.23f),new Vector3(1.8f,.28f,.42f),wood);for(int i=0;i<7;i++)Ball(root,"Window geranium",p+new Vector3(-.7f+i*.23f,-.87f,-.27f),new Vector3(.16f,.14f,.14f),i%2==0?flowerPink:leaf);}
        }
        private static void Roof(Transform root,Vector3 p,float w,float d,float rise,Material tile,Material gable)
        {
            var v=new[]{new Vector3(-w,0,-d),new Vector3(-w,0,d),new Vector3(0,rise,d),new Vector3(0,rise,-d),new Vector3(0,rise,-d),new Vector3(0,rise,d),new Vector3(w,0,d),new Vector3(w,0,-d)};MeshPart(root,"Pitched terracotta roof",p,v,new[]{0,1,2,0,2,3,4,5,6,4,6,7},tile);MeshPart(root,"Plastered triangular gables",p,new[]{new Vector3(-w,0,-d+.08f),new Vector3(0,rise,-d+.08f),new Vector3(w,0,-d+.08f),new Vector3(-w,0,d-.08f),new Vector3(w,0,d-.08f),new Vector3(0,rise,d-.08f)},new[]{0,1,2,3,4,5},gable);Beam(root,"Terracotta ridge cap",p+new Vector3(0,rise,-d-.15f),p+new Vector3(0,rise,d+.15f),.22f,tile);
            foreach(int side in new[]{-1,1}){Beam(root,"Painted gable bargeboard",p+new Vector3(-w-.05f,0,side*d),p+new Vector3(0,rise+.06f,side*d),.13f,ivory);Beam(root,"Painted gable bargeboard",p+new Vector3(0,rise+.06f,side*d),p+new Vector3(w+.05f,0,side*d),.13f,ivory);Box(root,"Old rain gutter",p+new Vector3(side*w,-.08f,0),new Vector3(.10f,.12f,d*2),iron);}
            for(float z=-d+.45f;z<d;z+=.9f)for(int side=-1;side<=1;side+=2)Beam(root,"Terracotta tile seam",p+new Vector3(side*w,.025f,z),p+new Vector3(0,rise+.025f,z),.026f,tile);
        }
        private static void BackGarden(Transform root,int site,int index)
        {
            Surface(root,"Back garden terrace",Rect.MinMaxRect(-5.5f,7,5.5f,12),.1f,paving,true);Bench(root,new Vector3(-3,0,10),2.8f);Table(root,new Vector3(2.5f,0,10));Pergola(root,new Vector3(-5,0,18),index%2==0);FlowerBed(root,new Vector3(7.9f,0,17.5f),new Vector2(6,4.1f),index+site);
            for(int i=0;i<2;i++){var bed=Group(root,"Raised vegetable bed",new Vector3(5.8f+i*3.7f,0,22));Box(bed,"Vegetable bed timber",Vector3.up*.22f,new Vector3(3.2f,.38f,3.2f),wood,true);Box(bed,"Vegetable bed soil",Vector3.up*.43f,new Vector3(2.9f,.05f,2.9f),soil);for(int x=0;x<3;x++)for(int z=0;z<3;z++)Ball(bed,"Kitchen garden greens",new Vector3(-1+x,.62f,-1+z),new Vector3(.3f,.19f,.32f),leafLight);}
            Tree(root,new Vector3(-10,0,20),index%3,1);Tree(root,new Vector3(10,0,9.4f),(index+1)%3,.85f);for(int i=0;i<6;i++){float z=12.8f+i*1.8f;var stone=Box(root,"Garden stepping stone",new Vector3(-.7f+Mathf.Sin(i)*.55f,.075f,z),new Vector3(1.1f,.08f,1.05f),paving);stone.transform.localRotation=Quaternion.Euler(0,i*27,0);}
            Box(root,"Rain water barrel",new Vector3(-6.9f,.59f,6.8f),new Vector3(.8f,1.1f,.8f),wood,true);Box(root,"Garden tool bench",new Vector3(-11,.7f,11),new Vector3(1.1f,1.3f,2.6f),wood,true);Lantern(root,new Vector3(5.1f,0,11),1.1f);Pot(root,new Vector3(-2.4f,0,7.6f),.65f,index+1);
            foreach(float x in new[]{-11f,-7f})Beam(root,"Clothesline post",new Vector3(x,.1f,23.2f),new Vector3(x,2.1f,23.2f),.055f,iron);Beam(root,"Garden clothesline",new Vector3(-11,2.1f,23.2f),new Vector3(-7,2.1f,23.2f),.018f,ivory);for(int i=0;i<3;i++)Box(root,"Drying garden linen",new Vector3(-10.5f+i*1.25f,1.63f,23.2f),new Vector3(.78f,.85f,.025f),i%2==0?ivory:blue);
        }
        private static void FenceLine(Transform root,Vector3 a,Vector3 b)
        {
            float length=Vector3.Distance(a,b);Vector3 direction=(b-a).normalized;int count=Mathf.Max(1,Mathf.CeilToInt(length/.52f));for(int i=0;i<=count;i++){Vector3 p=Vector3.Lerp(a,b,i/(float)count);Box(root,"Garden picket fence",p+Vector3.up*.68f,new Vector3(.075f,1.2f,.075f),ivory);if(i%5==0)Box(root,"Fence post cap",p+Vector3.up*1.33f,new Vector3(.18f,.11f,.18f),wood);}Beam(root,"Picket fence lower rail",a+Vector3.up*.38f,b+Vector3.up*.38f,.06f,wood);Beam(root,"Picket fence upper rail",a+Vector3.up*.98f,b+Vector3.up*.98f,.06f,wood);var barrier=new GameObject("Garden fence collision");barrier.transform.SetParent(root,false);barrier.transform.localPosition=(a+b)*.5f+Vector3.up*.65f;barrier.transform.localRotation=Quaternion.LookRotation(direction);barrier.AddComponent<BoxCollider>().size=new Vector3(.09f,1.3f,length);
        }
        private static void FlowerBed(Transform root,Vector3 p,Vector2 size,int seed)
        {
            var bed=Group(root,"Rose and marigold flower border",p);Box(bed,"Flower bed brick edging",Vector3.up*.16f,new Vector3(size.x,.3f,size.y),brick,true);Box(bed,"Flower bed soil",Vector3.up*.33f,new Vector3(size.x-.22f,.04f,size.y-.22f),soil);int columns=Mathf.CeilToInt(size.x/.6f),rows=Mathf.CeilToInt(size.y/.65f);
            for(int x=0;x<columns;x++)for(int z=0;z<rows;z++){float px=(x+.5f)/columns*size.x-size.x*.5f,pz=(z+.5f)/rows*size.y-size.y*.5f;Ball(bed,"Flower border leaves",new Vector3(px,.52f,pz),new Vector3(.29f,.19f,.29f),leaf);Material flower=(x+z+seed)%3==0?flowerPink:(x+z+seed)%3==1?flowerGold:flowerWhite;Ball(bed,"Garden flowers",new Vector3(px+.07f,.72f,pz),new Vector3(.17f,.13f,.17f),flower);}
        }
        private static void Tree(Transform root,Vector3 p,int style,float scale)
        {
            var tree=Group(root,"Heritage orchard tree",p);tree.localScale=Vector3.one*scale;Box(tree,"Garden tree trunk",new Vector3(0,1.75f,0),new Vector3(.24f,3.5f,.25f),wood,true);for(int i=0;i<4;i++){float angle=(i*95+style*27)*Mathf.Deg2Rad;var top=new Vector3(Mathf.Cos(angle)*1.2f,3.4f+(i%2)*.6f,Mathf.Sin(angle)*1.2f);Beam(tree,"Orchard branch",new Vector3(0,2.2f,0),top,.12f,wood);Ball(tree,"Garden tree crown",top+Vector3.up*.5f,new Vector3(1.7f,1.3f,1.6f),i%2==0?leaf:leafLight);}if(style==1)for(int i=0;i<7;i++)Ball(tree,"Garden fruit",new Vector3(Mathf.Sin(i*2)*1.6f,3.0f+(i%3)*.3f,Mathf.Cos(i*2)*1.4f),Vector3.one*.12f,flowerGold);
        }
        private static void Pot(Transform root,Vector3 p,float scale,int style){var g=Group(root,"Terracotta garden pot",p);g.localScale=Vector3.one*scale;Box(g,"Terracotta planter",Vector3.up*.35f,new Vector3(.65f,.65f,.65f),roof,true);Ball(g,"Potted shrub",Vector3.up*.85f,new Vector3(.48f,.55f,.48f),style%2==0?leafLight:leaf);}
        private static void Bench(Transform root,Vector3 p,float width)
        {var g=Group(root,"Old timber garden bench",p);for(int i=0;i<4;i++)Box(g,"Bench slatted seat",new Vector3(0,.52f,-.27f+i*.18f),new Vector3(width,.09f,.14f),wood);for(int i=0;i<3;i++)Box(g,"Bench slatted back",new Vector3(0,.8f+i*.18f,.35f),new Vector3(width,.12f,.075f),wood);foreach(int side in new[]{-1,1}){Beam(g,"Bench iron leg",new Vector3(side*(width*.37f),.04f,-.24f),new Vector3(side*(width*.37f),.53f,-.24f),.09f,iron,true);Beam(g,"Bench iron back leg",new Vector3(side*(width*.37f),.04f,.29f),new Vector3(side*(width*.37f),1.24f,.29f),.09f,iron,true);}}
        private static void Table(Transform root,Vector3 p){Box(root,"Garden table top",p+Vector3.up*.81f,new Vector3(1.8f,.12f,1.4f),wood,true);foreach(int side in new[]{-1,1})Beam(root,"Garden table legs",p+new Vector3(side*.65f,.05f,0),p+new Vector3(side*.65f,.78f,0),.1f,iron,true);Pot(root,p+new Vector3(0,.88f,0),.25f,1);}
        private static void Pergola(Transform root,Vector3 p,bool vines)
        {
            var g=Group(root,"Garden rose pergola",p);foreach(float x in new[]{-2.25f,2.25f})foreach(float z in new[]{-1.65f,1.65f})Box(g,"Pergola timber post",new Vector3(x,1.4f,z),new Vector3(.15f,2.8f,.15f),wood,true);foreach(float x in new[]{-2.3f,2.3f})Box(g,"Pergola roof bearer",new Vector3(x,2.85f,0),new Vector3(.16f,.19f,4),wood);for(int i=0;i<8;i++)Box(g,"Pergola overhead slat",new Vector3(-2.6f+i*.75f,3.0f,0),new Vector3(.13f,.16f,4.5f),wood);if(vines)for(int i=0;i<8;i++){Ball(g,"Climbing rose foliage",new Vector3(-2.2f+i*.62f,3.07f,1.5f),new Vector3(.55f,.18f,.4f),leaf);Ball(g,"Climbing roses",new Vector3(-2.2f+i*.62f,3.19f,1.48f),Vector3.one*.16f,flowerPink);}Bench(g,new Vector3(0,0,.8f),3);Surface(g,"Pergola paving",Rect.MinMaxRect(-2.6f,-2,2.6f,2),.08f,paving);
        }
        private static void Lantern(Transform root,Vector3 p,float height)
        {Beam(root,"Heritage lantern pole",p+Vector3.up*.05f,p+Vector3.up*height,.07f,iron,true);Box(root,"Lantern base",p+Vector3.up*.15f,new Vector3(.32f,.25f,.32f),paving);var r=Box(root,"Warm garden lantern",p+Vector3.up*(height+.13f),new Vector3(.24f,.34f,.24f),warm).GetComponent<Renderer>();bulbs.Add(r);Box(root,"Lantern dark cap",p+Vector3.up*(height+.34f),new Vector3(.36f,.10f,.36f),iron);}
        private static void Plaque(Transform root,string text,Vector3 p,float width)
        {var g=Group(root,"Framed neighbourhood sign",p);Box(g,"Heritage sign board",Vector3.up*1.45f,new Vector3(width,.85f,.18f),wood,true);foreach(int side in new[]{-1,1})Box(g,"Sign support",new Vector3(side*(width*.39f),.73f,0),new Vector3(.10f,1.5f,.1f),iron,true);var label=new GameObject("Heritage sign lettering").AddComponent<TextMeshPro>();label.transform.SetParent(g,false);label.transform.localPosition=new Vector3(0,1.45f,-.11f);label.font=TMP_Settings.GetFontAsset();label.text=text;label.fontSize=width<3?2.6f:4.5f;label.color=ivory.color;label.alignment=TextAlignmentOptions.Center;label.rectTransform.sizeDelta=new Vector2(width-.15f,.7f);label.raycastTarget=false;}

        private static void Park(Transform root,int site,List<Rect> roads)
        {
            var p=Group(root,site==0?"Oak Lane family playground and garden":"Willow pocket playground",new Vector3(site==0?-82:690,0,site==0?-155:395));float half=site==0?21:12;
            var localRoads=roads.Select(r=>Rect.MinMaxRect(r.xMin-p.position.x,r.yMin-p.position.z,r.xMax-p.position.x,r.yMax-p.position.z)).ToList();
            foreach(var piece in StreetPavingLayout.ClearRoads(Rect.MinMaxRect(-half,-64,half,58),localRoads))Surface(p,"Neighbourhood park lawn",piece,.055f,lawn);
            Paved(p,"Park central accessible path",Rect.MinMaxRect(-1.5f,site==0?-43:-62,1.5f,57),.10f,localRoads);for(int i=0;i<4;i++)Tree(p,new Vector3((i%2==0?-1:1)*(half-3),0,-51+i*31),i,1.2f);
            var play=Group(p,"Playground safe play court",new Vector3(0,0,28));Surface(play,"Playground safety surface",Rect.MinMaxRect(-half+2,-18,half-2,18),.085f,rubber);Swing(play,new Vector3(-Mathf.Min(half-6,9),0,7));PlayTower(play,new Vector3(Mathf.Min(half-5,8),0,2));Seesaw(play,new Vector3(-4,0,-9));Roundabout(play,new Vector3(half>16?9:3,0,-10));
            PlayDetails(play,half);var sand=Group(p,"Toddlers sandbox",new Vector3(-half+5,0,-14));Box(sand,"Sandbox timber rim",Vector3.up*.2f,new Vector3(6,.32f,5),wood,true);Box(sand,"Sandbox clean sand",Vector3.up*.37f,new Vector3(5.7f,.04f,4.7f),cream);Pot(sand,new Vector3(1,.4f,0),.4f,1);
            foreach(int side in new[]{-1,1}){Bench(p,new Vector3(side*(half-2),0,8),2.7f);Lantern(p,new Vector3(side*(half-2),0,7),2.5f);FlowerBed(p,new Vector3(side*(half-4),0,-20),new Vector2(5,3),site+side+3);}Pergola(p,new Vector3(4,0,-34),true);Bench(p,new Vector3(-5,0,-35),3.3f);Table(p,new Vector3(7,0,-42));for(int i=0;i<8;i++){float x=i%2==0?-2.2f:-.8f,z=42+i*.9f;Box(p,"Hopscotch chalk square",new Vector3(x,.11f,z),new Vector3(.95f,.009f,.77f),i%2==0?ivory:playBlue);}
            if(site==0){var court=Group(p,"Neighbourhood small sports court",new Vector3(0,0,-53));Surface(court,"Small sports court floor",Rect.MinMaxRect(-15,-8,15,8),.09f,playBlue);foreach(int side in new[]{-1,1}){Box(court,"Sports court sideline",new Vector3(0,.099f,side*7),new Vector3(28,.008f,.08f),ivory);Box(court,"Sports court end line",new Vector3(side*14,.099f,0),new Vector3(.08f,.008f,14),ivory);Goal(court,new Vector3(side*13,0,0),side*90);}Box(court,"Sports court centre line",new Vector3(0,.10f,0),new Vector3(.08f,.008f,14),ivory);}
            foreach(int side in new[]{-1,1})FlowerBed(p,new Vector3(side*(half-4),0,46),new Vector2(4,1.5f),site+2);
            Plaque(p,site==0?"OAK LANE PLAYGROUND":"WILLOW PLAY GARDEN",new Vector3(0,0,57),site==0?10:8);
        }
        private static void PlayDetails(Transform root,float half)
        {
            float swingX=-Mathf.Min(half-6,9),towerX=Mathf.Min(half-5,8);
            Surface(root,"Swing safety zone",Rect.MinMaxRect(swingX-4,3,swingX+4,11),.092f,playBlue);
            Surface(root,"Slide soft landing zone",Rect.MinMaxRect(towerX-2.7f,-4,towerX+2.7f,10),.093f,playRed);
            Surface(root,"Seesaw soft landing zone",Rect.MinMaxRect(-7,-11.3f,-1,-6.7f),.094f,playBlue);
            for(int side=-1;side<=1;side+=2)
            {
                FenceLine(root,new Vector3(-half+1,0,side*19),new Vector3(-2,0,side*19));FenceLine(root,new Vector3(2,0,side*19),new Vector3(half-1,0,side*19));
                if(side<0&&half>16){FenceLine(root,new Vector3(side*(half-1),0,-19),new Vector3(side*(half-1),0,-14));FenceLine(root,new Vector3(side*(half-1),0,-10),new Vector3(side*(half-1),0,19));}
                else FenceLine(root,new Vector3(side*(half-1),0,-19),new Vector3(side*(half-1),0,19));
                Box(root,"Playground perimeter chalk",new Vector3(0,.1f,side*16.6f),new Vector3(half*2-6,.01f,.09f),ivory);
            }
            if(half>16)
            {
                var climb=Group(root,"Arched climbing frame",new Vector3(-10,0,-1));Vector3 previous=Vector3.zero;
                for(int i=0;i<=10;i++)
                {
                    float angle=i*Mathf.PI/10;Vector3 point=new Vector3(0,.15f+Mathf.Sin(angle)*2.3f,-Mathf.Cos(angle)*2.7f);
                    Beam(climb,"Climbing frame rung",point+Vector3.left*1.4f,point+Vector3.right*1.4f,.075f,i%2==0?playBlue:playRed);
                    if(i>0)foreach(int side in new[]{-1,1})Beam(climb,"Climbing frame arch rail",previous+Vector3.right*side*1.4f,point+Vector3.right*side*1.4f,.1f,iron,i==1||i==10);previous=point;
                }
            }
            foreach(int side in new[]{-1,1})Bench(root,new Vector3(side*(half-3),0,-15),2.6f);
        }
        private static void Swing(Transform root,Vector3 p)
        {
            var g=Group(root,"Twin garden swings",p);foreach(float x in new[]{-2.9f,2.9f})foreach(int side in new[]{-1,1})Beam(g,"Swing fixed A-frame",new Vector3(x,.1f,side*1.45f),new Vector3(x,3.3f,0),.13f,playRed,true);Box(g,"Swing fixed top beam",Vector3.up*3.3f,new Vector3(6.2f,.17f,.17f),playBlue,true);foreach(float x in new[]{-1.4f,1.4f}){var pivot=Group(g,"Animated swing",new Vector3(x,3.22f,0));swings.Add(pivot);foreach(int side in new[]{-1,1})Beam(pivot,"Swing chain",new Vector3(side*.42f,0,0),new Vector3(side*.42f,-2.45f,0),.027f,iron);Box(pivot,"Swing seat",new Vector3(0,-2.45f,0),new Vector3(1.05f,.12f,.48f),wood);}
        }
        private static void PlayTower(Transform root,Vector3 p)
        {
            var g=Group(root,"Climbable timber play fort",p);foreach(int side in new[]{-1,1})foreach(int end in new[]{-1,1})Box(g,"Play fort support",new Vector3(side*1.15f,1.5f,end*1.15f),new Vector3(.14f,3,.14f),wood,true);Box(g,"Play fort platform",Vector3.up*1.62f,new Vector3(2.5f,.18f,2.5f),playBlue,true);Roof(g,new Vector3(0,3.1f,0),1.65f,1.6f,.85f,roof,ochre);foreach(int side in new[]{-1,1})Box(g,"Play fort safety rail",new Vector3(side*1.15f,2.3f,0),new Vector3(.1f,.7f,2.5f),playRed,true);for(int i=0;i<9;i++)Box(g,"Walkable playground stair",new Vector3(0,.09f+i*.18f,-5+i*.43f),new Vector3(1.0f,.18f,.55f),wood,true);
            var slide=Box(g,"Sloped playground slide",new Vector3(0,.91f,3.4f),new Vector3(1.0f,.10f,4.5f),ivory,true);slide.transform.localRotation=Quaternion.Euler(21,0,0);foreach(int side in new[]{-1,1}){var rail=Box(g,"Slide side rail",new Vector3(side*.54f,1.03f,3.4f),new Vector3(.10f,.22f,4.5f),playRed);rail.transform.localRotation=Quaternion.Euler(21,0,0);}
        }
        private static void Seesaw(Transform root,Vector3 p)
        {var g=Group(root,"Animated garden seesaw",p);Box(g,"Seesaw base",Vector3.up*.35f,new Vector3(.8f,.7f,.8f),playBlue,true);var pivot=Group(g,"Seesaw pivot",Vector3.up*.8f);seesaws.Add(pivot);Box(pivot,"Seesaw beam",Vector3.zero,new Vector3(5,.14f,.28f),wood);foreach(int side in new[]{-1,1}){Box(pivot,"Seesaw seat",new Vector3(side*1.95f,.07f,0),new Vector3(.7f,.12f,.6f),playRed);Beam(pivot,"Seesaw handhold",new Vector3(side*1.55f,.1f,0),new Vector3(side*1.55f,.48f,0),.05f,iron);}}
        private static void Roundabout(Transform root,Vector3 p)
        {var g=Group(root,"Slow playground roundabout",p);Box(g,"Roundabout fixed base",Vector3.up*.2f,new Vector3(.7f,.4f,.7f),iron,true);var spin=Group(g,"Roundabout rotating deck",Vector3.up*.36f);roundabouts.Add(spin);for(int side=-1;side<=1;side+=2){Box(spin,"Roundabout timber deck",new Vector3(side*.6f,0,0),new Vector3(1.2f,.13f,2.4f),wood);for(int end=-1;end<=1;end+=2){Beam(spin,"Roundabout handrail upright",new Vector3(side*.95f,.1f,end*.95f),new Vector3(side*.95f,.9f,end*.95f),.05f,playBlue);Beam(spin,"Roundabout handrail",new Vector3(side*.95f,.9f,-.95f),new Vector3(side*.95f,.9f,.95f),.05f,playBlue);}}}
        private static void Goal(Transform root,Vector3 p,float yaw)
        {var g=Group(root,"Small football goal",p,yaw);foreach(int side in new[]{-1,1}){Beam(g,"Goal post",new Vector3(side*1.5f,0,0),new Vector3(side*1.5f,1.8f,0),.07f,ivory,true);Beam(g,"Goal net side",new Vector3(side*1.5f,1.8f,0),new Vector3(side*1.5f,.05f,1),.035f,ivory);}Beam(g,"Goal crossbar",new Vector3(-1.5f,1.8f,0),new Vector3(1.5f,1.8f,0),.07f,ivory);for(int i=0;i<9;i++)Beam(g,"Goal net cord",new Vector3(-1.5f+i*.375f,1.8f,0),new Vector3(-1.5f+i*.375f,.05f,1),.009f,ivory);}
        private static void MeshPart(Transform root,string name,Vector3 position,Vector3[] vertices,int[] triangles,Material mat)
        {var mesh=new Mesh{name=name,vertices=vertices,triangles=triangles,uv=vertices.Select(v=>new Vector2(v.x,v.z)).ToArray()};mesh.RecalculateNormals();mesh.RecalculateBounds();temporary.Add(mesh);var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(root,false);go.transform.localPosition=position;go.GetComponent<MeshFilter>().sharedMesh=mesh;go.GetComponent<MeshRenderer>().sharedMaterial=mat;}
        private static Mesh Save(Mesh mesh,string path)
        {var old=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(old==null){AssetDatabase.CreateAsset(mesh,path);return mesh;}old.Clear();EditorUtility.CopySerialized(mesh,old);old.UploadMeshData(false);EditorUtility.SetDirty(old);if(mesh!=old)UnityEngine.Object.DestroyImmediate(mesh);return old;}
        private static bool Animated(Transform t)=>swings.Any(x=>t.IsChildOf(x))||seesaws.Any(x=>t.IsChildOf(x))||roundabouts.Any(x=>t.IsChildOf(x));
        private static void Combine(Transform root)
        {
            var filters=root.GetComponentsInChildren<MeshFilter>().Where(f=>f.GetComponent<TMP_Text>()==null&&!TownPopulationController.RoadName(f.name)&&f.name!="House doorway"&&!bulbs.Contains(f.GetComponent<Renderer>())).ToArray();
            foreach(var group in filters.GroupBy(f=>new{Material=f.GetComponent<MeshRenderer>().sharedMaterial,Parent=Animated(f.transform)?MovingRoot(f.transform):PlotRoot(f.transform,root)}))
            {
                var combined=new Mesh{name="Heritage "+group.Key.Material.name,indexFormat=IndexFormat.UInt32};var parent=group.Key.Parent;combined.CombineMeshes(group.Select(f=>new CombineInstance{mesh=f.sharedMesh,transform=parent.worldToLocalMatrix*f.transform.localToWorldMatrix}).ToArray(),true,true);combined.RecalculateBounds();var saved=Save(combined,Folder+"/Block"+(chunk++)+".asset");var go=new GameObject("Heritage batch "+group.Key.Material.name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(parent,false);go.GetComponent<MeshFilter>().sharedMesh=saved;go.GetComponent<MeshRenderer>().sharedMaterial=group.Key.Material;foreach(var f in group){var original=f.gameObject;UnityEngine.Object.DestroyImmediate(f.GetComponent<MeshRenderer>());UnityEngine.Object.DestroyImmediate(f);if(original.transform.childCount==0&&original.GetComponents<Component>().Length==1)UnityEngine.Object.DestroyImmediate(original);}
            }
            foreach(var f in root.GetComponentsInChildren<MeshFilter>())if(f.sharedMesh!=null&&!AssetDatabase.Contains(f.sharedMesh))f.sharedMesh=Save(UnityEngine.Object.Instantiate(f.sharedMesh),Folder+"/Marker"+(chunk++)+".asset");
        }
        private static Transform MovingRoot(Transform t){foreach(var x in swings.Concat(seesaws).Concat(roundabouts))if(t.IsChildOf(x))return x;return t;}
        private static Transform PlotRoot(Transform t,Transform root){for(var p=t;p!=root&&p!=null;p=p.parent)if(p.name.StartsWith("Heritage home")||p.name.Contains("family playground")||p.name.Contains("pocket playground")||HeritageNeighborhoodLayout.Names.Contains(p.name))return p;return root;}
        private static void RegisterLights(MotelGameRoot motel)
        {var lighting=motel.GetComponentInChildren<WorldNightLighting>();if(lighting==null)return;var so=new SerializedObject(lighting);var fixtures=so.FindProperty("fixtures");var surfaces=so.FindProperty("glowingSurfaces");int a=fixtures.arraySize,b=surfaces.arraySize;fixtures.arraySize+=bulbs.Count;surfaces.arraySize+=bulbs.Count;for(int i=0;i<bulbs.Count;i++){fixtures.GetArrayElementAtIndex(a+i).objectReferenceValue=bulbs[i].transform;surfaces.GetArrayElementAtIndex(b+i).objectReferenceValue=bulbs[i];}so.ApplyModifiedPropertiesWithoutUndo();}
    }
}

