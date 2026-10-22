using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Tilemaps;

namespace NoMansGround.Map
{
    public enum MapTilePixelResolution { Pixels16 = 16, Pixels32 = 32, Pixels64 = 64 }

    [RequireComponent(typeof(Tilemap), typeof(TilemapRenderer))]
    public sealed class MapTilemapView : MonoBehaviour
    {
        [SerializeField] private MapTilePixelResolution pixelResolution = MapTilePixelResolution.Pixels32;
        private readonly HashSet<Vector2Int> renderedZoneCells = new HashSet<Vector2Int>();
        private readonly Dictionary<TileBase, TileBase> resolutionTileCache = new Dictionary<TileBase, TileBase>();
        private readonly Dictionary<Sprite, Sprite> resolutionSpriteCache = new Dictionary<Sprite, Sprite>();
        private readonly List<Tile> resolutionTiles = new List<Tile>();
        private readonly List<Sprite> resolutionSprites = new List<Sprite>();
        private readonly List<Texture2D> resolutionTextures = new List<Texture2D>();
        private Tilemap fogTilemap;
        private Tilemap fogMemoryTilemap;
        private Tilemap targetTilemap;
        private Tilemap rememberedConstructionTilemap;
        private Tile fogTile;
        private Tile targetTile;
        private MapData fogMap;
        private TileBase[] rememberedTerrain;
        private Color[] rememberedColors;
        private byte[] renderedFog;

        public int PixelsPerTile => Enum.IsDefined(typeof(MapTilePixelResolution), pixelResolution)
            ? (int)pixelResolution : 16;

