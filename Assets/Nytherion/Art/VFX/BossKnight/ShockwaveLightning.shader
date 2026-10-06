Shader "Nytherion/2D/Shockwave Lightning"
{
    Properties
    {
        [MainTexture] _MainTex ("Lightning Sheet (1 column, rows top to bottom)", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _FrameCount ("Frame Count", Float) = 6
        _FrameIndex ("Frame Index", Float) = 0
        _Repeat ("Repeats Around Ring", Float) = 4
        _Scroll ("Horizontal Scroll", Float) = 0
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
            float2 uv : TEXCOORD0;
            half4 color : COLOR;
        };
        TEXTURE2D(_MainTex);
        SAMPLER(sampler_MainTex);
        CBUFFER_START(UnityPerMaterial)
            float4 _MainTex_TexelSize;
            half4 _Color;
            float _FrameCount;
            float _FrameIndex;
            float _Repeat;
            float _Scroll;
        CBUFFER_END
        Varyings Vert(Attributes input)
        {
            Varyings output;
            output.positionCS = TransformObjectToHClip(input.positionOS);
            output.uv = input.uv;
            output.color = input.color * _Color;
            return output;
        }
        half4 Frag(Varyings input) : SV_Target
        {
            float count = max(1.0, floor(_FrameCount + 0.5));
            float frame = clamp(floor(_FrameIndex), 0.0, count - 1.0);
            // PNG frame zero is the top row; texture UV zero is the bottom.
            float bottom = (count - 1.0 - frame) / count;
            float padding = min(_MainTex_TexelSize.y * 0.5, 0.25 / count);
            float2 uv;
            uv.x = frac(input.uv.x * max(1.0, _Repeat) + _Scroll);
            // Inset row boundaries to avoid sampling a neighboring frame.
            uv.y = lerp(bottom + padding, bottom + 1.0 / count - padding, saturate(input.uv.y));
            half4 textureColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv);
            return textureColor * input.color;
        }
        ENDHLSL
        Pass
        {
            Name "Shockwave2D"
            Tags { "LightMode" = "Universal2D" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            ENDHLSL
        }
        Pass
        {
            Name "ShockwaveForward"
            Tags { "LightMode" = "UniversalForward" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            ENDHLSL
        }
    }
}
