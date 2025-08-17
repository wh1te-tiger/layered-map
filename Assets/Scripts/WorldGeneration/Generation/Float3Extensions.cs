using Unity.Burst;
using Unity.Mathematics;

namespace WorldGeneration.Generation
{
    public static class Float3Extensions
    {
        [BurstCompile]
        public static int GetHash(ref this float3 v, float precision = 1000f)
        {
            int x = (int)math.round(v.x * precision);
            int y = (int)math.round(v.y * precision);
            int z = (int)math.round(v.z * precision);
            int h = 73856093 ^ x;
            h = (h * 19349663) ^ y;
            h = (h * 83492791) ^ z;
            return h;
        }
    }
}