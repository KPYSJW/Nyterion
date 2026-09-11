Shader "Nytherion/Combat/Laser Effect Line"
{
    Properties
    {
        [MainTexture] _MainTex ("LaserEffect2", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)
        _FrameCount ("Frame Count", Float) = 6
        _AnimationSpeed ("Animation Speed", Float) = 10
        _Tiling ("Length Tiling", Float) = 1
        _Intensity ("Color Intensity", Range(1, 3)) = 1
        _AlphaBoost ("Alpha Boost", Range(1, 4)) = 1
        [HideInInspector] _AnimationTime ("Animation Time", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        // 중심부는 배경을 가리고 외곽 알파는 부드럽게 남겨 레이저가 흐려 보이지 않게 합니다.
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        Pass
        {
            Name "LaserEffectLine"
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

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _FrameCount;
                float _AnimationSpeed;
                float _Tiling;
                float _Intensity;
                float _AlphaBoost;
                float _AnimationTime;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float frameCount = max(1.0, floor(_FrameCount + 0.5));
                // 발사할 때마다 0번 프레임부터 시작하고 마지막 프레임에서 멈춥니다.
                float frame = min(floor(_AnimationTime * _AnimationSpeed), frameCount - 1.0);

                // LaserEffect2.png는 가로 방향 레이저 6프레임이 가로로 배치된 시트입니다.
                // LineRenderer의 길이 UV를 현재 프레임 안에서 반복하고 세로 UV는 그대로 사용합니다.
                float2 laserUV;
                laserUV.x = (frame + frac(input.uv.x * max(0.0001, _Tiling))) / frameCount;
                laserUV.y = saturate(input.uv.y);

                half4 laser = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, laserUV);
                half4 tint = _Color * input.color;
                half alpha = saturate(laser.a * tint.a * _AlphaBoost);
                half3 color = saturate(laser.rgb * tint.rgb * _Intensity);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}
