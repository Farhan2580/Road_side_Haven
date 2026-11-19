using UnityEngine;
namespace RoadsideHaven
{
    public static class HospitalCampusLayout
    {
        public const string RootName="Haven Grand Medical Centre";
        public const int Floors=6;
        public const float Storey=4.8f, Ground=.12f;
        public static readonly Vector3 Centre=new Vector3(600,0,-435);
        public static readonly Rect Campus=Rect.MinMaxRect(470,-560,730,-360);
        public static readonly Rect Building=Rect.MinMaxRect(556,-458,644,-412);
        public static readonly string[] Departments={"EMERGENCY / RECEPTION","OUTPATIENT / DIAGNOSTICS","GENERAL WARDS","SURGERY / RECOVERY","ICU / CARDIAC CARE","MATERNITY / FAMILY CARE","ROOFTOP / AIR AMBULANCE"};
        public static Vector3 Point(float x,float y,float z)=>Centre+new Vector3(x,y,z);
        public static Vector3 LiftLanding(int floor)=>Point(-36,Ground+Storey*Mathf.Clamp(floor,0,Floors)+.08f,0);
        public static Vector3 Entrance=>Point(0,.22f,-49);
        public static Vector3 Pad=>Point(0,Ground+Floors*Storey+.12f,7);
        public static readonly Rect[] Roads={Rect.MinMaxRect(973,-527,987,-2),Rect.MinMaxRect(531,-527,987,-513),Rect.MinMaxRect(698,-527,712,-449),Rect.MinMaxRect(531,-475,712,-467),Rect.MinMaxRect(531,-527,539,-467)};
    }
}
