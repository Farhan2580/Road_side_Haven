using UnityEngine;
namespace MotelGame.World
{
    /// <summary>Meter-scale river survey, translated from the supplied 0..2000 plan into the project's centered map.</summary>
    public static class RiverTerrainProfile
    {
        public const float MapSize = 2000f;
        public const float WaterHeight = 0f;
        public const float GroundHeight = 1f;
        public const float BankWidth = 15f;
        public const float MaximumDepth = 4f;
        public const string BakeVersion = "River terrain step 1 v1";
        private static readonly Vector2[] Points = {
            new Vector2(0,1000),new Vector2(-40,700),new Vector2(40,400),new Vector2(-10,100),
            new Vector2(20,-200),new Vector2(-30,-500),new Vector2(10,-800),new Vector2(0,-1000)
        };
        /// <summary>Continuous cubic interpolation passes through every supplied control point. Z decreases downstream.</summary>
        public static float CenterX(float z)
        {
            z=Mathf.Clamp(z,-1000,1000);
            int i=0;while(i<Points.Length-2 && z<Points[i+1].y)i++;
            Vector2 a=Points[i],b=Points[i+1];
            float t=(z-a.y)/(b.y-a.y);
            float slopeA=i==0?0:(b.x-Points[i-1].x)/(b.y-Points[i-1].y);
            float slopeB=i+2>=Points.Length?0:(Points[i+2].x-a.x)/(Points[i+2].y-a.y);
            float delta=b.y-a.y;
            return (2*t*t*t-3*t*t+1)*a.x+(t*t*t-2*t*t+t)*slopeA*delta+(-2*t*t*t+3*t*t)*b.x+(t*t*t-t*t)*slopeB*delta;
        }
        /// <summary>Independent irregular banks retain a total width within 70..95m, including the central inlet.</summary>
        public static void Banks(float z,out float left,out float right)
        {
            float half=39.5f+4.5f*Mathf.Sin(z*.0075f);
            float noiseL=(Mathf.PerlinNoise(11.7f,z*.08f+100)-Mathf.PerlinNoise(11.7f,100))*6;
            float noiseR=(Mathf.PerlinNoise(53.1f,z*.08f+100)-Mathf.PerlinNoise(53.1f,100))*6;
            float inlet=8*Mathf.Exp(-z*z/(70*70));
            left=CenterX(z)-Mathf.Clamp(half+noiseL+inlet,35,47.5f);
            right=CenterX(z)+Mathf.Clamp(half+noiseR,35,47.5f);
        }
        /// <summary>One continuous bed and bank surface; no terrain holes or overlapping shore strips.</summary>
        public static float Height(float x,float z)
        {
            Banks(z,out float left,out float right);
            if(x>=left&&x<=right)
            {
                float across=(x-left)/(right-left);
                float depth=Mathf.Pow(Mathf.Max(0,Mathf.Sin(Mathf.PI*across)),.75f);
                return WaterHeight-MaximumDepth*depth;
            }
            float distance=x<left?left-x:x-right;
            if(distance>=BankWidth)return GroundHeight;
            return Mathf.Lerp(WaterHeight,GroundHeight,Mathf.SmoothStep(0,1,distance/BankWidth));
        }
        /// <summary>Distance outwards from the nearest bank, negative inside the river.</summary>
        public static float ShoreDistance(float x,float z)
        {Banks(z,out float left,out float right);return x<left?left-x:x>right?x-right:-Mathf.Min(x-left,right-x);}
    }
}