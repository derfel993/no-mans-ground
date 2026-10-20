using System;
using System.IO;
using NoMansGround.Map;
using NoMansGround.Soldiers;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace NoMansGround.Editor
{
    public static class MapPrototypeSetup
    {
        public const string ScenePath = "Assets/Scenes/MapPrototype.unity";
        private const string ArtFolder = "Assets/Art/Prototype";

        [MenuItem("No Man's Ground/Create Map Prototype Scene")]
        public static void CreateScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
            if (File.Exists(ScenePath) && !EditorUtility.DisplayDialog("Map prototype",
                "Replace the existing prototype scene? Terrain assets will be preserved.", "Replace", "Cancel"))
                return;

            BuildScene();
        }

        public static void BuildScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EnsureFolder("Assets", "Scenes");
            EnsureFolder("Assets", "Art");
            EnsureFolder("Assets/Art", "Prototype");
            Sprite sprite = CreateSprite();
            Tile dirt = CreateTile("Dirt", new Color(0.49f, 0.40f, 0.29f), sprite);
            Tile grass = CreateTile("Grass", new Color(0.39f, 0.46f, 0.28f), sprite);
            Tile mud = CreateTile("Mud", new Color(0.29f, 0.25f, 0.20f), sprite);

            var gridObject = new GameObject("Map", typeof(Grid), typeof(MapPrototype));
            var terrain = new GameObject("Ground", typeof(Tilemap), typeof(TilemapRenderer), typeof(MapTilemapView));
            terrain.transform.SetParent(gridObject.transform, false);
            var view = terrain.GetComponent<MapTilemapView>();
            view.Configure(dirt, grass, mud);

            var cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener),
                typeof(MapCameraController));
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(64f, 64f, -10f);
            var camera = cameraObject.GetComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 18f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 100f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.12f, 0.13f, 0.11f);
            camera.allowHDR = false;

            var prototype = gridObject.GetComponent<MapPrototype>();
            prototype.Configure(view, cameraObject.GetComponent<MapCameraController>());
            ConfigureSoldiers(prototype, view, camera);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = gridObject;
            Debug.Log("Map prototype ready: " + ScenePath);
        }

        [MenuItem("No Man's Ground/Add Soldiers to Map Prototype Scene")]
        [MenuItem("No Man's Ground/Update Map Prototype (Soldiers, Movement, Trenches)")]
        public static void AddSoldiersToScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
            UpgradeScene();
        }

        public static void UpgradeScene()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath);
            var prototype = UnityEngine.Object.FindAnyObjectByType<MapPrototype>();
            var view = UnityEngine.Object.FindAnyObjectByType<MapTilemapView>();
            Camera camera = Camera.main;
            if (prototype == null || view == null || camera == null)
                throw new InvalidOperationException("The prototype scene requires a map, tilemap view and main camera.");
            EnsureFolder("Assets", "Art");
            EnsureFolder("Assets/Art", "Prototype");
            ConfigureSoldiers(prototype, view, camera);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("Soldiers added to the existing map prototype scene.");
        }

        private static void ConfigureSoldiers(MapPrototype prototype, MapTilemapView view, Camera camera)
        {
            var roster = UnityEngine.Object.FindAnyObjectByType<SoldierRoster>();
            if (roster == null)
                roster = new GameObject("Soldiers", typeof(SoldierRoster)).GetComponent<SoldierRoster>();
            roster.Configure(prototype, view, CreateSoldierSprite(), CreateSoldierDirectionalSprites());
            roster.ConfigureCook(true);
            var orders = roster.GetComponent<SoldierMovementOrders>();
            if (orders == null)
                orders = roster.gameObject.AddComponent<SoldierMovementOrders>();
            orders.Configure(prototype, view, roster, camera);
            var trenches = roster.GetComponent<TrenchWorkOrders>();
            if (trenches == null)
                trenches = roster.gameObject.AddComponent<TrenchWorkOrders>();
            trenches.Configure(prototype, view, roster, camera);
            orders.ConfigureTrenches(trenches);
            view.ConfigureTrench(CreateTile("Trench", new Color(0.19f, 0.13f, 0.08f), CreateSprite()));
            view.ConfigureRock(CreateTile("Rock", new Color(0.34f, 0.35f, 0.36f), CreateSprite()));
            view.ConfigureGravel(CreateTile("Gravel", new Color(0.53f, 0.52f, 0.47f), CreateSprite()));
            view.ConfigureLadder(CreateTile("Ladder", Color.white, CreateLadderSprite()));
            view.ConfigureRoad(CreateTile("Road", new Color(0.56f, 0.54f, 0.49f), CreateSprite()));
            var kitchen = roster.GetComponent<KitchenSystem>();
            if (kitchen == null)
                kitchen = roster.gameObject.AddComponent<KitchenSystem>();
            kitchen.Configure(prototype, view, roster, CreateSprite());
            var panel = UnityEngine.Object.FindAnyObjectByType<SoldierDetailsPanel>();
            if (panel == null)
                panel = new GameObject("Soldier Selection", typeof(SoldierDetailsPanel)).GetComponent<SoldierDetailsPanel>();
            panel.Configure(roster, camera, orders, trenches, kitchen, prototype);
        }

        private static Sprite CreateSoldierSprite()
        {
            string path = ArtFolder + "/Soldier.png";
            if (!File.Exists(path))
            {
                var texture = new Texture2D(16, 16, TextureFormat.RGBA32, false);
                var pixels = new Color32[256];
                for (int y = 0; y < 16; y++)
                {
                    for (int x = 0; x < 16; x++)
                    {
                        int distance = (x - 8) * (x - 8) + (y - 9) * (y - 9);
                        if (x >= 3 && x <= 12 && y >= 2 && y <= 8)
                            pixels[y * 16 + x] = new Color32(95, 108, 77, 255);
                        if (distance <= 30)
                            pixels[y * 16 + x] = distance >= 19
                                ? new Color32(40, 48, 35, 255) : new Color32(155, 168, 116, 255);
                    }
                }
                texture.SetPixels32(pixels);
                texture.Apply();
                File.WriteAllBytes(path, texture.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(texture);
                AssetDatabase.ImportAsset(path);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 16f;
                importer.filterMode = FilterMode.Point;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.mipmapEnabled = false;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static Sprite[] CreateSoldierDirectionalSprites()
        {
            string[] directions = { "north", "north-east", "east", "south-east",
                "south", "south-west", "west", "north-west" };
            var sprites = new Sprite[directions.Length];
            for (int index = 0; index < directions.Length; index++)
            {
                string path = ArtFolder + "/Soldiers/ITA/soldier-ita-1-" + directions[index] + ".png";
                sprites[index] = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sprites[index] == null)
                    Debug.LogWarning("Directional soldier sprite not found: " + path);
            }
            return sprites;
        }

        private static Sprite CreateLadderSprite()
        {
            string path = ArtFolder + "/Ladder.png";
            if (!File.Exists(path))
            {
                var texture = new Texture2D(16, 16, TextureFormat.RGBA32, false);
                var pixels = new Color32[256];
                for (int y = 0; y < 16; y++)
                {
                    for (int x = 0; x < 16; x++)
                    {
                        bool rail = (x == 4 || x == 11) && y >= 2 && y <= 13;
                        bool rung = x >= 4 && x <= 11 && (y == 3 || y == 6 || y == 9 || y == 12);
                        pixels[y * 16 + x] = rail || rung
                            ? new Color32(211, 171, 96, 255) : new Color32(44, 30, 18, 255);
                    }
                }
                texture.SetPixels32(pixels);
                texture.Apply();
                File.WriteAllBytes(path, texture.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(texture);
                AssetDatabase.ImportAsset(path);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 16f;
                importer.isReadable = true;
                importer.filterMode = FilterMode.Point;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.mipmapEnabled = false;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static Sprite CreateSprite()
        {
            string path = ArtFolder + "/Terrain.png";
            if (!File.Exists(path))
            {
                var texture = new Texture2D(16, 16, TextureFormat.RGBA32, false);
                var pixels = new Color32[16 * 16];
                for (int y = 0; y < 16; y++)
                {
                    for (int x = 0; x < 16; x++)
                    {
                        byte shade = (byte)(x == 0 || y == 0 ? 205 : 235 + (x * 7 + y * 11) % 21);
                        pixels[y * 16 + x] = new Color32(shade, shade, shade, 255);
                    }
                }
                texture.SetPixels32(pixels);
                texture.Apply();
                File.WriteAllBytes(path, texture.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(texture);
                AssetDatabase.ImportAsset(path);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 16f;
                importer.isReadable = true;
                importer.filterMode = FilterMode.Point;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.mipmapEnabled = false;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static Tile CreateTile(string name, Color color, Sprite sprite)
        {
            string path = ArtFolder + "/" + name + ".asset";
            Tile tile = AssetDatabase.LoadAssetAtPath<Tile>(path);
            if (tile != null)
                return tile;
            tile = ScriptableObject.CreateInstance<Tile>();
            tile.sprite = sprite;
            tile.color = color;
            tile.colliderType = Tile.ColliderType.None;
            AssetDatabase.CreateAsset(tile, path);
            return tile;
        }

        private static void EnsureFolder(string parent, string name)
        {
            if (!AssetDatabase.IsValidFolder(parent + "/" + name))
                AssetDatabase.CreateFolder(parent, name);
        }
    }
}
