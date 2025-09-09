using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using WorldGeneration.Generation.Jobs;

namespace WorldGeneration.Generation
{
    public class MapGeneratorJobsV2 : IMapGenerator
    {
        private readonly MapConfiguration _config;
        private readonly Transform _root;

        public MapGeneratorJobsV2(MapConfiguration config, Transform root)
        {
            _config = config;
            _root = root;
        }

        public void GenerateMap()
        {
            float[] heightMap = _config.Heightmap.GetHeightMapArray();
            int mapWidth = _config.MapWidth;
            int mapHeight = _config.MapHeight;
            int heightLevels = _config.Heightmap.HeightLevels;
            float layerHeightStep = 1f / heightLevels;
            float worldLayerHeight = _config.LayerHeight;
            float cellSize = _config.CellSize;

            int gridX = mapWidth - 1;
            int gridY = mapHeight - 1;

            var gridCells = CreateGridCells(heightMap, gridX, gridY);

            var layerInfos = new NativeList<LayerInfo>(heightLevels, Allocator.TempJob);
            for (int i = 0; i < heightLevels; i++)
            {
                float isoThreshold = i * layerHeightStep;

                var layerInfo = CalculatePrefixes(in gridCells, mapWidth, mapHeight, i, isoThreshold);
                if (layerInfo.IndexCount != 0)
                {
                    layerInfos.Add(layerInfo);
                }
            }

            BuildMeshes(in layerInfos, in gridCells, gridX, gridY, cellSize, worldLayerHeight);

            foreach (var layerInfo in layerInfos)
            {
                layerInfo.DisposeTemps();
            }

            gridCells.Dispose();
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

        private LayerInfo CalculatePrefixes(in NativeArray<GridCell> gridCells, int nodesX, int nodesY, int index,
            float isoThreshold)
        {
            int gridX = nodesX - 1;
            int gridY = nodesY - 1;
            var trisPerCell = new NativeArray<int>(gridX * gridY, Allocator.TempJob);

            // 1: Флаги вершин
            var cornerFlags = new NativeArray<byte>(nodesY * nodesX, Allocator.TempJob);
            var edgeHTopFlags = new NativeArray<byte>(nodesY * (nodesX - 1), Allocator.TempJob);
            var edgeVTopFlags = new NativeArray<byte>((nodesY - 1) * nodesX, Allocator.TempJob);
            var hSideFlags = new NativeArray<byte>(nodesY * (nodesX - 1), Allocator.TempJob);
            var vSideFlags = new NativeArray<byte>((nodesY - 1) * nodesX, Allocator.TempJob);

            JobHandle markJobHandle = new MarkNeededJob
            {
                nodesX = nodesX,
                nodesY = nodesY,
                isoThreshold = isoThreshold,
                cells = gridCells,

                cornerFlags = cornerFlags,
                edgeHTopFlags = edgeHTopFlags,
                edgeVTopFlags = edgeVTopFlags,

                hSideFlags = hSideFlags,
                vSideFlags = vSideFlags,

                trisPerCell = trisPerCell
            }.Schedule(gridX * gridY, 128);

            // 2A: префикс по индексам 
            var triOffsets = new NativeArray<int>(trisPerCell.Length, Allocator.Persistent);
            var triTotal = new NativeReference<int>(Allocator.TempJob);
            var prefixIndexesJobHandle = new PrefixSumIntJob
            {
                values = trisPerCell,
                prefix = triOffsets,
                total = triTotal
            }.Schedule(markJobHandle);
            var disposeJobHandle = trisPerCell.Dispose(prefixIndexesJobHandle);

            // 2B: считаем префиксы по вершинам
            var cornerIndex = new NativeArray<int>(cornerFlags.Length, Allocator.Persistent);
            var edgeHTopIndex = new NativeArray<int>(edgeHTopFlags.Length, Allocator.Persistent);
            var edgeVTopIndexes = new NativeArray<int>(edgeVTopFlags.Length, Allocator.Persistent);
            var hSideIndexes = new NativeArray<int>(hSideFlags.Length, Allocator.Persistent);
            var vSideIndexes = new NativeArray<int>(vSideFlags.Length, Allocator.Persistent);

            var cornerCount = new NativeReference<int>(Allocator.TempJob);
            var hTopCount = new NativeReference<int>(Allocator.TempJob);
            var vTopCount = new NativeReference<int>(Allocator.TempJob);
            var hSideCount = new NativeReference<int>(Allocator.TempJob);
            var vSideCount = new NativeReference<int>(Allocator.TempJob);

            var offsetData = new OffsetData(cornerCount, hTopCount, vTopCount, hSideCount, vSideCount);

            // джобы (все зависят от markH)
            var jCorner = new PrefixSumFlagsJob
                    { flags = cornerFlags, weight = 1, firstIndex = cornerIndex, count = cornerCount }
                .Schedule(markJobHandle);
            var jHTop = new PrefixSumFlagsJob
                    { flags = edgeHTopFlags, weight = 1, firstIndex = edgeHTopIndex, count = hTopCount }
                .Schedule(markJobHandle);
            var jVTop = new PrefixSumFlagsJob
                    { flags = edgeVTopFlags, weight = 1, firstIndex = edgeVTopIndexes, count = vTopCount }
                .Schedule(markJobHandle);
            var jHSide = new PrefixSumFlagsJob
                    { flags = hSideFlags, weight = 2, firstIndex = hSideIndexes, count = hSideCount }
                .Schedule(markJobHandle);
            var jVSide = new PrefixSumFlagsJob
                    { flags = vSideFlags, weight = 2, firstIndex = vSideIndexes, count = vSideCount }
                .Schedule(markJobHandle);

            NativeArray<JobHandle> handlesPrefixSum =
                new NativeArray<JobHandle>(new[] { jCorner, jHTop, jVTop, jHSide, jVSide }, Allocator.TempJob);
            var prefixVertexJobHandle = JobHandle.CombineDependencies(handlesPrefixSum);

            // Сохраняем handle’ы от Dispose
            var d1 = cornerFlags.Dispose(prefixVertexJobHandle);
            var d2 = edgeHTopFlags.Dispose(prefixVertexJobHandle);
            var d3 = edgeVTopFlags.Dispose(prefixVertexJobHandle);
            var d4 = hSideFlags.Dispose(prefixVertexJobHandle);
            var d5 = vSideFlags.Dispose(prefixVertexJobHandle);

            NativeArray<JobHandle> disposeHandles =
                new NativeArray<JobHandle>(new[] { d1, d2, d3, d4, d5, disposeJobHandle }, Allocator.TempJob);
            // Объединяем «диспозы флагов»
            var disposeHandle = JobHandle.CombineDependencies(disposeHandles);


            // добавляем оффсеты, чтобы сразу получить ГЛОБАЛЬНЫЕ индексы
            var o1 =
                new AddBaseOffsetJob
                        { firstIndex = cornerIndex, offsetData = offsetData, offsetType = OffsetType.Corner }
                    .Schedule(cornerIndex.Length, 128, prefixVertexJobHandle);
            var o2 =
                new AddBaseOffsetJob
                        { firstIndex = edgeHTopIndex, offsetData = offsetData, offsetType = OffsetType.HTop }
                    .Schedule(edgeHTopIndex.Length, 128, prefixVertexJobHandle);
            var o3 =
                new AddBaseOffsetJob
                        { firstIndex = edgeVTopIndexes, offsetData = offsetData, offsetType = OffsetType.VTop }
                    .Schedule(edgeVTopIndexes.Length, 128, prefixVertexJobHandle);
            var o4 =
                new AddBaseOffsetJob
                        { firstIndex = hSideIndexes, offsetData = offsetData, offsetType = OffsetType.HSide }
                    .Schedule(hSideIndexes.Length, 128, prefixVertexJobHandle);
            var o5 =
                new AddBaseOffsetJob
                        { firstIndex = vSideIndexes, offsetData = offsetData, offsetType = OffsetType.VSide }
                    .Schedule(vSideIndexes.Length, 128, prefixVertexJobHandle);

            NativeArray<JobHandle> handlesAddOffset =
                new NativeArray<JobHandle>(new[] { o1, o2, o3, o4, o5 }, Allocator.TempJob);
            var finalPrefixJobHandle = JobHandle.CombineDependencies(handlesAddOffset);
            var allPrefixWork =
                JobHandle.CombineDependencies(finalPrefixJobHandle, prefixIndexesJobHandle, disposeHandle);
            allPrefixWork.Complete();

            int triangleCount = triTotal.Value;
            int indexCount = triangleCount * 3;

            var res = new LayerInfo
            {
                Index = index,
                Iso = isoThreshold,

                VertexCount = offsetData.GetOffset(OffsetType.All),
                IndexCount = indexCount,
                CornerIndexes = cornerIndex,
                EdgeHTopIndexes = edgeHTopIndex,
                EdgeVTopIndexes = edgeVTopIndexes,
                HSideIndexes = hSideIndexes,
                VSideIndexes = vSideIndexes,

                TriOffsets = triOffsets
            };

            triTotal.Dispose();
            handlesPrefixSum.Dispose();
            handlesAddOffset.Dispose();
            disposeHandles.Dispose();
            offsetData.DisposeTemps();
            return res;
        }

        private void BuildMeshes(in NativeList<LayerInfo> layerInfos, in NativeArray<GridCell> gridCells, int gridX,
            int gridY, float cellSize, float worldLayerHeight)
        {
            int nodeX = gridX + 1;
            int nodeY = gridY + 1;

            int layersCount = layerInfos.Length;
            var meshes = new Mesh[layersCount];
            for (int i = 0; i < layersCount; i++)
                meshes[i] = new Mesh { name = $"Layer_{layerInfos[i].Index}" };

            var meshDataArray = Mesh.AllocateWritableMeshData(layersCount);
            var buildHandles = new NativeArray<JobHandle>(layersCount, Allocator.TempJob);

            for (int i = 0; i < layerInfos.Length; i++)
            {
                var info = layerInfos[i];
                var meshData = meshDataArray[i];

                // 1: Vertex buffer (интерливинг: Position+UV0)
                meshData.SetVertexBufferParams(
                    info.VertexCount,
                    new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3, 0),
                    new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 2, 0)
                );
                var vertexData = meshData.GetVertexData<VertexData>(0);

                bool useU16 = info.VertexCount <= 65535;
                meshData.SetIndexBufferParams(info.IndexCount, useU16 ? IndexFormat.UInt16 : IndexFormat.UInt32);
                var ib16 = useU16 ? meshData.GetIndexData<ushort>() : default;
                var ib32 = useU16 ? default : meshData.GetIndexData<int>();
                if (useU16)
                {
                    ib32 = new NativeArray<int>(0, Allocator.TempJob);
                }
                else
                {
                    ib16 = new NativeArray<ushort>(0, Allocator.TempJob);
                }

                meshData.subMeshCount = 1;
                meshData.SetSubMesh(0,
                    new SubMeshDescriptor(0, info.IndexCount),
                    MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices);

                // 2A: заполняем вершинный буфер
                var vertexBufferJob = new BuildVertexBufferJob
                {
                    cells = gridCells,

                    cellsX = gridX,
                    cellsY = gridY,
                    nodesX = nodeX,
                    nodesY = nodeY,

                    cellSize = cellSize,
                    topY = (info.Index + 1) * worldLayerHeight, 
                    bottomY = info.Index * worldLayerHeight, 
                    LayerInfo = info,

                    vertexBuffer = vertexData
                }.Schedule();

                // 2B: индексы (верх)
                JobHandle topH = new BuildTopIndexBufferJob
                {
                    nodesX = nodeX,
                    nodesY = nodeY,
                    Cells = gridCells,
                    LayerInfo = info,
                    IB_16 = ib16,
                    IB_32 = ib32,
                    UseU16 = useU16
                }.Schedule(gridX * gridY, 128, vertexBufferJob);
                
                // 2C: индексы (бок)
                JobHandle sideH = new BuildSideIndexBufferJob
                {
                    NodesX = nodeX,
                    NodesY = nodeY,
                    Cells = gridCells,
                    LayerInfo = info,
                    IB_16 = ib16,
                    IB_32 = ib32,
                    UseU16 = useU16
                }.Schedule(gridX * gridY, 128, vertexBufferJob);

                buildHandles[i] = JobHandle.CombineDependencies(topH, sideH);
            }

