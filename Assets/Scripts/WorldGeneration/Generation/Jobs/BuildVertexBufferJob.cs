using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace WorldGeneration.Generation.Jobs
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct VertexData
    {
        public float3 pos;
        public float2 uv;
    }

    [BurstCompile]
    public struct BuildVertexBufferJob : IJob
    {
        // Поле клеток (значения v00,v10,v01,v11 и координаты)
        [ReadOnly] public NativeArray<GridCell> cells;
        
        // Геометрия карты
        [ReadOnly] public int cellsX, cellsY;
        [ReadOnly] public int nodesX, nodesY; // nodes = cells + 1
        [ReadOnly] public float cellSize;
        [ReadOnly] public float topY;
        [ReadOnly] public float bottomY;
        [ReadOnly] public LayerInfo LayerInfo;
        
        // Выход — интерливинг-буфер вершин
        [NativeDisableContainerSafetyRestriction] public NativeArray<VertexData> vertexBuffer;

        public void Execute()
        {
            // 1) УГЛЫ (corners)
            for (int gy = 0; gy < nodesY; gy++)
            {
                float z = gy * cellSize;
                for (int gx = 0; gx < nodesX; gx++)
                {
                    int idx = LayerInfo.CornerIndexes[gy * nodesX + gx];
                    if (idx < 0) continue; // этот угол не нужен

                    float x = gx * cellSize;
                    vertexBuffer[idx] = new VertexData
                    {
                        pos = new float3(x, topY, z),
                        uv = new float2(LayerInfo.Iso, 0f)
                    };
                }
            }

            // 2) Edge на ГОРИЗОНТАЛЬНЫХ ребрах (Top/Bottom) — крышка и стенки
            // Bottom: (gx,gy) → между (gx,gy) и (gx+1,gy) с v00/v10 клетки (gx,gy)
            // Top:    (gx,gy+1) → между (gx,gy+1) и (gx+1,gy+1) с v01/v11 клетки (gx,gy)
            for (int gy = 0; gy < nodesY; gy++)
            {
                for (int gx = 0; gx < nodesX - 1; gx++)
                {
                    // --- крышка ---
                    int topIdx = LayerInfo.EdgeHTopIndexes[gy * (nodesX - 1) + gx];
                    if (topIdx >= 0)
                    {
                        // Вычисляем t и точку точно по владельцу ребра
                        float3 p = ComputeEdgeHPosition(gx, gy);
                        vertexBuffer[topIdx] = new VertexData
                        {
                            pos = new float3(p.x, topY, p.z),
                            uv = new float2(LayerInfo.Iso, 0f)
                        };
                    }

                    /*// --- стенка (верх/низ) ---
                    int wallFirst = LayerInfo.HSideIndexes[gy * (nodesX - 1) + gx];
                    if (wallFirst >= 0)
                    {
                        float3 p = ComputeEdgeHPosition(gx, gy, out var tEdge);

                        // Верх стены
                        vertexBuffer[wallFirst] = new VertexData
                        {
                            pos = new float3(p.x, topY, p.z),
                            uv = new float2(tEdge, 0f) // u=доля вдоль ребра, v=0 (верх)
                        };
                        // Низ стены
                        vertexBuffer[wallFirst + 1] = new VertexData
                        {
                            pos = new float3(p.x, bottomY, p.z),
                            uv = new float2(tEdge, 1f) // v=1 (низ)
                        };
                    }*/
                }
            }

            // 3) Edge на ВЕРТИКАЛЬНЫХ ребрах (Left/Right) — крышка и стенки
            // Left:  (gx,gy) → между (gx,gy) и (gx,gy+1) с v00/v01 клетки (gx,gy)
            // Right: (gx+1,gy) → между (gx+1,gy) и (gx+1,gy+1) с v10/v11 клетки (gx,gy)
            for (int gy = 0; gy < nodesY - 1; gy++)
            {
                for (int gx = 0; gx < nodesX; gx++)
                {
                    // --- крышка ---
                    int topIdx = LayerInfo.EdgeVTopIndexes[gy * nodesX + gx];
                    if (topIdx >= 0)
                    {
                        float3 p = ComputeEdgeVPosition(gx, gy);
                        vertexBuffer[topIdx] = new VertexData
                        {
                            pos = new float3(p.x, topY, p.z),
                            uv = new float2(LayerInfo.Iso, 0f)
                        };
                    }

                    /*// --- стенка (верх/низ) ---
                    int wallFirst = LayerInfo.VSideIndexes[gy * nodesX + gx];
                    if (wallFirst >= 0)
                    {
                        float3 p = ComputeEdgeVPosition(gx, gy, out var tEdge);

                        vertexBuffer[wallFirst] = new VertexData
                        {
                            pos = new float3(p.x, topY, p.z),
                            uv = new float2(tEdge, 0f)
                        };
                        vertexBuffer[wallFirst + 1] = new VertexData
                        {
                            pos = new float3(p.x, bottomY, p.z),
                            uv = new float2(tEdge, 1f)
                        };
                    }*/
                }
            }
        }

        // ---------- Вычисление mid-точек и t (0..1) ----------

        // Горизонтальный ребро: (gx,gy) ↔ (gx+1,gy)
        float3 ComputeEdgeHPosition(int gx, int gy) => ComputeEdgeHPosition(gx, gy, out _);

        float3 ComputeEdgeHPosition(int gx, int gy, out float tEdge)
        {
            // Определяем «владельца» ребра и берём его скалярные значения
            // Если gy находится в диапазоне 0..cellsY-1 → это Bottom ребро клетки (gx,gy): v00..v10
            // Если gy == cellsY → это Top ребро клетки (gx,gy-1): v01..v11
            float vA, vB;
            float3 a, b;

            if (gy < cellsY) // Bottom ребро клетки (gx,gy)
            {
                int ci = gy * cellsX + gx;
                var c = cells[ci];
                vA = c.v00;
                vB = c.v10;
                a = new float3(gx * cellSize, 0, gy * cellSize);
                b = new float3((gx + 1) * cellSize, 0, gy * cellSize);
            }
            else // gy == cellsY → Top ребро клетки (gx,gy-1)
            {
                int ci = (gy - 1) * cellsX + gx;
                var c = cells[ci];
                vA = c.v01;
                vB = c.v11;
                a = new float3(gx * cellSize, 0, gy * cellSize);
                b = new float3((gx + 1) * cellSize, 0, gy * cellSize);
            }

            tEdge = SafeT(vA, vB, LayerInfo.Iso);
            return math.lerp(a, b, tEdge);
        }

        // Вертикальный ребро: (gx,gy) ↔ (gx,gy+1)
        float3 ComputeEdgeVPosition(int gx, int gy) => ComputeEdgeVPosition(gx, gy, out _);

        float3 ComputeEdgeVPosition(int gx, int gy, out float tEdge)
        {
            float v0, v1;
            float3 a, b;

            if (gx < cellsX) // Left ребро клетки (gx,gy): v00..v01
            {
                int ci = gy * cellsX + gx;
                var c = cells[ci];
                v0 = c.v00;
                v1 = c.v01;
                a = new float3(gx * cellSize, 0, gy * cellSize);
                b = new float3(gx * cellSize, 0, (gy + 1) * cellSize);
            }
            else // gx == cellsX → Right ребро клетки (gx-1,gy): v10..v11
            {
                int ci = gy * cellsX + (gx - 1);
                var c = cells[ci];
                v0 = c.v10;
                v1 = c.v11;
                a = new float3(gx * cellSize, 0, gy * cellSize);
                b = new float3(gx * cellSize, 0, (gy + 1) * cellSize);
            }

            tEdge = SafeT(v0, v1, LayerInfo.Iso);
            return math.lerp(a, b, tEdge);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static float SafeT(float v0, float v1, float iso)
        {
            float denom = v1 - v0;
            if (math.abs(denom) < 1e-6f) return 0.5f; // вырождение → середина
            return math.saturate((iso - v0) / denom);
        }
    }
}