using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
namespace RoadsideHaven.Editor
{
    public static partial class VisualPolishInstaller
    {
        private const string RiverFolder="Assets/_Game/Generated/River";
        private static void InstallVillageRiver(Transform motel)
        {
            LakeTextureAuthoring.Generate();var shader=AssetDatabase.LoadAssetAtPath<Shader>(LakeTextureAuthoring.Folder+"/VillageCityRiver.shadergraph");if(shader==null||ShaderUtil.ShaderHasError(shader)){Debug.LogError("River water Shader Graph has not finished importing.");return;}
            var legacy=motel.Find("Village City River v1");if(legacy!=null){legacy.gameObject.SetActive(false);if(Application.isPlaying)Object.Destroy(legacy.gameObject);else Undo.DestroyObjectImmediate(legacy.gameObject);}var previous=motel.Find("Village City River v2");if(previous!=null){previous.gameObject.SetActive(false);if(Application.isPlaying)Object.Destroy(previous.gameObject);else Undo.DestroyObjectImmediate(previous.gameObject);}var current=motel.Find("Village City River v3");if(current!=null){CutRiverTerrain(motel);RiverGroundPhysics.Apply(motel);ClearRiverPlants(motel);return;}
            if(!AssetDatabase.IsValidFolder(RiverFolder))AssetDatabase.CreateFolder("Assets/_Game/Generated","River");if(!AssetDatabase.IsValidFolder(BiomeMeshAuthoring.Folder))AssetDatabase.CreateFolder("Assets/_Game/Generated","Vegetation");
            MoveRiverPark(motel);MoveRiverParking(motel);var root=new GameObject("Village City River v3").transform;root.SetParent(motel,false);if(!Application.isPlaying)Undo.RegisterCreatedObjectUndo(root.gameObject,"Create village-city river and culverts");
            var water=RiverMaterial("Flowing river",shader,Color.white);water.SetColor("_ShallowColor",new Color(79/255f,195/255f,212/255f));water.SetColor("_DeepColor",new Color(26/255f,82/255f,118/255f));water.SetTexture("_WaveNormal",AssetDatabase.LoadAssetAtPath<Texture2D>(LakeTextureAuthoring.Folder+"/LakeWaveNormal.png"));
            var earth=RiverMaterial("River stone and soil",Shader.Find("Universal Render Pipeline/Lit"),new Color(.82f,.77f,.64f));earth.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(GroundTextureAuthoring.Folder+"/DirtPebbles_Albedo.png"));earth.SetFloat("_Smoothness",.22f);earth.SetTexture("_BumpMap",AssetDatabase.LoadAssetAtPath<Texture2D>(GroundTextureAuthoring.Folder+"/DirtPebbles_Normal.png"));earth.EnableKeyword("_NORMALMAP");earth.SetFloat("_BumpScale",.4f);
            for(float north=VillageRiverShape.North;north>VillageRiverShape.South;north-=80)
            {float south=Mathf.Max(VillageRiverShape.South,north-80);RiverMesh(root,"River flowing water",VillageRiverShape.Ribbon(north,south),water,4);RiverMesh(root,"River channel bed water",VillageRiverShape.Ribbon(north,south,1),earth,0);RiverMesh(root,"River bank marking",VillageRiverShape.Ribbon(north,south,2),earth,0);}
            var concrete=RiverMaterial("Culvert concrete",Shader.Find("Universal Render Pipeline/Lit"),new Color(.54f,.56f,.51f));concrete.SetFloat("_Cull",0);var metal=RiverMaterial("Bridge galvanized railing",concrete.shader,new Color(.38f,.43f,.44f));metal.SetFloat("_Metallic",.6f);metal.SetFloat("_Smoothness",.45f);
            var roadBounds=motel.GetComponentsInChildren<MeshRenderer>(true).Where(r=>r.gameObject.activeInHierarchy&&TownPopulationController.RoadName(r.name)&&r.name!="Showroom apron"&&r.bounds.size.x>r.bounds.size.z).Select(r=>r.bounds).ToArray();var crossings=new List<Bounds>();
            foreach(var b in roadBounds.OrderByDescending(b=>b.size.x))
            {float z=b.center.z;if(z<=VillageRiverShape.South||z>=VillageRiverShape.North)continue;var center=VillageRiverShape.Center(z);if(center.x<b.min.x||center.x>b.max.x)continue;if(crossings.Any(c=>Mathf.Abs(c.center.z-z)<Mathf.Max(c.size.z,b.size.z)*.5f+2))continue;crossings.Add(b);CreateCulvert(root,b,concrete,metal);}
            var plants=new List<BiomePlant>();
            for(float z=VillageRiverShape.South+25;z<VillageRiverShape.North-20;z+=55)
            {
                if(z> -50&&z<135)continue;var center=VillageRiverShape.Center(z);
                foreach(int side in new[]{-1,1})
                {var p=center+Vector3.right*side*(VillageRiverShape.HalfWidth(z)+6);p.y=-.08f;if(roadBounds.Any(b=>Mathf.Abs(p.z-b.center.z)<b.extents.z+8))continue;if(NearAnyRiverRoad(motel,p,4))continue;plants.Add(CreateBiomePlant(root,p,VegetationKind.Bamboo,plants.Count%3));}
            }
            InstallRiverShore(motel,root,roadBounds);root.gameObject.AddComponent<VegetationWind>().Configure(plants.ToArray());
            var probe=new GameObject("River local reflection").AddComponent<ReflectionProbe>();probe.transform.SetParent(root,false);probe.mode=ReflectionProbeMode.Realtime;probe.refreshMode=ReflectionProbeRefreshMode.ViaScripting;probe.timeSlicingMode=ReflectionProbeTimeSlicingMode.IndividualFaces;probe.resolution=128;probe.size=new Vector3(45,16,120);probe.farClipPlane=130;probe.boxProjection=true;probe.blendDistance=10;probe.cullingMask=~(1<<4);root.gameObject.AddComponent<RiverReflectionBudget>().Configure(probe);
            for(int i=0;i<3;i++){float z=35+i*8;var p=VillageRiverShape.Center(z);p.x-=VillageRiverShape.HalfWidth(z)+2.5f;p.y=.04f;RiverBox(root,"Riverwalk footpath",p,new Vector3(1.6f,.16f,8),concrete,true);}
            CutRiverTerrain(motel);RiverGroundPhysics.Apply(motel);ClearRiverPlants(motel);
        }
        private static void InstallRiverShore(Transform motel,Transform root,Bounds[] roads)
        {
            var shader=Shader.Find("Universal Render Pipeline/Lit");
            var stone=RiverMaterial("Wet river stones",shader,new Color(.39f,.43f,.36f));stone.SetFloat("_Smoothness",.4f);
            var leaves=RiverMaterial("River cattail leaves",shader,new Color(.28f,.40f,.13f));
            var seed=RiverMaterial("River cattail seeds",shader,new Color(.24f,.13f,.06f));
            int index=0;
            for(float z=VillageRiverShape.South+15;z<VillageRiverShape.North-15;z+=18)
            {
                if(roads.Any(b=>Mathf.Abs(z-b.center.z)<b.extents.z+8))continue;
                foreach(int side in new[]{-1,1})
                {
                    var p=VillageRiverShape.Center(z);p.x+=side*(VillageRiverShape.HalfWidth(z)+.35f);p.y=-.48f;
                    if(NearAnyRiverRoad(motel,p,2))continue;
                    float size=.48f+.16f*(index%4);
                    LakePart(root,"River shore stone marking",PrimitiveType.Sphere,p,new Vector3(size,.38f,size*.75f),stone,false);
                    if(index++%3!=0)continue;
                    for(int stem=0;stem<2;stem++)
                    {
                        var q=p+new Vector3(stem*.25f,-.05f,stem*.32f);float h=1.0f+(index%4)*.1f;
                        LakePart(root,"River cattail marking",PrimitiveType.Cylinder,q+Vector3.up*h*.5f,new Vector3(.035f,h*.5f,.035f),leaves,false);
                        LakePart(root,"River cattail seed marking",PrimitiveType.Capsule,q+Vector3.up*h,new Vector3(.10f,.19f,.10f),seed,false);
                        var leaf=LakePart(root,"River cattail leaf marking",PrimitiveType.Capsule,q+new Vector3(.12f,h*.45f,0),new Vector3(.06f,h*.4f,.09f),leaves,false);leaf.rotation=Quaternion.Euler(0,index*17,20);
                    }
                }
            }
            // Combine only shore decorations, in spatial chunks so distant banks can still be culled.
            var groups=root.GetComponentsInChildren<MeshRenderer>().Where(r=>r.name.StartsWith("River cattail")||r.name=="River shore stone marking").GroupBy(r=>new {r.sharedMaterial,Chunk=Mathf.FloorToInt(r.bounds.center.z/80)});
            foreach(var group in groups)
            {
                var parts=group.ToArray();var mesh=new Mesh{name="River shore "+group.Key.sharedMaterial.name+" "+group.Key.Chunk};
                mesh.CombineMeshes(parts.Select(r=>new CombineInstance{mesh=r.GetComponent<MeshFilter>().sharedMesh,transform=root.worldToLocalMatrix*r.localToWorldMatrix}).ToArray());
                RiverMesh(root,"River shore plants marking",mesh,group.Key.sharedMaterial,0);foreach(var part in parts)part.enabled=false;
            }
        }
        private static Material RiverMaterial(string name,Shader shader,Color color)
        {string path=RiverFolder+"/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);if(m==null){m=new Material(shader);AssetDatabase.CreateAsset(m,path);}m.SetColor(m.HasProperty("_BaseColor")?"_BaseColor":"_ShallowColor",color);m.enableInstancing=true;EditorUtility.SetDirty(m);return m;}
        private static void RiverMesh(Transform root,string name,Mesh mesh,Material material,int layer)
        {
            string path=RiverFolder+"/"+mesh.name+".asset";var saved=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(saved==null){saved=mesh;AssetDatabase.CreateAsset(saved,path);}else{EditorUtility.CopySerialized(mesh,saved);Object.DestroyImmediate(mesh);EditorUtility.SetDirty(saved);}
            var obj=new GameObject(name);obj.transform.SetParent(root,false);obj.layer=layer;obj.AddComponent<MeshFilter>().sharedMesh=saved;var r=obj.AddComponent<MeshRenderer>();r.sharedMaterial=material;r.shadowCastingMode=ShadowCastingMode.Off;if(name=="River flowing water")obj.AddComponent<RiverSurfaceMotion>();if(name=="River channel bed water"||name=="River bank marking")obj.AddComponent<MeshCollider>().sharedMesh=saved;
        }
        private static GameObject RiverBox(Transform root,string name,Vector3 p,Vector3 size,Material material,bool solid)
        {var obj=GameObject.CreatePrimitive(PrimitiveType.Cube);obj.name=name;obj.transform.SetParent(root,false);obj.transform.position=p;obj.transform.localScale=size;obj.GetComponent<Renderer>().sharedMaterial=material;if(!solid){var collider=obj.GetComponent<Collider>();collider.enabled=false;if(Application.isPlaying)Object.Destroy(collider);else Object.DestroyImmediate(collider);}return obj;}
        private static void CreateCulvert(Transform root,Bounds road,Material concrete,Material metal)
        {
            var center=VillageRiverShape.Center(road.center.z);var bridge=new GameObject("Twin culvert road crossing").transform;bridge.SetParent(root,false);float half=VillageRiverShape.HalfWidth(center.z),span=half*2+7,deck=road.size.z+6;
            RiverBox(bridge,"River bridge deck marking",new Vector3(center.x,-.10f,center.z),new Vector3(span,.28f,deck),concrete,false);
            foreach(int side in new[]{-1,1})
            {
                RiverBox(bridge,"River bridge footway",new Vector3(center.x,.04f,center.z+side*(road.extents.z+1.5f)),new Vector3(span,.16f,2.8f),concrete,true);
                RiverBox(bridge,"River culvert abutment marking",new Vector3(center.x+side*(half+.4f),-1.1f,center.z),new Vector3(.65f,1.8f,deck),concrete,false);
                RiverBox(bridge,"River bridge safety railing",new Vector3(center.x,.85f,center.z+side*(road.extents.z+3.05f)),new Vector3(span,.13f,.10f),metal,true);
                for(float x=-span*.5f;x<=span*.5f;x+=2.2f)RiverBox(bridge,"River bridge railing post",new Vector3(center.x+x,.50f,center.z+side*(road.extents.z+3.05f)),new Vector3(.11f,1,.11f),metal,true);
                foreach(int opening in new[]{-1,1})
                {var arch=VillageRiverShape.CulvertArch(half*.45f,.74f);arch.name="Culvert arch "+Mathf.RoundToInt(center.z)+" "+side+" "+opening;RiverMesh(bridge,"River culvert arch marking",arch,concrete,0);var obj=bridge.GetChild(bridge.childCount-1);obj.position=new Vector3(center.x+opening*half*.5f,-1.03f,center.z+side*(deck*.5f+.02f));}
            }
            RiverBox(bridge,"River centre pier marking",new Vector3(center.x,-1.12f,center.z),new Vector3(.26f,1.8f,deck),concrete,false);RiverFoam(bridge,center+Vector3.back*(deck*.5f+1.5f));
        }
        private static void RiverFoam(Transform root,Vector3 p)
        {
            var obj=new GameObject("Culvert flow foam particles");obj.transform.SetParent(root,false);obj.transform.position=p+Vector3.up*.025f;obj.transform.rotation=Quaternion.Euler(90,0,0);obj.layer=4;var ps=obj.AddComponent<ParticleSystem>();ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);var main=ps.main;main.maxParticles=12;main.startLifetime=2.2f;main.startSpeed=.35f;main.startSize=.5f;main.startColor=new Color(.9f,.98f,1,.22f);var fade=ps.colorOverLifetime;fade.enabled=true;var gradient=new Gradient();gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},new[]{new GradientAlphaKey(0,0),new GradientAlphaKey(1,.2f),new GradientAlphaKey(0,1)});fade.color=gradient;var emission=ps.emission;emission.rateOverTime=2.5f;var shape=ps.shape;shape.shapeType=ParticleSystemShapeType.Box;shape.scale=new Vector3(5,1,0);var material=RiverMaterial("River foam",Shader.Find("Universal Render Pipeline/Particles/Unlit"),Color.white);material.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(LakeTextureAuthoring.Folder+"/LakeRipple.png"));material.SetFloat("_Surface",1);material.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);material.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);material.SetFloat("_ZWrite",0);material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");material.renderQueue=3000;var renderer=ps.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=material;renderer.renderMode=ParticleSystemRenderMode.HorizontalBillboard;renderer.shadowCastingMode=ShadowCastingMode.Off;ps.Play();
        }
        private static bool NearAnyRiverRoad(Transform motel,Vector3 p,float margin)
        {foreach(var r in motel.GetComponentsInChildren<MeshRenderer>(true))if(r.gameObject.activeInHierarchy&&GroundSurfaceStyle.SurfaceFor(r.name)==1){var b=r.bounds;if(p.x>b.min.x-margin&&p.x<b.max.x+margin&&p.z>b.min.z-margin&&p.z<b.max.z+margin)return true;}return false;}
        private static void MoveRiverPark(Transform motel)
        {
            var delta=new Vector3(-14,0,40);
            foreach(var plant in motel.GetComponentsInChildren<BiomePlant>(true))if(plant.transform.position.x>14&&plant.transform.position.x<28&&plant.transform.position.z> -7&&plant.transform.position.z<14)plant.transform.position+=delta;
            foreach(var part in motel.GetComponentsInChildren<MeshRenderer>(true))
            {if(part.transform.parent==null||part.transform.parent.name!="Expanded District v1")continue;var p=part.bounds.center;if((part.name=="Park lawn"||part.name=="Park bench"||part.name=="Tree trunk"||part.name.StartsWith("Tree crown"))&&p.x>14&&p.x<28&&p.z> -7&&p.z<14)part.transform.position+=delta;}
        }
        private static void MoveRiverParking(Transform motel)
        {
            var parking=motel.Find("Motel parking courts v1");if(parking==null)return;
            var old=parking.GetComponentsInChildren<MeshRenderer>().FirstOrDefault(r=>r.name=="Showroom apron"&&r.bounds.center.x>0);if(old==null)return;
            var original=old.bounds;var delta=new Vector3(-34,0,-16);old.transform.position+=delta;
            foreach(var vehicle in motel.GetComponentsInChildren<EnterableVehicle>(true))
            {var p=vehicle.transform.position;if(p.x<original.min.x||p.x>original.max.x||p.z<original.min.z||p.z>original.max.z)continue;var body=vehicle.GetComponent<Rigidbody>();if(body!=null)body.position+=delta;else vehicle.transform.position+=delta;}
            foreach(var r in motel.GetComponentsInChildren<MeshRenderer>(true))
            {if(r.name!="Faded parking marking")continue;var p=r.bounds.center;if(p.x>=original.min.x-.2f&&p.x<=original.max.x+.2f&&p.z>=original.min.z-.2f&&p.z<=original.max.z+.2f)r.transform.position+=delta;}
        }
        private static void ClearRiverPlants(Transform motel)
        {foreach(var plant in motel.GetComponentsInChildren<BiomePlant>(true))if(!plant.transform.IsChildOf(motel.Find("Village City River v3"))&&VillageRiverShape.Contains(new Vector2(plant.transform.position.x,plant.transform.position.z),5.8f))plant.gameObject.SetActive(false);}
        private static void EndPool(Transform root,float z,Material water,Material earth)
        {
            var p=VillageRiverShape.Center(z);var mesh=OrganicLakeShape.Surface();mesh.name="River end pool "+Mathf.RoundToInt(z);var vertices=mesh.vertices;for(int i=0;i<vertices.Length;i++)vertices[i]=new Vector3(vertices[i].x*.20f+p.x,VillageRiverShape.WaterLevel,vertices[i].z*.20f+z);mesh.vertices=vertices;mesh.RecalculateBounds();RiverMesh(root,"River spring pool water",mesh,water,4);
            var bed=OrganicLakeShape.Surface(true);bed.name="River end pool bed "+Mathf.RoundToInt(z);vertices=bed.vertices;for(int i=0;i<vertices.Length;i++)vertices[i]=new Vector3(vertices[i].x*.2f+p.x,VillageRiverShape.WaterLevel+vertices[i].y,vertices[i].z*.2f+z);bed.vertices=vertices;bed.RecalculateNormals();bed.RecalculateBounds();RiverMesh(root,"River spring bed water",bed,earth,0);
            var bank=OrganicLakeShape.Bank();bank.name="River end pool shore "+Mathf.RoundToInt(z);vertices=bank.vertices;for(int i=0;i<vertices.Length;i++){bool outer=i>=vertices.Length/2;vertices[i]=new Vector3(vertices[i].x*.2f+p.x,outer?-.09f:VillageRiverShape.WaterLevel-.04f,vertices[i].z*.2f+z);}bank.vertices=vertices;bank.RecalculateNormals();bank.RecalculateBounds();RiverMesh(root,"River spring shore marking",bank,earth,0);
        }
        private static void CutRiverTerrain(Transform motel)
        {
            foreach(var terrain in motel.GetComponentsInChildren<Terrain>(true))
            {
                var data=terrain.terrainData;if(data==null)continue;if(data.heightmapResolution<1025){var originalSize=data.size;var oldHeights=data.GetHeights(0,0,data.heightmapResolution,data.heightmapResolution);int oldN=oldHeights.GetLength(0);var heights=new float[1025,1025];for(int zz=0;zz<1025;zz++)for(int xx=0;xx<1025;xx++)heights[zz,xx]=oldHeights[Mathf.RoundToInt(zz*(oldN-1)/1024f),Mathf.RoundToInt(xx*(oldN-1)/1024f)];data.heightmapResolution=1025;data.size=originalSize;data.SetHeights(0,0,heights);}int n=data.holesResolution;var holes=data.GetHoles(0,0,n,n);var origin=terrain.transform.position;var lakeOutline=OrganicLakeShape.Outline();for(int hz=0;hz<n;hz++)for(int hx=0;hx<n;hx++){var wp=new Vector2(origin.x+(hx+.5f)/n*data.size.x,origin.z+(hz+.5f)/n*data.size.z);holes[hz,hx]=Mathf.Abs(wp.x+200)>42||Mathf.Abs(wp.y+180)>32||!OrganicLakeShape.Contains(new Vector2(wp.x+200,wp.y+180),lakeOutline);}
                int x0=Mathf.Clamp(Mathf.FloorToInt((-370-origin.x)/data.size.x*n),0,n-1),x1=Mathf.Clamp(Mathf.CeilToInt((150-origin.x)/data.size.x*n),0,n-1),z0=Mathf.Clamp(Mathf.FloorToInt((VillageRiverShape.South-9-origin.z)/data.size.z*n),0,n-1),z1=Mathf.Clamp(Mathf.CeilToInt((VillageRiverShape.North+9-origin.z)/data.size.z*n),0,n-1);
                for(int z=z0;z<=z1;z++)for(int x=x0;x<=x1;x++){var p=new Vector2(origin.x+(x+.5f)/n*data.size.x,origin.z+(z+.5f)/n*data.size.z);if(VillageRiverShape.Contains(p,3.2f))holes[z,x]=false;}
                data.SetHoles(0,0,holes);if(!Application.isPlaying)EditorUtility.SetDirty(data);
            }
        }
    }
}
