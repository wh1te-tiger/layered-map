using UnityEngine;

namespace WorldGeneration.Generation
{
    /// <summary>
    /// Структура для хранения данных о слое с использованием MeshData
    /// </summary>
    public struct LayerData
    {
        public int layerIndex;
        public float baseHeight;
        public float topHeight;
        public float normalizedThreshold;
        public Mesh.MeshDataArray meshDataArray;
        public Mesh.MeshData meshData;
        
        public LayerData(int index, float baseH, float topH, float threshold)
        {
            layerIndex = index;
            baseHeight = baseH;
            topHeight = topH;
            normalizedThreshold = threshold;
            meshDataArray = Mesh.AllocateWritableMeshData(1);
            meshData = meshDataArray[0];
        }
        
        public void Dispose()
        {
            /*if (meshDataArray.IsCreated)
                meshDataArray.Dispose();*/
            meshDataArray.Dispose();
        }
        
        /// <summary>
        /// Создаёт Mesh из MeshData
        /// </summary>
        public Mesh CreateMesh()
        {
            var mesh = new Mesh();
            Mesh.ApplyAndDisposeWritableMeshData(meshDataArray, mesh);
            return mesh;
        }
    }
}