using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
namespace RoadsideHaven.Editor
{
    public static partial class VisualPolishInstaller
    {
        private static Rect[] streetClearanceRoads;

        private static void InstallCompleteStreets(Transform motel)
        {
            RoadMarkingCleanup.RemoveStopLines(motel);
            RoadMarkingCleanup.FlattenCrossings(motel);
            // Old road overlays had different top heights; their small vertical seams stopped fast cars.
            foreach(var surface in motel.GetComponentsInChildren<MeshRenderer>(true))
            {
                if(!surface.gameObject.activeInHierarchy||!TownPopulationController.RoadName(surface.name))continue;
                float correction=.06f-surface.bounds.max.y;if(Mathf.Abs(correction)<.0001f)continue;
                if(!Application.isPlaying)Undo.RecordObject(surface.transform,"Level road junction surfaces");
                surface.transform.position+=Vector3.up*correction;
            }
            var roadPhysics=RoadSurfacePhysics.Rebuild(motel);
            if(!Application.isPlaying&&roadPhysics.SurfaceMesh!=null)
            {
                string path=MaterialFolder+"/RoadDrivingSurface.asset";var saved=AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if(saved==null){AssetDatabase.CreateAsset(roadPhysics.SurfaceMesh,path);saved=roadPhysics.SurfaceMesh;}
                else EditorUtility.CopySerialized(roadPhysics.SurfaceMesh,saved);
                roadPhysics.UseSavedMesh(saved);EditorUtility.SetDirty(roadPhysics);EditorUtility.SetDirty(saved);
            }
            foreach(var version in new[]{"Complete Streets v1","Complete Streets v2","Complete Streets v3","Complete Streets v4","Complete Streets v5"})
            {var previous=motel.Find(version);if(previous!=null){previous.gameObject.SetActive(false);if(Application.isPlaying)Object.Destroy(previous.gameObject);else Undo.DestroyObjectImmediate(previous.gameObject);}}
            if(motel.Find("Complete Streets v6")!=null)return;
            var network=new TownRoadNetwork();
            foreach(var r in motel.GetComponentsInChildren<MeshRenderer>(true))if(r.gameObject.activeInHierarchy&&TownPopulationController.RoadName(r.name))network.Add(r.bounds.center,r.bounds.size);
            network.Build(null);var roads=network.Roads;
            streetClearanceRoads=roads.Select(r=>Rect.MinMaxRect(r.A.x-r.HalfWidth*(Mathf.Abs(r.B.z-r.A.z)>1?1:0),r.A.z-r.HalfWidth*(Mathf.Abs(r.B.x-r.A.x)>1?1:0),r.B.x+r.HalfWidth*(Mathf.Abs(r.B.z-r.A.z)>1?1:0),r.B.z+r.HalfWidth*(Mathf.Abs(r.B.x-r.A.x)>1?1:0))).ToArray();var crosses=TownJunctionLayout.Find(roads);
            var blockers=motel.GetComponentsInChildren<MeshRenderer>(true).Where(r=>r.gameObject.activeInHierarchy&&r.bounds.size.y>1.5f&&r.bounds.min.y<4&&!r.name.Contains("window")).Select(r=>r.bounds).ToArray();
            var root=new GameObject("Complete Streets v6").transform;root.SetParent(motel,false);
            if(!Application.isPlaying)Undo.RegisterCreatedObjectUndo(root.gameObject,"Add signals, sidewalks and avenue trees");
            foreach(var legacy in motel.GetComponentsInChildren<MeshRenderer>(true))
            {
                if(!legacy.transform.parent.gameObject.activeInHierarchy||(legacy.name!="Sidewalk"&&legacy.name!="City block sidewalk"))continue;
                var bounds=legacy.bounds;legacy.gameObject.SetActive(false);
                foreach(var piece in StreetPavingLayout.ClearRoads(Rect.MinMaxRect(bounds.min.x,bounds.min.z,bounds.max.x,bounds.max.z),streetClearanceRoads))
                    Solid(root,"Repaired block paving",new Vector3(piece.center.x,bounds.center.y,piece.center.y),new Vector3(piece.width,bounds.size.y,piece.height),legacy.sharedMaterial);
            }
            var paving=Material("Sidewalk paving",new Color(.55f,.57f,.55f));var curb=Material("Sidewalk curb",new Color(.78f,.78f,.71f));
            var bark=Material("Avenue bark",new Color(.30f,.20f,.12f));var oak=Material("Avenue oak",new Color(.22f,.42f,.13f));var pine=Material("Avenue pine",new Color(.08f,.25f,.17f));var palm=Material("Avenue palm",new Color(.28f,.48f,.18f));
            var redLamp=Material("Traffic red",new Color(1,.025f,.015f),true);var amberLamp=Material("Traffic amber",new Color(1,.55f,.015f),true);var greenLamp=Material("Traffic green",new Color(.03f,1,.28f),true);
            foreach(var material in new[]{paving,curb,bark,oak,pine,palm,redLamp,amberLamp,greenLamp})material.enableInstancing=true;
            foreach(var road in roads)
            {
                var axis=(road.B-road.A).normalized;float length=Vector3.Distance(road.A,road.B);if(length<40||Mathf.Abs(road.HalfWidth-4)>.05f)continue;
                bool horizontal=axis.x>.5f;var cuts=new List<Vector2>();
                foreach(var cross in crosses)
                {var projection=TownRoadNetwork.Project(cross.Center,road);if((projection-cross.Center).sqrMagnitude>.1f)continue;float t=Vector3.Dot(projection-road.A,axis),gap=horizontal?cross.X:cross.Z;cuts.Add(new Vector2(Mathf.Max(0,t-gap),Mathf.Min(length,t+gap)));}
                cuts.Sort((a,b)=>a.x.CompareTo(b.x));float end=0;
                foreach(var cut in cuts){if(cut.x>end)Pavement(root,road,axis,end,cut.x,paving,curb);end=Mathf.Max(end,cut.y);}if(end<length)Pavement(root,road,axis,end,length,paving,curb);
                if(length<150)continue;
                int index=0;
                for(float t=24;t<length-12;t+=62)for(int side=-1;side<=1;side+=2)
                {
                    var p=road.A+axis*t+Vector3.Cross(Vector3.up,axis)*side*(road.HalfWidth+5.6f);p.y=0;
                    if(crosses.Any(c=>Mathf.Abs(p.x-c.Center.x)<c.X+9&&Mathf.Abs(p.z-c.Center.z)<c.Z+9))continue;
                    var volume=new Bounds(p+Vector3.up*2.5f,new Vector3(4.5f,5,4.5f));if(blockers.Any(b=>b.Intersects(volume)))continue;
                    AvenueTree(root,p,index++%3,bark,oak,pine,palm);
                }
            }
            var controlled=TownJunctionLayout.SelectControlled(roads);
            var crossingWhite=Material("Crosswalk white",new Color(.88f,.90f,.88f));
            int id=0;
            foreach(var cross in controlled)
            {
                var junction=new GameObject("Signal junction "+id).transform;junction.SetParent(root,false);junction.position=cross.Center;
                var signal=junction.gameObject.AddComponent<StreetJunction>();signal.Configure(cross.X,cross.Z,(id++*11)%45);
                var nr=new List<Renderer>();var na=new List<Renderer>();var ng=new List<Renderer>();var er=new List<Renderer>();var ea=new List<Renderer>();var eg=new List<Renderer>();var walk=new List<Renderer>();
                if(cross.South)SignalHead(junction,new Vector3(cross.X+2.8f,0,-cross.Z-5),0,nr,na,ng,walk);
                if(cross.North)SignalHead(junction,new Vector3(-cross.X-2.8f,0,cross.Z+5),180,nr,na,ng,walk);
                if(cross.West)SignalHead(junction,new Vector3(-cross.X-5,0,-cross.Z-2.8f),90,er,ea,eg,walk);
                if(cross.East)SignalHead(junction,new Vector3(cross.X+5,0,cross.Z+2.8f),270,er,ea,eg,walk);
                if((cross.Center-new Vector3(0,0,-13)).sqrMagnitude<1)
                {
                    float laneX=Mathf.Min(3,cross.X*.45f),laneZ=Mathf.Min(3,cross.Z*.45f);
                    if(cross.South)LaneArrow(root,cross.Center+new Vector3(laneX,.09f,-cross.Z-14),Vector3.forward,crossingWhite);
                    if(cross.North)LaneArrow(root,cross.Center+new Vector3(-laneX,.09f,cross.Z+14),Vector3.back,crossingWhite);
                    if(cross.West)LaneArrow(root,cross.Center+new Vector3(-cross.X-14,.09f,-laneZ),Vector3.right,crossingWhite);
                    if(cross.East)LaneArrow(root,cross.Center+new Vector3(cross.X+14,.09f,laneZ),Vector3.left,crossingWhite);
                }
                signal.SetLamps(nr.ToArray(),na.ToArray(),ng.ToArray(),er.ToArray(),ea.ToArray(),eg.ToArray(),walk.ToArray(),redLamp,amberLamp,greenLamp,ink);
                for(int side=-1;side<=1;side+=2)
                {
                    if(side<0?cross.South:cross.North)for(float x=-cross.X+.5f;x<cross.X;x+=1.1f)RoadMarkingCleanup.CrossingStripe(root,cross.Center+new Vector3(x,.087f,side*(cross.Z+1.5f)),new Vector3(.55f,.015f,2.8f),crossingWhite);
                    if(side<0?cross.West:cross.East)for(float z=-cross.Z+.5f;z<cross.Z;z+=1.1f)RoadMarkingCleanup.CrossingStripe(root,cross.Center+new Vector3(side*(cross.X+1.5f),.087f,z),new Vector3(2.8f,.015f,.55f),crossingWhite);
                }
            }
        }
        private static void LaneArrow(Transform root,Vector3 point,Vector3 direction,Material white)
        {
            var arrow=new GameObject("Motel approach lane arrow").transform;arrow.SetParent(root,false);arrow.position=point;
            ArrowPart(arrow,point+new Vector3(0,0,-.35f),new Vector3(.18f,.012f,2),0,white);
            ArrowPart(arrow,point+new Vector3(-.38f,0,.55f),new Vector3(.17f,.012f,1.2f),45,white);
            ArrowPart(arrow,point+new Vector3(.38f,0,.55f),new Vector3(.17f,.012f,1.2f),-45,white);
            arrow.rotation=Quaternion.LookRotation(direction);
        }
        private static void ArrowPart(Transform root,Vector3 point,Vector3 size,float angle,Material material)
        {
            var part=Solid(root,"Lane arrow marking",point,size,material);part.transform.rotation=Quaternion.Euler(0,angle,0);
            var collider=part.GetComponent<Collider>();collider.enabled=false;if(Application.isPlaying)Object.Destroy(collider);else Object.DestroyImmediate(collider);
        }
        private static void Pavement(Transform root,TownRoadNetwork.Road road,Vector3 axis,float a,float b,Material paving,Material curb)
        {
            if(b-a<.3f)return;bool h=axis.x>.5f;var right=Vector3.Cross(Vector3.up,axis);var center=road.A+axis*((a+b)*.5f);center.y=.04f;
            for(int side=-1;side<=1;side+=2)
            {
                ClearPaving(root,"Public sidewalk",center+right*side*(road.HalfWidth+1.5f),h?new Vector3(b-a,.16f,3):new Vector3(3,.16f,b-a),paving);
                ClearPaving(root,"Low sidewalk curb",center+right*side*(road.HalfWidth+.10f)+Vector3.up*.025f,h?new Vector3(b-a,.21f,.18f):new Vector3(.18f,.21f,b-a),curb);
            }
        }
        private static void ClearPaving(Transform root,string name,Vector3 center,Vector3 size,Material material)
        {
            var rect=new Rect(center.x-size.x*.5f,center.z-size.z*.5f,size.x,size.z);
            foreach(var p in StreetPavingLayout.ClearRoads(rect,streetClearanceRoads))Solid(root,name,new Vector3(p.center.x,center.y,p.center.y),new Vector3(p.width,size.y,p.height),material);
        }
        private static void SignalHead(Transform root,Vector3 local,float yaw,List<Renderer> red,List<Renderer> amber,List<Renderer> green,List<Renderer> walk)
        {
            var pole=new GameObject("Traffic signal").transform;pole.SetParent(root,false);pole.localPosition=local;
            Solid(pole,"Traffic signal pole",pole.position+Vector3.up*1.8f,new Vector3(.13f,3.6f,.13f),ink);
            var housing=Solid(pole,"Traffic signal housing",pole.position+Vector3.up*3.35f,new Vector3(.45f,1.3f,.28f),ink).transform;
            housing.rotation=Quaternion.Euler(0,yaw,0);
            for(int i=0;i<3;i++)
            {
                var lamp=GameObject.CreatePrimitive(PrimitiveType.Sphere);lamp.name="Signal bulb";lamp.transform.SetParent(housing,false);lamp.transform.localPosition=new Vector3(0,.30f-i*.30f,-.6f);lamp.transform.localScale=new Vector3(.65f,.20f,.35f);
                var col=lamp.GetComponent<Collider>();col.enabled=false;if(Application.isPlaying)Object.Destroy(col);else Object.DestroyImmediate(col);
                (i==0?red:i==1?amber:green).Add(lamp.GetComponent<Renderer>());
            }
            var indicator=GameObject.CreatePrimitive(PrimitiveType.Cube);indicator.name="Walk indicator bulb";indicator.transform.SetParent(pole,false);indicator.transform.localPosition=new Vector3(.3f,2,0);indicator.transform.localScale=new Vector3(.25f,.32f,.12f);indicator.GetComponent<Collider>().enabled=false;walk.Add(indicator.GetComponent<Renderer>());
        }
        private static void AvenueTree(Transform root,Vector3 p,int variant,Material bark,Material oak,Material pine,Material palm)
        {
            var tree=new GameObject(variant==0?"Avenue oak":variant==1?"Avenue pine":"Avenue palm").transform;tree.SetParent(root,false);tree.position=p;
            Shape(tree,"Tree trunk",PrimitiveType.Cylinder,p+Vector3.up*2,new Vector3(.34f,2,.34f),bark);
            if(variant==0){Shape(tree,"Tree crown oak",PrimitiveType.Sphere,p+Vector3.up*4.3f,new Vector3(3.8f,3.2f,3.8f),oak);Shape(tree,"Tree crown oak upper",PrimitiveType.Sphere,p+new Vector3(.6f,5.3f,0),new Vector3(2.6f,2.4f,2.6f),oak);}
            else if(variant==1)for(int i=0;i<3;i++)Shape(tree,"Tree crown pine",PrimitiveType.Sphere,p+Vector3.up*(3.4f+i),new Vector3(2.8f-i*.65f,2.4f,2.8f-i*.65f),pine);
            else for(int i=0;i<5;i++)
            {float angle=i*Mathf.PI*2/5;Shape(tree,"Tree crown palm",PrimitiveType.Sphere,p+new Vector3(Mathf.Cos(angle)*1.1f,4.8f,Mathf.Sin(angle)*1.1f),new Vector3(2.4f,.42f,1.2f),palm);}
            var renderers=tree.GetComponentsInChildren<Renderer>();var lod=tree.gameObject.AddComponent<LODGroup>();lod.SetLODs(new[]{new LOD(.015f,renderers)});lod.RecalculateBounds();
        }
    }
}
