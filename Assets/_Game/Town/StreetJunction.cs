using System.Collections.Generic;
using UnityEngine;
namespace RoadsideHaven
{
    public enum StreetSignalPhase {NorthSouthGreen,NorthSouthAmber,AllRed,EastWestGreen,EastWestAmber,Walk}
    /// <summary>Timed protected junction: two vehicle stages, clearance intervals, then exclusive pedestrian crossing.</summary>
    public sealed class StreetJunction : MonoBehaviour
    {
        private static readonly List<StreetJunction> active=new List<StreetJunction>();
        [SerializeField] private float halfX=11,halfZ=11,offset;
        [SerializeField] private Renderer[] northSouthRed,northSouthAmber,northSouthGreen,eastWestRed,eastWestAmber,eastWestGreen,walkLamps;
        [SerializeField] private Material litRed,litAmber,litGreen,off;
        private StreetSignalPhase previous=(StreetSignalPhase)(-1);
        public float CycleSeconds=>43+WalkSeconds;
        public float WalkSeconds=>Mathf.Max(18,2*Mathf.Max(halfX,halfZ)/1.65f+4);
        public void Configure(float x,float z,float phaseOffset){halfX=x;halfZ=z;offset=phaseOffset;}
        public void SetLamps(Renderer[] nr,Renderer[] na,Renderer[] ng,Renderer[] er,Renderer[] ea,Renderer[] eg,Renderer[] walk,Material red,Material amber,Material green,Material dark)
        {northSouthRed=nr;northSouthAmber=na;northSouthGreen=ng;eastWestRed=er;eastWestAmber=ea;eastWestGreen=eg;walkLamps=walk;litRed=red;litAmber=amber;litGreen=green;off=dark;previous=(StreetSignalPhase)(-1);Update();}
        public StreetSignalPhase PhaseAt(float seconds)
        {
            float t=Mathf.Repeat(seconds+offset,CycleSeconds);
            if(t<14)return StreetSignalPhase.NorthSouthGreen;if(t<17)return StreetSignalPhase.NorthSouthAmber;if(t<19)return StreetSignalPhase.AllRed;
            if(t<33)return StreetSignalPhase.EastWestGreen;if(t<36)return StreetSignalPhase.EastWestAmber;if(t<38)return StreetSignalPhase.AllRed;
            if(t<38+WalkSeconds)return StreetSignalPhase.Walk;return StreetSignalPhase.AllRed;
        }
        private void OnEnable(){if(!active.Contains(this))active.Add(this);}
        private void OnDisable(){active.Remove(this);}
        private void Update()
        {
            var phase=PhaseAt(Time.time);if(phase==previous)return;previous=phase;
            Paint(northSouthRed,phase!=StreetSignalPhase.NorthSouthGreen&&phase!=StreetSignalPhase.NorthSouthAmber,litRed);Paint(northSouthAmber,phase==StreetSignalPhase.NorthSouthAmber,litAmber);Paint(northSouthGreen,phase==StreetSignalPhase.NorthSouthGreen,litGreen);
            Paint(eastWestRed,phase!=StreetSignalPhase.EastWestGreen&&phase!=StreetSignalPhase.EastWestAmber,litRed);Paint(eastWestAmber,phase==StreetSignalPhase.EastWestAmber,litAmber);Paint(eastWestGreen,phase==StreetSignalPhase.EastWestGreen,litGreen);Paint(walkLamps,phase==StreetSignalPhase.Walk,litGreen);
        }
        private void Paint(Renderer[] lamps,bool on,Material material){if(lamps==null)return;foreach(var lamp in lamps)if(lamp!=null)lamp.sharedMaterial=on?material:off;}
        public float LimitVehicle(Vector3 position,Vector3 direction,float front,float speed,float now)
        {
            var local=position-transform.position;bool horizontal=Mathf.Abs(direction.x)>Mathf.Abs(direction.z);
            float across=horizontal?Mathf.Abs(local.z):Mathf.Abs(local.x);if(across>(horizontal?halfZ:halfX)+1)return float.PositiveInfinity;
            float sign=Mathf.Sign(horizontal?direction.x:direction.z);float distance=-(horizontal?local.x:local.z)*sign;
            float remaining=distance-(horizontal?halfX:halfZ)-6-front;
            // Once committed beyond the stop line, clear the junction rather than stopping inside it.
            if(remaining<-.6f||remaining>80)return float.PositiveInfinity;
            var phase=PhaseAt(now);bool green=horizontal?phase==StreetSignalPhase.EastWestGreen:phase==StreetSignalPhase.NorthSouthGreen;
            bool amber=horizontal?phase==StreetSignalPhase.EastWestAmber:phase==StreetSignalPhase.NorthSouthAmber;
            if(green||amber&&remaining<speed*speed/24+1)return float.PositiveInfinity;
            return Mathf.Sqrt(2*8*Mathf.Max(0,remaining-.25f));
        }
        public bool WaitForWalk(Vector3 from,Vector3 to,float speed,float now)
        {
            var a=from-transform.position;var b=to-transform.position;var d=b-a;bool crossingZ=Mathf.Abs(d.z)>Mathf.Abs(d.x);
            float across=crossingZ?Mathf.Abs(a.x):Mathf.Abs(a.z);float edge=crossingZ?halfX:halfZ;
            if(Mathf.Abs(across-(edge+1.5f))>1.2f)return false;
            float along=crossingZ?a.z:a.x,target=crossingZ?b.z:b.x,width=crossingZ?halfZ:halfX;
            if(Mathf.Abs(along)<width+.25f||along*target>=0||Mathf.Abs(target)<width)return false;
            if(Mathf.Abs(along)>width+2.3f)return false;
            float time=Mathf.Repeat(now+offset,CycleSeconds);float needed=(2*width+1)/Mathf.Max(1,speed)+1;
            return PhaseAt(now)!=StreetSignalPhase.Walk||38+WalkSeconds-time<needed;
        }
        public static float VehicleLimit(Vector3 position,Vector3 direction,float front,float speed)
        {float limit=float.PositiveInfinity;foreach(var junction in active)if(junction!=null)limit=Mathf.Min(limit,junction.LimitVehicle(position,direction,front,speed,Time.time));return limit;}
        public static bool PedestrianMustWait(Vector3 from,Vector3 to,float speed)
        {foreach(var junction in active)if(junction!=null&&junction.WaitForWalk(from,to,speed,Time.time))return true;return false;}
    }
}
