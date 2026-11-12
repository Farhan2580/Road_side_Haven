using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;
namespace RoadsideHaven.Editor
{
    public static partial class VisualPolishInstaller
    {
        private static void InstallBiomeVegetation(Transform motel)
        {
            if(motel.Find("Regional vegetation v2")!=null)return;
            var carried=new List<Vector3>();var previous=motel.Find("Regional vegetation v1");if(previous!=null){foreach(var plant in previous.GetComponentsInChildren<BiomePlant>())if(plant.Kind==VegetationKind.StreetTree||plant.Kind==VegetationKind.Oak||plant.Kind==VegetationKind.Pine||plant.Kind==VegetationKind.Palm)carried.Add(plant.transform.position);previous.gameObject.SetActive(false);if(Application.isPlaying)Object.Destroy(previous.gameObject);else Undo.DestroyObjectImmediate(previous.gameObject);}
            if(!AssetDatabase.IsValidFolder(BiomeMeshAuthoring.Folder))AssetDatabase.CreateFolder("Assets/_Game/Generated","Vegetation");
            var geometry=motel.GetComponentsInChildren<MeshRenderer>(true).Where(r=>r.gameObject.activeInHierarchy&&r.GetComponentInParent<Rigidbody>()==null&&r.GetComponentInParent<CharacterController>()==null).ToArray();
            var roads=geometry.Where(r=>GroundSurfaceStyle.SurfaceFor(r.name)==1).Select(r=>XZ(r.bounds)).ToArray();
            var blockers=geometry.Where(r=>r.bounds.size.y>1&&r.bounds.size.x>.02f&&r.bounds.size.z>.02f&&!VegetationName(r.name)&&r.GetComponentInParent<BiomePlant>()==null).Select(r=>XZ(r.bounds)).ToArray();
            var doors=geometry.Where(r=>r.name.ToLowerInvariant().Contains("door")||r.name.ToLowerInvariant().Contains("entrance")).Select(r=>new Vector2(r.bounds.center.x,r.bounds.center.z)).ToArray();
            var grounds=geometry.Where(r=>GroundSurfaceStyle.SurfaceFor(r.name)==0||GroundSurfaceStyle.SurfaceFor(r.name)==3||GroundSurfaceStyle.SurfaceFor(r.name)==5).Select(r=>r.bounds).ToArray();
            var sources=new List<Vector3>(carried);var grouped=new HashSet<Transform>();
            foreach(var transform in motel.GetComponentsInChildren<Transform>(true))
                if(transform.gameObject.activeInHierarchy&&(transform.name=="Avenue oak"||transform.name=="Avenue pine"||transform.name=="Avenue palm"))
                {sources.Add(transform.position);grouped.Add(transform);transform.gameObject.SetActive(false);}
            foreach(var trunk in geometry)
            {
                if(trunk.name!="Tree trunk"||grouped.Any(g=>trunk.transform.IsChildOf(g)))continue;
                var p=trunk.bounds.center;p.y=trunk.bounds.min.y;sources.Add(p);trunk.gameObject.SetActive(false);
                foreach(var crown in geometry)if(crown.name.StartsWith("Tree crown",StringComparison.Ordinal)&&crown.transform.parent==trunk.transform.parent&&Vector2.Distance(new Vector2(crown.bounds.center.x,crown.bounds.center.z),new Vector2(p.x,p.z))<4.8f)crown.gameObject.SetActive(false);
            }
            var root=new GameObject("Regional vegetation v2").transform;root.SetParent(motel,false);if(!Application.isPlaying)Undo.RegisterCreatedObjectUndo(root.gameObject,"Landscape regional vegetation");
            var plants=new List<BiomePlant>();var occupied=new List<Vector2>();var kinds=new List<VegetationKind>();
            Action<Vector3,VegetationKind> place=(point,kind)=>
            {
                if(plants.Count>=380)return;var p=new Vector2(point.x,point.z);float radius=kind==VegetationKind.FlowerBed?1.8f:kind==VegetationKind.TrimmedBush?1.3f:.65f;
                if(!ClearPlanting(p,radius,roads,blockers,doors,occupied,kinds,kind))return;
                point.y=GroundHeight(point,grounds);var plant=CreateBiomePlant(root,point,kind,plants.Count%3);plants.Add(plant);occupied.Add(p);kinds.Add(kind);
            };
            // Intentional accents ensure each region has a distinct silhouette, independent of old tree placement.
            for(int i=0;i<16;i++)place(new Vector3(-850+i*82,0,-800+(i%3)*16),VegetationKind.Palm);
            for(int i=0;i<28;i++)place(new Vector3(-910+i*65,0,690+(i%3)*16),VegetationKind.Pine);
            for(int i=0;i<14;i++)place(new Vector3(-105-(i%4)*19,0,95+(i/4)*23),VegetationKind.Oak);
            var lake=motel.Find("Willow Lake 80x60 v2");if(lake!=null)
            {var outline=OrganicLakeShape.Outline();for(int i=4;i<outline.Length;i+=12){var p=outline[i]*1.38f;place(lake.position+new Vector3(p.x,0,p.y),VegetationKind.Bamboo);}}
            var fronts=geometry.Where(r=>(r.bounds.min.y<1&&r.bounds.size.y>2.5f&&r.bounds.size.x>5&&r.bounds.size.z>3&&GroundSurfaceStyle.SurfaceFor(r.name)<0&&!VegetationName(r.name))||r.name=="Office lobby floor").Take(48).ToArray();
            foreach(var front in fronts)
            {
                var b=front.bounds;
                foreach(int side in new[]{-1,1})place(new Vector3(b.center.x+side*Mathf.Max(3.8f,b.extents.x*.65f),0,b.min.z-2.6f),VegetationKind.TrimmedBush);
                if(plants.Count%3==0)place(new Vector3(b.max.x+2.6f,0,b.min.z-3.4f),VegetationKind.FlowerBed);
            }
            foreach(var original in sources)
            {
                var kind=BiomePlant.ForLocation(original);int before=plants.Count;place(original,kind);
                if(plants.Count==before)
                {
                    foreach(var road in roads.OrderBy(r=>DistanceToRect(new Vector2(original.x,original.z),r)).Take(1))
                    {
                        bool horizontal=road.width>road.height;var p=original;
                        if(horizontal)p.z=original.z>=road.center.y?road.yMax+4.8f:road.yMin-4.8f;else p.x=original.x>=road.center.x?road.xMax+4.8f:road.xMin-4.8f;place(p,BiomePlant.ForLocation(p));
                    }
                }
            }
            root.gameObject.AddComponent<VegetationWind>().Configure(plants.ToArray());if(!Application.isPlaying)EditorUtility.SetDirty(root.gameObject);
        }
        private static bool VegetationName(string value)
        {var name=value.ToLowerInvariant();return name.Contains("tree")||name.Contains("crown")||name.Contains("plant")||name.Contains("crop")||name.Contains("willow")||name.Contains("vegetation")||name.Contains("cattail")||name.Contains("flower");}
        private static Rect XZ(Bounds b)=>Rect.MinMaxRect(b.min.x,b.min.z,b.max.x,b.max.z);
        private static float DistanceToRect(Vector2 p,Rect r)
        {float x=Mathf.Max(r.xMin-p.x,0,p.x-r.xMax),z=Mathf.Max(r.yMin-p.y,0,p.y-r.yMax);return Mathf.Sqrt(x*x+z*z);}
        private static bool ClearPlanting(Vector2 p,float radius,Rect[] roads,Rect[] blockers,Vector2[] doors,List<Vector2> occupied,List<VegetationKind> kinds,VegetationKind kind)
        {
            if(VillageRiverShape.Contains(p,6f+radius))return false;
            foreach(var road in roads)if(DistanceToRect(p,road)<3.3f+radius)return false;
            foreach(var blocker in blockers)if(DistanceToRect(p,blocker)<radius+.25f)return false;
            foreach(var door in doors)if(Vector2.Distance(door,p)<3.2f+radius)return false;
            for(int i=0;i<occupied.Count;i++){float spacing=kind==VegetationKind.Oak||kinds[i]==VegetationKind.Oak?7:kind==VegetationKind.TrimmedBush&&kinds[i]==kind?2.8f:kind==VegetationKind.FlowerBed||kinds[i]==VegetationKind.FlowerBed?3.8f:4.6f;if(Vector2.Distance(p,occupied[i])<spacing)return false;}
            var lakePoint=new Vector2(p.x+200,p.y+180);if(OrganicLakeShape.Contains(lakePoint/1.16f,OrganicLakeShape.Outline()))return false;return true;
        }
        private static float GroundHeight(Vector3 p,Bounds[] grounds)
        {float height=-.1f;foreach(var b in grounds)if(p.x>=b.min.x&&p.x<=b.max.x&&p.z>=b.min.z&&p.z<=b.max.z)height=Mathf.Max(height,b.max.y);return height;}
        private static BiomePlant CreateBiomePlant(Transform root,Vector3 p,VegetationKind kind,int variant)
        {
            var obj=new GameObject("Biome "+kind);obj.transform.SetParent(root,false);obj.transform.position=p;
            float yaw=Mathf.Repeat(p.x*13.7f+p.z*7.3f,360);obj.transform.rotation=Quaternion.Euler(0,yaw,0);
            var plant=obj.AddComponent<BiomePlant>();plant.Kind=kind;var canopy=new GameObject("Wind canopy").transform;canopy.SetParent(obj.transform,false);canopy.localPosition=Vector3.up*(kind==VegetationKind.TrimmedBush||kind==VegetationKind.FlowerBed?0:3);plant.Canopy=canopy;
            var bark=VegetationMaterial(kind==VegetationKind.Bamboo?"Bamboo stems":"Bark",kind==VegetationKind.Bamboo?new Color(.34f,.49f,.18f):new Color(.58f,.43f,.28f));
            Color[] greens={new Color(.24f,.46f,.14f),new Color(.25f,.45f,.15f),new Color(.13f,.32f,.10f),new Color(.19f,.37f,.105f),new Color(.085f,.235f,.14f),new Color(.29f,.47f,.17f),new Color(.9f,.42f,.22f)};
            Color color=kind==VegetationKind.FlowerBed?(variant==0?new Color(.88f,.35f,.18f):variant==1?new Color(.65f,.36f,.77f):new Color(.93f,.75f,.26f)):greens[(int)kind]*(.94f+variant*.06f);
            var foliage=VegetationMaterial(kind+" leaves "+variant,color);foliage.SetFloat("_Cull",0);
            var woodMesh=BiomeMeshAuthoring.MeshFor(kind,variant,false);var leafMesh=BiomeMeshAuthoring.MeshFor(kind,variant,true);
            var wood=VegetationRenderer(obj.transform,"Tree bark marking "+kind,woodMesh,bark,Vector3.zero);var full=VegetationRenderer(canopy,"Tree crown "+kind,leafMesh,foliage,-canopy.localPosition);
            var lowMesh=LowCanopyMesh(kind);var low=VegetationRenderer(canopy,"Tree crown distant "+kind,lowMesh,foliage,-canopy.localPosition);
            if(kind==VegetationKind.Palm||kind==VegetationKind.Pine)low.gameObject.SetActive(false);
            var lod=obj.AddComponent<LODGroup>();var high=new List<Renderer>();if(woodMesh.vertexCount>0)high.Add(wood);high.Add(full);lod.SetLODs(new[]{new LOD(.12f,high.ToArray()),new LOD(.028f,kind==VegetationKind.Palm||kind==VegetationKind.Pine?high.ToArray():new Renderer[]{wood,low}),new LOD(.008f,new Renderer[]{kind==VegetationKind.Palm||kind==VegetationKind.Pine?full:low})});lod.fadeMode=LODFadeMode.CrossFade;lod.animateCrossFading=true;lod.RecalculateBounds();
            if(kind==VegetationKind.TrimmedBush||kind==VegetationKind.FlowerBed){var box=obj.AddComponent<BoxCollider>();box.center=Vector3.up*(kind==VegetationKind.TrimmedBush?.55f:.08f);box.size=kind==VegetationKind.TrimmedBush?new Vector3(2.4f,1.1f,.95f):new Vector3(3.4f,.16f,1.2f);}
            else {var collider=obj.AddComponent<CapsuleCollider>();collider.height=kind==VegetationKind.Pine?8:kind==VegetationKind.Bamboo?4.8f:kind==VegetationKind.Palm?7.5f:4;collider.center=Vector3.up*collider.height*.5f;collider.radius=kind==VegetationKind.Oak?.42f:kind==VegetationKind.Bamboo?.55f:.23f;}
            if(kind==VegetationKind.StreetTree)TreePit(obj.transform);
            return plant;
        }
        private static MeshRenderer VegetationRenderer(Transform root,string name,Mesh mesh,Material material,Vector3 local)
        {var obj=new GameObject(name);obj.transform.SetParent(root,false);obj.transform.localPosition=local;obj.AddComponent<MeshFilter>().sharedMesh=mesh;var renderer=obj.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;return renderer;}
        private static Material VegetationMaterial(string name,Color color)
        {string path=BiomeMeshAuthoring.Folder+"/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);if(m==null){m=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(m,path);}m.SetColor("_BaseColor",color);m.SetFloat("_Smoothness",.24f);m.enableInstancing=true;EditorUtility.SetDirty(m);return m;}
        private static Mesh LowCanopyMesh(VegetationKind kind)
        {
            string path=BiomeMeshAuthoring.Folder+"/"+kind+"_Distant.asset";var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(mesh!=null)return mesh;
            float h=kind==VegetationKind.Oak?6:kind==VegetationKind.Bamboo?4.5f:kind==VegetationKind.TrimmedBush?.6f:kind==VegetationKind.FlowerBed?.42f:5;
            float width=kind==VegetationKind.Oak?10:kind==VegetationKind.TrimmedBush?2.4f:kind==VegetationKind.FlowerBed?3.1f:kind==VegetationKind.Bamboo?3:4.8f;
            var vertices=new List<Vector3>();var triangles=new List<int>();const int rings=5,sides=10;
            for(int j=0;j<=rings;j++)for(int i=0;i<sides;i++){float a=j*Mathf.PI/rings,b=i*Mathf.PI*2/sides;vertices.Add(new Vector3(Mathf.Sin(a)*Mathf.Cos(b)*width*.5f,h+Mathf.Cos(a)*(kind==VegetationKind.TrimmedBush?.5f:kind==VegetationKind.FlowerBed?.15f:1.7f),Mathf.Sin(a)*Mathf.Sin(b)*width*.44f));}
            for(int j=0;j<rings;j++)for(int i=0;i<sides;i++){int a=j*sides+i,b=j*sides+(i+1)%sides,c=a+sides,d=b+sides;triangles.AddRange(new[]{a,b,c,b,d,c});}
            mesh=new Mesh{name=kind+" distant canopy"};mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();AssetDatabase.CreateAsset(mesh,path);return mesh;
        }
        private static void TreePit(Transform root)
        {
            var obj=GameObject.CreatePrimitive(PrimitiveType.Cylinder);obj.name="Street tree soil marking";obj.transform.SetParent(root,false);obj.transform.localPosition=Vector3.up*.02f;obj.transform.localScale=new Vector3(1.6f,.015f,1.6f);obj.GetComponent<Renderer>().sharedMaterial=VegetationMaterial("Tree pit earth",new Color(.28f,.20f,.12f));var collider=obj.GetComponent<Collider>();collider.enabled=false;if(Application.isPlaying)Object.Destroy(collider);else Object.DestroyImmediate(collider);
        }
    }
}
