// Wet street reflection (URP). A transparent sheet lying on the road / footpath that shows the planar reflection
// rendered by PlanarReflection.cs (_TB_ReflectionTex, in screen space), stronger at grazing angles (Fresnel),
// broken up by raindrops: rings expanding from random points, a few per square metre, re-seeded every drop.
// Strength follows the globals _TB_Wet (how wet the street is) and _TB_Rain (how hard it rains).
Shader "ThaiBoba/WetReflection"
{
    Properties
    {
        _Tint ("Reflection Tint", Color) = (0.82, 0.86, 0.95, 1)
        _Strength ("Strength", Range(0, 1)) = 0.85
        _Distort ("Ripple Distortion", Range(0, 0.08)) = 0.025
        _RippleScale ("Ripple cells per metre", Float) = 2.4
        _RippleSpeed ("Ripple Speed", Float) = 1.1
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent-60" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "WetReflection"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_TB_ReflectionTex);
            SAMPLER(sampler_TB_ReflectionTex);
            float _TB_Wet;
            float _TB_Rain;

            CBUFFER_START(UnityPerMaterial)
                half4 _Tint;
                float _Strength;
                float _Distort;
                float _RippleScale;
                float _RippleSpeed;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float4 screenPos : TEXCOORD1;
                float fogFactor : TEXCOORD2;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.screenPos = ComputeScreenPos(p.positionCS);
                o.fogFactor = ComputeFogFactor(p.positionCS.z);
                return o;
            }

            float Hash(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            // slope (xz) of the water surface from raindrop rings: two staggered grids of cells, one drop per cell
            float2 Ripples(float2 xz, float t)
            {
                float2 slope = 0;
                [unroll] for (int layer = 0; layer < 2; layer++)
                {
                    float2 q = xz * _RippleScale * (layer == 0 ? 1.0 : 1.63) + layer * 7.31;
                    float2 cell = floor(q);
                    float h = Hash(cell);
                    float rate = _RippleSpeed * (0.75 + 0.5 * h);
                    float cycle = t * rate + h * 13.7;
                    float age = frac(cycle);
                    float seed = floor(cycle);                                   // a new drop lands somewhere else each cycle
                    float2 centre = float2(Hash(cell + seed * 0.37 + 1.3), Hash(cell + seed * 0.71 + 7.1)) * 0.7 + 0.15;
                    float2 d = frac(q) - centre;
                    float r = length(d);
                    float x = (r - age * 0.5) * 38.0;
                    float wave = sin(x) * exp(-x * x * 0.08) * (1.0 - age) * (1.0 - age);
                    slope += d / max(r, 1e-3) * wave;
                }
                return slope;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float wet = saturate(_TB_Wet);
                clip(wet - 0.01);
                float2 uv = i.screenPos.xy / i.screenPos.w;
                float2 slope = Ripples(i.positionWS.xz, _Time.y) * saturate(_TB_Rain * 1.4);
                float3 view = normalize(GetCameraPositionWS() - i.positionWS);
                float fresnel = 0.18 + 0.82 * pow(1.0 - saturate(view.y), 4.0);
                half4 refl = SAMPLE_TEXTURE2D(_TB_ReflectionTex, sampler_TB_ReflectionTex, uv + slope * _Distort);
                half3 col = refl.rgb * _Tint.rgb;
                // the rings themselves catch a little sky light
                col += saturate(length(slope)) * 0.12 * _TB_Rain;
                col = MixFog(col, i.fogFactor);
                return half4(col, saturate(_Strength * wet * fresnel));
            }
            ENDHLSL
        }
    }
}
