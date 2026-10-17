using System;
using System.Collections.Generic;
using UnityEngine;

namespace NoMansGround.Map
{
    [Serializable]
    public sealed class MapGenerationSettings
    {
        [Range(1, 512)] public int width = 128;
        [Range(1, 512)] public int height = 128;
        public int seed = 1916;
        [Min(1f)] public float noiseScale = 18f;
        [Range(0f, 1f)] public float mudThreshold = 0.38f;
        [Range(0f, 1f)] public float grassThreshold = 0.58f;
        [Range(0f, 1f)] public float rockThreshold = 0.70f;

        public void Validate()
        {
            if (width < 1 || width > 512 || height < 1 || height > 512)
                throw new ArgumentOutOfRangeException(nameof(width), "Map dimensions must be between 1 and 512.");
            if (!float.IsFinite(noiseScale) || noiseScale < 1f)
                throw new ArgumentOutOfRangeException(nameof(noiseScale), "Noise scale must be finite and at least 1.");
            if (!float.IsFinite(mudThreshold) || !float.IsFinite(grassThreshold) || !float.IsFinite(rockThreshold)
                || mudThreshold < 0f || mudThreshold > 1f || grassThreshold < 0f || grassThreshold > 1f
                || rockThreshold < 0f || rockThreshold > 1f || mudThreshold >= grassThreshold
                || grassThreshold >= rockThreshold)
                throw new ArgumentException("Terrain thresholds must satisfy 0 <= mud < grass < rock <= 1.");
        }
    }

    public static class MapGenerator
    {
        public static MapData Generate(MapGenerationSettings settings)
        {
            if (settings == null)
                throw new ArgumentNullException(nameof(settings));
            settings.Validate();

            // Local PRNG: map generation must not change Unity's global random state.
            var random = new System.Random(settings.seed);
            float offsetX = (float)random.NextDouble() * 10000f;
            float offsetY = (float)random.NextDouble() * 10000f;
            var map = new MapData(settings.width, settings.height);
            const int spawnSafeRows = 10;

            for (int y = 0; y < map.Height; y++)
            {
                for (int x = 0; x < map.Width; x++)
                {
                    float noise = Mathf.PerlinNoise(offsetX + x / settings.noiseScale,
                        offsetY + y / settings.noiseScale);
                    map[x, y] = y >= spawnSafeRows && noise >= settings.rockThreshold ? TerrainKind.Rock
                        : noise < settings.mudThreshold ? TerrainKind.Mud
                        : noise >= settings.grassThreshold ? TerrainKind.Grass : TerrainKind.Dirt;
                }
            }

            int roadWidth = Mathf.Min(4, map.Width);
            int roadLength = Mathf.Min(6, map.Height);
            int roadStartX = (map.Width - roadWidth) / 2;
            for (int y = 0; y < roadLength; y++)
                for (int x = roadStartX; x < roadStartX + roadWidth; x++)
                    map[x, y] = TerrainKind.Road;

            CreateStorageArea(map, roadStartX, roadWidth, roadLength);

            var treeCandidates = new List<Vector2Int>();
            for (int y = 0; y < map.Height; y++)
                for (int x = 0; x < map.Width; x++)
                    if (map[x, y] != TerrainKind.Rock
                        && !map.IsStorageCell(new Vector2Int(x, y))
                        && !map.IsPantryCell(new Vector2Int(x, y)))
                        treeCandidates.Add(new Vector2Int(x, y));

            int treeCount = treeCandidates.Count * 2 / 5;
            for (int i = 0; i < treeCount; i++)
            {
                int swapIndex = i + random.Next(treeCandidates.Count - i);
                Vector2Int selected = treeCandidates[swapIndex];
                treeCandidates[swapIndex] = treeCandidates[i];
                treeCandidates[i] = selected;
                map.SetTree(treeCandidates[i], true);
            }

            return map;
        }

        private static void CreateStorageArea(MapData map, int roadStartX, int roadWidth, int roadLength)
        {
            const int storageSize = 4;
            int sideY = Mathf.Max(4, roadLength - storageSize + 1);
            if (sideY + storageSize <= map.Height)
            {
                int rightX = roadStartX + roadWidth;
                if (rightX + storageSize <= map.Width
                    && CreateDepotAndPantry(map, new Vector2Int(rightX, sideY)))
                    return;
                int leftX = roadStartX - storageSize;
                if (leftX >= 0
                    && CreateDepotAndPantry(map, new Vector2Int(leftX, sideY)))
                    return;
            }
        }

        private static bool CreateDepotAndPantry(MapData map, Vector2Int depotOrigin)
        {
            if (depotOrigin.y < 4
                || !map.CreateStorageArea(depotOrigin, 4, 4)
                || !map.CreatePantryArea(new Vector2Int(depotOrigin.x, depotOrigin.y - 4), 2, 4))
                return false;
            AddStartingTools(map.StorageStockpile);
            map.PantryStockpile.Add(ItemType.Rations, 1000);
            return true;
        }

        private static void AddStartingTools(ItemStockpile stockpile)
        {
            stockpile.Add(ItemType.Shovel, 6);
            stockpile.Add(ItemType.Axe, 2);
            stockpile.Add(ItemType.Pickaxe, 2);
        }
    }
}