            JobHandle.CombineDependencies(buildHandles).Complete();
            buildHandles.Dispose();

            Mesh.ApplyAndDisposeWritableMeshData(
                meshDataArray,
                meshes,
                MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices |
                MeshUpdateFlags.DontNotifyMeshUsers
            );

            // 5) Выставляем bounds и создаём GO для каждого слоя
            for (int i = 0; i < layerInfos.Length; i++)
            {
                var mesh = meshes[i];
                // bounds лучше задать руками (дёшево и верно):
                var topHeight = layerInfos[i].Index * worldLayerHeight;
                var center = new Vector3((gridX) * cellSize * 0.5f, topHeight * 0.5f, (gridY) * cellSize * 0.5f);
                var size = new Vector3(gridX * cellSize, worldLayerHeight, gridY * cellSize);
                mesh.bounds = new Bounds(center, size);

                var go = new GameObject($"Layer_{layerInfos[i].Index}");
                go.transform.SetParent(_root, false);
                var mf = go.AddComponent<MeshFilter>();
                var mr = go.AddComponent<MeshRenderer>();
                mf.sharedMesh = mesh;
                mr.sharedMaterial = _config.LayerMaterial;
            }
        }
    }

    public struct LayerInfo
    {
        // параметры слоя
        public int Index;
        public float Iso;

        public int VertexCount; // итоговое число вершин
        public int IndexCount; // итоговое число индексов

        public NativeArray<int> CornerIndexes;
        public NativeArray<int> EdgeHTopIndexes;
        public NativeArray<int> EdgeVTopIndexes;
        public NativeArray<int> HSideIndexes;
        public NativeArray<int> VSideIndexes;

        public NativeArray<int> TriOffsets;

        public void DisposeTemps()
        {
            if (CornerIndexes.IsCreated) CornerIndexes.Dispose();
            if (EdgeHTopIndexes.IsCreated) EdgeHTopIndexes.Dispose();
            if (EdgeVTopIndexes.IsCreated) EdgeVTopIndexes.Dispose();
            if (HSideIndexes.IsCreated) HSideIndexes.Dispose();
            if (VSideIndexes.IsCreated) VSideIndexes.Dispose();
            if (TriOffsets.IsCreated) TriOffsets.Dispose();
        }
    }
}