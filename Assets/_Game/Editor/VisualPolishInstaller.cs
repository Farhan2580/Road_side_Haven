using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace RoadsideHaven.Editor
{
    /// <summary>Adds a decorative layer outside the baked navigation geometry.</summary>
    [InitializeOnLoad]
    public static partial class VisualPolishInstaller
    {
        private const string LayerName = "Visual Polish v1";
        private const string MaterialFolder = "Assets/_Game/Generated/Visuals";
        private static Material ivory, timber, ink, leaf, mint, gold, red, glass, asphalt;

        static VisualPolishInstaller()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        // Install after entering Play Mode: opening the prototype does not modify its saved scene.
        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredPlayMode) return;
            var scene = SceneManager.GetActiveScene();
            if (scene.path != "Assets/_Game/Generated/RoadsideHavenPrototype.unity") return;
            var motel = scene.GetRootGameObjects()
                .SelectMany(go => go.GetComponentsInChildren<MotelGameRoot>(true)).FirstOrDefault();
            if (motel != null) Install(motel, TMP_Settings.GetFontAsset());
        }

        [MenuItem("Roadside Haven/Apply Visual Upgrade")]
        public static void UpgradeCurrentScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            var scene = SceneManager.GetActiveScene();
            var motels = scene.GetRootGameObjects()
                .SelectMany(go => go.GetComponentsInChildren<MotelGameRoot>(true)).ToArray();
            if (motels.Length != 1)
            {
                Debug.LogError("Open the Roadside Haven prototype scene before applying the visual upgrade.");
                return;
            }
            Install(motels[0], TMP_Settings.GetFontAsset());
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("Visual upgrade applied. Save the scene to keep it. Existing navigation and gameplay references were retained.");
        }

        /// <summary>Installs once; all decorations have no colliders and cannot intercept world taps.</summary>
        public static void Install(MotelGameRoot motel, TMP_FontAsset font)
        {
            if (!AssetDatabase.IsValidFolder(MaterialFolder))
                AssetDatabase.CreateFolder("Assets/_Game/Generated", "Visuals");
            ivory = Material("Ivory", new Color(.96f, .89f, .72f));
            timber = Material("Cedar", new Color(.44f, .24f, .14f));
            ink = Material("Midnight", new Color(.07f, .16f, .20f));
            leaf = Material("Foliage", new Color(.20f, .39f, .22f));
            mint = Material("Mint", new Color(.27f, .64f, .56f));
            gold = Material("Amber", new Color(1f, .63f, .22f), true);
            red = Material("Coral", new Color(.82f, .29f, .20f));
            glass = Material("Window", new Color(.28f, .51f, .61f));
            asphalt = Material("Asphalt", new Color(.20f, .25f, .27f));
            InstallAtmosphere(motel);
            if (motel.transform.Find(LayerName) != null)
            {
                ExpandDistrict(motel.transform);
                return;
            }
            var layer = new GameObject(LayerName).transform;
            layer.SetParent(motel.transform, false);
            if (!Application.isPlaying) Undo.RegisterCreatedObjectUndo(layer.gameObject, "Add motel visual upgrade");

            var fill = new GameObject("Soft sky fill", typeof(Light)).GetComponent<Light>();
            fill.transform.SetParent(layer, false);
            fill.type = LightType.Directional;
            fill.color = new Color(.67f, .80f, 1f);
            fill.intensity = .28f;
            fill.shadows = LightShadows.None;
            fill.transform.rotation = Quaternion.Euler(35f, 140f, 0f);

            // Road and vehicles sit beyond reception and shop arrival routes.
            Box(layer, "Road", new Vector3(0, -.02f, -13), new Vector3(34, .08f, 4), asphalt);
            for (int i = -4; i <= 4; i++)
                Box(layer, "Road dash", new Vector3(i * 3.8f, .03f, -13), new Vector3(1.7f, .015f, .10f), ivory);
            Box(layer, "Parking apron", new Vector3(0, -.01f, -9.8f), new Vector3(18, .05f, 2.3f), asphalt);
            for (int i = 0; i < 6; i++)
                Box(layer, "Parking stripe", new Vector3(-9 + i * 3.6f, .025f, -9.8f), new Vector3(.07f, .02f, 2.2f), ivory);
            Car(layer, new Vector3(-4.1f, .12f, -9.8f), red);
            Car(layer, new Vector3(3.1f, .12f, -9.8f), mint);

            for (int i = 0; i < 4; i++)
            {
                float x = -8.25f + i * 5.5f;
                Box(layer, "Rear cornice", new Vector3(x, 1.68f, 7), new Vector3(5.3f, .16f, .28f), ivory);
                Box(layer, "Headboard", new Vector3(x - 1, .89f, 6.15f), new Vector3(1.95f, .68f, .16f), timber);
                Box(layer, "Bed runner", new Vector3(x - 1, .83f, 3.95f), new Vector3(1.9f, .035f, .55f), i % 2 == 0 ? mint : ivory);
                Box(layer, "Woven rug", new Vector3(x + .15f, .20f, 3.5f), new Vector3(3.9f, .025f, 2.8f), i % 2 == 0 ? mint : red);
                Box(layer, "Window frame", new Vector3(x + 1.2f, 1.05f, 6.87f), new Vector3(1.65f, .85f, .10f), ivory);
                Box(layer, "Window pane", new Vector3(x + 1.2f, 1.05f, 6.80f), new Vector3(1.4f, .65f, .04f), glass);
                Box(layer, "Window mullion", new Vector3(x + 1.2f, 1.05f, 6.76f), new Vector3(.06f, .65f, .04f), ivory);
                Box(layer, "Lamp stem", new Vector3(x + .65f, .98f, 5.5f), new Vector3(.08f, .4f, .08f), ink);
                Shape(layer, "Lamp shade", PrimitiveType.Cylinder, new Vector3(x + .65f, 1.22f, 5.5f), new Vector3(.48f, .18f, .48f), gold);
                Box(layer, "Front trim", new Vector3(x, .18f, .85f), new Vector3(5.2f, .12f, .16f), ivory);
                Plant(layer, new Vector3(x - 2.1f, .2f, .45f), .7f);
            }

            // Canopy covers the rear of reception, leaving the queue visible.
            Box(layer, "Reception canopy", new Vector3(-6, 2.65f, -4.2f), new Vector3(4.5f, .18f, 1.8f), mint);
            for (int i = 0; i < 2; i++)
                Box(layer, "Canopy column", new Vector3(-8.1f + i * 4.2f, 1.35f, -4.9f), new Vector3(.14f, 2.6f, .14f), ivory);
            Box(layer, "Register", new Vector3(-6, 1.55f, -3.7f), new Vector3(.6f, .3f, .4f), ink);
            Plant(layer, new Vector3(-8.8f, .1f, -4.4f), 1f);
            Plant(layer, new Vector3(-3.2f, .1f, -4.4f), .8f);

            for (int shelf = 0; shelf < 3; shelf++)
            {
                float x = 5.4f + shelf * 1.5f;
                Box(layer, "Shop shelf back", new Vector3(x, 1.1f, -7.2f), new Vector3(1.25f, 1.6f, .12f), mint);
                for (int tier = 0; tier < 3; tier++)
                {
                    float y = .55f + tier * .5f;
                    Box(layer, "Shop shelf", new Vector3(x, y, -6.98f), new Vector3(1.3f, .07f, .5f), ivory);
                    for (int item = 0; item < 4; item++)
                        Box(layer, "Packaged goods", new Vector3(x - .45f + item * .3f, y + .2f, -6.98f),
                            new Vector3(.19f, .32f, .22f), (item + tier) % 2 == 0 ? red : gold);
                }
            }
            Box(layer, "Shop fascia", new Vector3(7, 2.15f, -7.25f), new Vector3(5, .27f, .35f), ink);
            for (int i = 0; i < 9; i++)
                Box(layer, "Shop awning stripe", new Vector3(4.8f + i * .55f, 2.35f, -7), new Vector3(.55f, .1f, .9f), i % 2 == 0 ? red : ivory);

            for (int i = 0; i < 5; i++)
            {
                Tree(layer, new Vector3(-12.2f, 0, -5 + i * 3.3f));
                Tree(layer, new Vector3(12.5f, 0, -5 + i * 3.3f));
            }
            Box(layer, "Sign post", new Vector3(-11, 2.1f, -10.4f), new Vector3(.22f, 4.2f, .22f), timber);
            Box(layer, "Roadside sign", new Vector3(-11, 3.9f, -10.4f), new Vector3(3.6f, 1.7f, .22f), ink);
            if (font != null)
            {
                var sign = new GameObject("Motel sign lettering").AddComponent<TextMeshPro>();
                sign.transform.SetParent(layer, false);
                sign.transform.position = new Vector3(-11, 3.9f, -10.57f);
                sign.font = font;
                sign.text = "ROADSIDE\n<size=65%>H A V E N</size>";
                sign.fontSize = 5;
                sign.alignment = TextAlignmentOptions.Center;
                sign.color = new Color(1f, .8f, .43f);
                sign.rectTransform.sizeDelta = new Vector2(3.4f, 1.6f);
            }
            ExpandDistrict(motel.transform);
            AssetDatabase.SaveAssets();
        }

        private static void ExpandDistrict(Transform motel)
        {
            InstallParkingArrivals(motel.GetComponent<MotelGameRoot>());
            var controller = motel.GetComponentInChildren<IsometricCameraController>();
            if (controller == null)
                controller = Object.FindFirstObjectByType<IsometricCameraController>();
            if (controller != null)
                controller.Configure(Vector3.zero, new Vector2(112, 82), new Vector2(6, 105));
            InstallVillageCity(motel, controller);
            InstallRegionalWorld(motel, controller);
            InstallOwnerActivities(motel, controller);
            if (motel.Find("Expanded District v1") != null) { InstallWorldLighting(motel, controller); return; }
            var district = new GameObject("Expanded District v1").transform;
            district.SetParent(motel, false);
            if (!Application.isPlaying) Undo.RegisterCreatedObjectUndo(district.gameObject, "Expand roadside district");
            // Large physical foundation; the established motel NavMesh remains independent.
            var foundation = GameObject.CreatePrimitive(PrimitiveType.Cube);
            foundation.name = "District ground";
            foundation.transform.SetParent(district, false);
            foundation.transform.position = new Vector3(0, -.51f, 5);
            foundation.transform.localScale = new Vector3(86, .5f, 74);
            foundation.GetComponent<Renderer>().sharedMaterial = leaf;
            Box(district, "Highway extension", new Vector3(0, .04f, -13), new Vector3(68, .08f, 4), asphalt);
            Box(district, "North avenue", new Vector3(0, .04f, 27), new Vector3(68, .08f, 4), asphalt);
            for (int side = -1; side <= 1; side += 2)
            {
                Box(district, "Connecting avenue", new Vector3(side * 32, .04f, 7), new Vector3(4, .08f, 40), asphalt);
                Box(district, "Sidewalk", new Vector3(side * 28.8f, .08f, 7), new Vector3(2, .18f, 35), ivory);
                for (int z = -8; z <= 23; z += 4)
                    Box(district, "Lane marking", new Vector3(side * 32, .09f, z), new Vector3(.1f, .02f, 1.8f), ivory);
            }
            for (int x = -28; x <= 28; x += 4)
            {
                Box(district, "North lane marking", new Vector3(x, .09f, 27), new Vector3(1.8f, .02f, .1f), ivory);
                if (Mathf.Abs(x) > 16)
                    Box(district, "South lane marking", new Vector3(x, .09f, -13), new Vector3(1.8f, .02f, .1f), ivory);
            }
            for (int block = 0; block < 5; block++)
            {
                float x = -23 + block * 11.5f;
                float height = 2.5f + block % 3 * .7f;
                Box(district, "Neighborhood building", new Vector3(x, height * .5f, 19), new Vector3(6, height, 5), block % 2 == 0 ? ivory : timber);
                Box(district, "Building roof", new Vector3(x, height + .15f, 19), new Vector3(6.4f, .3f, 5.4f), ink);
                for (int window = 0; window < 3; window++)
                    Box(district, "Street window", new Vector3(x - 1.9f + window * 1.9f, 1.5f, 16.45f), new Vector3(1.15f, 1.25f, .08f), glass);
                Plant(district, new Vector3(x + 3.8f, 0, 16), 1.2f);
                Tree(district, new Vector3(x, 0, 32));
            }
            Box(district, "Park lawn", new Vector3(21, -.08f, 3), new Vector3(12, .12f, 17), mint);
            for (int i = 0; i < 5; i++)
            {
                Tree(district, new Vector3(17 + i % 2 * 7, 0, -3 + i * 3));
                Box(district, "Park bench", new Vector3(21, .45f, -2 + i * 3), new Vector3(2, .2f, .65f), timber);
            }
            for (int side = -1; side <= 1; side += 2)
                for (int z = -7; z <= 23; z += 10)
                {
                    Box(district, "Street lamp pole", new Vector3(side * 28.8f, 2.1f, z), new Vector3(.12f, 4.2f, .12f), ink);
                    Box(district, "Street lamp", new Vector3(side * 28.8f, 4.2f, z), new Vector3(.7f, .18f, .4f), gold);
                }
            for (int i = 0; i < 3; i++)
            {
                var vehicle = new GameObject("District traffic " + (i + 1));
                vehicle.transform.SetParent(district, false);
                Car(vehicle.transform, Vector3.zero, i % 2 == 0 ? red : mint);
                vehicle.transform.position = new Vector3(-22 + i * 18, .15f, -14);
                var collider = vehicle.AddComponent<BoxCollider>();
                collider.center = new Vector3(0, .65f, 0);
                collider.size = new Vector3(1.6f, 1.3f, 2.8f);
                vehicle.AddComponent<Rigidbody>().isKinematic = true;
                vehicle.AddComponent<RoadTraffic>().Configure(new[] {
                    new Vector3(33, .15f, -14), new Vector3(33, .15f, 28),
                    new Vector3(-33, .15f, 28), new Vector3(-33, .15f, -14)
                }, 4.5f);
            }
            InstallWorldLighting(motel, controller);
        }

        private static void InstallParkingArrivals(MotelGameRoot motel)
        {
            if (motel == null || motel.HasParkingArrivals) return;
            var parking = motel.transform.Find("Parking Guest Arrivals");
            if (parking == null)
            {
                parking = new GameObject("Parking Guest Arrivals").transform;
                parking.SetParent(motel.transform, false);
                if (!Application.isPlaying) Undo.RegisterCreatedObjectUndo(parking.gameObject, "Add parking guest arrival points");
            }
            // These pedestrian points are on the existing courtyard, in front of parked cars.
            // They do not require baking the district foundation or crossing moving traffic.
            float[] bayX = { -7.2f, -4.1f, -.5f, 3.1f };
            var markers = new Transform[bayX.Length];
            for (int i = 0; i < markers.Length; i++)
            {
                markers[i] = parking.Find("Bay " + (i + 1));
                if (markers[i] != null) continue;
                markers[i] = new GameObject("Bay " + (i + 1)).transform;
                markers[i].SetParent(parking, false);
                markers[i].position = new Vector3(bayX[i], .15f, -7.8f);
                Box(parking, "Pedestrian bay marker", new Vector3(bayX[i], .08f, -7.8f), new Vector3(.55f, .025f, .14f), ivory);
            }
            if (!Application.isPlaying) Undo.RecordObject(motel, "Connect parking guest arrivals");
            motel.ConfigureParkingArrivals(markers);
            if (!Application.isPlaying) EditorUtility.SetDirty(motel);
        }

        private static void InstallAtmosphere(MotelGameRoot motel)
        {
            InstallPostProcessing(motel);
            var existingAtmosphere = motel.GetComponent<MotelAtmosphere>();
            if (existingAtmosphere != null)
            {
                ConfigureSeasonalSky(existingAtmosphere);
                return;
            }
            var sun = motel.gameObject.scene.GetRootGameObjects()
                .SelectMany(go => go.GetComponentsInChildren<Light>(true))
                .FirstOrDefault(light => light.type == LightType.Directional && light.name != "Soft sky fill");
            if (sun == null) return;
            var atmosphere = Application.isPlaying ? motel.gameObject.AddComponent<MotelAtmosphere>() :
                Undo.AddComponent<MotelAtmosphere>(motel.gameObject);
            atmosphere.Configure(sun);
            ConfigureSeasonalSky(atmosphere);
            if (!Application.isPlaying) EditorUtility.SetDirty(atmosphere);
        }

        private static void ConfigureSeasonalSky(MotelAtmosphere atmosphere)
        {
            var shader = Shader.Find("RoadsideHaven/Seasonal Sky");
            if (shader == null)
            {
                Debug.LogWarning("Seasonal sky shader is not imported yet. Refresh Assets and reapply the visual upgrade.");
                return;
            }
            const string skyPath = MaterialFolder + "/SeasonalSky.mat";
            var sky = AssetDatabase.LoadAssetAtPath<Material>(skyPath);
            if (sky == null) { sky = new Material(shader); AssetDatabase.CreateAsset(sky, skyPath); }
            atmosphere.ConfigureSky(sky);
            if (!Application.isPlaying) EditorUtility.SetDirty(atmosphere);
        }

        private static void InstallPostProcessing(MotelGameRoot motel)
        {
            var camera = motel.gameObject.scene.GetRootGameObjects()
                .SelectMany(go => go.GetComponentsInChildren<IsometricCameraController>(true))
                .Select(controller => controller.GetComponent<Camera>()).FirstOrDefault();
            if (camera == null) return;
            var cameraData = camera.GetUniversalAdditionalCameraData();
            cameraData.renderPostProcessing = true;
            cameraData.volumeLayerMask |= 1;
            cameraData.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
            camera.allowHDR = true;
            if (motel.transform.Find("Motel Color Grading") != null) return;
            const string path = MaterialFolder + "/MotelPresentation.asset";
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, path);
                var bloom = profile.Add<Bloom>(true);
                bloom.intensity.Override(.25f);
                bloom.threshold.Override(1.05f);
                bloom.scatter.Override(.55f);
                var tonemapping = profile.Add<Tonemapping>(true);
                tonemapping.mode.Override(TonemappingMode.ACES);
                var color = profile.Add<ColorAdjustments>(true);
                color.postExposure.Override(.15f);
                color.contrast.Override(8f);
                color.saturation.Override(-3f);
                foreach (var component in profile.components) AssetDatabase.AddObjectToAsset(component, profile);
                EditorUtility.SetDirty(profile);
                AssetDatabase.SaveAssets();
            }
            var grading = new GameObject("Motel Color Grading", typeof(Volume));
            grading.transform.SetParent(motel.transform, false);
            grading.GetComponent<Volume>().isGlobal = true;
            grading.GetComponent<Volume>().priority = 10;
            grading.GetComponent<Volume>().sharedProfile = profile;
            if (!Application.isPlaying) Undo.RegisterCreatedObjectUndo(grading, "Add motel color grading");
        }

        private static Material Material(string name, Color color, bool emissive = false)
        {
            string path = MaterialFolder + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.color = color;
            material.SetFloat("_Smoothness", .25f);
            if (emissive)
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", color * .65f);
            }
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static void Box(Transform parent, string name, Vector3 position, Vector3 scale, Material material)
            => Shape(parent, name, PrimitiveType.Cube, position, scale, material);

        private static void Shape(Transform parent, string name, PrimitiveType type, Vector3 position, Vector3 scale, Material material)
        {
            var shape = GameObject.CreatePrimitive(type);
            shape.name = name;
            shape.transform.SetParent(parent, false);
            shape.transform.position = position;
            shape.transform.localScale = scale;
            shape.GetComponent<Renderer>().sharedMaterial = material;
            var collider = shape.GetComponent<Collider>();
            collider.enabled = false;
            if (Application.isPlaying) Object.Destroy(collider);
            else Object.DestroyImmediate(collider);
        }

        private static void Plant(Transform parent, Vector3 position, float size)
        {
            Shape(parent, "Terracotta planter", PrimitiveType.Cylinder, position + Vector3.up * size * .25f,
                new Vector3(.6f, .25f, .6f) * size, red);
            Shape(parent, "Plant", PrimitiveType.Sphere, position + Vector3.up * size * .7f,
                new Vector3(.8f, .8f, .8f) * size, leaf);
        }

        private static void Tree(Transform parent, Vector3 position)
        {
            Box(parent, "Tree trunk", position + Vector3.up * 1.1f, new Vector3(.25f, 2.2f, .25f), timber);
            Shape(parent, "Tree crown", PrimitiveType.Sphere, position + Vector3.up * 2.5f, new Vector3(1.9f, 2.4f, 1.9f), leaf);
            Shape(parent, "Tree crown highlight", PrimitiveType.Sphere, position + new Vector3(.45f, 3.1f, 0), new Vector3(1.4f, 1.5f, 1.4f), mint);
        }

        private static void Car(Transform parent, Vector3 position, Material paint)
        {
            Box(parent, "Car body", position + Vector3.up * .45f, new Vector3(1.6f, .55f, 2.8f), paint);
            Box(parent, "Car cabin", position + new Vector3(0, .95f, .1f), new Vector3(1.35f, .55f, 1.4f), glass);
            Box(parent, "Car roof", position + new Vector3(0, 1.25f, .1f), new Vector3(1.4f, .08f, 1.1f), paint);
            for (int side = -1; side <= 1; side += 2)
                for (int axle = -1; axle <= 1; axle += 2)
                    Shape(parent, "Tire", PrimitiveType.Sphere, position + new Vector3(side * .78f, .25f, axle * .85f), new Vector3(.22f, .5f, .5f), ink);
            for (int side = -1; side <= 1; side += 2)
                Box(parent, "Headlight", position + new Vector3(side * .52f, .5f, -1.42f), new Vector3(.35f, .16f, .035f), gold);
        }
    }
}
