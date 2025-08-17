using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

namespace WorldGeneration.Generation.Jobs
{
    [BurstCompile]
    public struct FilterActiveCellsJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<GridCell> GridCells;
        public float LayerThreshold;
        
        [WriteOnly] public NativeList<int>.ParallelWriter ActiveCellIndices;
        
        public void Execute(int index)
        {
            GridCell cell = GridCells[index];
            var type = GridCell.GetCellType(cell.v00, cell.v01, cell.v10, cell.v11, LayerThreshold);
            
            if (type != 0)
            {
                ActiveCellIndices.AddNoResize(index);
            }
        }
    }
}