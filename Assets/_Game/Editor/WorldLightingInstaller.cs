using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace RoadsideHaven.Editor
{
    public static partial class VisualPolishInstaller
    {
        private static void InstallWorldLighting(Transform motel, IsometricCameraController camera)
        {
            if (motel.Find("World Night Lighting v1") != null) return;
            // Snapshot first so decorative poles do not get mistaken for buildings/roads.
            var geometry = motel.GetComponentsInChildren<MeshRenderer>(true);
            var root = new GameObject("World Night Lighting v1").transform;
            root.SetParent(motel, false);
            if (!Application.isPlaying) Undo.RegisterCreatedObjectUndo(root.gameObject, "Add world road building and garden lights");
            var glow = Material("Night fixture glass", new Color(1f, .82f, .52f), true);
            var points = new List<Transform>();
            var surfaces = new List<Renderer>();
            var occupied = new HashSet<Vector2Int>();
            foreach (var mesh in geometry)
            {
                if (!mesh.gameObject.activeInHierarchy) continue;
                var bounds = mesh.bounds;
                string name = mesh.name;
                bool road = name == "Connected road" || name == "Road" || name == "Highway extension" ||
                    name == "North avenue" || name == "Connecting avenue" || name == "Parking apron";
                if (road)
                {
                    bool horizontal = bounds.size.x >= bounds.size.z;
                    float length = horizontal ? bounds.size.x : bounds.size.z;
                    float width = horizontal ? bounds.size.z : bounds.size.x;
                    float spacing = width >= 10 ? 30 : 22;
                    int count = Mathf.Max(1, Mathf.CeilToInt(length / spacing));
                    for (int i = 0; i <= count; i++)
                    {
                        float along = Mathf.Lerp(-length * .5f + 2, length * .5f - 2, (float)i / count);
                        for (int side = -1; side <= 1; side += 2)
                        {
                            Vector3 position = bounds.center + (horizontal ? new Vector3(along, 0, side * (width * .5f + .8f)) : new Vector3(side * (width * .5f + .8f), 0, along));
                            position.y = 0;
                            var cell = new Vector2Int(Mathf.RoundToInt(position.x / 5), Mathf.RoundToInt(position.z / 5));
                            if (!occupied.Add(cell)) continue;
                            Fixture(root, position, width >= 10 ? 6 : 4, glow, points, surfaces, "Streetlight");
                        }
                    }
                }
                bool window = name == "Facade window" || name == "House window" || name == "Window pane" || name == "Street window";
                if (window) surfaces.Add(mesh);
                bool building = bounds.size.x >= 4 && bounds.size.y >= 2.5f && bounds.size.z >= 4 &&
                    (name.Contains("Warehouse") || name.Contains("Village house") || name.Contains("Neighborhood building") ||
                     name.Contains("SCHOOL") || name.Contains("COLLEGE") || name.Contains("CITY BLOCK") ||
                     name.Contains("CITY SHOPS") || name.Contains("CITY RESIDENCES") || name.Contains("HIGHWAY MOTEL") ||
                     name.Contains("BEACH HUT") || name.Contains("SUPPLIER"));
                if (building)
                {
                    for (int side = -1; side <= 1; side += 2)
                        Fixture(root, new Vector3(bounds.center.x + side * Mathf.Min(3, bounds.extents.x - .5f), 0, bounds.min.z - .8f),
                            2.4f, glow, points, surfaces, "Building lantern");
                }
                if (name == "Tree trunk" || name == "Terracotta planter")
                {
                    Vector3 position = new Vector3(bounds.center.x + 1, 0, bounds.center.z + 1);
                    var cell = new Vector2Int(Mathf.RoundToInt(position.x / 3), Mathf.RoundToInt(position.z / 3));
                    if (occupied.Add(cell)) Fixture(root, position, .85f, glow, points, surfaces, "Garden bollard");
                }
                if (name == "Lamp shade" || name == "Street lamp" || name == "Street lamp head" || name == "Headlight")
                {
                    // Existing static fixtures get night emission too; moving headlights stay with their mesh.
                    surfaces.Add(mesh);
                    if (name != "Headlight") points.Add(mesh.transform);
                }
            }
            // Motel room entries, reception, shop and park paths get dedicated warm fixtures.
            foreach (var room in motel.GetComponentsInChildren<RoomSelectable>(true))
                if (room.EntryPoint != null)
                {
                    var entry = room.EntryPoint.position;
                    Fixture(root, new Vector3(entry.x + 1, 0, entry.z - 1), 2, glow, points, surfaces, "Room entry lamp");
                }
            foreach (var position in new[] {new Vector3(-8,0,-4), new Vector3(9,0,-6), new Vector3(21,0,-3), new Vector3(21,0,6)})
                Fixture(root, position, 1.3f, glow, points, surfaces, "Courtyard garden lamp");
            root.gameObject.AddComponent<WorldNightLighting>().Configure(motel.GetComponent<MotelAtmosphere>(), camera, points.ToArray(), surfaces.Distinct().ToArray());
            Debug.Log($"Night lighting installed: {points.Count} fixture positions, {surfaces.Count} glow surfaces. At most 20 nearby unshadowed lights are enabled at night.");
        }

        private static void Fixture(Transform parent, Vector3 position, float height, Material glow,
            List<Transform> points, List<Renderer> surfaces, string name)
        {
            Box(parent, name + " post", position + Vector3.up * height * .5f, new Vector3(height > 3 ? .14f : .10f, height, .10f), ink);
            var bulb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            bulb.name = name + " bulb"; bulb.transform.SetParent(parent, false);
            bulb.transform.position = position + Vector3.up * height;
            bulb.transform.localScale = Vector3.one * (height > 3 ? .48f : .25f);
            var collider = bulb.GetComponent<Collider>(); collider.enabled = false;
            if (Application.isPlaying) Object.Destroy(collider); else Object.DestroyImmediate(collider);
            var renderer = bulb.GetComponent<Renderer>(); renderer.sharedMaterial = glow;
            surfaces.Add(renderer); points.Add(bulb.transform);
        }
    }
}
