using UnityEngine;
namespace RoadsideHaven
{
    /// <summary>Deterministic local steering and comfortable stopping, separate from road/signalling rules.</summary>
    public static class TownMotion
    {
        public static Vector3 PersonalSpace(Vector3 position,Vector3 desired,Vector3 other,Vector3 otherVelocity,int identity)
        {
            Vector3 away=position-other;away.y=0;float distance=away.magnitude;if(distance>2.1f)return Vector3.zero;
            if(distance<.015f)away=new Vector3((identity&1)==0?1:-1,0,.3f);else away/=distance;
            Vector3 separation=away*Mathf.Clamp01((1.05f-distance)/.85f);
            Vector3 relative=desired-otherVelocity;relative.y=0;
            float t=relative.sqrMagnitude>.01f?Mathf.Clamp(-Vector3.Dot(position-other,relative)/relative.sqrMagnitude,0,.9f):0;
            Vector3 closest=position-other+relative*t;closest.y=0;
            // Both approaching walkers keep to their own right; no random left/right oscillation.
            if(t>.05f&&closest.magnitude<.75f)separation+=Vector3.Cross(Vector3.up,desired.normalized)*(.75f-closest.magnitude)*.8f;
            return Vector3.ClampMagnitude(separation,1.2f);
        }
        public static float FollowingSpeed(float gap,float leadSpeed,float cruise,float braking=6)
        {
            if(gap<=.8f)return 0;
            float stopping=Mathf.Sqrt(Mathf.Max(0,leadSpeed*leadSpeed+2*braking*(gap-.8f)));
            float comfort=Mathf.Max(0,leadSpeed)+(gap-.8f)/1.3f;
            return Mathf.Clamp(Mathf.Min(stopping,comfort),0,cruise);
        }
        public static int PeopleBudget(bool phone)=>phone?128:192;
        public static int CarBudget(bool phone)=>phone?48:64;
    }
}
