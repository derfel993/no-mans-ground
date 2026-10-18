using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace NoMansGround.Map
{
    [RequireComponent(typeof(Tilemap), typeof(TilemapRenderer))]
    public sealed class MapTilemapView : MonoBehaviour
    {
        private readonly HashSet<Vector2Int> renderedZoneCells = new HashSet<Vector2Int>();
        private Tilemap buildingTilemap;
        private readonly Tile[] buildingTiles = new Tile[4];

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
                        buildingTilemap.SetColor(position, room.IsBuilt(cell)
                            ? Color.white : new Color(0.55f, 0.85f, 1f, 0.4f));
                    }
        }

        private Tile CreateBuildingTile(ItemType material, bool door)
        {
            var texture = new Texture2D(16, 16, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Point;
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
            texture.SetPixels32(pixels);
            texture.Apply();
            var tile = ScriptableObject.CreateInstance<Tile>();
            tile.name = (wood ? "Wood" : "Stone") + (door ? "Door" : "Wall");
            tile.sprite = Sprite.Create(texture, new Rect(0, 0, 16, 16), new Vector2(0.5f, 0.5f), 16f);
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
        private readonly GameObject[] storageItemDisplays = new GameObject[6];
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
            Tilemap.SetTile(new Vector3Int(cell.x, cell.y, 0), ladderTile);
        }

        public void CompleteTrench(MapData map, Vector2Int cell)
        {
            if (map == null)
                throw new ArgumentNullException(nameof(map));
            if (!map.IsWalkable(cell))
                throw new InvalidOperationException("A trench requires a walkable cell.");
            if (map.IsStorageCell(cell) || map.IsPantryCell(cell))
                throw new InvalidOperationException("Storage areas cannot be excavated.");
            if (trenchTile == null)
                throw new InvalidOperationException("Assign a trench tile before digging.");
            map[cell.x, cell.y] = TerrainKind.Trench;
            Tilemap.SetTile(new Vector3Int(cell.x, cell.y, 0), trenchTile);
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
            if (!map.Contains(cell) || map[cell.x, cell.y] != TerrainKind.Rock)
                throw new InvalidOperationException("Rock must exist before it can be mined.");
            if (gravelTile == null)
                throw new InvalidOperationException("Assign a gravel tile before mining rock.");
            map[cell.x, cell.y] = TerrainKind.Gravel;
            map.AddGroundItem(cell, ItemType.BuildingStone);
            var position = new Vector3Int(cell.x, cell.y, 0);
            Tilemap.SetTile(position, gravelTile);
            Tilemap.SetTileFlags(position, TileFlags.None);
            Tilemap.SetColor(position, Color.white);
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
                        case TerrainKind.Dirt: tiles[y * map.Width + x] = dirtTile; break;
                        case TerrainKind.Grass: tiles[y * map.Width + x] = grassTile; break;
                        case TerrainKind.Mud: tiles[y * map.Width + x] = mudTile; break;
                        case TerrainKind.Rock:
                            if (rockTile == null)
                                throw new InvalidOperationException("Assign a rock tile before rendering rocks.");
                            tiles[y * map.Width + x] = rockTile;
                            break;
                        case TerrainKind.Gravel:
                            if (gravelTile == null)
                                throw new InvalidOperationException("Assign a gravel tile before rendering gravel.");
                            tiles[y * map.Width + x] = gravelTile;
                            break;
                        case TerrainKind.Road:
                            if (roadTile == null)
                                throw new InvalidOperationException("Assign a road tile before rendering roads.");
                            tiles[y * map.Width + x] = roadTile;
                            break;
                        case TerrainKind.Trench:
                            if (trenchTile == null)
                                throw new InvalidOperationException("Assign a trench tile before rendering trenches.");
                            if (map.HasLadder(new Vector2Int(x, y)) && ladderTile == null)
                                throw new InvalidOperationException("Assign a ladder tile before rendering ladders.");
                            tiles[y * map.Width + x] = map.HasLadder(new Vector2Int(x, y)) ? ladderTile : trenchTile;
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
            RenderBuildings(map);
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
                { ItemType.Shovel, ItemType.Axe, ItemType.Pickaxe, ItemType.Rations, ItemType.Wood, ItemType.BuildingStone };
            Vector2Int[] depotSlots =
            {
                new Vector2Int(0, 2), new Vector2Int(1, 2), new Vector2Int(2, 2),
                Vector2Int.zero, new Vector2Int(0, 1), new Vector2Int(1, 1)
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

                if (runtimeItemSprites[i] == null)
                    runtimeItemSprites[i] = CreateItemSprite(types[i], i);
                if (storageItemDisplays[i] == null)
                    storageItemDisplays[i] = CreateStorageItemDisplay(types[i]);
                GameObject display = storageItemDisplays[i];
                display.SetActive(true);
                SpriteRenderer renderer = display.GetComponent<SpriteRenderer>();
                renderer.sprite = runtimeItemSprites[i];
                Vector2Int cell = isFood
                    ? map.PantryOrigin + new Vector2Int(0, 2)
                    : map.StorageOrigin + depotSlots[i];
                display.transform.position = Tilemap.GetCellCenterWorld(new Vector3Int(cell.x, cell.y, 0));
                display.transform.localScale = Vector3.one * 0.8f;
                display.transform.GetChild(0).localPosition = new Vector3(0f, -0.58f, 0f);
                string quantity = isFood ? GetStackSummary(stockpile, types[i]) : $"x{count}";
                display.GetComponentInChildren<TextMesh>().text = $"{ItemCatalog.Get(types[i]).DisplayName} {quantity}";
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
            runtimeItemTextures[index] = new Texture2D(16, 16, TextureFormat.RGBA32, false);
            Texture2D texture = runtimeItemTextures[index];
            texture.name = ItemCatalog.Get(type).DisplayName + " Sprite";
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;
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
                default:
                    throw new ArgumentOutOfRangeException(nameof(type));
            }

            texture.SetPixels32(pixels);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0f, 0f, 16f, 16f), new Vector2(0.5f, 0.5f), 16f);
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
                        tiles[y * map.Width + x] = treeTile;
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
            runtimeTreeTexture = new Texture2D(16, 16, TextureFormat.RGBA32, false);
            runtimeTreeTexture.filterMode = FilterMode.Point;
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
            runtimeTreeTexture.SetPixels32(pixels);
            runtimeTreeTexture.Apply();
            runtimeTreeSprite = Sprite.Create(runtimeTreeTexture, new Rect(0f, 0f, 16f, 16f),
                new Vector2(0.5f, 0.5f), 16f);
            runtimeTreeTile = ScriptableObject.CreateInstance<Tile>();
            runtimeTreeTile.sprite = runtimeTreeSprite;
            runtimeTreeTile.colliderType = Tile.ColliderType.None;
            runtimeTreeTile.flags = TileFlags.None;
            return runtimeTreeTile;
        }

        private void OnDestroy()
        {
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
                Tilemap.SetColor(position, map[cell.x, cell.y] == TerrainKind.Rock
                    ? Color.white : new Color(0.20f, 0.13f, 0.13f));
            }
        }
    }
}
