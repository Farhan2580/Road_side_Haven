using UnityEngine;
namespace RoadsideHaven
{
    public enum VegetationKind {StreetTree,Palm,TrimmedBush,Oak,Pine,Bamboo,FlowerBed}
    public sealed class BiomePlant : MonoBehaviour
    {
        public VegetationKind Kind;
        public Transform Canopy;
        public bool Evergreen=>Kind==VegetationKind.Palm||Kind==VegetationKind.Pine||Kind==VegetationKind.Bamboo||Kind==VegetationKind.TrimmedBush;
        public static VegetationKind ForLocation(Vector3 p,bool waterside=false)
        {
            if(waterside)return VegetationKind.Bamboo;if(p.z< -650)return VegetationKind.Palm;if(p.z>600)return VegetationKind.Pine;
            if(p.x< -370&&p.z> -30&&p.z<650||p.x>25&&p.z> -35&&p.z<140||Mathf.Abs(p.x)<35&&Mathf.Abs(p.z)<35)return VegetationKind.StreetTree;
            return VegetationKind.Oak;
        }
    }
}
