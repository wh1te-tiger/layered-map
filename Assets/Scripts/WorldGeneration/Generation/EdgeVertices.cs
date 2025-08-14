using Unity.Burst;
using Unity.Burst.CompilerServices;
using Unity.Mathematics;

namespace WorldGeneration.Generation
{
    //  v01  . T .  v11
    //  .             .
    //  L             R
    //  .             .
    //  v00  . B .  v10
    [BurstCompile]
    struct EdgeVertices
    {
        public float3 Left
        {
            get
            {
                if (!_leftComputed)
                {
                    s_InterpPtr.Invoke(ref _left, _v00, _v01, _v00Height, _v01Height, _targetHeight);
                    _leftComputed = true;
                }
                return _left;
            }
        }
        public float3 Right
        {
            get
            {
                if (!_rightComputed)
                {
                    s_InterpPtr.Invoke(ref _right,_v10, _v11, _v10Height, _v11Height, _targetHeight);
                    _rightComputed = true;
                }
                return _right;
            }
        }
        public float3 Bottom
        {
            get
            {
                if (!_bottomComputed)
                {
                    s_InterpPtr.Invoke(ref _bottom, _v00, _v10, _v00Height, _v10Height, _targetHeight);
                    _bottomComputed = true;
                }
                return _bottom;
            }
        }
        public float3 Top
        {
            get
            {
                if (!_topComputed)
                {
                    s_InterpPtr.Invoke(ref _top, _v01, _v11, _v01Height, _v11Height, _targetHeight);
                    _topComputed = true;
                }
                return _top;
            }
        }

        private float3 _left;
        private float3 _right;
        private float3 _bottom;
        private float3 _top;
        
        private bool _leftComputed;
        private bool _rightComputed;
        private bool _bottomComputed;
        private bool _topComputed;
        
        private readonly float3 _v00;
        private readonly float3 _v10;
        private readonly float3 _v01;
        private readonly float3 _v11;

        private readonly float _v00Height;
        private readonly float _v10Height;
        private readonly float _v01Height;
        private readonly float _v11Height;

        private readonly float _targetHeight;
        
        private delegate void InterpFn(ref float3 result, in float3 v1, in float3 v2, float h1, float h2, float target);
        private static readonly FunctionPointer<InterpFn> s_InterpPtr = BurstCompiler.CompileFunctionPointer<InterpFn>(InterpolateEdge);
        
        private const float Accuracy = 1e-20f;

        public EdgeVertices(float3 v00, float3 v10, float3 v01, float3 v11, float h00, float h10,
            float h01, float h11, float targetHeight)
        {
            _v00 = v00; _v10 = v10; _v01 = v01; _v11 = v11;
            _v00Height = h00; _v10Height = h10; _v01Height = h01; _v11Height = h11;
            _targetHeight = targetHeight;

            _left = default; _right = default; _bottom = default; _top = default;
            _leftComputed = _rightComputed = _bottomComputed = _topComputed = false;
        }

        [BurstCompile]
        private static void InterpolateEdge(ref float3 result, in float3 v1, in float3 v2, float h1, float h2, float target)
        {
            // Защита от деления на ноль в плоском ребре
            float denom = h2 - h1;
            // Hint к оптимизации: если denom == 0, t = 0 (или 0.5 — но выберем 0, чтобы стабильно брать v1)
            if (Hint.Likely(math.abs(denom) > Accuracy))
            {
                float t = (target - h1) / denom;
                t = math.saturate(t);
                result = new float3(
                    math.lerp(v1.x, v2.x, t),
                    v1.y, // сохраняем высоту
                    math.lerp(v1.z, v2.z, t)
                );
                return;
            }

            result = v1;
        }
    }
}