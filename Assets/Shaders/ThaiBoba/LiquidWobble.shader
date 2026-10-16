// Liquid inside a cup that stays level and sloshes (URP).
// Everything above a world-space surface plane is clipped; the cup's inner back faces are then seen through the
// cut and drawn in the "surface" colour, which reads as the liquid's top. LiquidSlosh.cs drives the plane.
Shader "ThaiBoba/LiquidWobble"
{
    Properties
    {
        _BaseColor ("Liquid Colour", Color) = (0.8, 0.5, 0.3, 1)
        _TopColor ("Surface Colour", Color) = (0.95, 0.75, 0.55, 1)
        _FoamColor ("Foam Line Colour", Color) = (1, 0.95, 0.88, 1)
        _FoamWidth ("Foam Line Width (m)", Range(0, 0.01)) = 0.003
        _RimPower ("Rim Power", Range(0.5, 8)) = 3
        _RimStrength ("Rim Strength", Range(0, 1)) = 0.25
        _Smoothness ("Smoothness", Range(0, 1)) = 0.6
        // set every frame by LiquidSlosh: xyz = a world point on the surface, w unused
        _FillPoint ("Fill Point (world)", Vector) = (0, 0, 0, 0)
        // x, y = surface slope along world X / Z (height change per metre)
        _Slope ("Surface Slope", Vector) = (0, 0, 0, 0)
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            half4 _TopColor;
            half4 _FoamColor;
            float _FoamWidth;
            float _RimPower;
            float _RimStrength;
            float _Smoothness;
            float4 _FillPoint;
            float4 _Slope;
        CBUFFER_END

        // signed height of a world point above the liquid surface (> 0 = above, clipped)
        float AboveSurface(float3 positionWS)
        {
            float3 d = positionWS - _FillPoint.xyz;
            return d.y - (_Slope.x * d.x + _Slope.y * d.z);
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float fogFactor : TEXCOORD2;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.fogFactor = ComputeFogFactor(p.positionCS.z);
                return o;
            }

            half4 frag(Varyings i, bool front : SV_IsFrontFace) : SV_Target
            {
                float h = AboveSurface(i.positionWS);
                clip(-h);

                Light mainLight = GetMainLight();
                half3 col;
                if (front)
                {
                    // side of the liquid seen through the cup wall
                    float3 n = normalize(i.normalWS);
                    float3 viewDir = normalize(GetWorldSpaceViewDir(i.positionWS));
                    half ndl = saturate(dot(n, mainLight.direction)) * 0.6h + 0.4h;
                    half3 ambient = SampleSH(n);
                    col = _BaseColor.rgb * (mainLight.color * ndl * 0.75h + ambient * 0.6h);
                    half rim = pow(1.0h - saturate(dot(n, viewDir)), _RimPower) * _RimStrength;
                    col += rim * _TopColor.rgb;
                    // thin foam line where the side meets the surface
                    col = lerp(col, _FoamColor.rgb * (mainLight.color * 0.6h + ambient * 0.6h), step(-_FoamWidth, h) );
                }
                else
                {
                    // inner back faces = the liquid top: lit as a tilted, slightly glossy surface
                    float3 n = normalize(float3(-_Slope.x, 1.0, -_Slope.y));
                    float3 viewDir = normalize(GetWorldSpaceViewDir(i.positionWS));
                    half ndl = saturate(dot(n, mainLight.direction)) * 0.5h + 0.5h;
                    half3 ambient = SampleSH(n);
                    col = _TopColor.rgb * (mainLight.color * ndl * 0.7h + ambient * 0.7h);
                    float3 hv = normalize(mainLight.direction + viewDir);
                    col += pow(saturate(dot(n, hv)), 8.0 + _Smoothness * 120.0) * _Smoothness * 0.5h * mainLight.color;
                }
                col = MixFog(col, i.fogFactor);
                return half4(col, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            Cull Off
            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; };
            Varyings vert(Attributes v)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                return o;
            }
            half frag(Varyings i) : SV_Target
            {
                clip(-AboveSurface(i.positionWS));
                return i.positionCS.z;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            Cull Off
            ZWrite On

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; float3 normalWS : TEXCOORD1; };
            Varyings vert(Attributes v)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                return o;
            }
            half4 frag(Varyings i, bool front : SV_IsFrontFace) : SV_Target
            {
                clip(-AboveSurface(i.positionWS));
                float3 n = front ? normalize(i.normalWS) : normalize(float3(-_Slope.x, 1.0, -_Slope.y));
                return half4(n, 0);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
