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
            int mapWidth = _config.MapWidth;
            int mapHeight = _config.MapHeight;
            int heightLevels = _config.Heightmap.HeightLevels;
            float layerHeightStep = 1f / heightLevels;
            float worldLayerHeight = _config.LayerHeight;
            float cellSize = _config.CellSize;

            int gridX = mapWidth - 1;
            int gridY = mapHeight - 1;


            var gridCells = CreateGridCells(_config.Heightmap.GetHeightMapArray(), gridX, gridY);
            var layerInfos = new NativeList<LayerInfo>(heightLevels, allocator: Allocator.TempJob);

            for (int layer = 0; layer < heightLevels; layer++)
            {
                float threshold = layer * layerHeightStep;

                using (var active = FilterActiveCells(gridCells, gridX, gridY, threshold))
                {
                    if (!active.IsCreated || active.Length == 0)
                    {
                        continue;
                    }

                    int activeCount = active.Length;

                    // 1. Параллельная генерация треугольников в NativeStream
                    var triangleStream = new NativeStream(activeCount, Allocator.TempJob);
                    var genJob = new ProcessCellsStreamJob
                    {
                        GridCells = gridCells,
                        ActiveCellIndices = active,
                        TopHeight = (layer + 1) * worldLayerHeight,
                        BaseHeight = layer * worldLayerHeight,
                        CellSize = cellSize,
                        NormalizedLayer = layer * layerHeightStep,
                        TriangleStreamWriter = triangleStream.AsWriter()
                    };

                    // Оптимальный размер батча: min(64, активных_ячеек/процессоров)
                    int batchSize = Mathf.Max(1, Mathf.Min(64, activeCount / (SystemInfo.processorCount * 2)));
                    JobHandle processJobHandle = genJob.Schedule(activeCount, batchSize);

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
                    JobHandle countH = countJob.Schedule(processJobHandle);

                    countH.Complete();
                    int triCount = triCountRef.Value;
                    int vertexCount = uniq.Count();

                    layerInfos.Add(new LayerInfo
                    {
                        Index = layer,
                        Threshold = threshold,
                        ActiveCellsCount = activeCount,
                        StreamRef = triangleStream,
                        TrisCount = triCount,
                        VertexCount = vertexCount
                    });

                    triCountRef.Dispose();
                    uniq.Dispose();
                }
            }
            
            // 2) Подготовим Mesh[] и единый MeshDataArray по числу НЕ пустых слоёв.
            int layersCount = layerInfos.Length;
            var meshes = new Mesh[layersCount];
            for (int i = 0; i < layersCount; i++)
                meshes[i] = new Mesh { name = $"Layer_{layerInfos[i].Index}" };

            var meshDataArray = Mesh.AllocateWritableMeshData(layersCount);
            var buildHandles = new NativeArray<JobHandle>(layersCount, Allocator.TempJob);

            for (int layer = 0; layer < layersCount; layer++)
            {
                var layerInfo = layerInfos[layer];
                var meshData = meshDataArray[layer];

                meshData.SetVertexBufferParams(
                    layerInfo.VertexCount,
                    new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3, 0),
                    new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 2, 1)
                );
                var vbPos = meshData.GetVertexData<float3>(0);
                var vbUV0 = meshData.GetVertexData<float2>(1);

                bool useU16 = layerInfo.VertexCount <= 65535;
                meshData.SetIndexBufferParams(layerInfo.TrisCount * 3,
                    useU16 ? IndexFormat.UInt16 : IndexFormat.UInt32);
                var ib16 = useU16 ? meshData.GetIndexData<ushort>() : default;
                var ib32 = useU16 ? default : meshData.GetIndexData<int>();

                meshData.subMeshCount = 1;
                meshData.SetSubMesh(0,
                    new SubMeshDescriptor(0, layerInfo.TrisCount * 3),
                    MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices
                );

                var indexMap = new NativeParallelHashMap<int, int>(layerInfo.VertexCount, Allocator.TempJob);
                var nextIndex = new NativeReference<int>(Allocator.TempJob);

                // 3. Сборка напрямую в MeshData
                var buildJob = new BuildMeshFromStreamJob
                {
                    TriangleStreamReader = layerInfo.StreamRef.AsReader(),
                    ForEachCount = layerInfo.ActiveCellsCount,
                    IsoThershold = layerInfo.Threshold,
                    pos_VertexBuffer = vbPos,
                    uv0_VertexBuffer = vbUV0,
                    IB_U16 = ib16,
                    IB_U32 = ib32,
                    UseU16 = useU16,
                    //IndexMap = indexMap,
                    NextIndex = nextIndex,
                };

                buildHandles[layer] = buildJob.Schedule();
                indexMap.Dispose(buildHandles[layer]);
                nextIndex.Dispose(buildHandles[layer]);
            }

            JobHandle.CombineDependencies(buildHandles).Complete();
            Mesh.ApplyAndDisposeWritableMeshData(
                meshDataArray,
                meshes,
                MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices |
                MeshUpdateFlags.DontNotifyMeshUsers
            );
            gridCells.Dispose();
            buildHandles.Dispose();

            for (int i = 0; i < layersCount; i++)
            {
                var mesh = meshes[i];
                mesh.RecalculateBounds();
                mesh.RecalculateNormals();

                var go = new GameObject($"Layer_{layerInfos[i].Index}");
                go.transform.SetParent(_root, false);
                var mf = go.AddComponent<MeshFilter>();
                var mr = go.AddComponent<MeshRenderer>();
                mf.sharedMesh = mesh;
                mr.sharedMaterial = _config.LayerMaterial;
            }

            foreach (var info in layerInfos)
            {
                info.DisposeTemps();
            }
            layerInfos.Dispose();
        }

        private static NativeArray<GridCell> CreateGridCells(float[] heightMap, int gridWidth, int gridHeight)
        {
            NativeArray<float> map = new NativeArray<float>(heightMap, Allocator.TempJob);
            try
            {
                // Создаем массив GridCell
                var cells = new NativeArray<GridCell>(gridWidth * gridHeight, Allocator.Persistent);

                var cellsJob = new CreateGridCellsJob
                {
                    HeightMap = map,
                    Width = gridWidth,
                    Cells = cells
                };

                // Оптимальный размер батча: min(64, активных_ячеек/процессоров)
                int batchSize = Mathf.Max(1, Mathf.Min(64, cells.Length / (SystemInfo.processorCount * 2)));
                cellsJob.Schedule(cells.Length, batchSize).Complete();

                return cells;
            }
            finally
            {
                map.Dispose();
            }
        }

        private static NativeArray<int> FilterActiveCells(in NativeArray<GridCell> gridCells, int gridWidth,
            int gridHeight, float threshold)
        {
            var list = new NativeList<int>(gridWidth * gridHeight, Allocator.TempJob);
            NativeArray<int> res;
            try
            {
                var filterJob = new FilterActiveCellsJob
                {
                    GridCells = gridCells,
                    LayerThreshold = threshold,
                    ActiveCellIndices = list.AsParallelWriter()
                };
                // Оптимальный размер батча: min(64, активных_ячеек/процессоров)
                int batchSize = Mathf.Max(1, Mathf.Min(64, list.Length / (SystemInfo.processorCount * 2)));
                filterJob.Schedule(gridWidth * gridHeight, batchSize).Complete();
                res = new NativeArray<int>(list.AsArray(), Allocator.TempJob);
            }
            finally
            {
                list.Dispose();
            }

            return res;
        }
    }

    struct LayerInfo
    {
        public int Index;
        public float Threshold;
        public int ActiveCellsCount;
        public NativeStream StreamRef;
        public int TrisCount;
        public int VertexCount;

        public void DisposeTemps()
        {
            StreamRef.Dispose();
        }
    }
}