using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using WorldGeneration.Generation.Jobs;

namespace WorldGeneration.Generation
{
    public class MapGeneratorJobsV1 : IMapGenerator
    {
        private readonly MapConfiguration _config;
        private readonly Transform _root;

        public MapGeneratorJobsV1(MapConfiguration config, Transform root)
        {
            _config = config;
            _root = root;
        }

        public void GenerateMap()
        {
            NativeArray<float> heightMap =
                new NativeArray<float>(_config.Heightmap.GetHeightMapArray(), Allocator.TempJob);
            int mapWidth = _config.MapWidth;
            int mapHeight = _config.MapHeight;
            int heightLevels = _config.Heightmap.HeightLevels;
            float layerHeightStep = 1f / heightLevels;
            float worldLayerHeight = _config.LayerHeight;
            float cellSize = _config.CellSize;


            // Создаем массив GridCell
            NativeArray<GridCell> cells =
                new NativeArray<GridCell>((mapWidth - 1) * (mapHeight - 1), Allocator.Persistent);
            var cellsJob = new CreateGridCellsJob
            {
                HeightMap = heightMap,
                Width = mapWidth,
                Cells = cells
            };
            
            // Оптимальный размер батча: min(64, активных_ячеек/процессоров)
            int batchSize = Mathf.Max(1, Mathf.Min(64, cells.Length / (SystemInfo.processorCount * 2)));
            cellsJob.Schedule(cells.Length, batchSize).Complete();
            heightMap.Dispose();

            // Фильтруем пустые клетки
            NativeArray<NativeList<int>> activeCellsIndices =
                new NativeArray<NativeList<int>>(heightLevels, Allocator.Persistent);
            NativeArray<JobHandle> filterHandles = new NativeArray<JobHandle>(heightLevels, Allocator.TempJob);

            for (int layer = 0; layer < heightLevels; layer++)
            {
                float threshold = layer * layerHeightStep;
                activeCellsIndices[layer] = new NativeList<int>((mapWidth - 1) * (mapHeight - 1), Allocator.Persistent);
                var filterJob = new FilterActiveCellsJob
                {
                    GridCells = cells,
                    LayerThreshold = threshold,
                    ActiveCellIndices = activeCellsIndices[layer].AsParallelWriter()
                };

                filterHandles[layer] = filterJob.Schedule(cells.Length, 32);
            }

            // Ожидаем завершения фильтрации
            JobHandle.CombineDependencies(filterHandles).Complete();
            filterHandles.Dispose();

            for (int layer = 0; layer < heightLevels; layer++)
            {
                var active = activeCellsIndices[layer];
                if (!active.IsCreated || active.Length == 0)
                {
                    if (active.IsCreated) active.Dispose();
                    continue;
                }
                
                int activeCount = active.Length;
                
                // 1. Параллельная генерация треугольников в NativeStream
                var triangleStream = new NativeStream(activeCount, Allocator.TempJob);
                var genJob = new ProcessCellsStreamJob
                {
                    GridCells = cells,
                    ActiveCellIndices = active,
                    TopHeight = (layer + 1) * worldLayerHeight,
                    BaseHeight = layer * worldLayerHeight,
                    CellSize = cellSize,
                    NormalizedLayer = layer * layerHeightStep,
                    TriangleStreamWriter = triangleStream.AsWriter()
                };
                
                // Оптимальный размер батча: min(64, активных_ячеек/процессоров)
                batchSize = Mathf.Max(1, Mathf.Min(64, activeCount / (SystemInfo.processorCount * 2)));
                JobHandle genH = genJob.Schedule(activeCount, batchSize);
                
                // 2. Считаем точные размеры (уникальные вершины и число треугольников)
                var uniq = new NativeParallelHashSet<int>(math.max(1024, activeCount * 8), Allocator.TempJob);
                var triCountRef = new NativeReference<int>(Allocator.TempJob);
                var countJob = new CountUniquesJob
                {
                    TriangleStreamReader = triangleStream.AsReader(),
                    ForEachCount = activeCount,
                    HashSet = uniq,
                    TriangleCount = triCountRef
                };
                JobHandle countH = countJob.Schedule(genH);
                
                // Нужны размеры — ждём только этот этап, он быстрый (Burst)
                countH.Complete();
                int triCount = triCountRef.Value;
                int vertexCount = uniq.Count();

                triCountRef.Dispose();
                uniq.Dispose();
                
                // Выделяем MeshData с точными размерами
                var mesh = new Mesh { name = $"Layer_{layer}" };
                var dataArray = Mesh.AllocateWritableMeshData(1);
                var meshData = dataArray[0];

                meshData.SetVertexBufferParams(
                    vertexCount,
                    new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3, 0),
                    new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 2, 1)
                );
                var vbPos = meshData.GetVertexData<float3>(0);
                var vbUV0 = meshData.GetVertexData<float2>(1);

                bool useU16 = vertexCount <= 65535;
                meshData.SetIndexBufferParams(triCount * 3, useU16 ? IndexFormat.UInt16 : IndexFormat.UInt32);
                var ib16 = useU16 ? meshData.GetIndexData<ushort>() : default;
                var ib32 = useU16 ? default : meshData.GetIndexData<int>();

                meshData.subMeshCount = 1;
                meshData.SetSubMesh(0,
                    new SubMeshDescriptor(0, triCount * 3),
                    MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices
                );
                
                // 3. Сборка напрямую в MeshData
                var map = new NativeParallelHashMap<int, int>(vertexCount, Allocator.TempJob);
                var next = new NativeReference<int>(Allocator.TempJob);

                var buildJob = new BuildMeshFromStreamJob
                {
                    TriangleStreamReader = triangleStream.AsReader(),
                    ForEachCount = activeCount,
                    NormalizedLayer = layer * layerHeightStep,
                    pos_VertexBuffer = vbPos,
                    uv0_VertexBuffer = vbUV0,
                    IB_U16 = ib16,
                    IB_U32 = ib32,
                    UseU16 = useU16,
                    HashToIndex = map,
                    NextIndex = next
                };

                JobHandle buildH = buildJob.Schedule(genH);
                buildH.Complete();
                
                // Применяем и чистим временные
                Mesh.ApplyAndDisposeWritableMeshData(
                    dataArray,
                    new[] { mesh },
                    MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices | MeshUpdateFlags.DontNotifyMeshUsers
                );
                mesh.RecalculateBounds();

                // Создаём объект сцены под слой
                var go = new GameObject($"Layer_{layer}");
                go.transform.SetParent(_root, false);
                var filter = go.AddComponent<MeshFilter>();
                var meshRenderer = go.AddComponent<MeshRenderer>();
                meshRenderer.sharedMaterial = _config.LayerMaterial;
                filter.sharedMesh = mesh;

                // Dispose
                map.Dispose();
                next.Dispose();
                triangleStream.Dispose();
                active.Dispose();
            }
            
            activeCellsIndices.Dispose();
            cells.Dispose();
        }
    }
}