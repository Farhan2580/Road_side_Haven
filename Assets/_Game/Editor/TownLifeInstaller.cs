using UnityEditor;
using UnityEngine;
namespace RoadsideHaven.Editor
{
    public static partial class VisualPolishInstaller
    {
        private static void InstallTownLife(Transform motel)
        {
            var existing=motel.Find("Town Life v1");
            if(existing!=null){existing.GetComponent<TownPopulationController>().ConfigureAllDistricts();var pickup=existing.Find("RESIDENTIAL PICKUP");if(pickup!=null)pickup.position=new Vector3(-100,0,-1.5f);return;}
            var root=new GameObject("Town Life v1").transform;root.SetParent(motel,false);
            if(!Application.isPlaying)Undo.RegisterCreatedObjectUndo(root.gameObject,"Add buses and town population");
            root.gameObject.AddComponent<TownPopulationController>().ConfigureAllDistricts();
            Stop(root,new Vector3(-100,0,-1.5f),"RESIDENTIAL PICKUP",false);
            Stop(root,new Vector3(-880,0,33),"SCHOOL BUS STOP",false);
            Stop(root,new Vector3(-937,0,230),"COLLEGE BUS STOP",true);
        }
        private static void Stop(Transform root,Vector3 point,string title,bool sideways)
        {
            var stop=new GameObject(title).transform;stop.SetParent(root,false);stop.position=point;
            if(sideways)stop.rotation=Quaternion.Euler(0,90,0);
            // Geometry helpers use world coordinates, so rotate the completed shelter afterwards.
            stop.rotation=Quaternion.identity;
            Solid(stop,"Bus stop platform",point+new Vector3(0,.02f,1),new Vector3(8,.16f,3),ivory);
            for(int side=-1;side<=1;side+=2)Solid(stop,"Shelter support",point+new Vector3(side*3.4f,1.5f,2),new Vector3(.13f,3,.13f),ink);
            Solid(stop,"Bus shelter roof",point+new Vector3(0,3.05f,1),new Vector3(8,.18f,3.2f),mint);
            Solid(stop,"Waiting bench",point+new Vector3(0,.55f,2),new Vector3(4,.18f,.6f),timber);
            LabelWorld(stop,title,point+new Vector3(0,2.5f,2),3);
            if(sideways)stop.rotation=Quaternion.Euler(0,90,0);
        }
    }
}
