using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

namespace WorldGeneration.Generation.Jobs
{
    [BurstCompile]
    public struct MarkNeededJob : IJobParallelFor
    {
        [ReadOnly] public int nodesX, nodesY; // = cellsX+1, cellsY+1
        [ReadOnly] public float isoThreshold;
        [ReadOnly] public NativeArray<GridCell> cells;

        // Флаги вершин верхней поверхности 
        [NativeDisableParallelForRestriction] 
        public NativeArray<byte> cornerFlags;
        [NativeDisableParallelForRestriction]
        public NativeArray<byte> edgeHTopFlags; // (горизонтальные рёбра)
        [NativeDisableParallelForRestriction]
        public NativeArray<byte> edgeVTopFlags; // (вертикальные рёбра)

        // Флаги вершин боковой поверхности 
        [NativeDisableParallelForRestriction] public NativeArray<byte> hSideFlags;  //   (горизонтальные рёбра) 
        [NativeDisableParallelForRestriction] public NativeArray<byte> vSideFlags;  //   (вертикальные рёбра)

        // Кол-во треугольников на клетку: крышка + стенки
        [NativeDisableParallelForRestriction] public NativeArray<int> trisPerCell;

        public void Execute(int i)
        {
            var cell = cells[i];
            int x = cell.coordinates.x;
            int y = cell.coordinates.y;

            // Маска 0..15 по порогу
            int cellType = GridCell.GetCellType(cell.v00, cell.v10, cell.v01, cell.v11, isoThreshold);

            // Верхняя поверхность: сколько треугольников 
            int topTris = MarchingSquaresLookUpTables.TriangleCount[cellType];

            // Рёбра, по которым линия уровня пересекает стороны клетки (Left=1,Right=2,Top=4,Bottom=8)
            int edgeMask = MarchingSquaresLookUpTables.EdgeMask[cellType];
            int edgeCount = Popcount4(edgeMask);

            // Стенки: на каждую ПАРУ mid-точек (а это 2 рёбра) — 2 треугольника ⇒ суммарно равно числу рёбер
            int sideTris = edgeCount;

            trisPerCell[i] = topTris ;
            
            if ((cellType & 1) != 0) SetCorner(x, y);
            if ((cellType & 2) != 0) SetCorner(x + 1, y);
            if ((cellType & 4) != 0) SetCorner(x, y + 1);
            if ((cellType & 8) != 0) SetCorner(x + 1, y + 1);

            // СЕРЕДИНЫ рёбер: помечаем и для крышки, и для стенок
            if ((edgeMask & 1) != 0)
            {
                SetEdgeV_Top(x, y);
                SetEdgeV_Side(x, y);
            } // Left: (x,y)-(x,y+1)
            if ((edgeMask & 2) != 0)
            {
                SetEdgeV_Top(x + 1, y);
                SetEdgeV_Side(x + 1, y);
            } // Right: (x+1,y)-(x+1,y+1)
            if ((edgeMask & 4) != 0)
            {
                SetEdgeH_Top(x, y + 1);
                SetEdgeH_Side(x, y + 1);
            } // Top: (x,y+1)-(x+1,y+1)
            if ((edgeMask & 8) != 0)
            {
                SetEdgeH_Top(x, y);
                SetEdgeH_Side(x, y);
            } // Bottom: (x,y)-(x+1,y)
        }

        // ----- helpers -----
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void SetCorner(int gx, int gy)
        {
            if ((uint)gx < (uint)nodesX && (uint)gy < (uint)nodesY)
                cornerFlags[gy * nodesX + gx] = 1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void SetEdgeH_Top(int gx, int gy) // между (gx,gy) и (gx+1,gy)
        {
            if ((uint)gx < (uint)(nodesX - 1) && (uint)gy < (uint)nodesY)
                edgeHTopFlags[gy * (nodesX - 1) + gx] = 1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void SetEdgeV_Top(int gx, int gy) // между (gx,gy) и (gx,gy+1)
        {
            if ((uint)gx < (uint)nodesX && (uint)gy < (uint)(nodesY - 1))
                edgeVTopFlags[gy * nodesX + gx] = 1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void SetEdgeH_Side(int gx, int gy)
        {
            if ((uint)gx < (uint)(nodesX - 1) && (uint)gy < (uint)nodesY)
                hSideFlags[gy * (nodesX - 1) + gx] = 1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void SetEdgeV_Side(int gx, int gy)
        {
            if ((uint)gx < (uint)nodesX && (uint)gy < (uint)(nodesY - 1))
                vSideFlags[gy * nodesX + gx] = 1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static int Popcount4(int m) => ((m >> 0) & 1) + ((m >> 1) & 1) + ((m >> 2) & 1) + ((m >> 3) & 1);
    }
}