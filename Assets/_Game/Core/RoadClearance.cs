using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Unity.AI.Navigation;
namespace RoadsideHaven
{
    /// <summary>One-time placement repair against the actual road footprints, preserving grouped geometry.</summary>
    public sealed class RoadClearance : MonoBehaviour
    {
        [SerializeField] private bool layoutValidated;
        public bool LayoutValidated=>layoutValidated;
        private const float Margin=.65f;
        private sealed class Unit
        {
            public readonly List<Transform> Parts=new List<Transform>();
            public Bounds Bounds;
        }
        private static Rect Footprint(Bounds b)=>Rect.MinMaxRect(b.min.x,b.min.z,b.max.x,b.max.z);
        private static bool Intersects(Rect a,Rect b,float margin)=>a.xMin<b.xMax+margin&&a.xMax>b.xMin-margin&&a.yMin<b.yMax+margin&&a.yMax>b.yMin-margin;
        private static bool Movable(Renderer r)
        {
            if(!r.gameObject.activeInHierarchy||(!r.enabled&&r.GetComponent<TMPro.TMP_Text>()==null)||r is ParticleSystemRenderer||r.GetComponentInParent<Rigidbody>()!=null||r.GetComponentInParent<CharacterController>()!=null||r.GetComponentInParent<NavMeshAgent>()!=null||r.GetComponentInParent<TownCitizenAgent>()!=null)return false;
            if(r.GetComponentInParent<AirportOperations>()!=null||r.GetComponentInParent<MotelDistrictArt>()!=null)return false;
            if(GroundSurfaceStyle.SurfaceFor(r.name)>=0)return false;
            if(r.GetComponent<TMPro.TMP_Text>()!=null)return true;
            string n=r.name.ToLowerInvariant();
            if(n.Contains("water")||n.Contains("marking")||n.Contains("marker")||n.Contains("dash")||n.Contains("stripe")||n.Contains("sidewalk")||n.Contains("paving")||n.Contains("walkway")||n.Contains("ground")||n.Contains("lawn")||n.Contains("apron")||n.Contains("car ")||n=="tire"||n.Contains("truck ")||n.Contains("helipad")||n.Contains("bridge")||n.Contains("culvert")||n.Contains("foundation"))return false;
            return true;
        }
        private static float SeedScore(Renderer r){var b=Measure(r);return (r.GetComponentInParent<WorldNpc>()!=null?3000000:r.name.EndsWith(" post")||r.name.Contains("lamp pole")?2500000:r.name.StartsWith("Wash ")||r.name=="Portal ring"||r.name=="Fast travel portal"?2000000:b.size.x>=3&&b.size.z>=2&&b.size.y>=1?1000000:0)+b.size.x*b.size.z;}
        private static Bounds Measure(Renderer r)
        {
            var b=r.bounds;
            var text=r.GetComponent<TMPro.TMP_Text>();
            if(text!=null&&r.GetComponentInParent<PhysicalWorldSign>()==null)b=new Bounds(r.transform.position,new Vector3(8,1,.5f));
            return b;
        }
        /// <summary>Relocates static roadside units once, returning the number of groups moved.</summary>
        public static int Resolve(Transform root)
        {
            var roads=new List<Rect>();
            foreach(var r in root.GetComponentsInChildren<MeshRenderer>(true))if(r.gameObject.activeInHierarchy&&TownPopulationController.RoadName(r.name))roads.Add(Footprint(r.bounds));
            if(roads.Count==0)return 0;
            MoveOriginalMotel(root,roads);
            var candidates=new List<Renderer>();
            foreach(var r in root.GetComponentsInChildren<Renderer>(true))if(Movable(r))candidates.Add(r);
            var measurements=new Dictionary<Renderer,Bounds>();var scores=new Dictionary<Renderer,float>();foreach(var r in candidates){measurements[r]=Measure(r);scores[r]=SeedScore(r);}
            candidates.Sort((a,b)=>scores[b].CompareTo(scores[a]));
            var parentGroups=new Dictionary<Transform,Transform>();
            var candidateSet=new HashSet<Renderer>(candidates);var siblings=new Dictionary<Transform,List<Renderer>>();
            foreach(var r in candidates){var parent=r.transform.parent;if(!siblings.TryGetValue(parent,out var list)){list=new List<Renderer>();siblings.Add(parent,list);}list.Add(r);}
            var claimed=new HashSet<Renderer>();var units=new List<Unit>();
            foreach(var anchor in candidates)
            {
                if(claimed.Contains(anchor))continue;
                var unit=new Unit();var bounds=measurements[anchor];Transform group=null;
                var plant=anchor.GetComponentInParent<BiomePlant>();
                if(plant!=null)group=plant.transform;
                else if(anchor.GetComponentInParent<WorldNpc>()!=null)group=anchor.GetComponentInParent<WorldNpc>().transform;
                else if(anchor.GetComponentInParent<PhysicalWorldSign>()!=null)group=anchor.GetComponentInParent<PhysicalWorldSign>().transform;
                else if(anchor.transform.parent!=root&&anchor.transform.parent!=null)
                {
                    var parent=anchor.transform.parent;
                    if(!parentGroups.TryGetValue(parent,out group))
                    {
                        var children=parent.GetComponentsInChildren<Renderer>(true);bool compact=children.Length>1;Bounds whole=bounds;
                        foreach(var child in children){if(!measurements.TryGetValue(child,out var cb)){compact=false;break;}whole.Encapsulate(cb);}
                        group=compact&&whole.size.x<80&&whole.size.z<80?parent:null;parentGroups[parent]=group;
                    }
                }
                var box=Footprint(bounds);bool wash=anchor.name.StartsWith("Wash ");bool building=bounds.size.x>=3&&bounds.size.z>=2&&(bounds.size.y>=1||anchor.name.Contains("shelter")||anchor.name.Contains("canopy"));
                bool pole=anchor.name.EndsWith(" post")||anchor.name.Contains("lamp pole");bool tree=anchor.name=="Tree trunk";bool portal=anchor.name=="Portal ring"||anchor.name=="Fast travel portal";
                if(wash)box=new Rect(bounds.center.x-10,bounds.center.z-7,20,14);
                if(pole)box=new Rect(bounds.center.x-1,bounds.center.z-1,2,2);
                if(tree)box=new Rect(bounds.center.x-2,bounds.center.z-2,4,4);
                if(portal)box=new Rect(bounds.center.x-3.5f,bounds.center.z-3.5f,7,7);
                IEnumerable<Renderer> choices=group!=null?group.GetComponentsInChildren<Renderer>(true):(building||tree||portal||pole||wash?siblings[anchor.transform.parent]:(IEnumerable<Renderer>)new[]{anchor});
                foreach(var part in choices)
                {
                    if(!candidateSet.Contains(part)||claimed.Contains(part))continue;
                    bool take=part==anchor||group!=null&&part.transform.IsChildOf(group);
                    if(group==null&&(building||tree||portal||pole||wash)&&part.transform.parent==anchor.transform.parent)
                    {
                        var p=measurements[part].center;
                        float padding=building?2.5f:.7f;bool fits=p.x>=box.xMin-padding&&p.x<=box.xMax+padding&&p.z>=box.yMin-padding&&p.z<=box.yMax+padding;
                        if(wash)fits&=part.name.StartsWith("Wash ")||part.name.Contains("CAR WASH");
                        if(pole)fits&=part.name.ToLowerInvariant().Contains("lamp")||part.name.EndsWith(" post")||part.name.EndsWith(" bulb")||part.name.ToLowerInvariant().Contains("sign");
                        if(tree)fits&=part.name.Contains("Tree");if(portal)fits&=part.name==anchor.name||part.GetComponent<TMPro.TMP_Text>()!=null;
                        take|=fits;
                    }
                    if(!take)continue;
                    claimed.Add(part);unit.Parts.Add(part.transform);bounds.Encapsulate(measurements[part]);
                }
                // Move parented signs/plants as one root. Flat generated buildings retain all sibling parts.
                if(group!=null){unit.Parts.Clear();unit.Parts.Add(group);}
                if(group!=null&&group.GetComponent<WorldNpc>()!=null)
                {
                    string title=group.GetComponent<WorldNpc>().DisplayName+" [L] sign";
                    foreach(var label in candidates)if(label.name==title&&!claimed.Contains(label)){unit.Parts.Add(label.transform);claimed.Add(label);bounds.Encapsulate(measurements[label]);break;}
                }
                unit.Bounds=bounds;units.Add(unit);
            }
            int moved=0;
            for(int index=0;index<units.Count;index++)
            {
                var unit=units[index];var footprint=Footprint(unit.Bounds);
                if(!TouchesRoad(footprint,roads,Margin))continue;
                if(!FindSpace(footprint,roads,units,index,out var delta)){Debug.LogError("No clear roadside space for "+unit.Parts[0].name,unit.Parts[0]);continue;}
                foreach(var part in unit.Parts){if(part.name=="Portal ring")root.GetComponentInChildren<MultiverseController>(true)?.OffsetPortal(delta);if(part.name=="Fast travel portal")root.GetComponentInChildren<OwnerLifeController>(true)?.OffsetNearbyPortal(unit.Bounds.center,delta);break;}
                foreach(var part in unit.Parts)
                {
                    var npc=part.GetComponent<WorldNpc>();
                    if(npc!=null&&npc.Id=="supplier"){root.GetComponentInChildren<OwnerSandboxController>(true)?.OffsetSupplier(delta);var marker=root.Find("Owner Activities/Supplier loading marker");if(marker!=null)marker.position+=delta;}
                    if(part.name.StartsWith("Wash ")){root.GetComponentInChildren<WorldDrivingController>(true)?.OffsetWashLocation(delta);var apron=root.Find("Village City World v1/Wash apron");if(apron!=null)apron.position+=delta;var spray=root.Find("Village City World v1/Car Wash Spray");if(spray!=null)spray.position+=delta;break;}
                }
                foreach(var part in unit.Parts)part.position+=delta;
                unit.Bounds=new Bounds(unit.Bounds.center+delta,unit.Bounds.size);moved++;
            }
            var stamp=root.GetComponent<RoadClearance>();if(stamp==null)stamp=root.gameObject.AddComponent<RoadClearance>();stamp.layoutValidated=true;
            Physics.SyncTransforms();return moved;
        }
        /// <summary>Signboards acquire their real size at startup; clear their completed assemblies once.</summary>
        public static int ClearSigns(Transform root)
        {
            var roads=new List<Rect>();foreach(var r in root.GetComponentsInChildren<MeshRenderer>(true))if(r.gameObject.activeInHierarchy&&TownPopulationController.RoadName(r.name))roads.Add(Footprint(r.bounds));
            int moved=0;
            foreach(var sign in root.GetComponentsInChildren<PhysicalWorldSign>(true))
            {
                if(!sign.gameObject.activeInHierarchy)continue;
                var renderers=sign.GetComponentsInChildren<Renderer>(true);if(renderers.Length==0)continue;
                var bounds=renderers[0].bounds;foreach(var r in renderers)bounds.Encapsulate(r.bounds);
                var footprint=Footprint(bounds);if(!TouchesRoad(footprint,roads,Margin))continue;
                bool placed=false;
                for(int distance=1;distance<=100&&!placed;distance++)for(int direction=0;direction<16;direction++)
                {
                    float angle=direction*Mathf.PI/8;var offset=new Vector2(Mathf.Cos(angle)*distance,Mathf.Sin(angle)*distance);
                    if(TouchesRoad(new Rect(footprint.position+offset,footprint.size),roads,Margin))continue;
                    sign.transform.position+=new Vector3(offset.x,0,offset.y);moved++;placed=true;break;
                }
            }
            Physics.SyncTransforms();return moved;
        }
        public static int CountRoadOverlaps(Transform root)
        {
            var roads=new List<Rect>();foreach(var r in root.GetComponentsInChildren<MeshRenderer>(true))if(r.gameObject.activeInHierarchy&&TownPopulationController.RoadName(r.name))roads.Add(Footprint(r.bounds));
            int count=0;foreach(var r in root.GetComponentsInChildren<Renderer>(true))if(Movable(r)&&TouchesRoad(Footprint(Measure(r)),roads,-.01f))count++;return count;
        }
        private static bool TouchesRoad(Rect box,List<Rect> roads,float margin)
        {foreach(var road in roads)if(Intersects(box,road,margin))return true;return false;}
        private static bool FindSpace(Rect footprint,List<Rect> roads,List<Unit> units,int self,out Vector3 delta)
        {
            var nearby=new List<Rect>();var center=footprint.center;
            for(int j=0;j<units.Count;j++)if(j!=self){var box=Footprint(units[j].Bounds);if(Vector2.Distance(center,box.center)<250+box.width+box.height+footprint.width+footprint.height)nearby.Add(box);}
            // Search nearby plots once; no per-frame work or hierarchy queries.
            for(int radius=1;radius<=50;radius++)
            {
                float distance=radius*4;
                for(int direction=0;direction<16;direction++)
                {
                    float angle=direction*Mathf.PI/8;
                    var shift=new Vector2(Mathf.Cos(angle)*distance,Mathf.Sin(angle)*distance);
                    var candidate=new Rect(footprint.position+shift,footprint.size);
                    if(candidate.xMin< -985||candidate.xMax>985||candidate.yMin< -985||candidate.yMax>985||TouchesRoad(candidate,roads,Margin))continue;
                    bool occupied=false;
                    for(int j=0;j<nearby.Count;j++){if(Intersects(candidate,nearby[j],.25f)){occupied=true;break;}}
                    if(occupied)continue;
                    delta=new Vector3(shift.x,0,shift.y);return true;
                }
            }
            delta=Vector3.zero;return false;
        }
        private static void MoveOriginalMotel(Transform root,List<Rect> roads)
        {
            var geometry=root.Find("Navigation Geometry");
            if(geometry==null||geometry.position.z>4)return;
            var desk=geometry.Find("Reception desk");if(desk==null)return;
            var renderer=desk.GetComponent<Renderer>();if(renderer==null||!TouchesRoad(Footprint(renderer.bounds),roads,0))return;
            var delta=Vector3.forward*8;
            geometry.position+=delta;
            var surface=geometry.GetComponent<NavMeshSurface>();if(surface!=null){surface.RemoveData();surface.AddData();}
            var visuals=root.Find("Visual Polish v1");
            if(visuals!=null)foreach(Transform part in visuals)
            {
                var r=part.GetComponent<Renderer>();if(r==null)continue;
                if(Movable(r)&&part.position.x> -15&&part.position.x<15&&part.position.z> -10&&part.position.z<9)part.position+=delta;
            }
            foreach(Transform part in root)
            {
                if(part==geometry||part==visuals)continue;
                string n=part.name;
                if(n.StartsWith("Queue ")||n=="Arrival"||n=="Exit"||n.StartsWith("Parking arrival")||n.Contains("Shop Navigation")||n=="Shop Extension"||n=="Convenience Shop"||n=="RECEPTION"||n.StartsWith("PIT STOP")||n=="ROADSIDE HAVEN")part.position+=delta;
            }
            var deliveryMarker=root.Find("Owner Activities/Shop delivery marker");if(deliveryMarker!=null)deliveryMarker.position+=delta;
            var portal=root.Find("Multiverse Portals v1");if(portal!=null)portal.position+=delta;
            var multiverse=root.GetComponentInChildren<MultiverseController>(true);if(multiverse!=null)multiverse.OffsetPortal(delta);
            var life=root.GetComponentInChildren<OwnerLifeController>(true);if(life!=null)life.OffsetBaseLocations(delta);
            var owner=root.GetComponentInChildren<OwnerSandboxController>(true);if(owner!=null){owner.transform.position+=delta;owner.OffsetDelivery(delta);}
            foreach(var npc in root.GetComponentsInChildren<WorldNpc>(true)){var p=npc.transform.position;if(p.x> -14&&p.x<14&&p.z> -10&&p.z<1)npc.transform.position+=delta;}
            foreach(var r in root.GetComponentsInChildren<Renderer>(true))
            {
                var p=r.transform.position;if(r.transform.parent==null||r.GetComponentInParent<WorldNpc>()!=null)continue;
                if((r.transform.parent.name=="Life Systems v1"||r.transform.parent.name=="Business Empire v1"||r.transform.parent.name=="Garage mechanic")&&p.x> -14&&p.x<14&&p.z> -10&&p.z<1)r.transform.position+=delta;
            }
        }
    }
}