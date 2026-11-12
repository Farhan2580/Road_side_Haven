using UnityEditor;
using UnityEngine;
namespace RoadsideHaven.Editor
{
    public static partial class VisualPolishInstaller
    {
        private static void UpgradeTrafficRoads(Transform motel)
        {
            ConfigureRoadWidths(motel);
            if(motel.Find("Public Services v1")!=null)return;
            var root=new GameObject("Public Services v1").transform;root.SetParent(motel,false);
            if(!Application.isPlaying)Undo.RegisterCreatedObjectUndo(root.gameObject,"Add police and clinic");
            ServiceBuilding(root,new Vector3(225,0,12),"POLICE STATION",false);
            ServiceBuilding(root,new Vector3(215,0,110),"COMMUNITY CLINIC",true);
            Road(root,new Vector3(205,0,90),new Vector3(72,.12f,18));
            foreach(var point in new[]{new Vector3(-415,0,50),new Vector3(205,0,300),new Vector3(445,0,-580)})
            {
                Solid(root,"Police box",point+Vector3.up*1.6f,new Vector3(6,3.2f,5),ivory);
                Box(root,"Police box blue band",point+Vector3.up*2.5f,new Vector3(6.1f,.4f,5.1f),mint);
                Box(root,"Police box window",point+new Vector3(0,1.8f,-2.54f),new Vector3(3,1.1f,.06f),glass);
                LabelWorld(root,"POLICE",point+new Vector3(0,3.8f,-2.5f),3);
            }
        }
        private static void ServiceBuilding(Transform root,Vector3 p,string title,bool clinic)
        {
            Solid(root,title,p+Vector3.up*2.7f,new Vector3(16,5.4f,12),ivory);
            Box(root,clinic?"Clinic entrance":"Police station entrance",p+new Vector3(0,1.5f,-6.05f),new Vector3(2,3,.08f),glass);
            Box(root,"Service roof",p+Vector3.up*5.5f,new Vector3(17,.3f,13),clinic?red:mint);
            LabelWorld(root,title,p+new Vector3(0,4.4f,-6.2f),4);
        }
    }
}
