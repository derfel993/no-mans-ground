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

        public Bounds WorldBounds { get; private set; }
        public Tilemap Tilemap => GetComponent<Tilemap>();

        public void Configure(TileBase dirt, TileBase grass, TileBase mud)
        {
            dirtTile = dirt;
            grassTile = grass;
            mudTile = mud;
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
                        default: throw new ArgumentOutOfRangeException(nameof(map), "Unknown terrain kind.");
                    }
                }
            }

            Tilemap tilemap = Tilemap;
            tilemap.ClearAllTiles();
            tilemap.SetTilesBlock(new BoundsInt(0, 0, 0, map.Width, map.Height, 1), tiles);
            tilemap.CompressBounds();

            var bounds = new Bounds(tilemap.CellToWorld(Vector3Int.zero), Vector3.zero);
            bounds.Encapsulate(tilemap.CellToWorld(new Vector3Int(map.Width, 0, 0)));
            bounds.Encapsulate(tilemap.CellToWorld(new Vector3Int(0, map.Height, 0)));
            bounds.Encapsulate(tilemap.CellToWorld(new Vector3Int(map.Width, map.Height, 0)));
            WorldBounds = bounds;
        }
    }
}
