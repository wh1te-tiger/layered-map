using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace WorldGeneration.Heightmap
{
    public static class PerlinNoiseWithJobs
    {
        public static float[] Generate(PerlinNoiseConfiguration config)
        {
            var noiseMap = GenerateInternal(config);
            /*// Конвертируем результат в двумерный массив
            float[,] result = new float[config.Width, config.Height];
            for (int y = 0; y < config.Height; y++)
            {
                for (int x = 0; x < config.Width; x++)
                {
                    result[x, y] = noiseMap[y * config.Width + x];
                }
            }*/
            
            return noiseMap;
        }
        
        private static float[] GenerateInternal(PerlinNoiseConfiguration config)
        {
            int width = config.Width;
            int height = config.Height;
            int length = width * height;
            
            // Выделяем временные массивы
            NativeArray<float> noiseMap = new NativeArray<float>(length, Allocator.TempJob);
            NativeArray<float2> octaveOffsets = new NativeArray<float2>(config.Octaves, Allocator.TempJob);
            NativeArray<float> curveTexture = new NativeArray<float>(256, Allocator.TempJob);
            
            // Генерируем смещения для октав
            Random rand = new Random((uint)config.Seed);
            for (int i = 0; i < config.Octaves; i++)
            {
                float offsetX = rand.NextInt(-100000, 100000) + config.Offset.x;
                float offsetY = rand.NextInt(-100000, 100000) + config.Offset.y;
                octaveOffsets[i] = new float2(offsetX, offsetY);
            }
            
            // Преобразуем кривую высот в текстуру
            for (int i = 0; i < 256; i++)
            {
                curveTexture[i] = config.HeightCurve.Evaluate(i / 255f);
            }
            
            // Создаем и запускаем джоб генерации шума
            var generateJob = new GenerateNoiseMapJob
            {
                Width = width,
                Height = height,
                NoiseScale = config.NoiseScale,
                Octaves = config.Octaves,
                Persistence = config.Persistence,
                Lacunarity = config.Lacunarity,
                OctaveOffsets = octaveOffsets,
                NoiseMap = noiseMap
            };
            JobHandle generateHandle = generateJob.Schedule(length, 64);
            generateHandle.Complete();
            
            // Находим min и max значения шума
            float minNoise = float.MaxValue;
            float maxNoise = float.MinValue;
            for (int i = 0; i < length; i++)
            {
                float value = noiseMap[i];
                if (value < minNoise) minNoise = value;
                if (value > maxNoise) maxNoise = value;
            }

            // Джоб нормализации и применения кривой
            var normalizeJob = new NormalizeAndApplyCurveJob
            {
                Width = width,
                Height = height,
                MinNoise = minNoise,
                MaxNoise = maxNoise,
                UseFalloff = config.UseFalloff,
                HeightLevels = config.HeightLevels,
                HeightMultiplier = config.HeightMultiplier,
                CurveTexture = curveTexture,
                NoiseMap = noiseMap
            };
            JobHandle normalizeHandle = normalizeJob.Schedule(length, 64);
            normalizeHandle.Complete();
            
            if (config.UseSmooth)
            {
                using (NativeArray<float> temp = new NativeArray<float>(length, Allocator.TempJob))
                {
                    int kernelSize = (int) math.ceil(3 * config.Sigma) * 2 + 1;
                    using (NativeArray<float> kernel = new NativeArray<float>(kernelSize, Allocator.TempJob))
                    {
                        GenerateGaussianKernel(kernel, kernelSize, config.Sigma);

                        // Горизонтальное размытие
                        var horizontalJob = new GaussianBlurHorizontalJob
                        {
                            Input = noiseMap,
                            Output = temp,
                            Kernel = kernel,
                            KernelSize = kernelSize,
                            Width = width,
                        };
                        JobHandle horizontalHandle = horizontalJob.Schedule(height, 1);
                        horizontalHandle.Complete();

                        // Вертикальное размытие
                        var verticalJob = new GaussianBlurVerticalJob
                        {
                            Input = temp,
                            Output = noiseMap,
                            Kernel = kernel,
                            KernelSize = kernelSize,
                            Width = width,
                            Height = height
                        };
                        JobHandle verticalHandle = verticalJob.Schedule(width, 1);
                        verticalHandle.Complete();
                    }
                }
            }

            var res = noiseMap.ToArray();
            
            // Освобождаем ресурсы
            noiseMap.Dispose();
            octaveOffsets.Dispose();
            curveTexture.Dispose();

            return res;
        }
        
        [BurstCompile]
        private struct GenerateNoiseMapJob : IJobParallelFor
        {
            public int Width;
            public int Height;
            public float NoiseScale;
            public int Octaves;
            public float Persistence;
            public float Lacunarity;
            [ReadOnly] public NativeArray<float2> OctaveOffsets;

            [WriteOnly] public NativeArray<float> NoiseMap;

            public void Execute(int index)
            {
                int x = index % Width;
                int y = index / Height;
                float amplitude = 1f;
                float frequency = 1f;
                float noiseHeight = 0f;

                for (int i = 0; i < Octaves; i++)
                {
                    float sampleX = x / NoiseScale * frequency + OctaveOffsets[i].x;
                    float sampleY = y / NoiseScale * frequency + OctaveOffsets[i].y;

                    float2 p = new float2(sampleX, sampleY);
                    float perlinValue = noise.snoise(p);
                    noiseHeight += perlinValue * amplitude;

                    amplitude *= Persistence;
                    frequency *= Lacunarity;
                }

                NoiseMap[index] = noiseHeight;
            }
        }
        
        [BurstCompile]
        private struct NormalizeAndApplyCurveJob : IJobParallelFor
        {
            public int Width;
            public int Height;
            public float MinNoise;
            public float MaxNoise;
            public bool UseFalloff;
            public int HeightLevels;
            public float HeightMultiplier;
            [ReadOnly] public NativeArray<float> CurveTexture;

            public NativeArray<float> NoiseMap;

            public void Execute(int index)
            {
                int x = index % Width;
                int y = index / Width;
                float value = NoiseMap[index];

                // Нормализация
                float normalizedValue = math.unlerp(MinNoise, MaxNoise, value);
                
                // Применение кривой
                int curveIndex = (int)(normalizedValue * 255);
                curveIndex = math.clamp(curveIndex, 0, 255);
                float curveValue = CurveTexture[curveIndex] * HeightMultiplier;

                // Применение falloff
                if (UseFalloff)
                {
                    float falloff = CalculateFalloff(x, y, Width, Height);
                    curveValue = math.clamp(curveValue - falloff, 0f, 1f);
                }

                // Дискретизация по уровням
                int layer = (int)(curveValue * HeightLevels);
                layer = math.clamp(layer, 0, HeightLevels - 1);
                NoiseMap[index] = (float)layer / HeightLevels;
            }

            private float CalculateFalloff(int x, int y, int mapWidth, int mapHeight)
            {
                const float a = 3f;
                const float b = 2.2f;

                float normalizedX = x / (float)mapWidth * 2 - 1;
                float normalizedY = y / (float)mapHeight * 2 - 1;

                float value = math.max(math.abs(normalizedX), math.abs(normalizedY));
                return math.pow(value, a) / b;
            }
        }
        
        [BurstCompile]
        private struct GaussianBlurHorizontalJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<float> Input;
            [NativeDisableParallelForRestriction] [WriteOnly] public NativeArray<float> Output;
            [ReadOnly] public NativeArray<float> Kernel;
            public int KernelSize;
            public int Width;

            public void Execute(int yIndex)
            {
                var y = yIndex;
                int halfKernel = KernelSize / 2;
                
                for (int x = 0; x < Width; x++)
                {
                    float sum = 0f;
                    float weightSum = 0f;

                    for (int k = -halfKernel; k <= halfKernel; k++)
                    {
                        int nx = x + k;
                        if (nx >= 0 && nx < Width)
                        {
                            int index = y * Width + nx;
                            float weight = Kernel[k + halfKernel];
                            sum += Input[index] * weight;
                            weightSum += weight;
                        }
                    }

                    int outputIndex = y * Width + x;
                    Output[outputIndex] = sum / weightSum;
                }
            }
        }

        [BurstCompile]
        private struct GaussianBlurVerticalJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<float> Input;
            [NativeDisableParallelForRestriction] [WriteOnly] public NativeArray<float> Output;
            [ReadOnly] public NativeArray<float> Kernel;
            public int KernelSize;
            public int Width;
            public int Height;

            public void Execute(int xIndex)
            {
                int x = xIndex;
                
                int halfKernel = KernelSize / 2;
                for (int y = 0; y < Height; y++)
                {
                    float sum = 0f;
                    float weightSum = 0f;

                    for (int k = -halfKernel; k <= halfKernel; k++)
                    {
                        int ny = y + k;
                        if (ny >= 0 && ny < Height)
                        {
                            int index = ny * Width + x;
                            float weight = Kernel[k + halfKernel];
                            sum += Input[index] * weight;
                            weightSum += weight;
                        }
                    }

                    int outputIndex = y * Width + x;
                    Output[outputIndex] = sum / weightSum;
                }
            }
        }

        [BurstCompile]
        private static void GenerateGaussianKernel(NativeArray<float> kernel, int size, float sigma)
        {
            float sum = 0f;
            float mean = size / 2f;

            for (int i = 0; i < size; i++)
            {
                float x = i - mean;
                float g = math.exp(-0.5f * (x * x) / (sigma * sigma));
                kernel[i] = g;
                sum += g;
            }

            // Нормализация ядра
            for (int i = 0; i < size; i++)
            {
                kernel[i] /= sum;
            }
        }
    }
}