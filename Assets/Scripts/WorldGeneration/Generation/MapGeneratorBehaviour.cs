using System;
using UnityEngine;

namespace WorldGeneration.Generation
{
    public class MapGeneratorBehaviour : MonoBehaviour
    {
        [SerializeField] private MapConfiguration config;
        [SerializeField] private GenerationType generationType;
        
        private void Start()
        {
            var root = new GameObject("Map").transform;
            IMapGenerator mapGenerator;
            switch (generationType)
            {
                case GenerationType.Base:
                    mapGenerator = new MapGeneratorSync(config, root);
                    break;
                case GenerationType.JobsV1:
                    mapGenerator = new MapGeneratorJobsV1(config, root);
                    break;
                case GenerationType.JobsV2:
                    mapGenerator =  new MapGeneratorJobsV2(config, root);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
            
            var t = DateTime.Now;
            mapGenerator.GenerateMap();
            Debug.Log($"Map creation time: {DateTime.Now - t}");
        }
    }
    
    public enum GenerationType
    {
        Base,
        JobsV1,
        JobsV2
    }
}