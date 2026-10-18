using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Pokupka.Editor
{
    // One-off setup: rebuilds the hub castle as plain walls with corner towers, then rescatters foliage around it.
    static class CastleBuilder
    {
        const string k_ScenePath = "Assets/Scenes/Hub.unity";
        const string k_ModelsFolder = "Assets/Models/FBX format";
        const string k_TuftMeshPath = "Assets/Art/Meshes/Tuft.asset";
        static readonly string[] k_TuftMaterials =
        {
            "Assets/Art/Materials/grassTuft.mat", "Assets/Art/Materials/grassTuft.mat", "Assets/Art/Materials/grassTuft.mat",
            "Assets/Art/Materials/flowerTuftYellow.mat", "Assets/Art/Materials/flowerTuftPurple.mat",
        };

        const int k_Size = 6;
        const float k_WallHeight = 3f;
        const float k_OuterRadius = 58f;
        const float k_GatePathHalfWidth = 3f;

        [MenuItem("pokupka/Rebuild Castle")]
        static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }
            var scene = EditorSceneManager.OpenScene(k_ScenePath, OpenSceneMode.Single);
            DestroyIfExists("Town");
            DestroyIfExists("Foliage");

            var wallPrefab = Load("wall");
            var floorPrefab = Load("floor");
            if (wallPrefab == null || floorPrefab == null)
            {
                return;
            }
            float scale = k_WallHeight / Mathf.Max(Size(wallPrefab).y, 0.01f);
            float cell = Mathf.Max(Size(floorPrefab).x, 0.01f) * scale;
            Vector3 CellCenter(int x, int z) => new Vector3((x - (k_Size - 1) * 0.5f) * cell, 0f, (z - (k_Size - 1) * 0.5f) * cell);

            var town = new GameObject("Town").transform;
            var floor = Group(town, "Floor");
            var walls = Group(town, "Walls");
            var towers = Group(town, "Towers");

            for (int x = 0; x < k_Size; x++)
            {
                for (int z = 0; z < k_Size; z++)
                {
                    Place(floorPrefab, floor, CellCenter(x, z), 0f, scale, 0f, true);
                }
            }

            Vector3 wallSize = Size(wallPrefab);
            float alongX = wallSize.x >= wallSize.z ? 0f : 90f;
            for (int i = 0; i < k_Size; i++)
            {
                Place(wallPrefab, walls, CellCenter(i, -1), alongX, scale, 0f, false);
                Place(wallPrefab, walls, CellCenter(i, k_Size), alongX + 180f, scale, 0f, false);
                Place(wallPrefab, walls, CellCenter(-1, i), alongX + 90f, scale, 0f, false);
                Place(wallPrefab, walls, CellCenter(k_Size, i), alongX + 270f, scale, 0f, false);
            }

            var towerPrefab = Load("tower");
            var towerTopPrefab = Load("tower-top");
            foreach (var (x, z) in new[] { (-1, -1), (k_Size, -1), (-1, k_Size), (k_Size, k_Size) })
            {
                if (towerPrefab == null)
                {
                    break;
                }
                Transform tower = Place(towerPrefab, towers, CellCenter(x, z), 0f, scale, 0f, false);
                if (towerTopPrefab != null)
                {
                    Place(towerTopPrefab, towers, CellCenter(x, z), 0f, scale, WorldBounds(tower.gameObject).max.y, false);
                }
            }

            ScatterFoliage(WorldBounds(walls.gameObject), scale);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[Castle] Done.");
        }

        static void ScatterFoliage(Bounds town, float treeScale)
        {
            var tuftMesh = AssetDatabase.LoadAssetAtPath<Mesh>(k_TuftMeshPath);
            var tuftMaterials = k_TuftMaterials.Select(AssetDatabase.LoadAssetAtPath<Material>).Where(m => m != null).ToArray();
            var ground = GameObject.Find("Ground");
            float groundY = ground != null ? ground.transform.position.y : 0f;

            var root = new GameObject("Foliage").transform;
            var trees = Group(root, "Trees");
            var tufts = Group(root, "Tufts");
            var random = new System.Random(11);

            bool IsFree(Vector3 p, float margin)
            {
                bool inTown = p.x > town.min.x - margin && p.x < town.max.x + margin && p.z > town.min.z - margin && p.z < town.max.z + margin;
                bool onGatePath = Mathf.Abs(p.x - town.center.x) < k_GatePathHalfWidth && p.z < town.min.z;
                return !inTown && !onGatePath && new Vector2(p.x, p.z).magnitude < k_OuterRadius;
            }

            void PlaceMany(int count, float margin, System.Action<Vector3> place)
            {
                int placed = 0;
                for (int attempt = 0; attempt < count * 20 && placed < count; attempt++)
                {
                    var p = new Vector3(Range(random, -k_OuterRadius, k_OuterRadius), 0f, Range(random, -k_OuterRadius, k_OuterRadius));
                    if (IsFree(p, margin))
                    {
                        place(p);
                        placed++;
                    }
                }
            }

            var treePrefab = Load("tree-large");
            var shrubPrefab = Load("tree-shrub");
            if (treePrefab != null)
            {
                PlaceMany(45, 4f, p => Place(treePrefab, trees, p, Range(random, 0f, 360f), treeScale * Range(random, 0.8f, 1.3f), groundY, false));
            }
            if (shrubPrefab != null)
            {
                PlaceMany(70, 2f, p => Place(shrubPrefab, trees, p, Range(random, 0f, 360f), treeScale * Range(random, 0.7f, 1.2f), groundY, false));
            }
            if (tuftMesh != null && tuftMaterials.Length > 0)
            {
                PlaceMany(650, 0.6f, p => PlaceTuft(tufts, tuftMesh, tuftMaterials, p, groundY, random));
            }
        }

        static Transform Place(GameObject prefab, Transform parent, Vector3 position, float rotationY, float scale, float y, bool alignTop)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            instance.transform.localScale = Vector3.one * scale;
            instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.Euler(0f, rotationY, 0f));
            Bounds bounds = WorldBounds(instance);
            float yOffset = alignTop ? y - bounds.max.y : y - bounds.min.y;
            instance.transform.position = new Vector3(position.x - bounds.center.x, yOffset, position.z - bounds.center.z);
            return instance.transform;
        }

        static void PlaceTuft(Transform parent, Mesh mesh, Material[] materials, Vector3 position, float groundY, System.Random random)
        {
            var tuft = new GameObject("Tuft");
            tuft.transform.SetParent(parent, false);
            tuft.transform.SetPositionAndRotation(new Vector3(position.x, groundY, position.z), Quaternion.Euler(0f, Range(random, 0f, 360f), 0f));
            tuft.transform.localScale = Vector3.one * Range(random, 0.8f, 1.3f);
            tuft.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = tuft.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = materials[random.Next(materials.Length)];
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            GameObjectUtility.SetStaticEditorFlags(tuft, StaticEditorFlags.BatchingStatic);
        }

        static GameObject Load(string name)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{k_ModelsFolder}/{name}.fbx");
            if (prefab == null)
            {
                Debug.LogWarning($"[Castle] Missing model '{name}'.");
            }
            return prefab;
        }

        static Vector3 Size(GameObject prefab)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            Vector3 size = WorldBounds(instance).size;
            Object.DestroyImmediate(instance);
            return size;
        }

        static float Range(System.Random random, float min, float max)
        {
            return min + (float)random.NextDouble() * (max - min);
        }

        static Transform Group(Transform parent, string name)
        {
            var group = new GameObject(name).transform;
            group.SetParent(parent, false);
            return group;
        }

        static void DestroyIfExists(string name)
        {
            var existing = GameObject.Find(name);
            if (existing != null)
            {
                Object.DestroyImmediate(existing);
            }
        }

        static Bounds WorldBounds(GameObject root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
            {
                return new Bounds(root.transform.position, Vector3.zero);
            }
            Bounds bounds = renderers[0].bounds;
            foreach (var renderer in renderers.Skip(1))
            {
                bounds.Encapsulate(renderer.bounds);
            }
            return bounds;
        }
    }
}
