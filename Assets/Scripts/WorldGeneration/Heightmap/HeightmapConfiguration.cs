using UnityEngine;

namespace WorldGeneration.Heightmap
{
    public abstract class HeightmapConfiguration : ScriptableObject
    {
        [Header("Размеры карты")]
        [Tooltip("Ширина карты высот в точках пикселях.")]
        [field: SerializeField] public int Width { get; private set; } = 256;
        
        [Tooltip("Высота карты высот в пикселях.")]
        [field: SerializeField] public int Height { get; private set; } = 256;
        
        [Tooltip("Количество уровней высот.")]
        [field: SerializeField] public int HeightLevels { get; private set; } = 12;
        
        [Tooltip("Falloff-эффект - плавное снижение высот к краям карты")]
        [field: SerializeField] public bool UseFalloff { get; private set; }
        
        [Tooltip("Градиент, для визуализации высоты")]
        [field: SerializeField] public Gradient HeightGradient { get; private set; }
        
        public bool RequiresUpdate => GetConfigHash() != ConfigHash;
        
        [System.NonSerialized] protected Texture2D CachedTexture;
        [System.NonSerialized] protected float[,] CachedHeightMap;
        [System.NonSerialized] protected int ConfigHash;

        
        protected abstract float[,] GenerateHeightMap();
        protected abstract int GetConfigHash();


        public float[,] GetHeightMap()
        {
            if (CachedHeightMap == null || RequiresUpdate)
            {
                CachedHeightMap = GenerateHeightMap();
                ConfigHash = GetConfigHash();
            }

            return CachedHeightMap;
        }
        
        public Texture2D GetHeightMapTexture()
        {
            if (CachedTexture == null || 
                CachedTexture.width != Width || 
                CachedTexture.height != Height ||
                RequiresUpdate)
            {
                UpdateTexture();
            }
            return CachedTexture;
        }
        
        public void ForceRefresh()
        {
            CachedHeightMap = null;
            CachedTexture = null;
        }
        
        private void UpdateTexture()
        {
            if (CachedTexture == null || CachedTexture.width != Width || CachedTexture.height != Height)
            {
                CachedTexture = new Texture2D(Width, Height, TextureFormat.RGBA32, false)
                {
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Point
                };
            }

            GetHeightMap();
            
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    float height = CachedHeightMap[x, y];
                    CachedTexture.SetPixel(x, y, HeightGradient.Evaluate(height));
                }
            }

            CachedTexture.Apply();
        }
    }
}