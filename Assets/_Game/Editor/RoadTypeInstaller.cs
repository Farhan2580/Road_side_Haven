using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
namespace RoadsideHaven.Editor
{
    public static partial class VisualPolishInstaller
    {
        private static Mesh roadPaintMesh;
        private static void ConfigureRoadWidths(Transform motel)
        {
            if(motel.Find("Mountain dirt route v1")==null)
            {
                var trail=new GameObject("Mountain dirt route v1").transform;trail.SetParent(motel,false);
                Road(trail,new Vector3(300,0,712),new Vector3(5,.12f,124));
            }
            foreach(var old in motel.GetComponentsInChildren<MeshRenderer>(true))if(old.name=="Parking apron"&&old.bounds.center.z> -20)old.gameObject.SetActive(false);
            if(motel.Find("Motel parking courts v1")==null)
            {
                var parking=new GameObject("Motel parking courts v1").transform;parking.SetParent(motel,false);
                foreach(float z in new[]{-32f,-48f})Solid(parking,"Showroom apron",new Vector3(-17,0,z),new Vector3(18,.12f,12),asphalt);
            }
            var roads=new List<MeshRenderer>();
            foreach(var renderer in motel.GetComponentsInChildren<MeshRenderer>(true))
            {
                if(!renderer.gameObject.activeInHierarchy||GroundSurfaceStyle.SurfaceFor(renderer.name)!=1)continue;
                var profile=renderer.GetComponent<RoadSurfaceProfile>();if(profile==null)profile=renderer.gameObject.AddComponent<RoadSurfaceProfile>();
                profile.Kind=RoadSurfaceProfile.Detect(renderer);if(!TownPopulationController.RoadName(renderer.name))continue;
                if(!Application.isPlaying)Undo.RecordObject(renderer.transform,"Apply road widths");var scale=renderer.transform.localScale;
                if(renderer.bounds.size.x>renderer.bounds.size.z)scale.z=RoadSurfaceProfile.Width(profile.Kind);else scale.x=RoadSurfaceProfile.Width(profile.Kind);renderer.transform.localScale=scale;roads.Add(renderer);
                if(!Application.isPlaying)EditorUtility.SetDirty(profile);
            }
            // Hide old asphalt overlays fully contained by a wider road; they otherwise fight the new road texture.
            foreach(var narrow in roads)foreach(var wide in roads)
            {
                if(narrow==wide||!wide.gameObject.activeSelf||!narrow.gameObject.activeSelf)continue;var a=narrow.bounds;var b=wide.bounds;
                if((a.size.x>a.size.z)!=(b.size.x>b.size.z)||b.size.x*b.size.z<=a.size.x*a.size.z+.1f)continue;
                if(a.min.x>=b.min.x-.01f&&a.max.x<=b.max.x+.01f&&a.min.z>=b.min.z-.01f&&a.max.z<=b.max.z+.01f)narrow.gameObject.SetActive(false);
            }
        }
        private static void InstallRoadSurfaceTypes(Transform motel)
        {
            RoadTextureAuthoring.Generate(GroundTextureAuthoring.Folder);GroundTextureAuthoring.ImportMaps();var materials=new Material[5];
            for(int i=0;i<5;i++)
            {
                var m=Material(RoadTextureAuthoring.Names[i],Color.white);m.SetColor("_BaseColor",Color.white);m.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(GroundTextureAuthoring.Folder+"/"+RoadTextureAuthoring.Names[i]+"_Albedo.png"));m.SetTexture("_BumpMap",AssetDatabase.LoadAssetAtPath<Texture2D>(GroundTextureAuthoring.Folder+"/"+RoadTextureAuthoring.Names[i]+"_Normal.png"));m.EnableKeyword("_NORMALMAP");m.SetFloat("_BumpScale",i==2||i==4?.55f:.28f);m.SetFloat("_Smoothness",i==2||i==4?.05f:.17f);m.enableInstancing=true;materials[i]=m;EditorUtility.SetDirty(m);
            }
            var style=motel.GetComponent<RoadSurfaceVisuals>();if(style==null)style=motel.gameObject.AddComponent<RoadSurfaceVisuals>();style.Configure(materials);if(!Application.isPlaying)EditorUtility.SetDirty(style);
            if(motel.Find("Road surface type finish v1")!=null)return;
            foreach(var old in motel.GetComponentsInChildren<MeshRenderer>(true))
            {string n=old.name.ToLowerInvariant();if((n.Contains("lane")||n.Contains("road dash")||n.Contains("parking stripe")||n.Contains("parking marking"))&&(n.Contains("marking")||n.Contains("dash")||n.Contains("stripe")))old.gameObject.SetActive(false);}
            var root=new GameObject("Road surface type finish v1").transform;root.SetParent(motel,false);if(!Application.isPlaying)Undo.RegisterCreatedObjectUndo(root.gameObject,"Add road profiles and detail");
            var white=Material("Road paint white",new Color(.83f,.85f,.82f));var yellow=Material("City centre yellow",new Color(.90f,.68f,.13f));var wear=Material("Road wear charcoal",new Color(.07f,.075f,.07f));var rail=Material("Highway galvanized rail",new Color(.48f,.53f,.55f));white.SetColor("_BaseColor",new Color(.83f,.85f,.82f));yellow.SetColor("_BaseColor",new Color(.90f,.68f,.13f));wear.SetColor("_BaseColor",new Color(.07f,.075f,.07f));rail.SetColor("_BaseColor",new Color(.48f,.53f,.55f));rail.SetFloat("_Metallic",.6f);rail.SetFloat("_Smoothness",.5f);
            var faded=Material("Worn parking paint",Color.white);faded.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(GroundTextureAuthoring.Folder+"/ParkingPaint.png"));TransparentRoadMark(faded);
            var oil=Material("Soft oil stain",Color.white);oil.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(GroundTextureAuthoring.Folder+"/RoadOil.png"));oil.SetFloat("_Smoothness",.45f);TransparentRoadMark(oil);
            var verge=Material("Highway grass strip",Color.white);verge.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(GroundTextureAuthoring.Folder+"/BaseGrass_Albedo.png"));
            var graph=new TownRoadNetwork();foreach(var renderer in motel.GetComponentsInChildren<MeshRenderer>(true))if(renderer.gameObject.activeInHierarchy&&TownPopulationController.RoadName(renderer.name))graph.Add(renderer.bounds.center,renderer.bounds.size);graph.Build(null);var junctions=TownJunctionLayout.Find(graph.Roads);
            foreach(var profile in motel.GetComponentsInChildren<RoadSurfaceProfile>(true))
            {
                if(!profile.gameObject.activeInHierarchy)continue;var bounds=profile.GetComponent<Renderer>().bounds;bool horizontal=bounds.size.x>bounds.size.z;float length=horizontal?bounds.size.x:bounds.size.z,width=horizontal?bounds.size.z:bounds.size.x;var axis=horizontal?Vector3.right:Vector3.forward;var side=Vector3.Cross(Vector3.up,axis);var center=bounds.center;center.y=.063f;
                if(profile.Kind==RoadSurfaceKind.Parking)
                {
                    for(float t=-length*.5f+2;t<length*.5f-2;t+=3.2f)for(int segment=0;segment<3;segment++)
                    {var p=center+axis*t+side*(segment*.8f-1);Paint(root,"Faded parking marking",p,horizontal?new Vector2(.09f,.55f):new Vector2(.55f,.09f),faded);}
                    continue;
                }
                bool highway=profile.Kind==RoadSurfaceKind.Highway,city=profile.Kind==RoadSurfaceKind.City;if(!highway&&!city)continue;
                for(float t=-length*.5f+12;t<length*.5f-8;t+=highway?18:14)
                {
                    var p=center+axis*t;if(NearJunction(p,junctions,14))continue;
                    if(city)Paint(root,"City yellow centre marking",p,horizontal?new Vector2(4,.12f):new Vector2(.12f,4),yellow);
                    else foreach(float lane in new[]{-3f,0,3f})Paint(root,"Four lane highway marking",p+side*lane,horizontal?new Vector2(6,.12f):new Vector2(.12f,6),white);
                }
                for(float t=-length*.5f+26;t<length*.5f-20;t+=76)
                {
                    var p=center+axis*t;if(NearJunction(p,junctions,20))continue;
                    if(city)Manhole(root,p+side*1.4f,rail,wear);
                    else
                    {
                        Paint(root,"Oil stain marking",p+side*4.4f,new Vector2(.65f,1.8f),oil);
                        foreach(int tire in new[]{-1,1})Paint(root,"Skid marking",p+side*(1.5f+tire*.55f),horizontal?new Vector2(7,.07f):new Vector2(.07f,7),wear);
                        for(int crack=0;crack<3;crack++)Paint(root,"Asphalt crack marking",p+axis*(crack*.6f)+side*(crack*.18f-3),new Vector2(.8f,.025f),wear,crack*23);
                    }
                }
                if(highway)for(float t=-length*.5f+14;t<length*.5f-12;t+=22)
                {
                    var p=center+axis*t;if(NearJunction(p,junctions,30)||Mathf.Repeat(t+length*.5f,110)>82)continue;
                    foreach(int sign in new[]{-1,1})
                    {
                        var position=p+side*sign*8.5f;position.y=.70f;Solid(root,"Road guardrail",position,horizontal?new Vector3(19,.32f,.14f):new Vector3(.14f,.32f,19),rail);
                        Paint(root,"Highway grass verge marking",p+side*sign*6.85f,horizontal?new Vector2(20,1.5f):new Vector2(1.5f,20),verge);
                        for(int post=-1;post<=1;post++)VisualPost(root,position+axis*(post*7)-Vector3.up*.32f,rail);
                    }
                }
            }
        }
        private static void TransparentRoadMark(Material material)
        {material.SetFloat("_Surface",1);material.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);material.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);material.SetFloat("_ZWrite",0);material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");material.SetOverrideTag("RenderType","Transparent");material.renderQueue=3000;EditorUtility.SetDirty(material);}
        private static bool NearJunction(Vector3 point,List<TownJunctionLayout.Junction> junctions,float margin)
        {foreach(var j in junctions)if(Mathf.Abs(point.x-j.Center.x)<j.X+margin&&Mathf.Abs(point.z-j.Center.z)<j.Z+margin)return true;return false;}
        private static void Paint(Transform parent,string name,Vector3 p,Vector2 size,Material material,float yaw=0,float height=.074f)
        {
            var obj=new GameObject(name);obj.transform.SetParent(parent,false);p.y=height;obj.transform.position=p;obj.transform.rotation=Quaternion.Euler(90,yaw,0);obj.transform.localScale=new Vector3(size.x,size.y,1);obj.layer=2;
            var filter=obj.AddComponent<MeshFilter>();if(roadPaintMesh==null){var temp=GameObject.CreatePrimitive(PrimitiveType.Quad);roadPaintMesh=temp.GetComponent<MeshFilter>().sharedMesh;temp.SetActive(false);if(Application.isPlaying)Object.Destroy(temp);else Object.DestroyImmediate(temp);}filter.sharedMesh=roadPaintMesh;
            var r=obj.AddComponent<MeshRenderer>();r.sharedMaterial=material;r.shadowCastingMode=ShadowCastingMode.Off;
        }
        private static void Manhole(Transform parent,Vector3 p,Material metal,Material dark)
        {
            var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(MaterialFolder+"/ManholeCover.asset");bool fresh=mesh==null;if(fresh)mesh=new Mesh{name="Flat manhole cover"};var vertices=new Vector3[33];var triangles=new int[32*3];
            for(int i=0;i<32;i++){float angle=i*Mathf.PI*2/32;vertices[i+1]=new Vector3(Mathf.Cos(angle)*.38f,0,Mathf.Sin(angle)*.38f);triangles[i*3]=0;triangles[i*3+1]=(i+1)%32+1;triangles[i*3+2]=i+1;}
            mesh.vertices=vertices;mesh.triangles=triangles;mesh.RecalculateNormals();if(fresh)AssetDatabase.CreateAsset(mesh,MaterialFolder+"/ManholeCover.asset");var obj=new GameObject("Manhole marking");obj.transform.SetParent(parent,false);p.y=.074f;obj.transform.position=p;obj.layer=2;obj.AddComponent<MeshFilter>().sharedMesh=mesh;obj.AddComponent<MeshRenderer>().sharedMaterial=metal;
            for(int i=-2;i<=2;i++)Paint(parent,"Manhole groove marking",p+Vector3.forward*i*.10f,new Vector2(.52f,.025f),dark,0,.076f);
        }
        private static void VisualPost(Transform root,Vector3 p,Material material)
        {var obj=Solid(root,"Guardrail post marking",p,new Vector3(.10f,.65f,.10f),material);var c=obj.GetComponent<Collider>();c.enabled=false;if(Application.isPlaying)Object.Destroy(c);else Object.DestroyImmediate(c);}
    }
}
