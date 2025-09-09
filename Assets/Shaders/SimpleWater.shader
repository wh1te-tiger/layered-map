Shader "URP/SimpleWater"
{
    Properties
    {
        // Цвета
        _ShallowColor ("Shallow Color", Color) = (0.23,0.66,0.95,1) // #3AA7F2
        _DeepColor    ("Deep Color",    Color) = (0.12,0.48,0.85,1) // #1E7AD9
        _Alpha        ("Alpha", Range(0,1)) = 0.92

        // Волны (две простые синусоиды)
        _Amp1         ("Wave1 Amplitude (m)", Range(0,1)) = 0.12
        _Freq1        ("Wave1 Frequency",     Range(0,10)) = 2.0
        _Speed1       ("Wave1 Speed",         Range(-5,5)) = 1.2
        _Dir1         ("Wave1 Direction (xy)", Vector) = (1,0,0,0)

        _Amp2         ("Wave2 Amplitude (m)", Range(0,1)) = 0.06
        _Freq2        ("Wave2 Frequency",     Range(0,10)) = 3.4
        _Speed2       ("Wave2 Speed",         Range(-5,5)) = -0.8
        _Dir2         ("Wave2 Direction (xy)", Vector) = (0,1,0,0)

        // Френель
        _FresnelPower    ("Fresnel Power",    Range(0.5,8)) = 3.0
        _FresnelStrength ("Fresnel Strength", Range(0,1)) = 0.4

        // Depth Fade (берег) — опционально, требует включённый Depth Texture
        _UseDepthFade      ("Use Depth Fade (0/1)", Float) = 0
        _ShoreFadeDistance ("Shore Fade Distance (m)", Range(0.01,5)) = 1.2
        _FoamBoost         ("Foam Boost", Range(0,2)) = 0.6
        _FoamColor         ("Foam Color", Color) = (1,1,1,1)
    }

    SubShader
    {
        Tags{
            "RenderType"="Transparent"
            "Queue"="Transparent"
            "RenderPipeline"="UniversalPipeline"
        }
        LOD 100
        Cull Back
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            Name "ForwardUnlit"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            // URP includes
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _ShallowColor;
                float4 _DeepColor;
                float  _Alpha;

                float  _Amp1, _Freq1, _Speed1;
                float4 _Dir1; // xy
                float  _Amp2, _Freq2, _Speed2;
                float4 _Dir2; // xy

                float  _FresnelPower;
                float  _FresnelStrength;

                float  _UseDepthFade;
                float  _ShoreFadeDistance;
                float  _FoamBoost;
                float4 _FoamColor;
            CBUFFER_END

            struct Attributes {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct VertexData {
                float4 positionCS : SV_POSITION;
                float3 worldPos   : TEXCOORD0;
                float3 viewDirWS  : TEXCOORD1;
                float  fogFactor  : TEXCOORD2;
                float4 screenPos  : TEXCOORD3;
            };

            // Простые две синус-волны
            float WaveHeight(float2 wsXZ, float t)
            {
                float2 d1 = normalize(_Dir1.xy);
                float2 d2 = normalize(_Dir2.xy);

                float w1 = _Amp1 * sin(dot(wsXZ, d1) * _Freq1 + _Speed1 * t);
                float w2 = _Amp2 * sin(dot(wsXZ, d2) * _Freq2 + _Speed2 * t);
                return w1 + w2;
            }

            VertexData vert (Attributes v)
            {
                VertexData o;

                // В мировые координаты до смещения
                float3 wp = TransformObjectToWorld(v.positionOS.xyz);

                // Время (URP): _Time.y — секунды*2
                float t = _Time.y;

                // Волновое смещение по Y
                wp.y += WaveHeight(wp.xz, t);

                o.positionCS = TransformWorldToHClip(wp);
                o.worldPos   = wp;

                // Направление на камеру (для френеля)
                float3 camPosWS = GetCameraPositionWS();
                o.viewDirWS = camPosWS - wp;

                // Fog + screenPos (для depth fade)
                o.fogFactor = ComputeFogFactor(o.positionCS.z);
                o.screenPos = ComputeScreenPos(o.positionCS);

                return o;
            }

            float3 LerpWaterColor(float3 shallow, float3 deep, float3 viewDir, float3 normalApprox)
            {
                // Френель: чем острее угол, тем ярче
                float nv = abs(dot(normalize(normalApprox), normalize(viewDir)));
                float fres = pow(1.0 - saturate(nv), _FresnelPower) * _FresnelStrength;
                return lerp(deep, shallow, fres);
            }

            half4 frag (VertexData i) : SV_Target
            {
                // Псевдо-нормаль (поверхность почти плоская)
                float3 nrm = float3(0,1,0);

                // Базовый цвет по френелю
                float3 baseCol = LerpWaterColor(_ShallowColor.rgb, _DeepColor.rgb, i.viewDirWS, nrm);

                // Опциональный берег через depth texture (нужно включить Depth Texture в Renderer)
                if (_UseDepthFade > 0.5f)
                {
                    // NDC
                    float2 uv = i.screenPos.xy / i.screenPos.w;

                    // Глубина сцены (terrain/мир) и текущей поверхности
                    #if defined(REQUIRES_SCENE_DEPTH)
                        float  sceneRaw = SampleSceneDepth(uv);
                        float  sceneEye = LinearEyeDepth(sceneRaw, _ZBufferParams);
                        float  surfEye  = i.screenPos.w;
                        float  diff     = sceneEye - surfEye;  // чем меньше, тем ближе берег
                        float  fade     = saturate(1.0 - diff / max(1e-3, _ShoreFadeDistance));

                        // Добавим пену у берега
                        float3 foam = _FoamColor.rgb * _FoamBoost * fade;
                        baseCol = lerp(baseCol, saturate(baseCol + foam), fade);
                    #endif
                }

                half4 col = half4(baseCol, _Alpha);

                // URP Fog
                col.rgb = MixFog(col.rgb, i.fogFactor);
                return col;
            }
            ENDHLSL
        }
    }
    FallBack Off
}
