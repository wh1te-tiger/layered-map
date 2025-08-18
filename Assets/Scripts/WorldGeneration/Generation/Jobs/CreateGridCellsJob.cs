using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace WorldGeneration.Generation.Jobs
{
    [BurstCompile]
    public struct CreateGridCellsJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<float> HeightMap;
        public int Width;
        
        [WriteOnly] public NativeArray<GridCell> Cells;
        
        public void Execute(int index)
        {
            int x = index % Width;
            int z = index / Width;
            
            var v00 = HeightMap[x + z * (Width + 1)];
            var v10 = HeightMap[x + 1 + z * (Width + 1)];
            var v01 = HeightMap[x + (z + 1) * (Width + 1)];
            var v11 = HeightMap[x + 1 + (z + 1) * (Width + 1)];
            
            Cells[index] = new GridCell(v00, v10, v01, v11, new int2(x,z));
        }
    }
}