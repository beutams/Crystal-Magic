Shader "CrystalMagic/DropSpriteBorder"
{
    Properties
    {
        [PerRendererData] _MainTex("Sprite Texture", 2D) = "white" {}
        _Color("Tint", Color) = (1, 1, 1, 1)
        [PerRendererData] _BorderColor("Quality Border", Color) = (1, 1, 1, 1)
        [PerRendererData] _BorderWidth("Border Width", Range(0, 0.25)) = 0.065
        [PerRendererData] _SpriteUvRect("Sprite UV Rect", Vector) = (0, 0, 1, 1)
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "CanUseSpriteAtlas" = "True"
        }

        Pass
        {
            Name "DropSprite"
            Tags { "LightMode" = "UniversalForward" }

            Cull Off
            Lighting Off
            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 2.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half4 _BorderColor;
                float _BorderWidth;
                float4 _SpriteUvRect;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.color = input.color * _Color;
                output.uv = input.uv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half4 spriteColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv) * input.color;
                float2 rectSize = max(_SpriteUvRect.zw - _SpriteUvRect.xy, float2(0.00001, 0.00001));
                float2 localUv = saturate((input.uv - _SpriteUvRect.xy) / rectSize);
                float edgeDistance = min(min(localUv.x, localUv.y), min(1.0 - localUv.x, 1.0 - localUv.y));
                float border = 1.0 - smoothstep(_BorderWidth, _BorderWidth + 0.012, edgeDistance);
                half borderAlpha = (half)border * _BorderColor.a;
                spriteColor.rgb = lerp(spriteColor.rgb, _BorderColor.rgb, (half)border * 0.9);
                spriteColor.a = max(spriteColor.a, borderAlpha);
                return spriteColor;
            }
            ENDHLSL
        }
    }
}
