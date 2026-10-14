Shader "Custom/SpriteFlash"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _FlashColor ("Flash Color", Color) = (1,1,1,1)
        _FlashAmount ("Flash Amount", Range(0,1)) = 0
        _OutlineColor ("Outline Color", Color) = (0.15,0.65,1,1)
        _OutlineWidth ("Outline Width (Pixels)", Range(0,4)) = 1
        _OutlineEnabled ("Outline Enabled", Float) = 0
        [MaterialToggle] PixelSnap ("Pixel snap", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        Blend One OneMinusSrcAlpha

        Pass
        {
        CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #pragma multi_compile_local _ PIXELSNAP_ON
            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            sampler2D _MainTex;
            fixed4 _Color;
            fixed4 _FlashColor;
            float _FlashAmount;
            float4 _MainTex_TexelSize;
            fixed4 _OutlineColor;
            float _OutlineWidth;
            float _OutlineEnabled;

            fixed SpriteAlpha(float2 uv)
            {
                float inside = step(0.0, uv.x) * step(uv.x, 1.0) *
                    step(0.0, uv.y) * step(uv.y, 1.0);
                return tex2D(_MainTex, uv).a * inside;
            }

            v2f vert(appdata_t IN)
            {
                v2f OUT;
                OUT.vertex = UnityObjectToClipPos(IN.vertex);
                OUT.texcoord = IN.texcoord;
                OUT.color = IN.color * _Color;
                #ifdef PIXELSNAP_ON
                OUT.vertex = UnityPixelSnap(OUT.vertex);
                #endif
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                fixed4 c = tex2D(_MainTex, IN.texcoord) * IN.color;
                // Straight Alpha 상태에서 원래 이미지 색상과 플래시 색상을 부드럽게 Lerp
                c.rgb = lerp(c.rgb, _FlashColor.rgb, _FlashAmount);
                // 최종 출력 시 Premultiplied Alpha(스프라이트 기본 블렌딩)를 위해 알파를 곱해줌
                c.rgb *= c.a;
                if (_OutlineEnabled > 0.5 && _OutlineWidth > 0.0)
                {
                    float2 pixel = _MainTex_TexelSize.xy * _OutlineWidth;
                    fixed adjacent = max(SpriteAlpha(IN.texcoord + float2(pixel.x, 0)),
                        SpriteAlpha(IN.texcoord - float2(pixel.x, 0)));
                    adjacent = max(adjacent, SpriteAlpha(IN.texcoord + float2(0, pixel.y)));
                    adjacent = max(adjacent, SpriteAlpha(IN.texcoord - float2(0, pixel.y)));
                    adjacent = max(adjacent, SpriteAlpha(IN.texcoord + pixel));
                    adjacent = max(adjacent, SpriteAlpha(IN.texcoord - pixel));
                    adjacent = max(adjacent, SpriteAlpha(IN.texcoord + float2(pixel.x, -pixel.y)));
                    adjacent = max(adjacent, SpriteAlpha(IN.texcoord + float2(-pixel.x, pixel.y)));
                    // Draw behind the sprite: hit flashes affect the body, not the blue outline.
                    fixed outlineAlpha = adjacent * _OutlineColor.a * IN.color.a * (1.0 - c.a);
                    c.rgb += _OutlineColor.rgb * outlineAlpha;
                    c.a += outlineAlpha;
                }
                return c;
            }
        ENDCG
        }
    }
}
