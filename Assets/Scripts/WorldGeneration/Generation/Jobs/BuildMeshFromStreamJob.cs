using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace WorldGeneration.Generation.Jobs
{
    [BurstCompile]
    public struct BuildMeshFromStreamJob : IJob
    {
        [ReadOnly] public NativeStream.Reader TriangleStreamReader;
        public int ForEachCount;

        public float IsoThershold;

        // Выход: прямой доступ к MeshData
        [NativeDisableContainerSafetyRestriction] [WriteOnly] public NativeArray<float3> pos_VertexBuffer; // data.GetVertexData<float3>()
        [NativeDisableContainerSafetyRestriction] [WriteOnly] public NativeArray<float2> uv0_VertexBuffer; // data.GetVertexData<float2>()

        [NativeDisableContainerSafetyRestriction] [WriteOnly] public NativeArray<ushort> IB_U16;
        [NativeDisableContainerSafetyRestriction] [WriteOnly] public NativeArray<int> IB_U32;
        public bool UseU16;

        // Внутренняя карта: hash -> назначенный индекс
        public NativeParallelHashMap<int, int> HashToIndex;
        public NativeReference<int> NextIndex;

        public void Execute()
        {
            int iWrite = 0;
            for (int fi = 0; fi < ForEachCount; fi++)
            {
                int items = TriangleStreamReader.BeginForEachIndex(fi);
                for (int k = 0; k < items; k += 3)
                {
                    var v0 = TriangleStreamReader.Read<float3>();
                    var v1 = TriangleStreamReader.Read<float3>();
                    var v2 = TriangleStreamReader.Read<float3>();

                    int indexV0 = GetOrAssign(v0);
                    int indexV1 = GetOrAssign(v1);
                    int indexV2 = GetOrAssign(v2);

                    if (UseU16)
                    {
                        IB_U16[iWrite++] = (ushort)indexV0;
                        IB_U16[iWrite++] = (ushort)indexV1;
                        IB_U16[iWrite++] = (ushort)indexV2;
                    }
                    else
                    {
                        IB_U32[iWrite++] = indexV0;
                        IB_U32[iWrite++] = indexV1;
                        IB_U32[iWrite++] = indexV2;
                    }
                }

                TriangleStreamReader.EndForEachIndex();
            }
        }

        int GetOrAssign(float3 p)
        {
            int h = p.GetHash();
            if (HashToIndex.TryGetValue(h, out int idx)) return idx;

            int newIdx = NextIndex.Value;
            NextIndex.Value = newIdx + 1;
            HashToIndex.TryAdd(h, newIdx);

            pos_VertexBuffer[newIdx] = p;
            uv0_VertexBuffer[newIdx] = new float2(IsoThershold, 0);
            return newIdx;
        }
    }
}