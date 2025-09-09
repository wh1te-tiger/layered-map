using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;

namespace WorldGeneration.Generation.Jobs
{
    [BurstCompile]
    public struct BuildTopIndexBufferJob : IJobParallelFor
    {
        // Входные данные по клеткам
        [ReadOnly] public NativeArray<GridCell> Cells;

        // Размеры сетки узлов (nodesX = cellsX+1, nodesY = cellsY+1)
        [ReadOnly] public int nodesX, nodesY;

        [ReadOnly] public LayerInfo LayerInfo;

        [NativeDisableContainerSafetyRestriction] [WriteOnly]
        public NativeArray<ushort> IB_16;

        [NativeDisableContainerSafetyRestriction] [WriteOnly]
        public NativeArray<int> IB_32;

        public bool UseU16;


        public void Execute(int index)
        {
            var cell = Cells[index];
            int x = cell.coordinates.x;
            int y = cell.coordinates.y;

            int mask = GridCell.GetCellType(cell.v00, cell.v10, cell.v01, cell.v11, LayerInfo.Iso);

            // старт записи в индекс-буфер (в индексах, не в байтах)
            int write = LayerInfo.TriOffsets[index] * 3;
            ProcessCell(x, y, mask, write);
        }

        private void ProcessCell(int x, int y, int mask, int wPointer)
        {
            int v00 = GetCornerIndex(x, y);
            int v10 = GetCornerIndex(x + 1, y);
            int v01 = GetCornerIndex(x, y + 1);
            int v11 = GetCornerIndex(x + 1, y + 1);

            int left = GetEdgeVerticalIndex(x, y);
            int right = GetEdgeVerticalIndex(x + 1, y);
            int bottom = GetEdgeHorizontalTopIndex(x, y);
            int top = GetEdgeHorizontalTopIndex(x, y + 1);

            switch (mask)
            {
                case 0: break;

                case 1: // w.AddTriangle(v00, left, bottom);
                    Write(v00, left, bottom, ref wPointer);
                    break;

                case 2: // w.AddTriangle(v10, bottom, right);
                    Write(v10, bottom, right, ref wPointer);
                    break;

                case 3: // w.AddQuad(v00, left, right, v10);
                    Write(v00, left, right, ref wPointer); // (a,b,c)
                    Write(v00, right, v10, ref wPointer); // (a,c,d)
                    break;

                case 4: // w.AddTriangle(v01, top, left);
                    Write(v01, top, left, ref wPointer);
                    break;

                case 5: // w.AddQuad(v00, v01, top, bottom);
                    Write(v00, v01, top, ref wPointer);
                    Write(v00, top, bottom, ref wPointer);
                    break;

                case 6:
                    // w.AddTriangle(v01, bottom, right);
                    // w.AddTriangle(v10, top, left);
                    Write(v01, bottom, right, ref wPointer);
                    Write(v10, top, left, ref wPointer);
                    break;

                case 7: // w.AddPentagon(v00, v01, top, right, v10);
                    Write(v00, v01, top, ref wPointer);
                    Write(v00, top, right, ref wPointer);
                    Write(v00, right, v10, ref wPointer);
                    break;

                case 8: // w.AddTriangle(v11, right, top);
                    Write(v11, right, top, ref wPointer);
                    break;

                case 9:
                    // w.AddTriangle(v00, left, bottom);
                    // w.AddTriangle(v11, right, top);
                    Write(v00, left, bottom, ref wPointer);
                    Write(v11, right, top, ref wPointer);
                    break;

                case 10: // w.AddQuad(bottom, top, v11, v10);
                    Write(bottom, top, v11, ref wPointer);
                    Write(bottom, v11, v10, ref wPointer);
                    break;

                case 11: // w.AddPentagon(v10, v00, left, top, v11);
                    Write(v10, v00, left, ref wPointer);
                    Write(v10, left, top, ref wPointer);
                    Write(v10, top, v11, ref wPointer);
                    break;

                case 12: // w.AddQuad(left, v01, v11, right);
                    Write(left, v01, v11, ref wPointer);
                    Write(left, v11, right, ref wPointer);
                    break;

                case 13: // w.AddPentagon(v01, v11, right, bottom, v00);
                    Write(v01, v11, right, ref wPointer);
                    Write(v01, right, bottom, ref wPointer);
                    Write(v01, bottom, v00, ref wPointer);
                    break;

                case 14: // w.AddPentagon(v11, v10, bottom, left, v01);
                    Write(v11, v10, bottom, ref wPointer);
                    Write(v11, bottom, left, ref wPointer);
                    Write(v11, left, v01, ref wPointer);
                    break;

                case 15: // w.AddQuad(v00, v01, v11, v10);
                    Write(v00, v01, v11, ref wPointer);
                    Write(v00, v11, v10, ref wPointer);
                    break;
            }
        }

        private void Write(int v0, int v1, int v2, ref int wPointer)
        {
            if (UseU16)
            {
                IB_16[wPointer++] = (ushort)v0;
                IB_16[wPointer++] = (ushort)v1;
                IB_16[wPointer++] = (ushort)v2;
            }
            else
            {
                IB_32[wPointer++] = v0;
                IB_32[wPointer++] = v1;
                IB_32[wPointer++] = v2;
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

        private int GetEdgeVerticalIndex(int x, int y)
        {
            return LayerInfo.EdgeVTopIndexes[y * nodesX + x];
        }
    }
}