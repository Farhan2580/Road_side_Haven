using UnityEngine;
namespace RoadsideHaven
{
    public enum RoadSurfaceKind {Highway,City,Dirt,Parking}
    public sealed class RoadSurfaceProfile : MonoBehaviour
    {
        public RoadSurfaceKind Kind;
        public static RoadSurfaceKind Detect(MeshRenderer surface)
        {
            var p=surface.transform;string name=surface.name;
            if(!TownPopulationController.RoadName(name))return RoadSurfaceKind.Parking;
            for(var parent=p.parent;parent!=null;parent=parent.parent)if(parent.name=="Modern Downtown v1"||parent.name.Contains("Industrial Zone"))return RoadSurfaceKind.City;
            if(p.parent!=null&&p.parent.name=="Regional World 2km v1")return RoadSurfaceKind.Highway;
            var c=surface.bounds.center;
            if(p.parent!=null&&p.parent.name.Contains("Mountain dirt"))return RoadSurfaceKind.Dirt;
            if(c.x< -35||c.z< -45&&Mathf.Abs(c.x)<150)return RoadSurfaceKind.Dirt;
            return RoadSurfaceKind.City;
        }
        public static float Width(RoadSurfaceKind kind)=>kind==RoadSurfaceKind.Highway?12:kind==RoadSurfaceKind.City?8:kind==RoadSurfaceKind.Dirt?5:0;
    }
}
