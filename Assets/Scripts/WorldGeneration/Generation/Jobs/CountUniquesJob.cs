using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace WorldGeneration.Generation.Jobs
{
    [BurstCompile]
    public struct CountUniquesJob : IJob
    {
        [ReadOnly] public NativeStream.Reader TriangleStreamReader;
        public int ForEachCount;

        public NativeParallelHashSet<int> HashSet;
        public NativeReference<int> TriangleCount;
        
        public void Execute()
        {
            int tri = 0;
            for (int fi = 0; fi < ForEachCount; fi++)
            {
                int items = TriangleStreamReader.BeginForEachIndex(fi);
                for (int k = 0; k < items; k += 3)
                {
                    var v0 = TriangleStreamReader.Read<float3>();
                    var v1 = TriangleStreamReader.Read<float3>();
                    var v2 = TriangleStreamReader.Read<float3>();
                    HashSet.Add(v0.GetHash());
                    HashSet.Add(v1.GetHash());
                    HashSet.Add(v2.GetHash());
                    tri++;
                }
                TriangleStreamReader.EndForEachIndex();
            }
            TriangleCount.Value = tri;
        }
    }
}