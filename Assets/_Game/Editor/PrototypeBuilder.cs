using System.IO;
using System.Linq;
using TMPro;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace RoadsideHaven.Editor
{
    /// <summary>Owns only generated prototype assets; preserves tuning on subsequent runs.</summary>
    public static class PrototypeBuilder
    {
        const string Folder = "Assets/_Game/Generated";
        const string TmpResourcesPackage = "TMP Essential Resources";
        static bool importingTmpResources;
        static TMP_FontAsset font;
        static Material cream, teal, wood, coral, grass, paving;
        [MenuItem("Roadside Haven/Create or Update Prototype")]
        public static void CreateOrUpdate()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (importingTmpResources)
            {
                Debug.Log("Roadside Haven is waiting for TMP Essential Resources to finish importing.");
                return;
            }
            if (!TryFindFont(out font))
            {
                ImportTmpResources();
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            // Existing prototypes are upgraded in place. Never regenerate the tested motel geometry.
            string scenePath = Folder + "/RoadsideHavenPrototype.unity";
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) != null)
            {
                var existingScene = SceneManager.GetActiveScene();
                if (existingScene.path != scenePath)
                    existingScene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                var motel = existingScene.GetRootGameObjects()
                    .SelectMany(go => go.GetComponentsInChildren<MotelGameRoot>(true)).FirstOrDefault();
                if (motel == null)
                {
                    Debug.LogError("The existing prototype scene has no MotelGameRoot. It was left unchanged.");
                    return;
                }
                if (ShopPrototypeInstaller.Install(motel, font))
                {
                    VisualPolishInstaller.Install(motel, font);
                    EditorSceneManager.MarkSceneDirty(existingScene);
                    EditorSceneManager.SaveScene(existingScene);
                    AssetDatabase.SaveAssets();
                    Debug.Log("Roadside Haven shop updated in place. Existing motel scene, configuration and navigation were preserved.");
                }
                return;
            }
            Directory.CreateDirectory(Folder);
            AssetDatabase.Refresh();
            var settings = AssetDatabase.LoadAssetAtPath<HotelGameSettings>(Folder + "/HotelSettings.asset");
            if (settings == null) { settings = ScriptableObject.CreateInstance<HotelGameSettings>(); AssetDatabase.CreateAsset(settings, Folder + "/HotelSettings.asset"); }
            cream = Mat("Sand", new Color(.92f,.82f,.63f)); teal = Mat("Lagoon", new Color(.10f,.43f,.43f));
            wood = Mat("Walnut", new Color(.35f,.21f,.13f)); coral = Mat("Terracotta", new Color(.85f,.35f,.23f));
            grass = Mat("Sage", new Color(.35f,.49f,.34f)); paving = Mat("Paving", new Color(.66f,.65f,.54f));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("Roadside Haven • Prototype");
            var game = root.AddComponent<MotelGameRoot>();
            var geometry = new GameObject("Navigation Geometry"); geometry.transform.SetParent(root.transform);
            Cube("Ground", geometry.transform, new Vector3(0,-.25f,0), new Vector3(27,.5f,23), grass);
            Cube("Connected courtyard and corridor", geometry.transform, new Vector3(0,.01f,-2), new Vector3(23,.08f,13), paving);
            for (int n=0;n<4;n++)
            {
                float x = -8.25f+n*5.5f;
                var room = new GameObject("Room " + (101+n)); room.transform.SetParent(geometry.transform);
                Cube("Floor", room.transform, new Vector3(x,.10f,4), new Vector3(5.2f,.16f,6), cream);
                Cube("Back wall", room.transform, new Vector3(x, .85f,7), new Vector3(5.3f,1.5f,.18f), teal);
                Cube("Side wall", room.transform, new Vector3(x-2.6f,.65f,4), new Vector3(.16f,1.1f,6), cream);
                Cube("Door left wall", room.transform, new Vector3(x-1.65f,.55f,1), new Vector3(1.9f,.9f,.15f), teal);
                Cube("Door right wall", room.transform, new Vector3(x+1.65f,.55f,1), new Vector3(1.9f,.9f,.15f), teal);
                Cube("Open door", room.transform, new Vector3(x+.70f,.65f,1.65f), new Vector3(.12f,1.2f,1.2f), wood);
                Cube("Bed frame", room.transform, new Vector3(x-1,.4f,4.8f), new Vector3(1.8f,.5f,2.9f), wood);
                Cube("Duvet", room.transform, new Vector3(x-1,.72f,4.6f), new Vector3(1.85f,.18f,2.3f), coral);
                Cube("Pillow", room.transform, new Vector3(x-1,.78f,5.65f), new Vector3(1.4f,.2f,.6f), cream);
                Cube("Nightstand", room.transform, new Vector3(x+.65f,.4f,5.5f), new Vector3(.7f,.6f,.7f), wood);
                var target = Point("Stay point", room.transform, new Vector3(x+1.25f,.25f,3.8f));
                room.AddComponent<RoomSelectable>().Configure((101+n).ToString(), target);
                Label((101+n).ToString(), room.transform, new Vector3(x,1.5f,1), .65f);
            }
            Cube("Reception desk", geometry.transform, new Vector3(-6,.65f,-3.7f), new Vector3(3,1.3f,1), wood);
            Cube("Desk top", geometry.transform, new Vector3(-6,1.35f,-3.7f), new Vector3(3.3f,.15f,1.2f), cream);
            Label("RECEPTION", root.transform, new Vector3(-6,2,-3.7f), .6f);
            Cube("Shop plinth", geometry.transform, new Vector3(7,.15f,-5.5f), new Vector3(5,.3f,4), cream);
            Cube("Shop counter", geometry.transform, new Vector3(7,.7f,-6), new Vector3(4,1.1f,.8f), teal);
            for (int i=0;i<5;i++) Cube("Display goods", geometry.transform,new Vector3(5.5f+i*.7f,1.5f,-6),new Vector3(.4f,.5f,.4f),i%2==0?coral:cream);
            Label("PIT STOP • SHOP", root.transform, new Vector3(7,2.4f,-6), .65f);
            Label("ROADSIDE HAVEN", root.transform, new Vector3(0,.3f,-9.5f), .8f);
            var queue = new Transform[8];
            for (int i=0;i<queue.Length;i++) queue[i]=Point("Queue " + i,root.transform,new Vector3(-6+i*1.45f,.2f,-1.9f));
            var spawn=Point("Arrival",root.transform,new Vector3(-10,.2f,-8));
            var exit=Point("Exit",root.transform,new Vector3(-11,.2f,-8));
            var surface=geometry.AddComponent<NavMeshSurface>(); surface.collectObjects=CollectObjects.Children; surface.useGeometry=NavMeshCollectGeometry.PhysicsColliders;
            surface.BuildNavMesh();
            if (surface.navMeshData == null || NavMesh.CalculateTriangulation().vertices.Length == 0) throw new System.InvalidOperationException("Navigation bake failed.");
            string navPath=Folder+"/PrototypeNavMesh.asset";
            var existing=AssetDatabase.LoadAssetAtPath<NavMeshData>(navPath);
            if(existing==null) AssetDatabase.CreateAsset(surface.navMeshData,navPath);
            else { var baked=surface.navMeshData; surface.RemoveData(); EditorUtility.CopySerialized(baked,existing); surface.navMeshData=existing; surface.AddData(); EditorUtility.SetDirty(existing); }
            var cameraObject=new GameObject("Isometric Camera",typeof(Camera),typeof(AudioListener)); cameraObject.tag="MainCamera";
            var camera=cameraObject.GetComponent<Camera>(); camera.orthographic=true; camera.orthographicSize=15; camera.backgroundColor=new Color(.65f,.76f,.75f); camera.clearFlags=CameraClearFlags.SolidColor;
            cameraObject.transform.rotation=Quaternion.Euler(52,0,0);
            var controller=cameraObject.AddComponent<IsometricCameraController>(); controller.Configure(new Vector3(0,0,0),new Vector2(9,7),new Vector2(7,18));
            cameraObject.transform.position=new Vector3(0,0,0)-cameraObject.transform.forward*24;
            cameraObject.transform.rotation=Quaternion.Euler(52,25,0); cameraObject.transform.position=-cameraObject.transform.forward*30;
            var light=new GameObject("Warm afternoon",typeof(Light)).GetComponent<Light>(); light.type=LightType.Directional; light.intensity=1.35f; light.color=new Color(1,.88f,.72f); light.shadows=LightShadows.Soft; light.transform.rotation=Quaternion.Euler(50,-35,0);
            RenderSettings.ambientLight=new Color(.65f,.70f,.72f); RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;
            var hud=BuildHud(root.transform);
            new GameObject("EventSystem",typeof(EventSystem),typeof(InputSystemUIInputModule));
            game.Configure(settings,surface,spawn,exit,queue,coral,hud,controller);
            ShopPrototypeInstaller.Install(game, font);
            VisualPolishInstaller.Install(game, font);
            PlayerSettings.defaultInterfaceOrientation=UIOrientation.LandscapeLeft;
            PlayerSettings.allowedAutorotateToPortrait=false; PlayerSettings.allowedAutorotateToPortraitUpsideDown=false;
            EditorSceneManager.SaveScene(scene,Folder+"/RoadsideHavenPrototype.unity");
            var scenes=EditorBuildSettings.scenes.ToList();
            if(!scenes.Any(s=>s.path==Folder+"/RoadsideHavenPrototype.unity")) scenes.Add(new EditorBuildSettingsScene(Folder+"/RoadsideHavenPrototype.unity",true));
            EditorBuildSettings.scenes=scenes.ToArray(); AssetDatabase.SaveAssets();
            Debug.Log("Roadside Haven generated with baked navigation. Press Play to begin.");
        }

        /// <summary>Uses the configured default before searching project fonts; an empty search is safe.</summary>
        static bool TryFindFont(out TMP_FontAsset result)
        {
            result = null;
            // TMP components also need settings. This lookup does not open TMP's import dialog.
            if (TMP_Settings.LoadDefaultSettings() == null) return false;
            result = TMP_Settings.GetFontAsset();
            if (result != null) return true;

            result = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
                "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
            if (result != null) return true;

            foreach (string guid in AssetDatabase.FindAssets("t:TMP_FontAsset", new[] { "Assets" }))
            {
                var candidate = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(guid));
                // Avoid selecting empty dynamic fallback assets as the primary HUD font.
                if (candidate != null && candidate.characterTable != null && candidate.characterTable.Count > 0)
                {
                    result = candidate;
                    return true;
                }
            }
            return false;
        }

        /// <summary>Package imports finish asynchronously; resume only after the completion event.</summary>
        static void ImportTmpResources()
        {
            var package = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(TMP_FontAsset).Assembly);
            string archive = package == null ? null : Path.Combine(package.resolvedPath,
                "Package Resources", TmpResourcesPackage + ".unitypackage");
            if (archive == null || !File.Exists(archive))
            {
                Debug.LogError("Roadside Haven could not locate TMP Essential Resources in the installed package. " +
                    "Use Window > TextMeshPro > Import TMP Essential Resources, then run Create or Update Prototype again.");
                return;
            }

            importingTmpResources = true;
            AssetDatabase.importPackageCompleted += OnTmpImportCompleted;
            AssetDatabase.importPackageFailed += OnTmpImportFailed;
            AssetDatabase.importPackageCancelled += OnTmpImportCancelled;
            AssemblyReloadEvents.beforeAssemblyReload += CancelPendingTmpImport;
            try
            {
                Debug.Log("Roadside Haven is importing TMP Essential Resources. Scene generation will resume when ready.");
                AssetDatabase.ImportPackage(archive, false);
            }
            catch (System.Exception exception)
            {
                CancelPendingTmpImport();
                Debug.LogException(exception);
            }
        }

        static bool IsTmpPackage(string packageName) =>
            Path.GetFileNameWithoutExtension(packageName) == TmpResourcesPackage;

        static void OnTmpImportCompleted(string packageName)
        {
            if (!IsTmpPackage(packageName)) return;
            // Leave the import callback before creating assets or changing scenes.
            RemoveTmpImportCallbacks();
            EditorApplication.delayCall += ResumeAfterTmpImport;
        }

        static void ResumeAfterTmpImport()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += ResumeAfterTmpImport;
                return;
            }
            CancelPendingTmpImport();
            if (!TryFindFont(out font))
            {
                Debug.LogError("TMP import completed, but its settings or font could not be loaded. " +
                    "Use Window > TextMeshPro > Import TMP Essential Resources, then retry Create or Update Prototype.");
                return;
            }
            CreateOrUpdate();
        }

        static void OnTmpImportFailed(string packageName, string error)
        {
            if (!IsTmpPackage(packageName)) return;
            CancelPendingTmpImport();
            Debug.LogError("Roadside Haven: TMP resources import failed: " + error);
        }

        static void OnTmpImportCancelled(string packageName)
        {
            if (!IsTmpPackage(packageName)) return;
            CancelPendingTmpImport();
            Debug.LogWarning("Roadside Haven: TMP resources import was cancelled. Run Create or Update Prototype to retry.");
        }

        static void RemoveTmpImportCallbacks()
        {
            AssetDatabase.importPackageCompleted -= OnTmpImportCompleted;
            AssetDatabase.importPackageFailed -= OnTmpImportFailed;
            AssetDatabase.importPackageCancelled -= OnTmpImportCancelled;
        }

        static void CancelPendingTmpImport()
        {
            RemoveTmpImportCallbacks();
            EditorApplication.delayCall -= ResumeAfterTmpImport;
            AssemblyReloadEvents.beforeAssemblyReload -= CancelPendingTmpImport;
            importingTmpResources = false;
        }

        static Material Mat(string name,Color color)
        {
            string path=Folder+"/"+name+".mat"; var m=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(m==null){m=new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m,path);}
            m.color=color; m.SetFloat("_Smoothness",.15f); EditorUtility.SetDirty(m); return m;
        }
        static GameObject Cube(string name,Transform parent,Vector3 position,Vector3 scale,Material material)
        {
            var o=GameObject.CreatePrimitive(PrimitiveType.Cube); o.name=name; o.transform.SetParent(parent); o.transform.position=position; o.transform.localScale=scale; o.GetComponent<Renderer>().sharedMaterial=material; return o;
        }
        static Transform Point(string name,Transform parent,Vector3 position){var t=new GameObject(name).transform;t.SetParent(parent);t.position=position;return t;}
        static void Label(string value,Transform parent,Vector3 position,float size)
        {
            var t=new GameObject(value).AddComponent<TextMeshPro>(); t.transform.SetParent(parent); t.transform.position=position; t.transform.rotation=Quaternion.Euler(45,25,0); t.font=font; t.text=value; t.fontSize=size*10; t.alignment=TextAlignmentOptions.Center; t.color=new Color(.12f,.19f,.18f); t.rectTransform.sizeDelta=new Vector2(8,2);
        }
        static RectTransform Rect(string name,Transform parent,Vector2 min,Vector2 max)
        {
            var r=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();r.SetParent(parent,false);r.anchorMin=min;r.anchorMax=max;r.offsetMin=Vector2.zero;r.offsetMax=Vector2.zero;return r;
        }
        static TMP_Text Text(string value,Transform parent,Vector2 min,Vector2 max,int size=26)
        {
            var r=Rect(value,parent,min,max);var t=r.gameObject.AddComponent<TextMeshProUGUI>();t.font=font;t.text=value;t.fontSize=size;t.color=new Color(.96f,.91f,.8f);t.alignment=TextAlignmentOptions.Center;t.raycastTarget=false;return t;
        }
        static MotelHUD BuildHud(Transform parent)
        {
            var canvas=new GameObject("Motel HUD",typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));canvas.transform.SetParent(parent);canvas.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;
            var scaler=canvas.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1280,720);scaler.matchWidthOrHeight=.5f;
            var safe=Rect("Safe Area",canvas.transform,Vector2.zero,Vector2.one);safe.gameObject.AddComponent<SafeAreaFitter>();
            var top=Rect("Status",safe,new Vector2(.02f,.89f),new Vector2(.98f,.98f));top.gameObject.AddComponent<Image>().color=new Color(.08f,.19f,.19f,.96f);
            var money=Text("COINS",top,new Vector2(0,0),new Vector2(.33f,1));var occupied=Text("ROOMS",top,new Vector2(.33f,0),new Vector2(.66f,1));var waiting=Text("WAITING",top,new Vector2(.66f,0),Vector2.one);
            var bottom=Rect("Actions",safe,new Vector2(.02f,.02f),new Vector2(.98f,.23f));bottom.gameObject.AddComponent<Image>().color=new Color(.08f,.19f,.19f,.96f);
            var selection=Text("Selection",bottom,new Vector2(.01f,.48f),new Vector2(.76f,1),24);
            var instruction=Text("Instructions",bottom,new Vector2(.01f,0),new Vector2(.76f,.48f),21);
            var feedback=Text("Welcome to Roadside Haven",safe,new Vector2(.12f,.79f),new Vector2(.88f,.87f),24);
            var buttonRect=Rect("Clean room",bottom,new Vector2(.78f,.12f),new Vector2(.99f,.88f));buttonRect.gameObject.AddComponent<Image>().color=new Color(.73f,.30f,.18f);var button=buttonRect.gameObject.AddComponent<Button>();var label=Text("CLEAN ROOM",buttonRect,Vector2.zero,Vector2.one,25);
            var hud=canvas.AddComponent<MotelHUD>();hud.Configure(money,occupied,waiting,selection,instruction,feedback,button,label);return hud;
        }
    }
}
