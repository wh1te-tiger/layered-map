using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;

namespace WorldGeneration.Generation
{
    public static class StreamExtensions
    {
        [BurstCompile]
        public static void AddTriangle(this ref NativeStream.Writer w, ref float3 v0, ref float3 v1, ref float3 v2)
        {
            w.Write(v0);
            w.Write(v1);
            w.Write(v2);
        }
        
        [BurstCompile]
        public static void AddQuad(this ref NativeStream.Writer w, ref float3 v0, ref float3 v1, ref float3 v2, ref float3 v3)
        {
            w.AddTriangle(ref v0, ref v1, ref v2);
            w.AddTriangle(ref v0, ref v2, ref v3);
        }
        
        [BurstCompile]
        public static void AddPentagon(this ref NativeStream.Writer w, ref float3 v0, ref float3 v1, ref float3 v2, ref float3 v3, ref float3 v4)
        {
            w.AddTriangle(ref v0, ref v1, ref v2);
            w.AddTriangle(ref v0, ref v2, ref v3);
            w.AddTriangle(ref v0, ref v3, ref v4);
        }
        
        [BurstCompile]
        public static void AddSideFace(this ref NativeStream.Writer w, ref float3 topEdgeStart, ref float3 topEdgeEnd, float bottomHeight)
        {
            // Нижние вершины боковой грани
            float3 bottomEdgeStart = new float3(topEdgeStart.x, bottomHeight, topEdgeStart.z);
            float3 bottomEdgeEnd = new float3(topEdgeEnd.x, bottomHeight, topEdgeEnd.z);

            // Добавляем боковую грань
            w.AddQuad(ref bottomEdgeStart, ref topEdgeStart, ref topEdgeEnd, ref bottomEdgeEnd);
        }
    }
}