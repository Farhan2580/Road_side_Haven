using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RoadsideHaven.Editor
{
    /// <summary>Adds shop references and nonblocking markers without replacing the working motel scene.</summary>
    public static class ShopPrototypeInstaller
    {
        private const string ConfigurationFolder = "Assets/_Game/Shop/Configuration";
        private const string ExtensionName = "Shop Extension";

        [MenuItem("Roadside Haven/Add or Update Shop in Current Scene")]
        public static void UpgradeCurrentScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            var scene = SceneManager.GetActiveScene();
            var motels = scene.GetRootGameObjects()
                .SelectMany(go => go.GetComponentsInChildren<MotelGameRoot>(true)).ToArray();
            if (motels.Length != 1)
            {
                Debug.LogError("Open a scene containing exactly one MotelGameRoot before adding the shop.");
                return;
            }
            if (TMP_Settings.LoadDefaultSettings() == null || TMP_Settings.GetFontAsset() == null)
            {
                Debug.LogError("Import TMP Essential Resources through Window > TextMeshPro before upgrading this scene.");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (!Install(motels[0], TMP_Settings.GetFontAsset())) return;
            EditorSceneManager.MarkSceneDirty(scene);
            if (EditorSceneManager.SaveScene(scene))
            {
                AssetDatabase.SaveAssets();
                Debug.Log("Convenience shop installed. Motel geometry, tuning and baked NavMesh were preserved. Press Play, then tap the shop counter.");
            }
        }

        /// <summary>Idempotently wires a shop using existing counter geometry. Existing complete configurations are retained.</summary>
        public static bool Install(MotelGameRoot motel, TMP_FontAsset font)
        {
            if (motel == null || font == null) return false;
            var shop = motel.GetComponent<ShopController>();
            if (shop != null && shop.IsConfigured) return true;

            var counterTransform = motel.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(t => t.name == "Shop counter" && t.GetComponent<Collider>() != null);
            if (counterTransform == null)
            {
                Debug.LogError("The scene needs the existing Shop counter collider. No motel objects were replaced.", motel);
                return false;
            }

            EnsureFolder(ConfigurationFolder);
            var definitions = new[]
            {
                Product("water", "Water", 2, 5),
                Product("chips", "Chips", 3, 7),
                Product("biscuits", "Biscuits", 2, 5),
                Product("toiletries", "Toiletries", 6, 12)
            };
            string settingsPath = ConfigurationFolder + "/ShopSettings.asset";
            var settings = AssetDatabase.LoadAssetAtPath<ShopSettings>(settingsPath);
            if (settings == null)
            {
                RejectOccupiedPath(settingsPath);
                settings = ScriptableObject.CreateInstance<ShopSettings>();
                settings.ConfigureDefaults(definitions);
                AssetDatabase.CreateAsset(settings, settingsPath);
            }

            var extension = motel.transform.Find(ExtensionName);
            if (extension == null)
            {
                extension = new GameObject(ExtensionName).transform;
                extension.SetParent(motel.transform, false);
                Undo.RegisterCreatedObjectUndo(extension.gameObject, "Add convenience shop");
            }

            var counter = counterTransform.GetComponent<Collider>();
            // Ignore primitive scaling when offsetting the markers. These are world-meter offsets.
            var origin = counter.bounds.center;
            origin.y = counter.bounds.min.y + 0.15f;
            var rotation = Quaternion.Euler(0f, counterTransform.eulerAngles.y, 0f);
            var shelves = new Transform[4];
            for (int i = 0; i < shelves.Length; i++)
                shelves[i] = Marker(extension, "Browse " + definitions[i].Id,
                    origin + rotation * new Vector3(-1.8f + i * 1.2f, 0f, 3f));

            // Separate counter queue; no reception marker is reused or repositioned.
            var queueOffsets = new[]
            {
                new Vector3(0f, 0f, 1.1f), new Vector3(1.3f, 0f, 1.1f),
                new Vector3(2.8f, 0f, 1.1f), new Vector3(4f, 0f, 1.1f),
                new Vector3(4f, 0f, -0.3f), new Vector3(4f, 0f, -1.7f)
            };
            var queue = new Transform[queueOffsets.Length];
            for (int i = 0; i < queue.Length; i++)
                queue[i] = Marker(extension, "Shop queue " + i, origin + rotation * queueOffsets[i]);
            var spawn = Marker(extension, "Shop arrival", origin + rotation * new Vector3(4f, 0f, -3f));
            var exit = Marker(extension, "Shop exit", origin + rotation * new Vector3(5f, 0f, -3f));

            var hud = motel.GetComponentInChildren<ShopHUD>(true);
            if (hud == null)
            {
                hud = ShopHUD.Create(motel.transform, font);
                Undo.RegisterCreatedObjectUndo(hud.gameObject, "Add shop HUD");
            }
            if (shop == null) shop = Undo.AddComponent<ShopController>(motel.gameObject);
            Undo.RecordObject(shop, "Configure convenience shop");
            shop.Configure(settings, spawn, exit, shelves, queue, counter, CustomerMaterial(), hud);
            EditorUtility.SetDirty(shop);
            AssetDatabase.SaveAssets();
            return true;
        }

        private static ProductDefinition Product(string id, string displayName, int cost, int price)
        {
            string path = ConfigurationFolder + "/" + displayName + ".asset";
            var product = AssetDatabase.LoadAssetAtPath<ProductDefinition>(path);
            if (product != null) return product;
            RejectOccupiedPath(path);
            product = ScriptableObject.CreateInstance<ProductDefinition>();
            product.ConfigureDefaults(id, displayName, cost, price, 12, 8, 4);
            AssetDatabase.CreateAsset(product, path);
            return product;
        }

        private static Material CustomerMaterial()
        {
            string path = ConfigurationFolder + "/ShopCustomer.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            RejectOccupiedPath(path);
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) throw new InvalidOperationException("The installed URP Lit shader could not be found.");
            material = new Material(shader) { color = new Color(0.25f, 0.47f, 0.77f) };
            material.SetFloat("_Smoothness", 0.15f);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static Transform Marker(Transform parent, string name, Vector3 position)
        {
            var marker = parent.Find(name);
            if (marker != null) return marker;
            marker = new GameObject(name).transform;
            marker.SetParent(parent, false);
            marker.position = position;
            Undo.RegisterCreatedObjectUndo(marker.gameObject, "Add shop navigation marker");
            return marker;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int slash = path.LastIndexOf('/');
            string parent = path.Substring(0, slash);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, path.Substring(slash + 1));
        }

        private static void RejectOccupiedPath(string path)
        {
            if (AssetDatabase.LoadMainAssetAtPath(path) != null)
                throw new InvalidOperationException("A different asset already occupies " + path + ". It was left unchanged.");
        }
    }
}
