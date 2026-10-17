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
        private GameObject treeLayer;
        private Tilemap treeTilemap;
        private Tile runtimeTreeTile;
        private Sprite runtimeTreeSprite;
        private Texture2D runtimeTreeTexture;
        [SerializeField] private TileBase dirtTile;
        [SerializeField] private TileBase grassTile;
        [SerializeField] private TileBase mudTile;
        [SerializeField] private TileBase rockTile;
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
            if (trenchTile == null)
                throw new InvalidOperationException("Assign a trench tile before digging.");
            map[cell.x, cell.y] = TerrainKind.Trench;
            Tilemap.SetTile(new Vector3Int(cell.x, cell.y, 0), trenchTile);
        }

        public void Render(MapData map)
        {
            if (map == null)
                throw new ArgumentNullException(nameof(map));
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
            RenderTrees(map);
        }

        private void RenderTrees(MapData map)
        {
            if (map.TreeCount == 0)
            {
                if (treeTilemap != null)
                    treeTilemap.ClearAllTiles();
                return;
            }

            EnsureTreeLayer();
            var tiles = new TileBase[map.Width * map.Height];
            for (int y = 0; y < map.Height; y++)
                for (int x = 0; x < map.Width; x++)
                    if (map.HasTree(new Vector2Int(x, y)))
                        tiles[y * map.Width + x] = treeTile;
            treeTilemap.ClearAllTiles();
            treeTilemap.SetTilesBlock(new BoundsInt(0, 0, 0, map.Width, map.Height, 1), tiles);
            treeTilemap.CompressBounds();
        }

        private void EnsureTreeLayer()
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
            if (treeTile == null)
                treeTile = CreateRuntimeTreeTile();
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
            if (treeLayer != null)
                DestroyGeneratedObject(treeLayer);
            if (runtimeTreeTile != null)
                DestroyGeneratedObject(runtimeTreeTile);
            if (runtimeTreeSprite != null)
                DestroyGeneratedObject(runtimeTreeSprite);
            if (runtimeTreeTexture != null)
                DestroyGeneratedObject(runtimeTreeTexture);
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
                case MapZoneKind.Pantry: return new Color(0.35f, 0.67f, 0.44f);
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
