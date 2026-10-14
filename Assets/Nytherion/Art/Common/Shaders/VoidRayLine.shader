Shader "Nytherion/Combat/Void Ray Additive"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        [MainColor] _Color ("Tint", Color) = (1, 1, 1, 1)
        _Intensity ("Intensity", Range(0.5, 3)) = 1.2
        _StrokeExpansion ("Texture Stroke Expansion", Range(0, 2)) = 0
        [HideInInspector] _FrameCount ("Horizontal Frame Count", Float) = 1
        [HideInInspector] _FrameIndex ("Current Frame", Float) = 0
        [HideInInspector] _FlowOffset ("Length Flow Offset", Float) = 0
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
                float _FrameCount;
                float _FrameIndex;
                float _FlowOffset;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.color = input.color * _Color;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float frameCount = max(1.0, floor(_FrameCount + 0.5));
                float2 textureUV = input.uv * _MainTex_ST.xy + _MainTex_ST.zw;
                if (frameCount > 1.0)
                {
                    // LineRenderer의 길이 UV를 현재 32x32 프레임 안에서만 반복합니다.
                    // 프레임 경계를 연속 샘플링하지 않아 이웃 프레임이 선 위에 섞이지 않습니다.
                    float frame = clamp(floor(_FrameIndex + 0.5), 0.0, frameCount - 1.0);
                    float localU = frac(input.uv.x * max(abs(_MainTex_ST.x), 0.0001) +
                        _MainTex_ST.z + _FlowOffset);
                    textureUV.x = (frame + localU) / frameCount;
                }

                half4 textureColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, textureUV);
                float2 strokeOffset = float2(0, _MainTex_TexelSize.y * _StrokeExpansion);
                half4 upperColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, textureUV + strokeOffset);
                half4 lowerColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, textureUV - strokeOffset);
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