        public void RenderFog(MapData map)
        {
            if (map == null)
                throw new ArgumentNullException(nameof(map));
            if (!map.Fog.Enabled)
            {
                if (fogTilemap != null) fogTilemap.ClearAllTiles();
                if (fogMemoryTilemap != null) fogMemoryTilemap.ClearAllTiles();
                if (rememberedConstructionTilemap != null) rememberedConstructionTilemap.ClearAllTiles();
                fogMap = null;
                return;
            }
            if (fogTilemap == null)
            {
                var layer = new GameObject("Fog of War", typeof(Tilemap), typeof(TilemapRenderer));
                layer.transform.SetParent(transform, false);
                fogTilemap = layer.GetComponent<Tilemap>();
                layer.GetComponent<TilemapRenderer>().sortingOrder = 50;
                layer.GetComponent<TilemapRenderer>().sortingLayerID = GetComponent<TilemapRenderer>().sortingLayerID;
                var memory = new GameObject("Explored Terrain", typeof(Tilemap), typeof(TilemapRenderer));
                memory.transform.SetParent(transform, false);
                fogMemoryTilemap = memory.GetComponent<Tilemap>();
                memory.GetComponent<TilemapRenderer>().sortingOrder = 51;
                memory.GetComponent<TilemapRenderer>().sortingLayerID = GetComponent<TilemapRenderer>().sortingLayerID;
                var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                texture.SetPixel(0, 0, Color.black);
                texture.Apply();
                fogTile = ScriptableObject.CreateInstance<Tile>();
                fogTile.name = "Unexplored";
                fogTile.sprite = Sprite.Create(texture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
                fogTile.flags = TileFlags.None;
                fogTile.colliderType = Tile.ColliderType.None;
            }
            if (fogMap != map)
            {
                fogMap = map;
                int count = map.Width * map.Height;
                rememberedTerrain = new TileBase[count];
                rememberedColors = new Color[count];
                renderedFog = new byte[count];
                var tiles = new TileBase[count];
                for (int index = 0; index < count; index++) tiles[index] = fogTile;
                fogTilemap.ClearAllTiles();
                fogMemoryTilemap.ClearAllTiles();
                fogTilemap.SetTilesBlock(new BoundsInt(0, 0, 0, map.Width, map.Height, 1), tiles);
            }
            for (int y = 0; y < map.Height; y++)
                for (int x = 0; x < map.Width; x++)
                {
                    var cell = new Vector2Int(x, y);
                    var position = new Vector3Int(x, y, 0);
                    int index = y * map.Width + x;
                    byte state = map.Fog.IsVisible(cell) ? (byte)2 : map.Fog.IsExplored(cell) ? (byte)1 : (byte)0;
                    if (state == 2)
                    {
                        rememberedTerrain[index] = Tilemap.GetTile(position);
                        rememberedColors[index] = Tilemap.GetColor(position);
                    }
                    if (state == renderedFog[index])
                        continue;
                    renderedFog[index] = state;
                    fogTilemap.SetTile(position, state == 2 ? null : fogTile);
                    fogMemoryTilemap.SetTile(position, state == 1 ? rememberedTerrain[index] : null);
                    if (state == 1 && rememberedTerrain[index] != null)
                    {
                        Color tint = rememberedColors[index] * 0.3f;
                        tint.a = 1f;
                        fogMemoryTilemap.SetTileFlags(position, TileFlags.None);
                        fogMemoryTilemap.SetColor(position, tint);
                    }
                }
                    RenderRememberedConstruction(map);
        }
        private Tilemap buildingTilemap;
        private readonly Tile[] buildingTiles = new Tile[4];
        private readonly Tile[] mountainTerrainTiles = new Tile[17];
        private Tilemap specializedRoomTilemap;
        private Tile specializedRoomTile;
        private GameObject specializedRoomLabelsRoot;
        private MapData specializedRoomLabelMap;
        private readonly Dictionary<SpecializedRoom, TextMesh> specializedRoomLabels =
            new Dictionary<SpecializedRoom, TextMesh>();

        private Tile GetMountainTerrainTile(int index)
        {
            if (mountainTerrainTiles[index] != null)
                return mountainTerrainTiles[index];
            bool mountain = index == 0;
            int edges = index - 1;
            var pixels = new Color32[256];
            for (int y = 0; y < 16; y++)
                for (int x = 0; x < 16; x++)
                {
                    bool grain = (x * 17 + y * 11) % 13 < 3;
                    bool wall = !mountain && (((edges & 1) != 0 && x < 3)
                        || ((edges & 2) != 0 && x >= 13) || ((edges & 4) != 0 && y < 3)
                        || ((edges & 8) != 0 && y >= 13));
                    pixels[y * 16 + x] = mountain || wall
                        ? (grain ? new Color32(64, 68, 63, 255) : new Color32(43, 47, 44, 255))
                        : (grain ? new Color32(114, 102, 81, 255) : new Color32(88, 78, 62, 255));
                    if (!mountain && wall && (x == 2 || x == 13 || y == 2 || y == 13))
                        pixels[y * 16 + x] = new Color32(26, 29, 27, 255);
                }
            Texture2D texture = CreatePixelTexture(pixels, 16, 16);
            var tile = ScriptableObject.CreateInstance<Tile>();
            tile.name = mountain ? "Montagna" : "Galleria";
            tile.sprite = CreatePixelSprite(texture, tile.name);
            tile.flags = TileFlags.None;
            tile.colliderType = Tile.ColliderType.None;
            mountainTerrainTiles[index] = tile;
            return tile;
        }

        private Tile GetTunnelTile(MapData map, Vector2Int cell)
        {
            int edges = (map.IsMineable(cell + Vector2Int.left) ? 1 : 0)
                | (map.IsMineable(cell + Vector2Int.right) ? 2 : 0)
                | (map.IsMineable(cell + Vector2Int.down) ? 4 : 0)
                | (map.IsMineable(cell + Vector2Int.up) ? 8 : 0);
            return GetMountainTerrainTile(edges + 1);
        }
        private Tilemap roofTilemap;
        private Tile roofTile;
        private MapData roofMap;
        private BuildingRoom hiddenRoof;
        private Camera roofCamera;

        public void RenderBuildings(MapData map)
        {
            if (buildingTilemap == null)
            {
                var layer = new GameObject("Buildings", typeof(Tilemap), typeof(TilemapRenderer));
                layer.transform.SetParent(transform, false);
                buildingTilemap = layer.GetComponent<Tilemap>();
                layer.GetComponent<TilemapRenderer>().sortingOrder = 2;
            }
            buildingTilemap.ClearAllTiles();
            foreach (BuildingRoom room in map.Buildings)
                for (int y = room.Bounds.yMin; y < room.Bounds.yMax; y++)
                    for (int x = room.Bounds.xMin; x < room.Bounds.xMax; x++)
                    {
                        var cell = new Vector2Int(x, y);
                        if (!room.IsPerimeter(cell))
                            continue;
                        bool door = room.Door == cell;
                        int index = (room.Material == ItemType.Wood ? 0 : 2) + (door ? 1 : 0);
                        if (buildingTiles[index] == null)
                            buildingTiles[index] = CreateBuildingTile(room.Material, door);
                        var position = new Vector3Int(x, y, 0);
                        buildingTilemap.SetTile(position, buildingTiles[index]);
                        buildingTilemap.SetColor(position, room.IsBuilt(cell) && (!door || room.DoorBuilt)
                            ? Color.white : new Color(0.55f, 0.85f, 1f, 0.4f));
                    }
            foreach (TunnelDoor door in map.TunnelDoors)
            {
                if (buildingTiles[1] == null)
                    buildingTiles[1] = CreateBuildingTile(ItemType.Wood, true);
                var position = new Vector3Int(door.Cell.x, door.Cell.y, 0);
                buildingTilemap.SetTile(position, buildingTiles[1]);
                buildingTilemap.SetColor(position, door.IsBuilt ? Color.white : new Color(0.55f, 0.85f, 1f, 0.4f));
            }
            RenderRememberedConstruction(map);
            RenderSpecializedRooms(map);
            RenderRoofs(map);
        }

        private void RenderRememberedConstruction(MapData map)
        {
            if (rememberedConstructionTilemap == null)
            {
                var layer = new GameObject("Remembered Construction", typeof(Tilemap), typeof(TilemapRenderer));
                layer.transform.SetParent(transform, false);
                rememberedConstructionTilemap = layer.GetComponent<Tilemap>();
                var renderer = layer.GetComponent<TilemapRenderer>();
                renderer.sortingOrder = 52;
                renderer.sortingLayerID = GetComponent<TilemapRenderer>().sortingLayerID;
            }
            rememberedConstructionTilemap.ClearAllTiles();
            if (!map.Fog.Enabled)
                return;

            foreach (BuildingRoom room in map.Buildings)
                for (int y = room.Bounds.yMin; y < room.Bounds.yMax; y++)
                    for (int x = room.Bounds.xMin; x < room.Bounds.xMax; x++)
                    {
                        var cell = new Vector2Int(x, y);
                        if (!room.IsPerimeter(cell) || room.IsBuilt(cell)
                            || !map.Fog.IsExplored(cell) || map.Fog.IsVisible(cell))
                            continue;
                        bool door = room.Door == cell;
                        int index = (room.Material == ItemType.Wood ? 0 : 2) + (door ? 1 : 0);
                        if (buildingTiles[index] == null)
                            buildingTiles[index] = CreateBuildingTile(room.Material, door);
                        var position = new Vector3Int(x, y, 0);
                        rememberedConstructionTilemap.SetTile(position, buildingTiles[index]);
                        rememberedConstructionTilemap.SetTileFlags(position, TileFlags.None);
                        rememberedConstructionTilemap.SetColor(position, new Color(0.55f, 0.85f, 1f, 0.65f));
                    }
        }

        public void RenderSpecializedRooms(MapData map)
        {
            if (map == null)
                throw new ArgumentNullException(nameof(map));
            if (specializedRoomTilemap == null)
            {
                var layer = new GameObject("Specialized Rooms", typeof(Tilemap), typeof(TilemapRenderer));
                layer.transform.SetParent(transform, false);
                specializedRoomTilemap = layer.GetComponent<Tilemap>();
                var renderer = layer.GetComponent<TilemapRenderer>();
                renderer.sortingOrder = 1;
                renderer.sortingLayerID = GetComponent<TilemapRenderer>().sortingLayerID;
            }
            if (specializedRoomTile == null)
            {
                var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                texture.SetPixel(0, 0, Color.white);
                texture.Apply();
                specializedRoomTile = ScriptableObject.CreateInstance<Tile>();
                specializedRoomTile.name = "Specialized Room";
                specializedRoomTile.sprite = Sprite.Create(texture, new Rect(0, 0, 1, 1),
                    new Vector2(0.5f, 0.5f), 1f);
                specializedRoomTile.flags = TileFlags.None;
                specializedRoomTile.colliderType = Tile.ColliderType.None;
            }
            if (specializedRoomLabelMap != map)
            {
                if (specializedRoomLabelsRoot != null)
                    DestroyGeneratedObject(specializedRoomLabelsRoot);
                specializedRoomLabels.Clear();
                specializedRoomLabelsRoot = new GameObject("Specialized Room Labels");
                specializedRoomLabelsRoot.transform.SetParent(transform, false);
                specializedRoomLabelMap = map;
            }

            specializedRoomTilemap.ClearAllTiles();
            foreach (SpecializedRoom room in map.SpecializedRooms)
            {
                for (int y = room.Bounds.yMin; y < room.Bounds.yMax; y++)
                    for (int x = room.Bounds.xMin; x < room.Bounds.xMax; x++)
                    {
                        var position = new Vector3Int(x, y, 0);
                        specializedRoomTilemap.SetTile(position, specializedRoomTile);
                        specializedRoomTilemap.SetTileFlags(position, TileFlags.None);
                        specializedRoomTilemap.SetColor(position, SpecializationColor(room.Specialization));
                    }
                RenderSpecializedRoomLabel(room);
            }
        }

        private void RenderSpecializedRoomLabel(SpecializedRoom room)
        {
            if (!specializedRoomLabels.TryGetValue(room, out TextMesh label) || label == null)
            {
                var labelObject = new GameObject("Room label - " + room.Specialization, typeof(TextMesh));
                labelObject.transform.SetParent(specializedRoomLabelsRoot.transform, false);
                label = labelObject.GetComponent<TextMesh>();
                label.anchor = TextAnchor.UpperLeft;
                label.alignment = TextAlignment.Left;
                label.fontSize = 32;
                label.fontStyle = FontStyle.Bold;
                label.color = new Color(0.97f, 0.97f, 0.91f);
                MeshRenderer renderer = label.GetComponent<MeshRenderer>();
                renderer.sortingLayerID = GetComponent<TilemapRenderer>().sortingLayerID;
                renderer.sortingOrder = 21;
                specializedRoomLabels[room] = label;
            }

            string text = RoomSpecializationLabel(room.Specialization);
            label.text = text;
            label.characterSize = Mathf.Min(0.12f,
                (room.Bounds.width - 0.12f) / Mathf.Max(1f, text.Length * 0.55f));
            Vector3 topLeft = Tilemap.CellToWorld(new Vector3Int(room.Bounds.xMin, room.Bounds.yMax, 0));
            label.transform.position = topLeft + new Vector3(0.08f, -0.08f, 0f);
        }

        private static string RoomSpecializationLabel(RoomSpecialization specialization)
        {
            switch (specialization)
            {
                case RoomSpecialization.Command: return "COMANDO";
                case RoomSpecialization.TelegraphStation: return "STAZIONE TELEGRAFO";
                case RoomSpecialization.Kitchen: return "CUCINA";
                case RoomSpecialization.Armory: return "ARMERIA";
                case RoomSpecialization.Shelter: return "RICOVERO";
                case RoomSpecialization.Hospital: return "OSPEDALE";
                case RoomSpecialization.Warehouse: return "MAGAZZINO";
                default: throw new ArgumentOutOfRangeException(nameof(specialization));
            }
        }

        public static Color SpecializationColor(RoomSpecialization specialization)
        {
            switch (specialization)
            {
                case RoomSpecialization.Command: return new Color(0.25f, 0.55f, 0.78f, 0.48f);
                case RoomSpecialization.TelegraphStation: return new Color(0.35f, 0.78f, 0.80f, 0.48f);
                case RoomSpecialization.Kitchen: return new Color(0.88f, 0.56f, 0.22f, 0.48f);
                case RoomSpecialization.Armory: return new Color(0.78f, 0.27f, 0.23f, 0.48f);
                case RoomSpecialization.Shelter: return new Color(0.56f, 0.68f, 0.49f, 0.48f);
                case RoomSpecialization.Hospital: return new Color(0.83f, 0.86f, 0.72f, 0.48f);
                case RoomSpecialization.Warehouse: return new Color(0.86f, 0.74f, 0.32f, 0.48f);
                default: throw new ArgumentOutOfRangeException(nameof(specialization));
            }
        }

        private void RenderRoofs(MapData map)
        {
            roofMap = map;
            hiddenRoof = null;
            if (roofTilemap == null)
            {
                var layer = new GameObject("Roofs", typeof(Tilemap), typeof(TilemapRenderer));
                layer.transform.SetParent(transform, false);
                roofTilemap = layer.GetComponent<Tilemap>();
                layer.GetComponent<TilemapRenderer>().sortingOrder = 20;
            }
            roofTilemap.ClearAllTiles();
            foreach (BuildingRoom room in map.Buildings)
            {
                if (!room.IsComplete || room.IsStandaloneWall)
                    continue;
                if (roofTile == null)
                    roofTile = CreateRoofTile();
                for (int y = room.Bounds.yMin; y < room.Bounds.yMax; y++)
                    for (int x = room.Bounds.xMin; x < room.Bounds.xMax; x++)
                        roofTilemap.SetTile(new Vector3Int(x, y, 0), roofTile);
            }
            RefreshRoofPointer();
        }

        private Tile CreateRoofTile()
        {
            var pixels = new Color32[256];
            for (int y = 0; y < 16; y++)
                for (int x = 0; x < 16; x++)
                    pixels[y * 16 + x] = x % 8 == 0 || y == 0
                        ? new Color32(53, 65, 62, 255)
                        : x % 8 == 1 ? new Color32(143, 160, 150, 255)
                        : new Color32(100, 121, 111, 255);
            Texture2D texture = CreatePixelTexture(pixels, 16, 16);
            var tile = ScriptableObject.CreateInstance<Tile>();
            tile.name = "Roof";
            tile.sprite = CreatePixelSprite(texture, tile.name);
            tile.flags = TileFlags.None;
            tile.colliderType = Tile.ColliderType.None;
            return tile;
        }

        private void LateUpdate()
        {
            RefreshRoofPointer();
        }

        private void RefreshRoofPointer()
        {
            if (roofCamera == null || !roofCamera.isActiveAndEnabled)
                roofCamera = Camera.main;
            Mouse mouse = Mouse.current;
            Vector2? pointer = mouse != null && (EventSystem.current == null
                || !EventSystem.current.IsPointerOverGameObject()) ? mouse.position.ReadValue() : (Vector2?)null;
            UpdateRoofVisibility(roofCamera, pointer);
        }

        public void UpdateRoofVisibility(Camera camera, Vector2? pointer)
        {
            BuildingRoom hovered = null;
            if (roofMap != null && camera != null && pointer.HasValue && camera.pixelRect.Contains(pointer.Value))
            {
                Vector3 world = camera.ScreenToWorldPoint(new Vector3(pointer.Value.x, pointer.Value.y,
                    transform.position.z - camera.transform.position.z));
                Vector3Int position = Tilemap.WorldToCell(world);
                BuildingRoom room = roofMap.GetBuilding(new Vector2Int(position.x, position.y));
                if (room != null && room.IsComplete && roofMap.Fog.IsVisible(new Vector2Int(position.x, position.y)))
                    hovered = room;
            }
            if (hiddenRoof == hovered)
                return;
            SetRoofColor(hiddenRoof, Color.white);
            hiddenRoof = hovered;
            SetRoofColor(hiddenRoof, Color.clear);
        }

        private void SetRoofColor(BuildingRoom room, Color color)
        {
            if (room == null || roofTilemap == null)
                return;
            for (int y = room.Bounds.yMin; y < room.Bounds.yMax; y++)
                for (int x = room.Bounds.xMin; x < room.Bounds.xMax; x++)
                    roofTilemap.SetColor(new Vector3Int(x, y, 0), color);
        }

        private void OnDisable()
        {
            UpdateRoofVisibility(null, null);
        }

        private Tile CreateBuildingTile(ItemType material, bool door)
        {
            var pixels = new Color32[256];
            bool wood = material == ItemType.Wood;
            for (int y = 0; y < 16; y++)
                for (int x = 0; x < 16; x++)
                {
                    bool seam = wood ? x % 4 == 0 : y % 4 == 0 || (x + (y / 4 % 2) * 4) % 8 == 0;
                    Color32 color = wood
                        ? (seam ? new Color32(72, 46, 27, 255) : new Color32(168, 116, 65, 255))
                        : (seam ? new Color32(65, 69, 65, 255) : new Color32(165, 172, 160, 255));
                    if (door && x >= 3 && x <= 12)
                        color = x == 3 || x == 12 || y == 0 || y == 15
                            ? new Color32(40, 43, 38, 255) : new Color32(88, 135, 112, 255);
                    if (door && x == 10 && y == 8)
                        color = new Color32(245, 215, 105, 255);
                    pixels[y * 16 + x] = color;
                }
            Texture2D texture = CreatePixelTexture(pixels, 16, 16);
            var tile = ScriptableObject.CreateInstance<Tile>();
            tile.name = (wood ? "Wood" : "Stone") + (door ? "Door" : "Wall");
            tile.sprite = CreatePixelSprite(texture, tile.name);
            tile.flags = TileFlags.None;
            tile.colliderType = Tile.ColliderType.None;
            return tile;
        }
        private GameObject treeLayer;
        private Tilemap treeTilemap;
        private Tile runtimeTreeTile;
        private Sprite runtimeTreeSprite;
        private Texture2D runtimeTreeTexture;
        private readonly Tile[] runtimeGroundItemTiles = new Tile[Enum.GetValues(typeof(ItemType)).Length];
        private MapData stockpileMap;
        private GameObject storageItemsRoot;
        private readonly GameObject[] storageItemDisplays = new GameObject[Enum.GetValues(typeof(ItemType)).Length];
        private readonly Sprite[] runtimeItemSprites = new Sprite[Enum.GetValues(typeof(ItemType)).Length];
        private readonly Texture2D[] runtimeItemTextures = new Texture2D[Enum.GetValues(typeof(ItemType)).Length];
        [SerializeField] private TileBase dirtTile;
        [SerializeField] private TileBase grassTile;
        [SerializeField] private TileBase mudTile;
        [SerializeField] private TileBase rockTile;
        [SerializeField] private TileBase gravelTile;
        [SerializeField] private TileBase treeTile;
        [SerializeField] private TileBase trenchTile;
        [SerializeField] private TileBase ladderTile;
        [SerializeField] private TileBase roadTile;

        public Bounds WorldBounds { get; private set; }
        public Tilemap Tilemap => GetComponent<Tilemap>();

        public void Configure(TileBase dirt, TileBase grass, TileBase mud)
        {
            dirtTile = dirt;
            grassTile = grass;
            mudTile = mud;
        }

        public void ConfigureTrench(TileBase trench)
        {
            trenchTile = trench;
        }

        public void ConfigureRoad(TileBase road)
        {
            roadTile = road;
        }

        public void ConfigureRock(TileBase rock)
        {
            rockTile = rock;
        }

        public void ConfigureGravel(TileBase gravel)
        {
            gravelTile = gravel;
        }

        public void ConfigureLadder(TileBase ladder)
        {
            ladderTile = ladder;
        }

        public void CompleteLadder(MapData map, Vector2Int cell)
        {
            if (map == null)
                throw new ArgumentNullException(nameof(map));
            if (ladderTile == null)
                throw new InvalidOperationException("Assign a ladder tile before installing ladders.");
            map.AddLadder(cell);
            Tilemap.SetTile(new Vector3Int(cell.x, cell.y, 0), ResolveTile(ladderTile));
        }

        public void CompleteTrench(MapData map, Vector2Int cell)
        {
            if (map == null)
                throw new ArgumentNullException(nameof(map));
            if (!map.IsWalkable(cell) || map.IsTunnel(cell))
                throw new InvalidOperationException("A trench requires a walkable cell.");
            if (map.IsStorageCell(cell) || map.IsPantryCell(cell))
                throw new InvalidOperationException("Storage areas cannot be excavated.");
            if (trenchTile == null)
                throw new InvalidOperationException("Assign a trench tile before digging.");
            map[cell.x, cell.y] = TerrainKind.Trench;
            Tilemap.SetTile(new Vector3Int(cell.x, cell.y, 0), ResolveTile(trenchTile));
        }

        public void CompleteTreeCut(MapData map, Vector2Int cell)
        {
            if (map == null)
                throw new ArgumentNullException(nameof(map));
            if (!map.HasTree(cell))
                throw new InvalidOperationException("A tree must exist before it can be cut.");
            map.SetTree(cell, false);
            map.AddGroundItem(cell, ItemType.Wood);
            RenderTrees(map);
        }

        public void RefreshGroundItems(MapData map)
        {
            if (map == null)
                throw new ArgumentNullException(nameof(map));
            RenderTrees(map);
        }

        public void CompleteRockMine(MapData map, Vector2Int cell)
        {
            if (map == null)
                throw new ArgumentNullException(nameof(map));
            if (!map.IsMineable(cell))
                throw new InvalidOperationException("Rock must exist before it can be mined.");
            bool mountain = map[cell.x, cell.y] == TerrainKind.Mountain;
            if (!mountain && dirtTile == null)
                throw new InvalidOperationException("Assign a dirt tile before mining rock.");
            map[cell.x, cell.y] = mountain ? TerrainKind.Tunnel : TerrainKind.Dirt;
            map.AddGroundItem(cell, ItemType.BuildingStone);
            var position = new Vector3Int(cell.x, cell.y, 0);
            Tilemap.SetTile(position, mountain ? GetTunnelTile(map, cell) : ResolveTile(dirtTile));
            Tilemap.SetTileFlags(position, TileFlags.None);
            Tilemap.SetColor(position, Color.white);
            foreach (Vector2Int offset in new[] { Vector2Int.left, Vector2Int.right, Vector2Int.down, Vector2Int.up })
            {
                Vector2Int neighbour = cell + offset;
                if (map.IsTunnel(neighbour))
                    Tilemap.SetTile(new Vector3Int(neighbour.x, neighbour.y, 0), GetTunnelTile(map, neighbour));
            }
            RenderTrees(map);
        }

        public void Render(MapData map)
        {
            if (map == null)
                throw new ArgumentNullException(nameof(map));
            BindStockpiles(map);
            if (dirtTile == null || grassTile == null || mudTile == null)
                throw new InvalidOperationException("Assign all three terrain tiles to the MapTilemapView.");

            var tiles = new TileBase[map.Width * map.Height];
            for (int y = 0; y < map.Height; y++)
            {
                for (int x = 0; x < map.Width; x++)
                {
                    switch (map[x, y])
                    {
                        case TerrainKind.Dirt: tiles[y * map.Width + x] = ResolveTile(dirtTile); break;
                        case TerrainKind.Grass: tiles[y * map.Width + x] = ResolveTile(grassTile); break;
                        case TerrainKind.Mud: tiles[y * map.Width + x] = ResolveTile(mudTile); break;
                        case TerrainKind.Mountain: tiles[y * map.Width + x] = GetMountainTerrainTile(0); break;
                        case TerrainKind.Tunnel: tiles[y * map.Width + x] = GetTunnelTile(map, new Vector2Int(x, y)); break;
                        case TerrainKind.Rock:
                            if (rockTile == null)
                                throw new InvalidOperationException("Assign a rock tile before rendering rocks.");
                            tiles[y * map.Width + x] = ResolveTile(rockTile);
                            break;
                        case TerrainKind.Gravel:
                            if (gravelTile == null)
                                throw new InvalidOperationException("Assign a gravel tile before rendering gravel.");
                            tiles[y * map.Width + x] = ResolveTile(gravelTile);
                            break;
                        case TerrainKind.Road:
                            if (roadTile == null)
                                throw new InvalidOperationException("Assign a road tile before rendering roads.");
                            tiles[y * map.Width + x] = ResolveTile(roadTile);
                            break;
                        case TerrainKind.Trench:
                            if (trenchTile == null)
                                throw new InvalidOperationException("Assign a trench tile before rendering trenches.");
                            if (map.HasLadder(new Vector2Int(x, y)) && ladderTile == null)
                                throw new InvalidOperationException("Assign a ladder tile before rendering ladders.");
                            tiles[y * map.Width + x] = map.HasLadder(new Vector2Int(x, y))
                                ? ResolveTile(ladderTile) : ResolveTile(trenchTile);
                            break;
                        default: throw new ArgumentOutOfRangeException(nameof(map), "Unknown terrain kind.");
                    }
                }
            }

            Tilemap tilemap = Tilemap;
            tilemap.ClearAllTiles();
            renderedZoneCells.Clear();
            tilemap.SetTilesBlock(new BoundsInt(0, 0, 0, map.Width, map.Height, 1), tiles);
            tilemap.CompressBounds();
            for (int y = 0; y < map.Height; y++)
                for (int x = 0; x < map.Width; x++)
                    if (!map.IsWalkable(new Vector2Int(x, y)))
                        RefreshWalkability(map, new Vector2Int(x, y));

            var bounds = new Bounds(tilemap.CellToWorld(Vector3Int.zero), Vector3.zero);
            bounds.Encapsulate(tilemap.CellToWorld(new Vector3Int(map.Width, 0, 0)));
            bounds.Encapsulate(tilemap.CellToWorld(new Vector3Int(0, map.Height, 0)));
            bounds.Encapsulate(tilemap.CellToWorld(new Vector3Int(map.Width, map.Height, 0)));
            WorldBounds = bounds;
            RenderZones(map);
            RenderStorageArea(map);
            RenderTrees(map);
            RenderTargets(map);
            RenderBuildings(map);
            RenderFog(map);
        }

        public void RenderTargets(MapData map)
        {
            if (map == null)
                throw new ArgumentNullException(nameof(map));
            if (targetTilemap == null)
            {
                var layer = new GameObject("Target Markers", typeof(Tilemap), typeof(TilemapRenderer));
                layer.transform.SetParent(transform, false);
                targetTilemap = layer.GetComponent<Tilemap>();
                TilemapRenderer renderer = layer.GetComponent<TilemapRenderer>();
                renderer.sortingOrder = 8;
                renderer.sortingLayerID = GetComponent<TilemapRenderer>().sortingLayerID;
            }
            if (targetTile == null)
            {
                int size = PixelsPerTile;
                var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
                var pixels = new Color32[size * size];
                int center = size / 2;
                float scale = size / 32f;
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                    {
                        int dx = x - center;
                        int dy = y - center;
                        float distance = dx * dx + dy * dy;
                        bool crosshair = (Mathf.Abs(dx) <= scale && Mathf.Abs(dy) >= 10f * scale
                                && Mathf.Abs(dy) <= 15f * scale)
                            || (Mathf.Abs(dy) <= scale && Mathf.Abs(dx) >= 10f * scale
                                && Mathf.Abs(dx) <= 15f * scale);
                        Color32 color = default;
                        if (distance >= 38f * scale * scale && distance <= 78f * scale * scale)
                            color = distance >= 68f * scale * scale
                                ? new Color32(35, 35, 31, 255) : new Color32(220, 62, 48, 255);
                        if (crosshair)
                            color = new Color32(242, 222, 186, 255);
                        if (distance <= 8f * scale * scale)
                            color = new Color32(242, 222, 186, 255);
                        pixels[y * size + x] = color;
                    }
                texture.SetPixels32(pixels);
                texture.Apply();
                targetTile = ScriptableObject.CreateInstance<Tile>();
                targetTile.name = "Target Marker";
                targetTile.sprite = Sprite.Create(texture, new Rect(0, 0, size, size),
                    new Vector2(0.5f, 0.5f), size);
                targetTile.flags = TileFlags.None;
                targetTile.colliderType = Tile.ColliderType.None;
            }
            targetTilemap.ClearAllTiles();
            foreach (Vector2Int cell in map.TargetCells)
                targetTilemap.SetTile(new Vector3Int(cell.x, cell.y, 0), targetTile);
        }

        public void ShowTargetShot(Vector2Int cell, bool hit)
        {
            if (targetTilemap == null)
                return;
            var position = new Vector3Int(cell.x, cell.y, 0);
            if (!targetTilemap.HasTile(position))
                return;
            targetTilemap.SetTileFlags(position, TileFlags.None);
            targetTilemap.SetColor(position, hit
                ? new Color(0.55f, 1f, 0.62f) : new Color(1f, 0.72f, 0.35f));
            StartCoroutine(RestoreTargetColor(position));
        }

        private System.Collections.IEnumerator RestoreTargetColor(Vector3Int position)
        {
            yield return new WaitForSeconds(0.18f);
            if (targetTilemap != null && targetTilemap.HasTile(position))
            {
                targetTilemap.SetTileFlags(position, TileFlags.None);
                targetTilemap.SetColor(position, Color.white);
            }
        }

        private void RenderStorageArea(MapData map)
        {
            if (!map.HasStorageArea && !map.HasPantryArea)
            {
                if (storageItemsRoot != null)
                    storageItemsRoot.SetActive(false);
                return;
            }

            if (map.HasStorageArea)
                TintArea(map.StorageOrigin, map.StorageWidth, map.StorageHeight,
                    Color.Lerp(Color.white, new Color(0.76f, 0.66f, 0.38f), 0.45f));
            if (map.HasPantryArea)
                TintArea(map.PantryOrigin, map.PantryWidth, map.PantryHeight,
                    Color.Lerp(Color.white, new Color(0.42f, 0.65f, 0.38f), 0.4f));

            RenderStorageItems(map);
    }

        private void TintArea(Vector2Int origin, int width, int height, Color tint)
        {
            for (int y = origin.y; y < origin.y + height; y++)
                for (int x = origin.x; x < origin.x + width; x++)
                {
                    var position = new Vector3Int(x, y, 0);
                    Tilemap.SetTileFlags(position, TileFlags.None);
                    Tilemap.SetColor(position, tint);
                }
        }

        private void BindStockpiles(MapData map)
        {
            if (stockpileMap == map)
                return;
            if (stockpileMap != null)
            {
                stockpileMap.StorageStockpile.Changed -= RefreshStorageItems;
                stockpileMap.PantryStockpile.Changed -= RefreshStorageItems;
            }
            stockpileMap = map;
            stockpileMap.StorageStockpile.Changed += RefreshStorageItems;
            stockpileMap.PantryStockpile.Changed += RefreshStorageItems;
        }

        private void RefreshStorageItems()
        {
            if (stockpileMap != null)
                RenderStorageItems(stockpileMap);
        }

        private void RenderStorageItems(MapData map)
        {
            if (storageItemsRoot == null)
            {
                storageItemsRoot = new GameObject("Depot stockpile items");
                storageItemsRoot.transform.SetParent(transform, false);
            }
            storageItemsRoot.SetActive(true);

            ItemType[] types =
                { ItemType.Shovel, ItemType.Axe, ItemType.Pickaxe, ItemType.Rations, ItemType.Wood, ItemType.BuildingStone,
                    ItemType.ConstructionHammer };
            Vector2Int[] depotSlots =
            {
                new Vector2Int(0, 2), new Vector2Int(1, 2), new Vector2Int(2, 2),
                Vector2Int.zero, new Vector2Int(0, 1), new Vector2Int(1, 1), new Vector2Int(3, 2)
            };
            for (int i = 0; i < types.Length; i++)
            {
                bool isFood = types[i] == ItemType.Rations;
                ItemStockpile stockpile = isFood ? map.PantryStockpile : map.StorageStockpile;
                int count = stockpile.Count(types[i]);
                if (count == 0)
                {
                    if (storageItemDisplays[i] != null)
                        storageItemDisplays[i].SetActive(false);
                    continue;
                }

                int itemIndex = (int)types[i];
                if (runtimeItemSprites[itemIndex] == null)
                    runtimeItemSprites[itemIndex] = CreateItemSprite(types[i], itemIndex);
                if (storageItemDisplays[i] == null)
                    storageItemDisplays[i] = CreateStorageItemDisplay(types[i]);
                GameObject display = storageItemDisplays[i];
                display.SetActive(true);
                SpriteRenderer renderer = display.GetComponent<SpriteRenderer>();
                renderer.sprite = runtimeItemSprites[itemIndex];
                Vector2Int cell = isFood
                    ? map.PantryOrigin + new Vector2Int(0, 2)
                    : map.StorageOrigin + depotSlots[i];
                display.transform.position = Tilemap.GetCellCenterWorld(new Vector3Int(cell.x, cell.y, 0));
                display.transform.localScale = Vector3.one * 0.8f;
                display.transform.GetChild(0).localPosition = new Vector3(0f, -0.58f, 0f);
                string quantity = isFood ? GetStackSummary(stockpile, types[i]) : $"x{count}";
                display.GetComponentInChildren<TextMesh>().text = types[i] == ItemType.ConstructionHammer
                    ? $"Martello\n{quantity}" : $"{ItemCatalog.Get(types[i]).DisplayName} {quantity}";
            }
        }

        private static string GetStackSummary(ItemStockpile stockpile, ItemType type)
        {
            IReadOnlyList<ItemStack> stacks = stockpile.GetStacks(type);
            string summary = string.Empty;
            for (int i = 0; i < stacks.Count; i++)
                summary += (i == 0 ? string.Empty : "+") + stacks[i].Count;
            return $"{stockpile.Count(type)} ({summary})";
        }

        private GameObject CreateStorageItemDisplay(ItemType type)
        {
            var display = new GameObject(type.ToString(), typeof(SpriteRenderer));
            display.transform.SetParent(storageItemsRoot.transform, false);
            display.GetComponent<SpriteRenderer>().sortingOrder = 5;
            var labelObject = new GameObject("Count", typeof(TextMesh));
            labelObject.transform.SetParent(display.transform, false);
            TextMesh label = labelObject.GetComponent<TextMesh>();
            label.anchor = TextAnchor.MiddleCenter;
            label.alignment = TextAlignment.Center;
            label.fontSize = 32;
            label.characterSize = 0.055f;
            label.color = new Color(0.95f, 0.94f, 0.83f);
            label.GetComponent<MeshRenderer>().sortingOrder = 6;
            return display;
        }

        private Sprite CreateItemSprite(ItemType type, int index)
        {
            var pixels = new Color32[256];
            Color32 wood = new Color32(112, 73, 39, 255);
            Color32 woodLight = new Color32(170, 119, 61, 255);
            Color32 metal = new Color32(190, 197, 190, 255);
            Color32 metalDark = new Color32(91, 105, 108, 255);

            switch (type)
            {
                case ItemType.Shovel:
                    for (int y = 2; y <= 11; y++)
                    {
                        int x = 6 + (y - 2) / 2;
                        SetPixel(pixels, x, y, wood);
                        SetPixel(pixels, x + 1, y, woodLight);
                    }
                    for (int y = 12; y <= 14; y++)
                        for (int x = 4 + (y - 12); x <= 10 - (y - 12); x++)
                            SetPixel(pixels, x, y, x == 4 + (y - 12) ? metalDark : metal);
                    break;
                case ItemType.Axe:
                    for (int y = 2; y <= 12; y++)
                    {
                        SetPixel(pixels, 7, y, wood);
                        SetPixel(pixels, 8, y, woodLight);
                    }
                    for (int y = 10; y <= 14; y++)
                        for (int x = 8; x <= 13 - Mathf.Abs(y - 12); x++)
                            SetPixel(pixels, x, y, y == 12 ? metal : metalDark);
                    break;
                case ItemType.Pickaxe:
                    for (int y = 2; y <= 11; y++)
                    {
                        int x = 7 + (y > 7 ? 1 : 0);
                        SetPixel(pixels, x, y, wood);
                        SetPixel(pixels, x + 1, y, woodLight);
                    }
                    for (int x = 2; x <= 13; x++)
                    {
                        SetPixel(pixels, x, 12, metalDark);
                        SetPixel(pixels, x, 13, metal);
                    }
                    SetPixel(pixels, 1, 14, metalDark);
                    SetPixel(pixels, 2, 14, metal);
                    SetPixel(pixels, 13, 14, metal);
                    SetPixel(pixels, 14, 14, metalDark);
                    break;
                case ItemType.ConstructionHammer:
                    for (int y = 2; y <= 11; y++)
                    {
                        SetPixel(pixels, 7, y, wood);
                        SetPixel(pixels, 8, y, woodLight);
                    }
                    for (int y = 10; y <= 14; y++)
                        for (int x = 3; x <= 12; x++)
                            SetPixel(pixels, x, y, y == 14 || x == 3 ? metal : metalDark);
                    break;
                case ItemType.Rations:
                    for (int y = 3; y <= 12; y++)
                        for (int x = 3; x <= 12; x++)
                            SetPixel(pixels, x, y, x == 3 || x == 12 || y == 3 || y == 12
                                ? new Color32(77, 66, 44, 255) : new Color32(190, 156, 99, 255));
                    for (int x = 4; x <= 11; x++)
                    {
                        SetPixel(pixels, x, 7, new Color32(106, 129, 71, 255));
                        SetPixel(pixels, x, 8, new Color32(106, 129, 71, 255));
                    }
                    SetPixel(pixels, 7, 9, new Color32(231, 216, 174, 255));
                    SetPixel(pixels, 8, 9, new Color32(231, 216, 174, 255));
                    break;
                case ItemType.Wood:
                    for (int y = 3; y <= 12; y += 4)
                        for (int x = 2; x <= 13; x++)
                        {
                            SetPixel(pixels, x, y, new Color32(82, 53, 31, 255));
                            SetPixel(pixels, x, y + 1, new Color32(150, 102, 55, 255));
                            SetPixel(pixels, x, y + 2, x == 2 || x == 13
                                ? new Color32(205, 166, 102, 255) : new Color32(82, 53, 31, 255));
                        }
                    break;
                case ItemType.BuildingStone:
                    for (int y = 3; y <= 12; y++)
                        for (int x = 3; x <= 12; x++)
                        {
                            bool edge = x == 3 || x == 12 || y == 3 || y == 12;
                            bool highlight = (x + y) % 4 == 0;
                            SetPixel(pixels, x, y, edge ? new Color32(65, 68, 67, 255)
                                : highlight ? new Color32(161, 160, 146, 255)
                                : new Color32(112, 115, 111, 255));
                        }
                    break;
                case ItemType.FullCanteen:
                case ItemType.EmptyCanteen:
                    for (int row = 3; row <= 10; row++)
                        for (int column = 4; column <= 11; column++)
                            SetPixel(pixels, column, row, column == 4 || column == 11 || row == 3
                                ? metalDark : metal);
                    for (int row = 11; row <= 13; row++)
                        for (int column = 6; column <= 9; column++)
                            SetPixel(pixels, column, row, row == 13 ? wood : metalDark);
                    for (int row = 5; row <= 8; row++)
                        for (int column = 6; column <= 9; column++)
                            SetPixel(pixels, column, row, type == ItemType.FullCanteen
                                ? new Color32(67, 135, 181, 255) : metalDark);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(type));
            }

            runtimeItemTextures[index] = CreatePixelTexture(pixels, 16, 16);
            runtimeItemTextures[index].name = ItemCatalog.Get(type).DisplayName + " Sprite";
            return CreatePixelSprite(runtimeItemTextures[index], runtimeItemTextures[index].name);
        }

