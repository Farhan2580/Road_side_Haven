using System;
using System.IO;
using MotelGame.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
namespace RoadsideHaven.Editor
{
    /// <summary>Builds a separate, baked river terrain review scene without changing the live motel scene.</summary>
    public static class RiverTerrainStepOne
    {
        public const string Folder="Assets/_Game/Generated/RiverStep1";
        public const string ScenePath=Folder+"/RiverStep1.unity";
        private const int HeightResolution=1025;
        private const int PaintResolution=512;
        private const float Bottom=-8;
        private const float HeightRange=16;
        [MenuItem("Roadside Haven/River/Build Step 1 Terrain Scene")]
        public static void Build()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode){Debug.LogWarning("Stop Play Mode before baking river terrain.");return;}
            EnsureFolder(Folder);
            // Backup previous generated work before regeneration. The gameplay scene is never overwritten.
            if(AssetDatabase.LoadAssetAtPath<TerrainData>(Folder+"/RiverTerrain.asset")!=null)
            {
                string backup="Backups/RiverStep1/Generated-"+DateTime.Now.ToString("yyyyMMdd-HHmmss");
                Directory.CreateDirectory(backup);
                foreach(string file in Directory.GetFiles(Folder))if(!file.EndsWith(".meta"))File.Copy(file,Path.Combine(backup,Path.GetFileName(file)),true);
            }
            Scene previous=SceneManager.GetActiveScene();
            if(previous.path==ScenePath&&previous.isDirty)throw new InvalidOperationException("Save or close the existing river review scene before rebuilding.");
            bool emptyUntitled=string.IsNullOrEmpty(previous.path)&&previous.GetRootGameObjects().Length==0;
            if(string.IsNullOrEmpty(previous.path)&&!emptyUntitled)throw new InvalidOperationException("Save the current untitled scene before building the river review scene.");
            var mode=emptyUntitled||(previous.path==ScenePath&&!previous.isDirty)?NewSceneMode.Single:NewSceneMode.Additive;
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,mode);
            SceneManager.SetActiveScene(scene);
            try
            {
                var root=new GameObject(RiverTerrainProfile.BakeVersion).transform;
                var terrain=BuildTerrain(root);
                BuildWater(root);
                BuildReviewRig(root);
                var sun=new GameObject("Daylight").AddComponent<Light>();sun.transform.SetParent(root,false);sun.type=LightType.Directional;sun.intensity=1.1f;sun.shadows=LightShadows.Soft;sun.transform.rotation=Quaternion.Euler(42,-30,0);
                RenderSettings.ambientMode=AmbientMode.Trilight;RenderSettings.ambientSkyColor=new Color(.55f,.65f,.75f);RenderSettings.ambientEquatorColor=new Color(.35f,.40f,.28f);RenderSettings.ambientGroundColor=new Color(.17f,.20f,.13f);
                RenderSettings.fog=false;
                // A fixed sun and baked geometry isolate phase-one ground and collision checks.
                AssetDatabase.SaveAssets();
                EditorSceneManager.SaveScene(scene,ScenePath);
                if(mode==NewSceneMode.Additive){EditorSceneManager.CloseScene(scene,true);if(previous.IsValid())SceneManager.SetActiveScene(previous);}else Selection.activeGameObject=terrain.gameObject;
                Debug.Log("River Step 1 baked: "+ScenePath+". WASD walk, Shift run, hold right mouse to look, R return to start. Terrain generation never runs in Play Mode.");
            }
            catch {EditorSceneManager.CloseScene(scene,true);if(previous.IsValid())SceneManager.SetActiveScene(previous);throw;}
        }
        [MenuItem("Roadside Haven/River/Open Step 1 Terrain Scene")]
        public static void Open()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)return;
            if(!File.Exists(ScenePath)){Debug.LogWarning("Build the Step 1 terrain scene first.");return;}
            if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
            EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);
        }
        private static Terrain BuildTerrain(Transform root)
        {
            var data=AssetDatabase.LoadAssetAtPath<TerrainData>(Folder+"/RiverTerrain.asset");
            if(data==null){data=new TerrainData{name="Continuous 2km river terrain"};AssetDatabase.CreateAsset(data,Folder+"/RiverTerrain.asset");}
            data.heightmapResolution=HeightResolution;data.alphamapResolution=PaintResolution;data.baseMapResolution=1024;data.size=new Vector3(2000,HeightRange,2000);
            var heights=new float[HeightResolution,HeightResolution];
            for(int z=0;z<HeightResolution;z++)
            {
                float wz=-1000+2000f*z/(HeightResolution-1);
                for(int x=0;x<HeightResolution;x++){float wx=-1000+2000f*x/(HeightResolution-1);heights[z,x]=(RiverTerrainProfile.Height(wx,wz)-Bottom)/HeightRange;}
            }
            data.SetHeights(0,0,heights);
            int holesResolution=data.holesResolution;var solid=new bool[holesResolution,holesResolution];
            for(int z=0;z<holesResolution;z++)for(int x=0;x<holesResolution;x++)solid[z,x]=true;
            data.SetHoles(0,0,solid);
            string[] names={"BaseGrass","DirtPebbles","DryGrass","RockyGravel"};var layers=new TerrainLayer[names.Length];
            for(int i=0;i<names.Length;i++)
            {
                string path=Folder+"/"+names[i]+".terrainlayer";var layer=AssetDatabase.LoadAssetAtPath<TerrainLayer>(path);
                if(layer==null){layer=new TerrainLayer{name=names[i]};AssetDatabase.CreateAsset(layer,path);}
                layer.diffuseTexture=AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_Game/Art/Surfaces/"+names[i]+"_Albedo.png");layer.normalMapTexture=AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_Game/Art/Surfaces/"+names[i]+"_Normal.png");
                if(layer.diffuseTexture==null||layer.normalMapTexture==null)throw new InvalidOperationException("Missing terrain texture pair: "+names[i]);
                layer.tileSize=Vector2.one*(i==0?15:8);layer.normalScale=.25f;layer.smoothness=i==1?.2f:.06f;layers[i]=layer;EditorUtility.SetDirty(layer);
            }
            data.terrainLayers=layers;
            var paint=new float[PaintResolution,PaintResolution,4];
            for(int z=0;z<PaintResolution;z++)for(int x=0;x<PaintResolution;x++)
            {
                float wx=-1000+2000f*x/(PaintResolution-1),wz=-1000+2000f*z/(PaintResolution-1);
                float d=RiverTerrainProfile.ShoreDistance(wx,wz);
                float grass=Mathf.SmoothStep(0,1,Mathf.InverseLerp(6,15,d));
                float dry=d>0?(1-grass)*Mathf.SmoothStep(0,1,d/6):0;
                paint[z,x,0]=grass;paint[z,x,1]=1-grass-dry;paint[z,x,2]=dry;
            }
            data.SetAlphamaps(0,0,paint);
            var go=Terrain.CreateTerrainGameObject(data);go.name="Continuous terrain and collision";go.transform.SetParent(root,false);go.transform.position=new Vector3(-1000,Bottom,-1000);
            var terrain=go.GetComponent<Terrain>();terrain.drawInstanced=true;terrain.heightmapPixelError=3;terrain.basemapDistance=1000;terrain.detailObjectDensity=0;terrain.drawTreesAndFoliage=false;
            terrain.materialTemplate=Material("Terrain",Shader.Find("Universal Render Pipeline/Terrain/Lit"),Color.white);
            terrain.GetComponent<TerrainCollider>().terrainData=data;terrain.Flush();EditorUtility.SetDirty(data);
            return terrain;
        }
        private static void BuildWater(Transform root)
        {
            var shader=AssetDatabase.LoadAssetAtPath<Shader>("Assets/_Game/Art/Water/VillageCityRiver.shadergraph");
            if(shader==null)throw new InvalidOperationException("Wait for the existing water Shader Graph to finish importing.");
            var material=Material("Water preview",shader,Color.white);
            material.SetColor("_ShallowColor",new Color(79/255f,195/255f,212/255f));material.SetColor("_DeepColor",new Color(26/255f,82/255f,118/255f));material.SetTexture("_WaveNormal",AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_Game/Art/Water/LakeWaveNormal.png"));
            const int rows=50,columns=16;
            for(int chunk=0;chunk<20;chunk++)
            {
                float start=1000-chunk*100;var vertices=new Vector3[(rows+1)*(columns+1)];var uv=new Vector2[vertices.Length];var colors=new Color[vertices.Length];var triangles=new int[rows*columns*6];
                for(int row=0;row<=rows;row++)
                {
                    float z=start-row*2;RiverTerrainProfile.Banks(z,out float left,out float right);
                    for(int col=0;col<=columns;col++){int i=row*(columns+1)+col;float t=col/(float)columns;vertices[i]=new Vector3(Mathf.Lerp(left,right,t),RiverTerrainProfile.WaterHeight,z);uv[i]=new Vector2(t,(1000-z)/80);colors[i]=new Color(Mathf.Sin(Mathf.PI*t),0,0,1);}
                }
                int index=0;for(int row=0;row<rows;row++)for(int col=0;col<columns;col++){int a=row*(columns+1)+col,b=a+1,c=a+columns+1,d=c+1;triangles[index++]=a;triangles[index++]=b;triangles[index++]=c;triangles[index++]=b;triangles[index++]=d;triangles[index++]=c;}
                string path=Folder+"/WaterChunk"+chunk+".asset";var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(mesh==null){mesh=new Mesh{name="Water chunk "+chunk};AssetDatabase.CreateAsset(mesh,path);}mesh.Clear();mesh.vertices=vertices;mesh.uv=uv;mesh.colors=colors;mesh.triangles=triangles;mesh.RecalculateNormals();mesh.RecalculateBounds();EditorUtility.SetDirty(mesh);
                var go=new GameObject("Preview water "+chunk);go.transform.SetParent(root,false);go.layer=4;go.AddComponent<MeshFilter>().sharedMesh=mesh;var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;renderer.shadowCastingMode=ShadowCastingMode.Off;
            }
        }
        private static void BuildReviewRig(Transform root)
        {
            var player=new GameObject("Terrain review walker");player.transform.SetParent(root,false);player.transform.position=new Vector3(-100,1.2f,900);player.transform.rotation=Quaternion.Euler(0,90,0);
            var controller=player.AddComponent<CharacterController>();controller.height=1.8f;controller.center=new Vector3(0,.9f,0);controller.radius=.35f;controller.stepOffset=.3f;
            var camera=new GameObject("Review camera").AddComponent<Camera>();camera.transform.SetParent(player.transform,false);camera.transform.localPosition=Vector3.up*1.6f;camera.tag="MainCamera";camera.nearClipPlane=.1f;camera.farClipPlane=2200;camera.fieldOfView=65;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.5f,.68f,.8f);camera.gameObject.AddComponent<AudioListener>();camera.gameObject.AddComponent<UniversalAdditionalCameraData>();
            player.AddComponent<RiverTerrainWalker>().Configure(camera.transform);
        }
        private static Material Material(string name,Shader shader,Color tint)
        {
            if(shader==null)throw new InvalidOperationException("Required URP shader is unavailable: "+name);
            string path=Folder+"/"+name+".mat";var material=AssetDatabase.LoadAssetAtPath<Material>(path);if(material==null){material=new Material(shader);AssetDatabase.CreateAsset(material,path);}material.shader=shader;if(material.HasProperty("_BaseColor"))material.SetColor("_BaseColor",tint);material.enableInstancing=true;EditorUtility.SetDirty(material);return material;
        }
        private static void EnsureFolder(string path)
        {if(AssetDatabase.IsValidFolder(path))return;string parent=Path.GetDirectoryName(path).Replace((char)92,'/');EnsureFolder(parent);AssetDatabase.CreateFolder(parent,Path.GetFileName(path));}
    }
}