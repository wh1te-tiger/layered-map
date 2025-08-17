using UnityEngine;

namespace WorldGeneration.Generation
{
    public abstract class MapGenerator
    {
        protected readonly MapConfiguration Config;
        protected readonly Transform Root;

        public MapGenerator(MapConfiguration config, Transform root)
        {
            Config = config;
            Root = root;
        }
        
        public abstract void GenerateMap();
    }
}