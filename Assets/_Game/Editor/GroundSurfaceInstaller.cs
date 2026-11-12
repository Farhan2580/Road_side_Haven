using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
namespace RoadsideHaven.Editor
{
    public static partial class VisualPolishInstaller
    {
        private static void InstallGroundMaterials(Transform motel)
        {
            GroundTextureAuthoring.Generate(GroundTextureAuthoring.Folder);GroundTextureAuthoring.ImportMaps();
            var materials=new Material[6];float[] smoothness={.08f,.22f,.10f,.12f,.19f,.25f};
            for(int i=0;i<materials.Length;i++)
            {
                string name=GroundTextureAuthoring.Names[i];var material=Material("Surface "+name,Color.white);materials[i]=material;
                material.SetColor("_BaseColor",Color.white);material.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(GroundTextureAuthoring.Folder+"/"+name+"_Albedo.png"));
                material.SetTexture("_BumpMap",AssetDatabase.LoadAssetAtPath<Texture2D>(GroundTextureAuthoring.Folder+"/"+name+"_Normal.png"));material.EnableKeyword("_NORMALMAP");material.SetFloat("_BumpScale",i==0?.65f:i==1?.35f:.55f);material.SetFloat("_Smoothness",smoothness[i]);material.SetFloat("_Metallic",0);material.enableInstancing=true;
                if(i==0){material.SetTexture("_DetailAlbedoMap",AssetDatabase.LoadAssetAtPath<Texture2D>(GroundTextureAuthoring.Folder+"/GroundMacro.png"));material.SetFloat("_DetailAlbedoMapScale",1);material.SetFloat("_DetailNormalMapScale",0);material.EnableKeyword("_DETAIL_MULX2");}
                EditorUtility.SetDirty(material);
            }
            var style=motel.GetComponent<GroundSurfaceStyle>();if(style==null)style=motel.gameObject.AddComponent<GroundSurfaceStyle>();style.Configure(materials);if(!Application.isPlaying)EditorUtility.SetDirty(style);
            InstallGroundShoulders(motel);
            InstallLayeredGrass(motel);
            InstallRoadSurfaceTypes(motel);
        }
        private static void InstallGroundShoulders(Transform motel)
        {
            if(motel.Find("Ground shoulder finish v1")!=null)return;
            var root=new GameObject("Ground shoulder finish v1").transform;root.SetParent(motel,false);if(!Application.isPlaying)Undo.RegisterCreatedObjectUndo(root.gameObject,"Add ground transitions");
            var material=Material("Soft earth shoulder",Color.white);material.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(GroundTextureAuthoring.Folder+"/Shoulder_Albedo.png"));
            material.SetFloat("_Surface",1);material.SetFloat("_Blend",0);material.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);material.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);material.SetFloat("_ZWrite",0);material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");material.SetOverrideTag("RenderType","Transparent");material.renderQueue=3000;
            var surfaces=motel.GetComponentsInChildren<MeshRenderer>(true);var grounds=surfaces.Where(r=>r.gameObject.activeInHierarchy&&GroundSurfaceStyle.SurfaceFor(r.name)==0).Select(r=>r.bounds).ToArray();
            var graph=new TownRoadNetwork();foreach(var r in surfaces)if(r.gameObject.activeInHierarchy&&TownPopulationController.RoadName(r.name))graph.Add(r.bounds.center,r.bounds.size);graph.Build(null);
            var roads=graph.Roads;var rectangles=roads.Select(r=>Rect.MinMaxRect(Mathf.Min(r.A.x,r.B.x)-(r.A.x==r.B.x?r.HalfWidth:0),Mathf.Min(r.A.z,r.B.z)-(r.A.z==r.B.z?r.HalfWidth:0),Mathf.Max(r.A.x,r.B.x)+(r.A.x==r.B.x?r.HalfWidth:0),Mathf.Max(r.A.z,r.B.z)+(r.A.z==r.B.z?r.HalfWidth:0))).ToArray();
            foreach(var road in roads)
            {
                if(Vector3.Distance(road.A,road.B)<100)continue;bool horizontal=road.B.x-road.A.x>1;var axis=(road.B-road.A).normalized;var side=Vector3.Cross(Vector3.up,axis);
                foreach(int sign in new[]{-1,1})
                {
                    var center=(road.A+road.B)*.5f+side*sign*(road.HalfWidth+4.2f);float length=Vector3.Distance(road.A,road.B);
                    var rect=horizontal?new Rect(center.x-length*.5f,center.z-1,length,2):new Rect(center.x-1,center.z-length*.5f,2,length);
                    foreach(var piece in StreetPavingLayout.ClearRoads(rect,rectangles))
                    {
                        float height=float.MinValue;foreach(var ground in grounds)if(piece.center.x>=ground.min.x&&piece.center.x<=ground.max.x&&piece.center.y>=ground.min.z&&piece.center.y<=ground.max.z)height=Mathf.Max(height,ground.max.y);if(height==float.MinValue)continue;
                        var obj=GameObject.CreatePrimitive(PrimitiveType.Quad);obj.name="Ground shoulder marking";obj.transform.SetParent(root,false);obj.transform.position=new Vector3(piece.center.x,height+.003f,piece.center.y);obj.transform.rotation=Quaternion.Euler(90,horizontal?0:90,0);obj.transform.localScale=new Vector3(horizontal?piece.width:piece.height,horizontal?piece.height:piece.width,1);obj.layer=2;
                        var collider=obj.GetComponent<Collider>();collider.enabled=false;if(Application.isPlaying)Object.Destroy(collider);else Object.DestroyImmediate(collider);
                        var renderer=obj.GetComponent<Renderer>();renderer.sharedMaterial=material;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
                        var block=new MaterialPropertyBlock();block.SetVector("_BaseMap_ST",new Vector4((horizontal?piece.width:piece.height)/4,1,0,0));renderer.SetPropertyBlock(block);
                    }
                }
            }
            EditorUtility.SetDirty(material);
        }
    }
}
