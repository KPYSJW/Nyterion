Shader "Nytherion/Combat/Void Ray Additive"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        [MainColor] _Color ("Tint", Color) = (1, 1, 1, 1)
        _Intensity ("Intensity", Range(0.5, 3)) = 1.2
        _StrokeExpansion ("Texture Stroke Expansion", Range(0, 2)) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderPipeline" = "UniversalPipeline"
        }

        Blend SrcAlpha One
        Cull Off
        ZWrite Off

        Pass
        {
            Name "VoidRayAdditive"
            Tags { "LightMode" = "Universal2D" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            float4 _MainTex_TexelSize;

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _Color;
                half _Intensity;
                half _StrokeExpansion;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.color = input.color * _Color;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half4 textureColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                float2 strokeOffset = float2(0, _MainTex_TexelSize.y * _StrokeExpansion);
                half4 upperColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv + strokeOffset);
                half4 lowerColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv - strokeOffset);
                half expandedAlpha = max(textureColor.a, max(upperColor.a, lowerColor.a));
                half3 expandedPremultiplied = max(textureColor.rgb * textureColor.a,
                    max(upperColor.rgb * upperColor.a, lowerColor.rgb * lowerColor.a));
                textureColor.rgb = expandedPremultiplied / max(expandedAlpha, 0.0001h);
                textureColor.a = expandedAlpha;
                half alpha = textureColor.a * input.color.a;
                return half4(textureColor.rgb * input.color.rgb * _Intensity, alpha);
            }
            ENDHLSL
        }
    }
}
