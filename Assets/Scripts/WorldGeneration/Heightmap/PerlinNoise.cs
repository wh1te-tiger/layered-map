using UnityEngine;

namespace WorldGeneration.Heightmap
{
    public static class PerlinNoise
    {
        public static float[,] Generate(PerlinNoiseConfiguration config)
        {
            float[,] noiseMap = new float[config.Width, config.Height];
            System.Random prng = new System.Random(config.Seed);
            Vector2[] octaveOffsets = new Vector2[config.Octaves];
        
            for (int i = 0; i < config.Octaves; i++)
            {
                var octaveSeed = prng.Next();
                var octavePrng = new System.Random(octaveSeed);
                float offsetX = octavePrng.Next(-100000, 100000) + config.Offset.x;
                float offsetY = octavePrng.Next(-100000, 100000) + config.Offset.y;
                octaveOffsets[i] = new Vector2(offsetX, offsetY);
            }
            
            float maxNoiseHeight = float.MinValue;
            float minNoiseHeight = float.MaxValue;
            
            for (int y = 0; y < config.Height; y++)
            {
                for (int x = 0; x < config.Width; x++)
                {
                    float amplitude = 1;
                    float frequency = 1;
                    float noiseHeight = 0;

                    for (int i = 0; i < config.Octaves; i++)
                    {
                        float sampleX = x / config.NoiseScale * frequency + octaveOffsets[i].x;
                        float sampleY = y / config.NoiseScale * frequency + octaveOffsets[i].y;

                        float perlinValue = Mathf.PerlinNoise(sampleX, sampleY) * 2 - 1;
                        noiseHeight += perlinValue * amplitude;

                        amplitude *= config.Persistence;
                        frequency *= config.Lacunarity;
                    }

                    if (noiseHeight > maxNoiseHeight) maxNoiseHeight = noiseHeight;
                    if (noiseHeight < minNoiseHeight) minNoiseHeight = noiseHeight;

                    noiseMap[x, y] = noiseHeight;
                }
            }
            
            // Нормализация и применение кривой
            for (int y = 0; y < config.Height; y++)
            {
                for (int x = 0; x < config.Width; x++)
                {
                    float value = noiseMap[x, y];
                    value = Mathf.InverseLerp(minNoiseHeight, maxNoiseHeight, value);
                    value = config.HeightCurve.Evaluate(value) * config.HeightMultiplier;
                
                    if (config.UseFalloff)
                    {
                        float falloff = CalculateFalloff(x, y, config.Width, config.Height);
                        value = Mathf.Clamp01(value - falloff);
                    }
                    
                    // Дискретизация по уровням
                    int layer = Mathf.FloorToInt(value * config.HeightLevels);
                    layer = Mathf.Clamp(layer, 0, config.HeightLevels - 1);
            
                    // Задаём высоту уровня
                    noiseMap[x, y] = 1f / config.HeightLevels * layer;
                }
            }
        
            return ApplySmooth(noiseMap, config);
        }
        
        private static float CalculateFalloff(int x, int y, int mapWidth, int mapHeight)
        {
            float a = 3f;
            float b = 2.2f;
        
            float normalizedX = x / (float)mapWidth * 2 - 1;
            float normalizedY = y / (float)mapHeight * 2 - 1;
        
            return Mathf.Pow(Mathf.Max(Mathf.Abs(normalizedX), Mathf.Abs(normalizedY)), a) / b;
        }
        
        private static float[,] ApplySmooth(in float[,] noiseMap, PerlinNoiseConfiguration config)
        {
            return config.UseSmooth? GaussianBlur(noiseMap, config.Sigma) : noiseMap;
        }
        
        private static float[,] GaussianBlur(in float[,] noiseMap, float sigma)
        {
            var width = noiseMap.GetLength(0);
            var height = noiseMap.GetLength(1);
            
            int kernelSize = Mathf.CeilToInt(3 * sigma) * 2 + 1;
            float[,] kernel = GenerateGaussianKernel(kernelSize, sigma);
            float[,] blurredMap = new float[width, height];

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float sum = 0f;
                    float weightSum = 0f;

                    for (int ky = -kernelSize / 2; ky <= kernelSize / 2; ky++)
                    {
                        for (int kx = -kernelSize / 2; kx <= kernelSize / 2; kx++)
                        {
                            int nx = Mathf.Clamp(x + kx, 0, width - 1);
                            int ny = Mathf.Clamp(y + ky, 0, height - 1);

                            float weight = kernel[ky + kernelSize / 2, kx + kernelSize / 2];
                            sum += noiseMap[nx, ny] * weight;
                            weightSum += weight;
                        }
                    }

                    blurredMap[x, y] = sum / weightSum;
                }
            }

            return blurredMap;
        }

        private static float[,] GenerateGaussianKernel(int size, float sigma)
        {
            float[,] kernel = new float[size, size];
            float mean = size / 2f;
            float sum = 0f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    kernel[y, x] =
                        Mathf.Exp(-0.5f * (Mathf.Pow((x - mean) / sigma, 2.0f) + Mathf.Pow((y - mean) / sigma, 2.0f)))
                        / (2 * Mathf.PI * sigma * sigma);
                    sum += kernel[y, x];
                }
            }

            // Нормализуем ядро
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    kernel[y, x] /= sum;
                }
            }

            return kernel;
        }
    }
}