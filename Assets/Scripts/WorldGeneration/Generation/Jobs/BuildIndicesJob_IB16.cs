using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;

namespace WorldGeneration.Generation.Jobs
{
    [BurstCompile]
    public struct BuildIndicesJob_IB16 : IJobParallelFor
    {
        [ReadOnly] public int nodesX, nodesY; // nodes = cells+1
        [ReadOnly] public NativeArray<GridCell> Cells;
        [ReadOnly] public LayerInfo LayerInfo;
        
        [NativeDisableContainerSafetyRestriction] public NativeArray<ushort> IB_U16;

        public void Execute(int index)
        {
            var cell = Cells[index];
            int x = cell.coordinates.x;
            int y = cell.coordinates.y;

            int mask = GridCell.GetCellType(cell.v00, cell.v10, cell.v01, cell.v11, LayerInfo.Iso);
            if (mask == 0) return;
        
            int topTris = MarchingSquaresLookUpTables.TriangleCount[mask];
            var wTop = LayerInfo.TriOffsets[index] * 3;;
            var wSide = wTop + topTris * 3;
        
            ProcessCell(x, y, mask, wTop, wSide);
        }

        private void ProcessCell(int x, int y, int mask, int wTopPointer, int wSidePointer)
        {
            var v00 = GetCornerIndex(x, y);
            var v10 = GetCornerIndex(x + 1, y);
            var v01 = GetCornerIndex(x, y + 1);
            var v11 = GetCornerIndex(x + 1, y + 1);

            var left = GetEdgeVerticalTopIndex(x, y);
            var right = GetEdgeVerticalTopIndex(x + 1, y);
            var bottom = GetEdgeHorizontalTopIndex(x, y);
            var top = GetEdgeHorizontalTopIndex(x, y + 1);
        
            var l = GetEdgeVerticalSideIndex(x, y);
            var r = GetEdgeVerticalSideIndex(x + 1, y);
            var b = GetEdgeHorizontalSideIndex(x, y);
            var t = GetEdgeHorizontalSideIndex(x, y + 1);
        
            switch (mask)
            {
                case 1: 
                    Write(v00, left, bottom, ref wTopPointer);
                    QuadFromPair(b, l, ref wSidePointer);
                    break;
                case 2: 
                    Write(v10, bottom, right, ref wTopPointer);
                    QuadFromPair(r, b, ref wSidePointer);
                    break;
                case 3:
                    Write(v00, left, right, ref wTopPointer);
                    Write(v00, right, v10, ref wTopPointer);
                    QuadFromPair(r, l, ref wSidePointer);
                    break;
                case 4: 
                    Write(v01, top, left, ref wTopPointer);
                    QuadFromPair(l, t, ref wSidePointer);
                    break;
                case 5:
                    Write(v00, v01, top, ref wTopPointer);
                    Write(v00, top, bottom, ref wTopPointer);
                    QuadFromPair(b, t, ref wSidePointer); 
                    break;
                case 6:
                    /*Write(v01, bottom, right, ref wTopPointer);
                    Write(v10, top, left, ref wTopPointer);
                    QuadFromPair(r, t, ref wSidePointer);*/
                    break;
                case 7:
                    Write(v00, v01, top, ref wTopPointer);
                    Write(v00, top, right, ref wTopPointer);
                    Write(v00, right, v10, ref wTopPointer);
                    QuadFromPair(r, t, ref wSidePointer); 
                    break;
                case 8: 
                    Write(v11, right, top, ref wTopPointer);
                    QuadFromPair(t, r, ref wSidePointer);
                    break;
                case 9:
                    /*Write(v00, left, bottom, ref wTopPointer);
                    Write(v11, right, top, ref wTopPointer);
                    QuadFromPair(t, l, ref wSidePointer);*/
                    break;
                case 10:
                    Write(bottom, top, v11, ref wTopPointer);
                    Write(bottom, v11, v10, ref wTopPointer);
                    QuadFromPair(t, b, ref wSidePointer);
                    break;
                case 11:
                    Write(v10, v00, left, ref wTopPointer);
                    Write(v10, left, top, ref wTopPointer);
                    Write(v10, top, v11, ref wTopPointer);
                    QuadFromPair(t, l, ref wSidePointer); 
                    break;
                case 12:
                    Write(left, v01, v11, ref wTopPointer);
                    Write(left, v11, right, ref wTopPointer);
                    QuadFromPair(l, r, ref wSidePointer); 
                    break;
                case 13:
                    Write(v01, v11, right, ref wTopPointer);
                    Write(v01, right, bottom, ref wTopPointer);
                    Write(v01, bottom, v00, ref wTopPointer);
                    QuadFromPair(b, r, ref wSidePointer);
                    break;
                case 14:
                    Write(v11, v10, bottom, ref wTopPointer);
                    Write(v11, bottom, left, ref wTopPointer);
                    Write(v11, left, v01, ref wTopPointer);
                    QuadFromPair(l, b, ref wSidePointer);
                    break;
                case 15:
                    Write(v00, v01, v11, ref wTopPointer);
                    Write(v00, v11, v10, ref wTopPointer);
                    break;
            }
        }
    
        private int GetCornerIndex(int x, int y)
        {
            return LayerInfo.CornerIndexes[y * nodesX + x];
        }

        private int GetEdgeHorizontalTopIndex(int x, int y)
        {
            return LayerInfo.EdgeHTopIndexes[y * (nodesX - 1) + x];
        }

        private int GetEdgeVerticalTopIndex(int x, int y)
        {
            return LayerInfo.EdgeVTopIndexes[y * nodesX + x];
        }
    
        private int GetEdgeHorizontalSideIndex(int x, int y)
        {
            return LayerInfo.HSideIndexes[y * (nodesX - 1) + x];
        }

        private int GetEdgeVerticalSideIndex(int x, int y)
        {
            return LayerInfo.VSideIndexes[y * nodesX + x];
        }

        private void Write(int v0, int v1, int v2, ref int wPointer)
        {
            IB_U16[wPointer++] = (ushort)v0;
            IB_U16[wPointer++] = (ushort)v1;
            IB_U16[wPointer++] = (ushort)v2;
        }
    
        void QuadFromPair(int aFirst, int bFirst, ref int w)
        {
            if (aFirst < 0 || bFirst < 0) return;
            int aTop = aFirst, aBot = aFirst + 1;
            int bTop = bFirst, bBot = bFirst + 1;
            Write(aBot, aTop, bTop, ref w);
            Write(aBot, bTop, bBot, ref w);
        }
    }
}