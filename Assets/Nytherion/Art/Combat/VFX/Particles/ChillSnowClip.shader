Shader "Nytherion/Combat/Chill Snow Clip"
{
    Properties
    {
        [MainTexture] _MainTex ("눈 텍스처", 2D) = "white" {}
        _Color ("색상", Color) = (1, 1, 1, 1)
        [HideInInspector] _ClipRect ("표시 영역", Vector) = (-10000, -10000, 10000, 10000)
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" }
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        struct Attributes
        {
            float3 positionOS : POSITION;
            float2 uv : TEXCOORD0;
            half4 color : COLOR;
        };
        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float2 positionWS : TEXCOORD1;
            float2 uv : TEXCOORD0;
            half4 color : COLOR;
        };

        TEXTURE2D(_MainTex);
        SAMPLER(sampler_MainTex);
        CBUFFER_START(UnityPerMaterial)
            half4 _Color;
            float4 _ClipRect;
        CBUFFER_END

        Varyings Vert(Attributes input)
        {
            Varyings output;
            float3 world = TransformObjectToWorld(input.positionOS);
            output.positionCS = TransformWorldToHClip(world);
            output.positionWS = world.xy;
            output.uv = input.uv;
            output.color = input.color * _Color;
            return output;
        }

        half4 Frag(Varyings input) : SV_Target
        {
            // 입자의 중심이 아닌 픽셀 단위로 잘라 영역 경계에 자연스럽게 걸칩니다.
            clip(input.positionWS.x - _ClipRect.x);
            clip(input.positionWS.y - _ClipRect.y);
            clip(_ClipRect.z - input.positionWS.x);
            clip(_ClipRect.w - input.positionWS.y);
            return SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv) * input.color;
        }
        ENDHLSL

        Pass
        {
            Name "ChillSnow2D"
            Tags { "LightMode" = "Universal2D" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            ENDHLSL
        }
        Pass
        {
            Name "ChillSnowForward"
            Tags { "LightMode" = "UniversalForward" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            ENDHLSL
        }
    }
}
