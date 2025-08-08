using UnityEngine;
using WorldGeneration.Heightmap;

namespace WorldGeneration.Generation
{
    [CreateAssetMenu(fileName = "Map", menuName = "Game/Configs/Map/Map", order = 0)]
    public class MapConfiguration : ScriptableObject
    {
        [field: SerializeField] public HeightmapConfiguration Heightmap { get; private set; }
        [field: SerializeField] public float LayerHeight { get; private set; }
        [field: SerializeField] public float CellSize { get; private set; }
        [field: SerializeField] public Material LayerMaterial { get; private set; }
        
        public int MapWidth => Heightmap.Width;
        public int MapHeight => Heightmap.Height;
    }
}