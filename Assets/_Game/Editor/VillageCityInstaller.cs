using TMPro;
using UnityEditor;
using UnityEngine;

namespace RoadsideHaven.Editor
{
    public static partial class VisualPolishInstaller
    {
        private static void InstallVillageCity(Transform motel, IsometricCameraController camera)
        {
            InstallRoadExtensions(motel);
            if (motel.Find("Village City World v1") != null) return;
            var world = new GameObject("Village City World v1").transform;
            world.SetParent(motel, false);
            if (!Application.isPlaying) Undo.RegisterCreatedObjectUndo(world.gameObject, "Add village and city world");
            Solid(world, "World foundation", new Vector3(0, -.32f, 10), new Vector3(240, .5f, 180), leaf);
            Solid(world, "West boundary", new Vector3(-119, 1, 10), new Vector3(.5f, 2, 178), timber);
            Solid(world, "East boundary", new Vector3(119, 1, 10), new Vector3(.5f, 2, 178), timber);
            Solid(world, "North boundary", new Vector3(0, 1, 99), new Vector3(238, 2, .5f), timber);
            Solid(world, "South boundary", new Vector3(0, 1, -79), new Vector3(238, 2, .5f), timber);
            Road(world, new Vector3(0, 0, -13), new Vector3(224, .12f, 7));
            Road(world, new Vector3(65, 0, 21), new Vector3(7, .12f, 68));
            Road(world, new Vector3(-65, 0, 14), new Vector3(6, .12f, 54));
            Road(world, new Vector3(80, 0, 35), new Vector3(55, .12f, 7));
            Road(world, new Vector3(-75, 0, 30), new Vector3(60, .12f, 5));
            Road(world, new Vector3(80, 0, 60), new Vector3(55, .12f, 6));
            Road(world, new Vector3(105, 0, 24), new Vector3(6, .12f, 72));
            Road(world, new Vector3(55, 0, -23), new Vector3(10, .12f, 20));
            Road(world, new Vector3(-49, 0, -21), new Vector3(14, .12f, 16));
            // Broad junctions remain clear of buildings and props.
            for (int x = -106; x <= 106; x += 6)
                if (Mathf.Abs(x - 65) > 5 && Mathf.Abs(x + 65) > 5)
                    Box(world, "Highway center dash", new Vector3(x, .071f, -13), new Vector3(2.4f, .015f, .12f), ivory);
            for (int x = -105; x <= 105; x += 14)
            {
                Tree(world, new Vector3(x, 0, -34));
                Box(world, "Roadside lamp pole", new Vector3(x, 2.2f, -8), new Vector3(.14f, 4.4f, .14f), ink);
                Box(world, "Roadside lamp head", new Vector3(x, 4.4f, -8), new Vector3(.8f, .16f, .4f), gold);
            }

            // Village: homes, cultivated plots, pond and market around the west lanes.
            for (int i = 0; i < 8; i++)
            {
                float x = -105 + i % 4 * 10;
                float z = i < 4 ? 4 : 43;
                Solid(world, "Village house", new Vector3(x, 1.45f, z), new Vector3(7, 2.9f, 6), i % 2 == 0 ? ivory : timber);
                var roof = Solid(world, "Pitched tin roof", new Vector3(x - 1.8f, 3.3f, z), new Vector3(4, .18f, 7), red);
                roof.transform.rotation = Quaternion.Euler(0, 0, 22);
                var roofOther = Solid(world, "Pitched roof opposite", new Vector3(x + 1.8f, 3.3f, z), new Vector3(4, .18f, 7), red);
                roofOther.transform.rotation = Quaternion.Euler(0, 0, -22);
                Box(world, "House doorway", new Vector3(x, 1.05f, z - 3.02f), new Vector3(1.1f, 2.1f, .05f), ink);
                for (int window = -1; window <= 1; window += 2)
                    Box(world, "House window", new Vector3(x + window * 2, 1.6f, z - 3.04f), new Vector3(1.1f, 1, .06f), glass);
                Tree(world, new Vector3(x - 4, 0, z + 4));
            }
            for (int field = 0; field < 3; field++)
            {
                Box(world, "Cultivated field", new Vector3(-100 + field * 12, -.03f, 18), new Vector3(12, .07f, 12), timber);
                for (int row = 0; row < 8; row++)
                    Box(world, "Crop row", new Vector3(-105 + field * 12 + row * 1.4f, .15f, 18), new Vector3(.45f, .30f, 11), mint);
            }
            Box(world, "Village pond bank", new Vector3(-97, .01f, -23), new Vector3(16, .12f, 10), timber);
            Box(world, "Pond water", new Vector3(-97, .08f, -23), new Vector3(14.5f, .03f, 8.5f), glass);
            for (int stall = 0; stall < 3; stall++)
            {
                Solid(world, "Village market stall", new Vector3(-85 + stall * 8, .7f, -2), new Vector3(4, 1.4f, 2), timber);
                Box(world, "Market canopy", new Vector3(-85 + stall * 8, 2.7f, -2), new Vector3(5, .15f, 3), stall % 2 == 0 ? red : mint);
            }

            // City: schools and college are identifiable exterior landmarks, not classroom simulations.
            Campus(world, new Vector3(51, 0, 46), "HAVEN SCHOOL", 12, 4, mint);
            Campus(world, new Vector3(86, 0, 46), "CITY COLLEGE", 20, 7, ivory);
            for (int i = 0; i < 6; i++)
            {
                float x = i % 3 == 0 ? 46 : i % 3 == 1 ? 80 : 96;
                float z = i < 3 ? 8 : 73;
                Campus(world, new Vector3(x, 0, z), i < 3 ? "CITY SHOPS" : "CITY RESIDENCES", 11, 6 + i * 1.4f, i % 2 == 0 ? ivory : mint);
            }
            LabelWorld(world, "VILLAGE", new Vector3(-77, .3f, -6), 14);
            LabelWorld(world, "HAVEN CITY", new Vector3(79, .3f, -5), 14);

            // Drive-through wash: open front/back with physical side walls.
            Solid(world, "Wash apron", new Vector3(55, -.01f, -24), new Vector3(10, .14f, 12), asphalt);
            Solid(world, "Wash left wall", new Vector3(51.2f, 1.8f, -24), new Vector3(.3f, 3.6f, 9), mint);
            Solid(world, "Wash right wall", new Vector3(58.8f, 1.8f, -24), new Vector3(.3f, 3.6f, 9), mint);
            Box(world, "Wash roof", new Vector3(55, 3.7f, -24), new Vector3(8, .25f, 10), ivory);
            LabelWorld(world, "CAR WASH", new Vector3(55, 4.4f, -19), 8);
            LabelWorld(world, "CNG STAND", new Vector3(-49, 2.5f, -25), 8);
            Box(world, "CNG shelter", new Vector3(-49, 3.2f, -25), new Vector3(13, .2f, 3), mint);
            var fleet = new[] { DriveVehicle(world, new Vector3(0, .3f, -20), false),
                DriveVehicle(world, new Vector3(-48, .3f, -20), true) };
            var driving = world.gameObject.AddComponent<WorldDrivingController>();
            driving.Configure(fleet, camera);
            var spray = new GameObject("Car Wash Spray").transform;
            spray.SetParent(world, false);
            for (int i = 0; i < 10; i++)
                Box(spray, "Water stream", new Vector3(52.5f + i * .55f, 1.6f, -24), new Vector3(.035f, 2.8f, .035f), glass);
            spray.gameObject.SetActive(false);
            if (camera != null)
            {
                var lens = camera.GetComponent<Camera>();
                lens.farClipPlane = Mathf.Max(600, lens.farClipPlane);
            }
        }

