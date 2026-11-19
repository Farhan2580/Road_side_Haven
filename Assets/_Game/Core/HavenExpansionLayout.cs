using UnityEngine;

namespace RoadsideHaven
{
    /// <summary>Reserved parcels for the three new districts on previously empty land, clear of every public road.</summary>
    public static class HavenExpansionLayout
    {
        public const string Lakeside = "Lakeside Park v1", Sports = "Haven Sports Park v1", Farm = "Haven Valley Farm v1";
        public static readonly Rect LakesideArea = Rect.MinMaxRect(-320, 140, 160, 540);
        public static readonly Rect SportsArea = Rect.MinMaxRect(20, -400, 380, -78);
        public static readonly Rect FarmArea = Rect.MinMaxRect(-336, -420, -40, -226);

        // Lakeside: organic lake, a loop promenade and a pier from the east shore.
        public static readonly Vector3 LakeCentre = new Vector3(-90, 0, 340);
        public const float LakeRadiusX = 85, LakeRadiusZ = 60;
        public static Vector2 LakeOutline(float angle, float scale = 1)
        {
            float r = 1 + .08f * Mathf.Sin(angle * 3 + .4f) + .05f * Mathf.Cos(angle * 5) + .03f * Mathf.Sin(angle * 7);
            return new Vector2(LakeCentre.x + Mathf.Cos(angle) * LakeRadiusX * r * scale, LakeCentre.z + Mathf.Sin(angle) * LakeRadiusZ * r * scale);
        }
        public static bool InLake(Vector2 p, float scale = 1)
        {
            float angle = Mathf.Atan2((p.y - LakeCentre.z) / LakeRadiusZ, (p.x - LakeCentre.x) / LakeRadiusX);
            var edge = LakeOutline(angle, scale);
            return (p - new Vector2(LakeCentre.x, LakeCentre.z)).sqrMagnitude <= (edge - new Vector2(LakeCentre.x, LakeCentre.z)).sqrMagnitude;
        }
        public static Vector3[] LakeLoop()
        {
            var route = new Vector3[25];
            for (int i = 0; i < 24; i++) { var p = LakeOutline(i * Mathf.PI * 2 / 24, 1.16f); route[i] = new Vector3(p.x, .12f, p.y); }
            route[24] = route[0];
            return route;
        }
        public static readonly Rect LakeParking = Rect.MinMaxRect(112, 300, 160, 352);

        // Sports: stadium pitch with a running track, courts and a clubhouse.
        public static readonly Vector3 PitchCentre = new Vector3(230, 0, -250);
        public const float PitchHalfX = 34, PitchHalfZ = 52.5f, TrackInner = 37, TrackOuter = 45;
        public static readonly Rect SportsParking = Rect.MinMaxRect(40, -150, 150, -100);
        public static Vector3[] TrackLoop()
        {
            var route = new Vector3[33]; float r = TrackInner + 2, half = PitchHalfZ;
            for (int i = 0; i < 16; i++) { float a = Mathf.PI * i / 15; route[i] = new Vector3(PitchCentre.x + Mathf.Cos(a) * r, .12f, PitchCentre.z + half + Mathf.Sin(a) * r); }
            for (int i = 0; i < 16; i++) { float a = Mathf.PI + Mathf.PI * i / 15; route[16 + i] = new Vector3(PitchCentre.x + Mathf.Cos(a) * r, .12f, PitchCentre.z - half + Mathf.Sin(a) * r); }
            route[32] = route[0];
            return route;
        }

        // Farm: lane from the western road, buildings, fields, orchard and a cow paddock.
        public static readonly Rect FarmLane = Rect.MinMaxRect(-336, -304, -236, -296);
        public static readonly Rect Paddock = Rect.MinMaxRect(-112, -410, -52, -350);
        public static readonly Vector3 WindmillPosition = new Vector3(-290, 0, -262);
    }
}
