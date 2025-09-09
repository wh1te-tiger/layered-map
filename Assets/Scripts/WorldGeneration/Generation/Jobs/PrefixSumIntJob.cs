using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

namespace WorldGeneration.Generation.Jobs
{
    [BurstCompile]
    public struct PrefixSumIntJob : IJob
    {
        [ReadOnly] public NativeArray<int> values;   // per-cell triangle count
        public NativeArray<int> prefix;              // out: exclusive prefix (в штуках триугольников)
        public NativeReference<int> total;           // out: sum(values)
        
        public void Execute()
        {
            int s = 0;
            for (int i = 0; i < values.Length; i++)
            {
                prefix[i] = s;
                s += values[i];
            }
            total.Value = s;
        }
    }
}