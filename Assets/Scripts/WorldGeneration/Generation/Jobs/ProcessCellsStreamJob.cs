using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace WorldGeneration.Generation.Jobs
{
    [BurstCompile]
    public struct ProcessCellsStreamJob :  IJobParallelFor
    {
        // Вход
        [ReadOnly] public NativeArray<GridCell> GridCells;
        [ReadOnly] public NativeList<int> ActiveCellIndices;
        public float TopHeight;
        public float BaseHeight;
        public float CellSize;
        public float NormalizedLayer;
        
        // Выход — поток вершин треугольников
        [WriteOnly] public NativeStream.Writer TriangleStreamWriter;
        
        public void Execute(int index)
        {
            int cellIndex = ActiveCellIndices[index];
            GridCell cell = GridCells[cellIndex];
            
            int cellType = GridCell.GetCellType(
                cell.v00, cell.v10, cell.v01, cell.v11,
                NormalizedLayer
            );
            
            // Открываем shard для записи этого индекса
            var w = TriangleStreamWriter;
            w.BeginForEachIndex(index);
            ProcessCell(cell, cellType, ref w);
            w.EndForEachIndex();
        }

        private void ProcessCell(GridCell cell, int cellType, ref NativeStream.Writer w)
        {
            float3 v00 = new float3(cell.coordinates.x * CellSize, TopHeight, cell.coordinates.y * CellSize);
            float3 v10 = new float3((cell.coordinates.x + 1) * CellSize, TopHeight, cell.coordinates.y * CellSize);
            float3 v01 = new float3(cell.coordinates.x * CellSize, TopHeight, (cell.coordinates.y + 1) * CellSize);
            float3 v11 = new float3((cell.coordinates.x + 1) * CellSize, TopHeight, (cell.coordinates.y + 1) * CellSize);
            
            // Координаты серединных вершин на рёбрах
            var edgeVertices = new EdgeVertices(
                v00, v10, v01, v11,
                cell.v00, cell.v10, cell.v01, cell.v11,
                NormalizedLayer
            );

            // Обработка 15 случаев (0 - отфильтрован)
            switch (cellType)
            {
                case 1: // Только v00 внутри
                    var left = edgeVertices.Left;
                    var bottom = edgeVertices.Bottom;
                    w.AddTriangle(ref v00, ref left, ref bottom);
                    w.AddSideFace(ref bottom, ref left, BaseHeight);
                    break;

                case 2: // Только v10 внутри
                    var right = edgeVertices.Right;
                    bottom = edgeVertices.Bottom;
                    w.AddTriangle(ref v10, ref bottom, ref right);
                    w.AddSideFace(ref right, ref bottom, BaseHeight);
                    break;

                case 3: // v00 и v10 внутри
                    left = edgeVertices.Left;
                    right = edgeVertices.Right;
                    w.AddQuad(ref v00, ref left, ref right, ref v10);
                    w.AddSideFace(ref right, ref left, BaseHeight);
                    break;

                case 4: // Только v01 внутри
                    left = edgeVertices.Left;
                    var top = edgeVertices.Top;
                    w.AddTriangle(ref v01, ref top, ref left);
                    w.AddSideFace(ref left, ref top, BaseHeight);
                    break;

                case 5: // v00 и v01 внутри
                    top = edgeVertices.Top;
                    bottom = edgeVertices.Bottom;
                    w.AddQuad(ref v00, ref v01, ref top, ref bottom);
                    w.AddSideFace(ref bottom, ref top, BaseHeight);
                    break;

                case 6:
                    /*AddTriangle(GetOrAddVertex(_cellVertices[2], normalizedThreshold),
                        GetOrAddVertex(edgeVertices.Bottom, normalizedThreshold),
                        GetOrAddVertex(edgeVertices.Right, normalizedThreshold));
                    AddTriangle(GetOrAddVertex(_cellVertices[1], normalizedThreshold), GetOrAddVertex(edgeVertices.Top, normalizedThreshold),
                        GetOrAddVertex(edgeVertices.Left, normalizedThreshold));*/
                    break;

                case 7: // v00, v10 и v01 внутри
                    top = edgeVertices.Top;
                    right = edgeVertices.Right;
                    w.AddPentagon(ref v00, ref v01, ref top, ref right, ref v10);
                    w.AddSideFace( ref right, ref top, BaseHeight);
                    break;

                case 8: // Только v11 внутри
                    right = edgeVertices.Right;
                    top = edgeVertices.Top;
                    w.AddTriangle(ref v11, ref right, ref top);
                    w.AddSideFace( ref top, ref right, BaseHeight);
                    break;

                case 9: // v00 и v11 внутри
                    /*AddTriangle(GetOrAddVertex(_cellVertices[0], normalizedThreshold),
                        GetOrAddVertex(edgeVertices.Left, normalizedThreshold),
                        GetOrAddVertex(edgeVertices.Bottom, normalizedThreshold));
                    AddTriangle(GetOrAddVertex(_cellVertices[3], normalizedThreshold),
                        GetOrAddVertex(edgeVertices.Right, normalizedThreshold),
                        GetOrAddVertex(edgeVertices.Top, normalizedThreshold));*/
                    break;

                case 10: // v10 и v11 внутри
                    top = edgeVertices.Top;
                    bottom = edgeVertices.Bottom;
                    w.AddQuad(ref bottom, ref top, ref v11, ref v10);
                    w.AddSideFace(ref top, ref bottom, BaseHeight);
                    break;

                case 11: // v00, v10 и v11 внутри
                    left = edgeVertices.Left;
                    top = edgeVertices.Top;
                    w.AddPentagon(ref v10, ref v00, ref left, ref top, ref v11);
                    w.AddSideFace( ref top, ref left, BaseHeight);
                    break;

                case 12: // v01 и v11 внутри
                    left = edgeVertices.Left;
                    right = edgeVertices.Right;
                    w.AddQuad(ref left, ref v01, ref v11,ref right);
                    w.AddSideFace(ref left, ref right, BaseHeight);
                    break;

                case 13: // v00, v01 и v11 внутри
                    right = edgeVertices.Right;
                    bottom = edgeVertices.Bottom;
                    w.AddPentagon(ref v01, ref v11, ref right, ref bottom, ref v00);
                    w.AddSideFace( ref bottom, ref right, BaseHeight);
                    break;

                case 14: // v10, v01 и v11 внутри
                    left = edgeVertices.Left;
                    bottom = edgeVertices.Bottom;
                    w.AddPentagon(ref v11, ref v10, ref bottom, ref left, ref v01);
                    w.AddSideFace( ref left, ref bottom, BaseHeight);
                    break;

                case 15: // Все вершины внутри
                    w.AddQuad(ref v00, ref v01, ref v11, ref v10);
                    break;
            }
        }
    }
}