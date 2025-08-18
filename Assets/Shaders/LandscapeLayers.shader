Shader "URP/LandscapeLayers"
{
    Properties
    {
        _RampTex        ("Ramp (256x1, Clamp)", 2D) = "white" {}
        _Ambient        ("Flat Ambient", Range(0,1)) = 0.15
        _TopDownLit     ("Top-Down Light", Range(0,1)) = 0.35
        _Saturation     ("Saturation", Range(0,2)) = 1
        _Contrast       ("Contrast", Range(0,2)) = 1
    }

    SubShader
    {
        Tags{
            "RenderType"="Opaque"
            "Queue"="Geometry"
            "RenderPipeline"="UniversalPipeline"
        }

        Pass
        {
            Name "ForwardUnlit"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            // URP shader libs
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_RampTex); SAMPLER(sampler_RampTex);

            CBUFFER_START(UnityPerMaterial)
                float _Ambient;
                float _TopDownLit;
                float _Saturation;
                float _Contrast;
            CBUFFER_END

            struct Attributes {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv0        : TEXCOORD0; // uv0.x = t01 (0..1)
            };

            struct VertexData {
                float4 positionCS : SV_POSITION;
                float3 worldPos   : TEXCOORD0;
                float3 worldNrm   : TEXCOORD1;
                float2 uv0        : TEXCOORD2;
                float  fogFactor  : TEXCOORD3;
            };

            VertexData vert (Attributes v)
            {
                VertexData o;
                float3 wp = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(wp);
                o.worldPos   = wp;
                o.worldNrm   = TransformObjectToWorldNormal(v.normalOS);
                o.uv0        = v.uv0;

                // URP fog: использовать clip-space Z
                o.fogFactor = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            float3 sampleRamp(float t){ return SAMPLE_TEXTURE2D(_RampTex, sampler_RampTex, float2(saturate(t),0.5)).rgb; }
            float3 topDownLighting(float3 n){ float nl = saturate(n.y); return float3(nl,nl,nl); }
            float3 saturateColor(float3 c, float s){ float l=dot(c,float3(0.2126,0.7152,0.0722)); return lerp(float3(l,l,l), c, s); }
            float3 contrastColor(float3 c, float k){ return saturate((c-0.5)*k+0.5); }

            half4 frag (VertexData i) : SV_Target
            {
                float t = saturate(i.uv0.x);
                float3 col = sampleRamp(t);
                
                // Простая подсветка сверху
                float3 nrm = normalize(i.worldNrm);
                col *= (_Ambient + _TopDownLit * topDownLighting(nrm));

                // Пост-коррекция
                col = saturateColor(col, _Saturation);
                col = contrastColor(col, _Contrast);

                half4 outCol = half4(col, 1);

                // URP fog
                outCol.rgb = MixFog(outCol.rgb, i.fogFactor);
                return outCol;
            }
            ENDHLSL
        }
    }
    FallBack Off
}