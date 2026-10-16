using System;
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

        public void Validate()
        {
            if (width < 1 || width > 512 || height < 1 || height > 512)
                throw new ArgumentOutOfRangeException(nameof(width), "Map dimensions must be between 1 and 512.");
            if (!float.IsFinite(noiseScale) || noiseScale < 1f)
                throw new ArgumentOutOfRangeException(nameof(noiseScale), "Noise scale must be finite and at least 1.");
            if (!float.IsFinite(mudThreshold) || !float.IsFinite(grassThreshold)
                || mudThreshold < 0f || grassThreshold > 1f || mudThreshold >= grassThreshold)
                throw new ArgumentException("Terrain thresholds must satisfy 0 <= mud < grass <= 1.");
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

            for (int y = 0; y < map.Height; y++)
            {
                for (int x = 0; x < map.Width; x++)
                {
                    float noise = Mathf.PerlinNoise(offsetX + x / settings.noiseScale,
                        offsetY + y / settings.noiseScale);
                    map[x, y] = noise < settings.mudThreshold ? TerrainKind.Mud
                        : noise >= settings.grassThreshold ? TerrainKind.Grass : TerrainKind.Dirt;
                }
            }

            int roadWidth = Mathf.Min(4, map.Width);
            int roadLength = Mathf.Min(6, map.Height);
            int roadStartX = (map.Width - roadWidth) / 2;
            for (int y = 0; y < roadLength; y++)
                for (int x = roadStartX; x < roadStartX + roadWidth; x++)
                    map[x, y] = TerrainKind.Road;

            return map;
        }
    }
}