        private static void InstallRoadExtensions(Transform motel)
        {
            if (motel.Find("Outer Roads v1") != null) return;
            var roads = new GameObject("Outer Roads v1").transform;
            roads.SetParent(motel, false);
            if (!Application.isPlaying) Undo.RegisterCreatedObjectUndo(roads.gameObject, "Extend village city roads");
            Road(roads, new Vector3(0, 0, -60), new Vector3(232, .12f, 6));
            Road(roads, new Vector3(0, 0, 88), new Vector3(232, .12f, 6));
            Road(roads, new Vector3(0, 0, 60), new Vector3(232, .12f, 6));
            for (int side = -1; side <= 1; side += 2)
                Road(roads, new Vector3(side * 113, 0, 14), new Vector3(6, .12f, 148));
            Road(roads, new Vector3(-45, 0, 0), new Vector3(6, .12f, 120));
            Road(roads, new Vector3(35, 0, 14), new Vector3(6, .12f, 148));
            Road(roads, new Vector3(0, 0, -38), new Vector3(6, .12f, 44));
            foreach (float z in new[] { -60f, 60f, 88f })
                for (int x = -108; x <= 108; x += 6)
                    if (Mathf.Abs(x + 45) > 5 && Mathf.Abs(x - 35) > 5 && Mathf.Abs(x) > 5)
                        Box(roads, "Outer road center dash", new Vector3(x, .071f, z), new Vector3(2.4f, .015f, .12f), ivory);
            foreach (float x in new[] { -113f, 113f, 35f })
                for (int z = -54; z < 84; z += 6)
                    if (Mathf.Abs(z + 13) > 6 && Mathf.Abs(z - 60) > 5)
                        Box(roads, "Outer road center dash", new Vector3(x, .071f, z), new Vector3(.12f, .015f, 2.4f), ivory);
        }