        private Texture2D CreatePixelTexture(Color32[] sourcePixels, int sourceWidth, int sourceHeight)
        {
            int size = PixelsPerTile;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    int sourceX = Mathf.Min(sourceWidth - 1, x * sourceWidth / size);
                    int sourceY = Mathf.Min(sourceHeight - 1, y * sourceHeight / size);
                    pixels[y * size + x] = sourcePixels[sourceY * sourceWidth + sourceX];
                }
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.SetPixels32(pixels);
            texture.Apply();
            return texture;
        }

        private Sprite CreatePixelSprite(Texture2D texture, string spriteName)
        {
            int size = PixelsPerTile;
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, size, size),
                new Vector2(0.5f, 0.5f), size);
            sprite.name = spriteName;
            return sprite;
        }

        private TileBase ResolveTile(TileBase source)
        {
            if (source is not Tile tile || tile.sprite == null)
                return source;
            if (tile.sprite.rect.width == PixelsPerTile && tile.sprite.rect.height == PixelsPerTile
                && Mathf.Approximately(tile.sprite.pixelsPerUnit, PixelsPerTile))
                return source;
            if (resolutionTileCache.TryGetValue(source, out TileBase cached))
                return cached;

            Sprite sprite = ResolveSprite(tile.sprite);
            var variant = ScriptableObject.CreateInstance<Tile>();
            variant.name = tile.name;
            variant.sprite = sprite;
            variant.color = tile.color;
            variant.transform = tile.transform;
            variant.flags = tile.flags;
            variant.colliderType = tile.colliderType;
            resolutionTiles.Add(variant);
            resolutionTileCache.Add(source, variant);
            return variant;
        }

        private Sprite ResolveSprite(Sprite source)
        {
            if (resolutionSpriteCache.TryGetValue(source, out Sprite cached))
                return cached;
            if (!source.texture.isReadable)
                throw new InvalidOperationException("Enable Read/Write on the texture for tile " + source.name + ".");

            Rect rect = source.textureRect;
            int sourceX = Mathf.RoundToInt(rect.x);
            int sourceY = Mathf.RoundToInt(rect.y);
            int sourceWidth = Mathf.RoundToInt(rect.width);
            int sourceHeight = Mathf.RoundToInt(rect.height);
            Color32[] texturePixels = source.texture.GetPixels32();
            int textureWidth = source.texture.width;
            var pixels = new Color32[sourceWidth * sourceHeight];
            for (int y = 0; y < sourceHeight; y++)
                for (int x = 0; x < sourceWidth; x++)
                    pixels[y * sourceWidth + x] = texturePixels[(sourceY + y) * textureWidth + sourceX + x];
            Texture2D texture = CreatePixelTexture(pixels, sourceWidth, sourceHeight);
            texture.name = source.name + " " + PixelsPerTile + "px";
            resolutionTextures.Add(texture);

            Vector2 pivot = new Vector2(source.pivot.x / source.rect.width,
                source.pivot.y / source.rect.height);
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, PixelsPerTile, PixelsPerTile),
                pivot, PixelsPerTile);
            sprite.name = texture.name;
            resolutionSprites.Add(sprite);
            resolutionSpriteCache.Add(source, sprite);
            return sprite;
        }

        private static void SetPixel(Color32[] pixels, int x, int y, Color32 color)
        {
            if (x >= 0 && x < 16 && y >= 0 && y < 16)
                pixels[y * 16 + x] = color;
        }

        private void RenderTrees(MapData map)
        {
            if (map.TreeCount == 0 && map.GroundItemCount == 0)
            {
                if (treeTilemap != null)
                    treeTilemap.ClearAllTiles();
                return;
            }

            EnsureTreeLayer(map);
            var tiles = new TileBase[map.Width * map.Height];
            for (int y = 0; y < map.Height; y++)
                for (int x = 0; x < map.Width; x++)
                {
                    var cell = new Vector2Int(x, y);
                    if (map.HasTree(cell))
                        tiles[y * map.Width + x] = ResolveTile(treeTile);
                    else if (map.TryGetGroundItem(cell, out ItemType item))
                        tiles[y * map.Width + x] = GetGroundItemTile(item);
                }
            treeTilemap.ClearAllTiles();
            treeTilemap.SetTilesBlock(new BoundsInt(0, 0, 0, map.Width, map.Height, 1), tiles);
            treeTilemap.CompressBounds();
        }

        private void EnsureTreeLayer(MapData map)
        {
            if (treeTilemap == null)
            {
                treeLayer = new GameObject("Trees", typeof(Tilemap), typeof(TilemapRenderer));
                treeLayer.transform.SetParent(transform, false);
                treeTilemap = treeLayer.GetComponent<Tilemap>();
                TilemapRenderer baseRenderer = GetComponent<TilemapRenderer>();
                TilemapRenderer treeRenderer = treeLayer.GetComponent<TilemapRenderer>();
                treeRenderer.sortingLayerID = baseRenderer.sortingLayerID;
                treeRenderer.sortingOrder = baseRenderer.sortingOrder + 1;
            }
            if (treeTile == null && map.TreeCount > 0)
                treeTile = CreateRuntimeTreeTile();
        }

        private Tile GetGroundItemTile(ItemType item)
        {
            int index = (int)item;
            if (runtimeGroundItemTiles[index] != null)
                return runtimeGroundItemTiles[index];
            if (runtimeItemSprites[index] == null)
                runtimeItemSprites[index] = CreateItemSprite(item, index);
            var tile = ScriptableObject.CreateInstance<Tile>();
            tile.name = ItemCatalog.Get(item).DisplayName;
            tile.sprite = runtimeItemSprites[index];
            tile.colliderType = Tile.ColliderType.None;
            tile.flags = TileFlags.None;
            runtimeGroundItemTiles[index] = tile;
            return tile;
        }

        private Tile CreateRuntimeTreeTile()
        {
            var pixels = new Color32[256];
            for (int y = 0; y < 7; y++)
                for (int x = 7; x <= 8; x++)
                    pixels[y * 16 + x] = new Color32(105, 72, 39, 255);
            for (int y = 4; y < 16; y++)
            {
                int radius = Mathf.Min(6, (y - 3) / 2);
                for (int x = 8 - radius; x <= 8 + radius; x++)
                {
                    bool edge = Mathf.Abs(x - 8) == radius;
                    pixels[y * 16 + x] = edge
                        ? new Color32(37, 78, 39, 255)
                        : (x + y) % 4 == 0
                            ? new Color32(75, 133, 60, 255) : new Color32(50, 105, 48, 255);
                }
            }
            runtimeTreeTexture = CreatePixelTexture(pixels, 16, 16);
            runtimeTreeSprite = CreatePixelSprite(runtimeTreeTexture, "Tree");
            runtimeTreeTile = ScriptableObject.CreateInstance<Tile>();
            runtimeTreeTile.sprite = runtimeTreeSprite;
            runtimeTreeTile.colliderType = Tile.ColliderType.None;
            runtimeTreeTile.flags = TileFlags.None;
            return runtimeTreeTile;
        }

        private void OnDestroy()
        {
            foreach (Tile tile in resolutionTiles)
                DestroyGeneratedObject(tile);
            foreach (Sprite sprite in resolutionSprites)
                DestroyGeneratedObject(sprite);
            foreach (Texture2D texture in resolutionTextures)
                DestroyGeneratedObject(texture);
            if (fogTilemap != null) DestroyGeneratedObject(fogTilemap.gameObject);
            if (fogMemoryTilemap != null) DestroyGeneratedObject(fogMemoryTilemap.gameObject);
            if (targetTilemap != null) DestroyGeneratedObject(targetTilemap.gameObject);
            if (targetTile != null)
            {
                DestroyGeneratedObject(targetTile.sprite.texture);
                DestroyGeneratedObject(targetTile.sprite);
                DestroyGeneratedObject(targetTile);
            }
            if (fogTile != null)
            {
                DestroyGeneratedObject(fogTile.sprite.texture);
                DestroyGeneratedObject(fogTile.sprite);
                DestroyGeneratedObject(fogTile);
            }
            foreach (Tile tile in mountainTerrainTiles)
                if (tile != null)
                {
                    DestroyGeneratedObject(tile.sprite.texture);
                    DestroyGeneratedObject(tile.sprite);
                    DestroyGeneratedObject(tile);
                }
            if (roofTile != null)
            {
                DestroyGeneratedObject(roofTile.sprite.texture);
                DestroyGeneratedObject(roofTile.sprite);
                DestroyGeneratedObject(roofTile);
            }
            foreach (Tile tile in buildingTiles)
                if (tile != null)
                {
                    DestroyGeneratedObject(tile.sprite.texture);
                    DestroyGeneratedObject(tile.sprite);
                    DestroyGeneratedObject(tile);
                }
            if (stockpileMap != null)
            {
                stockpileMap.StorageStockpile.Changed -= RefreshStorageItems;
                stockpileMap.PantryStockpile.Changed -= RefreshStorageItems;
            }
            if (treeLayer != null)
                DestroyGeneratedObject(treeLayer);
            if (runtimeTreeTile != null)
                DestroyGeneratedObject(runtimeTreeTile);
            if (runtimeTreeSprite != null)
                DestroyGeneratedObject(runtimeTreeSprite);
            if (runtimeTreeTexture != null)
                DestroyGeneratedObject(runtimeTreeTexture);
            foreach (Tile itemTile in runtimeGroundItemTiles)
                if (itemTile != null)
                    DestroyGeneratedObject(itemTile);
            if (storageItemsRoot != null)
                DestroyGeneratedObject(storageItemsRoot);
            if (specializedRoomLabelsRoot != null)
                DestroyGeneratedObject(specializedRoomLabelsRoot);
            for (int i = 0; i < runtimeItemSprites.Length; i++)
            {
                if (runtimeItemSprites[i] != null)
                    DestroyGeneratedObject(runtimeItemSprites[i]);
                if (runtimeItemTextures[i] != null)
                    DestroyGeneratedObject(runtimeItemTextures[i]);
            }
        }

        private static void DestroyGeneratedObject(UnityEngine.Object obj)
        {
            if (Application.isPlaying)
                UnityEngine.Object.Destroy(obj);
            else
                UnityEngine.Object.DestroyImmediate(obj);
        }

        public void RenderZones(MapData map)
        {
            if (map == null)
                throw new ArgumentNullException(nameof(map));
            Tilemap tilemap = Tilemap;
            foreach (Vector2Int cell in renderedZoneCells)
            {
                var position = new Vector3Int(cell.x, cell.y, 0);
                tilemap.SetTileFlags(position, TileFlags.None);
                tilemap.SetColor(position, map.IsWalkable(cell)
                    ? Color.white : new Color(0.20f, 0.13f, 0.13f));
            }
            renderedZoneCells.Clear();
            for (int y = 0; y < map.Height; y++)
                for (int x = 0; x < map.Width; x++)
                {
                    var cell = new Vector2Int(x, y);
                    if (!map.TryGetZone(cell, out MapZoneKind kind))
                        continue;
                    Color tint = Color.Lerp(Color.white, ZoneColor(kind), 0.42f);
                    var position = new Vector3Int(x, y, 0);
                    tilemap.SetTileFlags(position, TileFlags.None);
                    tilemap.SetColor(position, tint);
                    renderedZoneCells.Add(cell);
                }
        }

        public static Color ZoneColor(MapZoneKind kind)
        {
            switch (kind)
            {
                case MapZoneKind.Dump: return new Color(0.58f, 0.60f, 0.58f);
                case MapZoneKind.MaterialsDepot: return new Color(0.82f, 0.24f, 0.20f);
                case MapZoneKind.Cemetery: return new Color(0.22f, 0.23f, 0.24f);
                default: throw new ArgumentOutOfRangeException(nameof(kind));
            }
        }

        public void RefreshWalkability(MapData map, Vector2Int cell)
        {
            if (map == null)
                throw new ArgumentNullException(nameof(map));
            if (!map.Contains(cell))
                throw new ArgumentOutOfRangeException(nameof(cell));
            var position = new Vector3Int(cell.x, cell.y, 0);
            Tilemap.RefreshTile(position);
            if (!map.IsWalkable(cell))
            {
                Tilemap.SetTileFlags(position, TileFlags.None);
                Tilemap.SetColor(position, map.IsMineable(cell)
                    ? Color.white : new Color(0.20f, 0.13f, 0.13f));
            }
        }
    }
}
