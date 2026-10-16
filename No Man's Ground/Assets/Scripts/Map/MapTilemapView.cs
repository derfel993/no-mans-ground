using System;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace NoMansGround.Map
{
    [RequireComponent(typeof(Tilemap), typeof(TilemapRenderer))]
    public sealed class MapTilemapView : MonoBehaviour
    {
        [SerializeField] private TileBase dirtTile;
        [SerializeField] private TileBase grassTile;
        [SerializeField] private TileBase mudTile;
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
                Tilemap.SetColor(position, new Color(0.20f, 0.13f, 0.13f));
            }
        }
    }
}
