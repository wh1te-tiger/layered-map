using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

namespace WorldGeneration.Generation.Jobs
{
    [BurstCompile]
    public struct PrefixSumFlagsJob : IJob
    {
        [ReadOnly] public NativeArray<byte> flags; // 0/1
        public int weight;
        
        // Выход:
        // firstIndex[i] = стартовый локальный индекс (exclusive prefix), либо -1 если флага нет
        public NativeArray<int> firstIndex;
        // count = суммарное число вершин в этой категории с учётом веса
        public NativeReference<int> count;
        
        public void Execute()
        {
            int c = 0;
            for (int i = 0; i < flags.Length; i++)
            {
                if (flags[i] != 0) { firstIndex[i] = c; c += weight; }
                else firstIndex[i] = -1;
            }
            count.Value = c;
        }
    }
}