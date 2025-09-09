using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

namespace WorldGeneration.Generation.Jobs
{
    [BurstCompile]
    public struct AddBaseOffsetJob : IJobParallelFor
    {
        public NativeArray<int> firstIndex; // in/out
        
        [ReadOnly] public OffsetData offsetData;
        [ReadOnly] public OffsetType offsetType;
        
        public void Execute(int index)
        {
            int v = firstIndex[index];
            if (v >= 0) firstIndex[index] = v + offsetData.GetOffset(offsetType);
        }
    }
}