        private static GameObject Solid(Transform parent, string name, Vector3 position, Vector3 scale, Material material)
        {
            var shape = GameObject.CreatePrimitive(PrimitiveType.Cube);
            shape.name = name;
            shape.transform.SetParent(parent, false);
            shape.transform.position = position;
            shape.transform.localScale = scale;
            shape.GetComponent<Renderer>().sharedMaterial = material;
            return shape;
        }

        private static void Road(Transform parent, Vector3 position, Vector3 scale)
            => Solid(parent, "Connected road", position, scale, asphalt);

        private static void Campus(Transform parent, Vector3 position, string name, float width, float height, Material material)
        {
            Solid(parent, name, position + Vector3.up * height * .5f, new Vector3(width, height, 8), material);
            Box(parent, "Roof coping", position + Vector3.up * (height + .15f), new Vector3(width + .5f, .3f, 8.5f), ink);
            int floors = Mathf.Min(3, Mathf.FloorToInt(height / 2.5f));
            int windows = Mathf.Min(6, Mathf.FloorToInt(width / 2));
            for (int floor = 0; floor < floors; floor++)
                for (int window = 0; window < windows; window++)
                    Box(parent, "Facade window", position + new Vector3(-width * .5f + (window + 1) * width / (windows + 1),
                        (floor + .6f) * height / Mathf.Max(1, floors), -4.04f), new Vector3(1.1f, 1.25f, .08f), glass);
            Box(parent, "Main entrance", position + new Vector3(0, 1.1f, -4.06f), new Vector3(1.8f, 2.2f, .08f), ink);
            LabelWorld(parent, name, position + new Vector3(0, height + 1, -4), 7);
        }

        private static void LabelWorld(Transform parent, string text, Vector3 position, float size)
        {
            var label = new GameObject(text + " sign").AddComponent<TextMeshPro>();
            label.transform.SetParent(parent, false);
            label.transform.position = position;
            label.transform.rotation = Quaternion.Euler(45, 0, 0);
            label.font = TMP_Settings.GetFontAsset();
            label.text = text;
            label.fontSize = size;
            label.alignment = TextAlignmentOptions.Center;
            label.rectTransform.sizeDelta = new Vector2(24, 3);
            label.color = new Color(1, .9f, .65f);
        }

        private static Rigidbody DriveVehicle(Transform world, Vector3 position, bool cng)
        {
            var vehicle = new GameObject(cng ? "Player CNG" : "Player car").transform;
            vehicle.SetParent(world, false);
            var visual = new GameObject("Vehicle mesh").transform;
            visual.SetParent(vehicle, false);
            if (!cng) { Car(visual, Vector3.zero, red); visual.rotation = Quaternion.Euler(0, 180, 0); }
            else
            {
                Box(visual, "CNG green body", new Vector3(0, .6f, 0), new Vector3(1.45f, .7f, 2.4f), mint);
                Box(visual, "CNG black canopy", new Vector3(0, 1.65f, -.3f), new Vector3(1.6f, .18f, 1.8f), ink);
                Box(visual, "CNG windscreen", new Vector3(0, 1.24f, .82f), new Vector3(1.25f, .6f, .08f), glass);
                for (int side = -1; side <= 1; side += 2)
                {
                    Box(visual, "Canopy support", new Vector3(side * .68f, 1.3f, -.9f), new Vector3(.07f, .8f, .07f), ink);
                    Shape(visual, "Rear wheel", PrimitiveType.Sphere, new Vector3(side * .72f, .28f, -.75f), new Vector3(.19f, .5f, .5f), ink);
                }
                Shape(visual, "Front wheel", PrimitiveType.Sphere, new Vector3(0, .28f, 1f), new Vector3(.19f, .5f, .5f), ink);
            }
            vehicle.position = position;
            var collider = vehicle.gameObject.AddComponent<BoxCollider>();
            collider.center = new Vector3(0, .7f, 0);
            collider.size = new Vector3(1.6f, 1.4f, 2.8f);
            var rigidbody = vehicle.gameObject.AddComponent<Rigidbody>();
            rigidbody.mass = cng ? 450 : 1100;
            rigidbody.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
            rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            rigidbody.isKinematic = true;
            return rigidbody;
        }
    }
}
