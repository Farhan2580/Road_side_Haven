using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace RoadsideHaven.Editor
{
    public static class RoadClearanceEditor
    {
        [MenuItem("Roadside Haven/Clear Objects From Roads")]
        public static void Clear()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode){Debug.LogWarning("Stop Play Mode before repairing roadside placement.");return;}
            var scene=SceneManager.GetActiveScene();var root=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<MotelGameRoot>(true)).FirstOrDefault();
            if(root==null){Debug.LogWarning("Open the motel gameplay scene first.");return;}
            if(!string.IsNullOrEmpty(scene.path)&&File.Exists(scene.path))
            {
                string folder="Backups/RoadClearance/"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");Directory.CreateDirectory(folder);File.Copy(scene.path,folder+"/"+Path.GetFileName(scene.path),true);
            }
            if(root.GetComponent<RoadClearance>()==null)Undo.AddComponent<RoadClearance>(root.gameObject);
            Undo.RegisterFullObjectHierarchyUndo(root.gameObject,"Clear objects from roads");
            int moved=RoadClearance.Resolve(root.transform);EditorSceneManager.MarkSceneDirty(scene);
            int remaining=RoadClearance.CountRoadOverlaps(root.transform);
            Debug.Log("Road clearance: moved "+moved+" groups; remaining static road overlaps: "+remaining+". Save the scene to retain the layout.");
        }
    }
}