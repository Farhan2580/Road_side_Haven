using UnityEditor;
using UnityEngine;
namespace RoadsideHaven.Editor
{
    public static partial class VisualPolishInstaller
    {
        private static void InstallSportsCarShowcase(Transform motel)
        {
            if(motel.Find("Performance Motors v1")!=null)return;
            var root=new GameObject("Performance Motors v1").transform;root.SetParent(motel,false);
            if(!Application.isPlaying)Undo.RegisterCreatedObjectUndo(root.gameObject,"Add modern sports-car showcase");
            Solid(root,"Showroom apron",new Vector3(-10,-.01f,-42),new Vector3(42,.14f,13),asphalt);
            Road(root,new Vector3(-27,0,-29),new Vector3(6,.12f,28));
            for(int i=0;i<5;i++)
            {
                var body=DriveVehicle(root,new Vector3(-24+i*7,.07f,-42),false);body.name=((ModernCarStyle)i).ToString();
                body.gameObject.AddComponent<EnterableVehicle>().Configure(1);
                body.gameObject.AddComponent<ModernCarVisuals>().Configure(i);
                Box(root,"Showroom bay marking",new Vector3(-27.5f+i*7,.071f,-42),new Vector3(.07f,.015f,8),ivory);
            }
            LabelWorld(root,"PERFORMANCE MOTORS",new Vector3(-10,3,-49),6);
        }
    }
}
