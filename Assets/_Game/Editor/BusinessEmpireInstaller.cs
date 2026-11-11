using TMPro;
using UnityEditor;
using UnityEngine;
namespace RoadsideHaven.Editor
{
    public static partial class VisualPolishInstaller
    {
        private static void InstallBusinessEmpire(Transform motel)
        {
            if(motel.Find("Business Empire v1")!=null)return;
            string folder="Assets/_Game/Business/Configuration";
            if(!AssetDatabase.IsValidFolder(folder))AssetDatabase.CreateFolder("Assets/_Game/Business","Configuration");
            var catalog=AssetDatabase.LoadAssetAtPath<BusinessCatalog>(folder+"/BusinessCatalog.asset");
            if(catalog==null){catalog=ScriptableObject.CreateInstance<BusinessCatalog>();catalog.ConfigureDefaults();AssetDatabase.CreateAsset(catalog,folder+"/BusinessCatalog.asset");}
            var root=new GameObject("Business Empire v1").transform;root.SetParent(motel,false);
            if(!Application.isPlaying)Undo.RegisterCreatedObjectUndo(root.gameObject,"Add business empire properties");
            var controller=root.gameObject.AddComponent<BusinessEmpireController>();controller.Configure(motel.GetComponent<MotelGameRoot>(),catalog);
            Campus(root,new Vector3(-430,0,210),"CITY FAST FOOD",18,4,red);
            Road(root,new Vector3(-430,0,195),new Vector3(10,.12f,30));
            Solid(root,"Gas station forecourt",new Vector3(-220,-.01f,620),new Vector3(50,.14f,40),asphalt);
            Box(root,"Gas station canopy",new Vector3(-220,5,620),new Vector3(30,.3f,18),mint);
            for(int i=0;i<4;i++)Solid(root,"Fuel pump",new Vector3(-230+i*7,1,620),new Vector3(1.2f,2,1),red);
            Road(root,new Vector3(-220,0,640),new Vector3(12,.12f,25));
            Solid(root,"VIP helipad",new Vector3(270,.02f,580),new Vector3(42,.15f,42),asphalt);
            Box(root,"Helipad H left",new Vector3(267,.11f,580),new Vector3(.7f,.02f,10),ivory);
            Box(root,"Helipad H right",new Vector3(273,.11f,580),new Vector3(.7f,.02f,10),ivory);
            Box(root,"Helipad H bar",new Vector3(270,.11f,580),new Vector3(6,.02f,.7f),ivory);
            Road(root,new Vector3(270,0,615),new Vector3(12,.12f,70));
            Campus(root,new Vector3(-730,0,420),"CITY BANK",35,14,ivory);
            Road(root,new Vector3(-730,0,398),new Vector3(10,.12f,35));
            var specs=catalog.Build();var positions=new[]{new Vector3(-11,2,1),new Vector3(10,2,-6),new Vector3(180,3,714),new Vector3(-880,3,64),
                new Vector3(-220,3,602),new Vector3(-430,3,204),new Vector3(270,3,555),new Vector3(-730,3,414)};
            for(int i=0;i<Mathf.Min(specs.Length,positions.Length);i++)
            {
                var sign=new GameObject("Property board "+specs[i].Id).AddComponent<TextMeshPro>();sign.transform.SetParent(root,false);sign.transform.position=positions[i];
                sign.font=TMP_Settings.GetFontAsset();sign.text=specs[i].Name;sign.fontSize=4;sign.rectTransform.sizeDelta=new Vector2(4,1);sign.alignment=TextAlignmentOptions.Center;
                sign.gameObject.AddComponent<BusinessSite>().Configure(i,specs[i].Name,sign,controller);
            }
            AssetDatabase.SaveAssets();
        }
    }
}
