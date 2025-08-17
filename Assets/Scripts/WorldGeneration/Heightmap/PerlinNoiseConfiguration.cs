using UnityEngine;

namespace WorldGeneration.Heightmap
{
    [CreateAssetMenu(fileName = "PerlinNoise", menuName = "Game/Configs/Heightmap/PerlinNoise", order = 0)]
    public class PerlinNoiseConfiguration : HeightmapConfiguration
    {
        [Header("Параметры шума")] 
        [Tooltip("Масштаб шума. Чем больше значение, тем более растянутыми будут высоты.")]
        [field: SerializeField] public float NoiseScale { get; private set; } = 20f;
        
        [Tooltip("Количество октав шума. Большее значение добавляет больше деталей, но увеличивает вычислительную нагрузку.")] 
        [field: SerializeField] public int Octaves { get; private set; } = 4;
        
        [Tooltip("Коэффициент грубости. Определяет, как быстро уменьшается амплитуда шума с каждой октавой.")]
        [Range(0f, 1f)] [field: SerializeField] public float Persistence { get; private set; } = 0.5f;
        
        [Tooltip("Коэффициент увеличения частоты шума с каждой октавой.")] 
        [field: SerializeField] public float Lacunarity { get; private set; } = 2f;
        
        [Tooltip("Случайное смещение для шума. Используется для генерации уникальных карт.")] 
        [field: SerializeField] public Vector2 Offset { get; private set; }
        
        [Tooltip("Коэффициент высот шума.")] 
        [Range(0f, 1f)] [field: SerializeField] public float HeightMultiplier { get; private set; } = 1f;
        
        [Tooltip("Кривая высот шума.")] 
        [field: SerializeField] public AnimationCurve HeightCurve { get; private set; }
        
        [Tooltip("Число, для генерации карты")]
        [Range(0, 999999)] [field: SerializeField] public int Seed { get; private set; }
        
        [Header("Smoothing Settings")]
        [Tooltip("Включить или отключить сглаживание.")]
        [field: SerializeField] public bool UseSmooth { get; private set; }
        
        [Tooltip("Степень размытия по Гауссу.")]
        [Range(0, 10f)] [field: SerializeField] public float Sigma { get; private set; } = 1f;
        
        protected override float[] GenerateHeightMap() => PerlinNoiseWithJobs.Generate(this);

        protected override int GetConfigHash()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + Width.GetHashCode();
                hash = hash * 31 + Height.GetHashCode();
                hash = hash * 31 + HeightLevels.GetHashCode();
                hash = hash * 31 + NoiseScale.GetHashCode();
                hash = hash * 31 + Octaves;
                hash = hash * 31 + Persistence.GetHashCode();
                hash = hash * 31 + Lacunarity.GetHashCode();
                hash = hash * 31 + Offset.GetHashCode();
                hash = hash * 31 + HeightMultiplier.GetHashCode();
                hash = hash * 31 + UseFalloff.GetHashCode();
                hash = hash * 31 + Seed.GetHashCode();
                hash = hash * 31 + UseSmooth.GetHashCode();
                hash = hash * 31 + Sigma.GetHashCode();
                return hash;
            }
        }
    }
}