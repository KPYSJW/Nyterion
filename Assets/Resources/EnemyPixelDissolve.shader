Shader "Nytherion/2D/Enemy Pixel Dissolve"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _MaskTex ("Mask", 2D) = "white" {}
        [HideInInspector] _Color ("Tint", Color) = (1,1,1,1)
        [HideInInspector] _RendererColor ("Renderer Color", Color) = (1,1,1,1)
        [HDR] _FragmentColor ("Fragment Color", Color) = (0.0078,0.0078,0.0078,0.1333)
        _DissolveAmount ("Dissolve Amount", Range(0,1)) = 0
        _PixelGrid ("Pixel Grid", Vector) = (16,16,0,0)
        _SpriteUVRect ("Sprite UV Rect", Vector) = (0,0,1,1)
        _Seed ("Seed", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
            "CanUseSpriteAtlas" = "True"
        }

        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        Pass
        {
            Tags { "LightMode" = "Universal2D" }

            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/LightingUtility.hlsl"

            #pragma vertex LitDissolveVertex
            #pragma fragment LitDissolveFragment
            #pragma multi_compile_instancing
            #pragma multi_compile USE_SHAPE_LIGHT_TYPE_0 __
            #pragma multi_compile USE_SHAPE_LIGHT_TYPE_1 __
            #pragma multi_compile USE_SHAPE_LIGHT_TYPE_2 __
            #pragma multi_compile USE_SHAPE_LIGHT_TYPE_3 __

            struct Attributes
            {
                float3 positionOS : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
                half2 lightingUV : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            TEXTURE2D(_MaskTex);
            SAMPLER(sampler_MaskTex);
            float4 _MainTex_ST;
            half4 _Color;
            half4 _RendererColor;
            half4 _FragmentColor;
            float4 _PixelGrid;
            float4 _SpriteUVRect;
            float _DissolveAmount;
            float _Seed;

            #if USE_SHAPE_LIGHT_TYPE_0
            SHAPE_LIGHT(0)
            #endif
            #if USE_SHAPE_LIGHT_TYPE_1
            SHAPE_LIGHT(1)
            #endif
            #if USE_SHAPE_LIGHT_TYPE_2
            SHAPE_LIGHT(2)
            #endif
            #if USE_SHAPE_LIGHT_TYPE_3
            SHAPE_LIGHT(3)
            #endif

            Varyings LitDissolveVertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                #ifdef UNITY_INSTANCING_ENABLED
                input.positionOS = UnityFlipSprite(input.positionOS, unity_SpriteFlip);
                #endif

                output.positionCS = TransformObjectToHClip(input.positionOS);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.lightingUV =
                    half2(ComputeScreenPos(output.positionCS / output.positionCS.w).xy);
                output.color = input.color * _Color * _RendererColor;

                #ifdef UNITY_INSTANCING_ENABLED
                output.color *= unity_SpriteColor;
                #endif

                return output;
            }

            float PixelHash(float2 cell)
            {
                cell += float2(_Seed * 17.17, _Seed * 41.73);
                return frac(sin(dot(cell, float2(12.9898, 78.233))) * 43758.5453);
            }

            void CalculateCellState(
                float2 uv,
                out float corruptionHash,
                out float dissolveHash)
            {
                float2 rectSize = max(_SpriteUVRect.zw, float2(0.00001, 0.00001));
                float2 localUV = saturate((uv - _SpriteUVRect.xy) / rectSize);
                float2 cell = floor(
                    localUV * max(_PixelGrid.xy, float2(1.0, 1.0)));
                float2 mediumCell = floor(cell / 2.0);
                float2 largeCell = floor(cell / 5.0);

                float corruptionFine = PixelHash(cell + float2(11.3, 47.9));
                float corruptionMedium = PixelHash(
                    mediumCell + float2(73.1, 19.7));
                float corruptionLarge = PixelHash(
                    largeCell + float2(5.9, 131.3));
                corruptionHash =
                    corruptionFine * 0.4 +
                    corruptionMedium * 0.4 +
                    corruptionLarge * 0.2;

                float dissolveFine = PixelHash(cell + float2(37.1, 91.7));
                float dissolveMedium = PixelHash(
                    mediumCell + float2(149.3, 23.5));
                float dissolveLarge = PixelHash(
                    largeCell + float2(61.7, 173.9));
                dissolveHash =
                    dissolveFine * 0.35 +
                    dissolveMedium * 0.45 +
                    dissolveLarge * 0.2;
            }

            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/CombinedShapeLightShared.hlsl"

            half4 LitDissolveFragment(Varyings input) : SV_Target
            {
                half4 mainColor =
                    SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv) * input.color;
                clip(mainColor.a - 0.001h);

                float corruptionHash;
                float dissolveHash;
                CalculateCellState(input.uv, corruptionHash, dissolveHash);

                float corruptionProgress = saturate(_DissolveAmount / 0.42);
                float corruptionMask = step(corruptionHash, corruptionProgress);
                float dissolveProgress = saturate((_DissolveAmount - 0.18) / 0.82);
                float clippingEnabled = step(0.1801, _DissolveAmount);
                float protectedDissolveHash = lerp(
                    1.0,
                    dissolveHash,
                    clippingEnabled);
                clip(protectedDissolveHash - dissolveProgress - 0.0001);

                half4 mask = SAMPLE_TEXTURE2D(_MaskTex, sampler_MaskTex, input.uv);
                SurfaceData2D surfaceData;
                InputData2D inputData;
                InitializeSurfaceData(mainColor.rgb, mainColor.a, mask, surfaceData);
                InitializeInputData(input.uv, input.lightingUV, inputData);

                half4 litColor = CombinedShapeLightShared(surfaceData, inputData);
                half flicker = 0.82h + 0.18h * sin(
                    _Time.y * 18.0h + corruptionHash * 6.2831853h);
                litColor.rgb = lerp(
                    litColor.rgb,
                    _FragmentColor.rgb * flicker,
                    corruptionMask);
                return litColor;
            }
            ENDHLSL
        }

        Pass
        {
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"

            #pragma vertex UnlitDissolveVertex
            #pragma fragment UnlitDissolveFragment
            #pragma multi_compile_instancing

            struct Attributes
            {
                float3 positionOS : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            float4 _MainTex_ST;
            half4 _Color;
            half4 _RendererColor;
            half4 _FragmentColor;
            float4 _PixelGrid;
            float4 _SpriteUVRect;
            float _DissolveAmount;
            float _Seed;

            Varyings UnlitDissolveVertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                #ifdef UNITY_INSTANCING_ENABLED
                input.positionOS = UnityFlipSprite(input.positionOS, unity_SpriteFlip);
                #endif

                output.positionCS = TransformObjectToHClip(input.positionOS);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.color = input.color * _Color * _RendererColor;

                #ifdef UNITY_INSTANCING_ENABLED
                output.color *= unity_SpriteColor;
                #endif

                return output;
            }

            float PixelHash(float2 cell)
            {
                cell += float2(_Seed * 17.17, _Seed * 41.73);
                return frac(sin(dot(cell, float2(12.9898, 78.233))) * 43758.5453);
            }

            half4 UnlitDissolveFragment(Varyings input) : SV_Target
            {
                half4 spriteColor =
                    SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv) * input.color;
                clip(spriteColor.a - 0.001h);

                float2 rectSize = max(_SpriteUVRect.zw, float2(0.00001, 0.00001));
                float2 localUV = saturate((input.uv - _SpriteUVRect.xy) / rectSize);
                float2 cell = floor(
                    localUV * max(_PixelGrid.xy, float2(1.0, 1.0)));
                float2 mediumCell = floor(cell / 2.0);
                float2 largeCell = floor(cell / 5.0);

                float corruptionFine = PixelHash(cell + float2(11.3, 47.9));
                float corruptionMedium = PixelHash(
                    mediumCell + float2(73.1, 19.7));
                float corruptionLarge = PixelHash(
                    largeCell + float2(5.9, 131.3));
                float corruptionHash =
                    corruptionFine * 0.4 +
                    corruptionMedium * 0.4 +
                    corruptionLarge * 0.2;

                float dissolveFine = PixelHash(cell + float2(37.1, 91.7));
                float dissolveMedium = PixelHash(
                    mediumCell + float2(149.3, 23.5));
                float dissolveLarge = PixelHash(
                    largeCell + float2(61.7, 173.9));
                float dissolveHash =
                    dissolveFine * 0.35 +
                    dissolveMedium * 0.45 +
                    dissolveLarge * 0.2;

                float corruptionProgress = saturate(_DissolveAmount / 0.42);
                float corruptionMask = step(corruptionHash, corruptionProgress);
                float dissolveProgress = saturate((_DissolveAmount - 0.18) / 0.82);
                float clippingEnabled = step(0.1801, _DissolveAmount);
                float protectedDissolveHash = lerp(
                    1.0,
                    dissolveHash,
                    clippingEnabled);
                clip(protectedDissolveHash - dissolveProgress - 0.0001);

                half flicker = 0.82h + 0.18h * sin(
                    _Time.y * 18.0h + corruptionHash * 6.2831853h);
                spriteColor.rgb = lerp(
                    spriteColor.rgb,
                    _FragmentColor.rgb * flicker,
                    corruptionMask);
                return spriteColor;
            }
            ENDHLSL
        }
    }

    Fallback "Sprites/Default"
}
