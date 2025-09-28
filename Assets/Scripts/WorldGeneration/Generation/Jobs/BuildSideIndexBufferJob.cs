/*using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

namespace WorldGeneration.Generation.Jobs
{
    [BurstCompile]
    public  struct BuildSideIndexBufferJob : IJobParallelFor
    {
        [ReadOnly] public int NodesX, NodesY;
        [ReadOnly] public NativeArray<GridCell> Cells;
        [ReadOnly] public LayerInfo LayerInfo;
        [ReadOnly] public bool UseU16;
        
        [NativeDisableParallelForRestriction] [WriteOnly] public NativeArray<ushort> IB_16;
        [NativeDisableParallelForRestriction] [WriteOnly] public NativeArray<int> IB_32;
        public void Execute(int index)
        {
            var cell = Cells[index];
            int x = cell.coordinates.x;
            int y = cell.coordinates.y;

            int mask = GridCell.GetCellType(cell.v00, cell.v10, cell.v01, cell.v11, LayerInfo.Iso);
            if (mask is 0 or 15) return;

            // начало окна клетки в общем IB:
            int startCell = LayerInfo.TriOffsets[index] * 3;

            // отделяем крышу от стен: topTris берём из LUT ещё раз (дёшево)
            int topTris = MarchingSquaresLookUpTables.TriangleCount[mask];

            // начало секции СТЕНОК этой клетки
            int wPointer = startCell + topTris * 3;

            int left = GetEdgeVerticalIndex(x, y);
            int right = GetEdgeVerticalIndex(x + 1, y);
            int bottom = GetEdgeHorizontalTopIndex(x, y);
            int top = GetEdgeHorizontalTopIndex(x, y + 1);

            switch (mask)
            {
                case 1:  QuadFromPair(left, bottom, ref wPointer); break;
                case 2:  QuadFromPair(bottom, right, ref wPointer); break;
                case 3:  QuadFromPair(left, right, ref wPointer); break;
                case 4:  QuadFromPair(top, left, ref wPointer); break;
                case 5:  QuadFromPair(bottom, top, ref wPointer); break;
                case 6:  QuadFromPair(right, top, ref wPointer); break;
                case 7:  QuadFromPair(right, top, ref wPointer); break;
                case 8:  QuadFromPair(right, top, ref wPointer); break;
                case 9:  QuadFromPair(top, left, ref wPointer); break;
                case 10: QuadFromPair(bottom, top, ref wPointer); break;
                case 11: QuadFromPair(left, bottom, ref wPointer); break;
                case 12: QuadFromPair(left, right, ref wPointer); break;
                case 13: QuadFromPair(bottom, right, ref wPointer); break;
                case 14: QuadFromPair(left, bottom, ref wPointer); break;
            }
        }

        private int GetEdgeHorizontalTopIndex(int x, int y)
        {
            return LayerInfo.EdgeHTopIndexes[y * (NodesX - 1) + x];
        }

        private int GetEdgeVerticalIndex(int x, int y)
        {
            return LayerInfo.EdgeVTopIndexes[y * NodesX + x];
        }

        void QuadFromPair(int aFirst, int bFirst, ref int wPointer)
        {
            if (aFirst < 0 || bFirst < 0) return;
            int aTop = aFirst, aBot = aFirst + 1;
            int bTop = bFirst, bBot = bFirst + 1;

            Write(aTop, bTop, bBot, ref wPointer);
            Write(aTop, bBot, aBot, ref wPointer);
        }

        void Write(int a, int b, int c, ref int wPointer)
        {
            if (UseU16)
            {
                IB_16[wPointer++] = (ushort)a;
                IB_16[wPointer++] = (ushort)b;
                IB_16[wPointer++] = (ushort)c;
            }
            else
            {
                IB_32[wPointer++] = a;
                IB_32[wPointer++] = b;
                IB_32[wPointer++] = c;
            }
        }
    }
}*/