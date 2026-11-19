using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace RoadsideHaven.Editor
{
    [InitializeOnLoad]
    public static class HospitalEditorAccess
    {
        static HospitalEditorAccess(){EditorApplication.delayCall+=Snapshot;EditorApplication.projectChanged+=Snapshot;EditorApplication.playModeStateChanged+=PlayState;}
        static void PlayState(PlayModeStateChange state){if(state==PlayModeStateChange.EnteredPlayMode){HospitalTravel.Install();Snapshot();}}
        [MenuItem("Roadside Haven/Hospital/Go to Hospital (Play Mode)")]
        public static void Go(){HospitalTravel.Visit(UnityEngine.Object.FindFirstObjectByType<MotelGameRoot>());}
        [MenuItem("Roadside Haven/Hospital/Go to Hospital (Play Mode)",true)]
        static bool CanGo()=>EditorApplication.isPlaying&&!EditorApplication.isCompiling;
        public static void Focus()
        {
            var motel=UnityEngine.Object.FindFirstObjectByType<MotelGameRoot>();if(motel==null)return;
            var hospital=motel.GetComponentInChildren<HospitalCampusLife>(true);
            if(hospital==null){hospital=InstallCampus(motel);}
            if(hospital==null){Debug.LogWarning("Hospital is not installed. Use Roadside Haven > Hospital > Build Grand Medical Centre.");return;}
            Selection.activeGameObject=hospital.gameObject;SceneView.lastActiveSceneView?.Frame(new Bounds(HospitalCampusLayout.Centre+Vector3.up*12,new Vector3(230,80,190)),false);
        }
        [Serializable] class State{public string scene;public bool playing,compiling,hospital,modern,owner,walking,driving;public Vector3 player;public string capturedUtc;}
        static void Snapshot()
        {
            if(EditorApplication.isCompiling){EditorApplication.delayCall+=Snapshot;return;}
            var h=UnityEngine.Object.FindFirstObjectByType<HospitalCampusLife>(FindObjectsInactive.Include);var p=UnityEngine.Object.FindFirstObjectByType<OwnerSandboxController>(FindObjectsInactive.Include);var car=UnityEngine.Object.FindFirstObjectByType<WorldDrivingController>(FindObjectsInactive.Include);
            var state=new State{scene=SceneManager.GetActiveScene().path,playing=EditorApplication.isPlaying,compiling=EditorApplication.isCompiling,hospital=h!=null,modern=h!=null&&h.transform.Find(HospitalArchitecture.Layer)!=null,owner=p!=null,walking=p!=null&&p.IsWalking,driving=car!=null&&car.IsDriving,player=p==null?Vector3.zero:p.transform.position,capturedUtc=DateTime.UtcNow.ToString("o")};
            Directory.CreateDirectory("Backups/HospitalCampus");File.WriteAllText("Backups/HospitalCampus/editor-state.json",JsonUtility.ToJson(state,true));
            if(EditorApplication.isPlaying)HospitalTravel.Install();
            else if(h==null&&SceneManager.GetActiveScene().path=="Assets/_Game/Generated/RoadsideHavenPrototype.unity"&&Application.dataPath.Replace('\\','/').EndsWith("/RoadsideHaven/Assets")&&Resources.Load<GameObject>(HospitalTravel.ResourceName)!=null){
                var motel=UnityEngine.Object.FindFirstObjectByType<MotelGameRoot>();if(motel!=null&&InstallCampus(motel)!=null)EditorApplication.delayCall+=Snapshot;
            }
            if(h!=null&&HospitalArchitecture.Apply(h)){if(!Application.isPlaying)EditorSceneManager.MarkSceneDirty(h.gameObject.scene);EditorApplication.delayCall+=Snapshot;}
        }
        static HospitalCampusLife InstallCampus(MotelGameRoot motel)
        {
            var hospital=HospitalTravel.EnsureCampus(motel);if(hospital==null||Application.isPlaying)return hospital;
            // Retain the user's currently open scene and edits; add only the missing campus.
            var surface=motel.GetComponent<RoadSurfacePhysics>();var mesh=surface==null?null:surface.SurfaceMesh;
            if(mesh!=null&&!AssetDatabase.Contains(mesh)){
                const string path=HospitalCampusBuilder.Folder+"/HospitalOpenSceneRoads.asset";var stored=AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if(stored==null){stored=UnityEngine.Object.Instantiate(mesh);AssetDatabase.CreateAsset(stored,path);}else{stored.Clear();EditorUtility.CopySerialized(mesh,stored);EditorUtility.SetDirty(stored);}surface.UseSavedMesh(stored);AssetDatabase.SaveAssets();
            }
            EditorSceneManager.MarkSceneDirty(motel.gameObject.scene);return hospital;
        }
    }
}
