using TMPro;
using UnityEditor;
using UnityEngine;

namespace RoadsideHaven.Editor
{
    public static partial class VisualPolishInstaller
    {
        private static void InstallRegionalWorld(Transform motel, IsometricCameraController camera)
        {
            if (camera != null) camera.Configure(Vector3.zero, new Vector2(970, 970), new Vector2(6, 1100));
            var village = motel.Find("Village City World v1");
            if (village != null)
                foreach (string wall in new[] { "West boundary", "East boundary", "North boundary", "South boundary" })
                {
                    var boundary = village.Find(wall);
                    if (boundary != null) boundary.gameObject.SetActive(false);
                }
            if (motel.Find("Regional World 2km v1") != null) return;
            var region = new GameObject("Regional World 2km v1").transform;
            region.SetParent(motel, false);
            if (!Application.isPlaying) Undo.RegisterCreatedObjectUndo(region.gameObject, "Create 2km regional world");
            Solid(region, "Regional foundation 2000m x 2000m", new Vector3(0, -.6f, 0), new Vector3(2000, 1, 2000), leaf);
            var sand = Material("Coastal sand", new Color(.80f, .71f, .48f));
            var ocean = Material("Ocean blue", new Color(.08f, .35f, .52f));
            var rock = Material("Mountain stone", new Color(.35f, .38f, .34f));
            var concrete = Material("Industrial concrete", new Color(.46f, .48f, .47f));
            // All five zones share the central road junction; north = +Z, west = -X.
            Road(region, new Vector3(0, 0, -13), new Vector3(1960, .12f, 14));
            Road(region, new Vector3(180, 0, 350), new Vector3(14, .12f, 720));
            Road(region, new Vector3(-350, 0, -13), new Vector3(14, .12f, 1420));
            Road(region, new Vector3(420, 0, -13), new Vector3(14, .12f, 1420));
            Road(region, new Vector3(0, 0, 650), new Vector3(1960, .12f, 18));
            Road(region, new Vector3(0, 0, -620), new Vector3(1960, .12f, 12));
            for (int x = -950; x <= 950; x += 50)
                foreach (float z in new[] { -13f, 650f, -620f })
                    if (Mathf.Abs(x) > 20 && Mathf.Abs(x + 350) > 20 && Mathf.Abs(x - 420) > 20)
                        Box(region, "Highway lane dash", new Vector3(x, .075f, z), new Vector3(14, .02f, .2f), ivory);
            for (int side = -1; side <= 1; side += 2)
            {
                Solid(region, "Regional side boundary", new Vector3(side * 999, 1, 0), new Vector3(2, 3, 2000), rock);
                Solid(region, "Regional end boundary", new Vector3(0, 1, side * 999), new Vector3(2000, 3, 2), rock);
            }
            // West city blocks leave the north/south avenue and horizontal streets clear.
            var city = Zone(region, "City Zone - West", new Vector3(-620, 0, 220));
            foreach (float z in new[] { 180f, 380f })
                Road(city, new Vector3(-620, 0, z), new Vector3(550, .12f, 12));
            for (int block = 0; block < 20; block++)
            {
                float x = -880 + block % 5 * 100;
                float z = 70 + block / 5 * 150;
                float height = 12 + block % 5 * 8;
                Campus(city, new Vector3(x, 0, z), block == 0 ? "CITY SCHOOL" : block == 5 ? "CITY COLLEGE" : "CITY BLOCK", 40, height, block % 2 == 0 ? ivory : concrete);
            }
            RegionSign(city, "CITY / WEST", new Vector3(-620, 8, -55));
            // Northern mountain ridge stays behind the highway; peaks are stylized collision masses.
            var north = Zone(region, "Mountain Highway Zone - North", new Vector3(0, 0, 800));
            for (int peak = 0; peak < 12; peak++)
            {
                float x = -900 + peak * 160;
                float height = 100 + peak % 4 * 35;
                var mountain = Solid(north, "Mountain ridge", new Vector3(x, height * .35f, 870), new Vector3(160, height, 170), rock);
                mountain.transform.rotation = Quaternion.Euler(0, 20, 35);
            }
            Campus(north, new Vector3(180, 0, 720), "HIGHWAY MOTEL", 65, 8, ivory);
            Solid(north, "Truck rest apron", new Vector3(-170, -.025f, 720), new Vector3(180, .15f, 90), asphalt);
            Road(north, new Vector3(-170, 0, 690), new Vector3(14, .12f, 80));
            Road(north, new Vector3(180, 0, 680), new Vector3(14, .12f, 60));
            for (int truck = 0; truck < 4; truck++)
            {
                float x = -230 + truck * 38;
                Solid(north, "Parked truck trailer", new Vector3(x, 2.8f, 725), new Vector3(8, 5.5f, 22), ivory);
                Solid(north, "Parked truck cab", new Vector3(x, 2, 709), new Vector3(8, 4, 8), red);
            }
            RegionSign(north, "MOUNTAIN HIGHWAY / NORTH", new Vector3(0, 9, 600));
            // South coast: physical sand remains driveable; water is visual, behind a shoreline barrier.
            var beach = Zone(region, "Beach Zone - South", new Vector3(-300, 0, -780));
            Solid(beach, "Beach sand", new Vector3(-280, -.03f, -760), new Vector3(1400, .12f, 220), sand);
            Box(beach, "Coastal water", new Vector3(-280, .03f, -940), new Vector3(1400, .08f, 120), ocean);
            Solid(beach, "Shoreline safety edge", new Vector3(-280, .7f, -872), new Vector3(1400, 1.5f, 1), timber);
            Road(beach, new Vector3(-350, 0, -710), new Vector3(12, .12f, 180));
            for (int hut = 0; hut < 8; hut++)
            {
                Campus(beach, new Vector3(-850 + hut * 140, 0, -710), "BEACH HUT", 12, 3, timber);
                Tree(beach, new Vector3(-850 + hut * 140, 0, -790));
            }
            RegionSign(beach, "BEACH / SOUTH", new Vector3(-350, 8, -660));
            // Eastern industrial estate: warehouses, loading yards and chimneys.
            var industrial = Zone(region, "Industrial Zone - East", new Vector3(700, 0, -300));
            Road(industrial, new Vector3(700, 0, -230), new Vector3(560, .12f, 14));
            Road(industrial, new Vector3(700, 0, 160), new Vector3(560, .12f, 14));
            for (int factory = 0; factory < 8; factory++)
            {
                float x = 520 + factory % 4 * 120;
                float z = factory < 4 ? -130 : 260;
                Solid(industrial, "Warehouse", new Vector3(x, 10, z), new Vector3(65, 20, 65), concrete);
                Box(industrial, "Warehouse roof", new Vector3(x, 20.3f, z), new Vector3(68, .6f, 68), ink);
                Solid(industrial, "Factory chimney", new Vector3(x + 40, 18, z + 20), new Vector3(8, 36, 8), timber);
                Solid(industrial, "Loading yard", new Vector3(x, -.025f, z - 55), new Vector3(80, .15f, 35), asphalt);
            }
            RegionSign(industrial, "INDUSTRIAL / EAST", new Vector3(560, 9, -55));
            Zone(region, "Motel District - Center", Vector3.zero);
            RegionSign(region, "MOTEL DISTRICT / MAIN BASE", new Vector3(0, 8, 130));
            AssetDatabase.SaveAssets();
        }

        private static Transform Zone(Transform parent, string name, Vector3 center)
        {
            var zone = new GameObject(name).transform;
            zone.SetParent(parent, false);
            // Marker positions identify zone centers; generated geometry uses world coordinates.
            zone.position = center;
            return zone;
        }

        private static void RegionSign(Transform parent, string text, Vector3 position)
            => LabelWorld(parent, text, position, 16);
    }
}
