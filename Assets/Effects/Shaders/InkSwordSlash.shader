Shader "BlindBlade/InkSwordSlash"
{
    Properties
    {
        _InkColor ("Ink Color", Color) = (0.008, 0.012, 0.018, 1)
        [HDR] _LightColor ("Brush Light", Color) = (1.5, 1.65, 1.8, 1)
        _WashColor ("Inner Wash", Color) = (0.55, 0.65, 0.78, 1)
        _BrushContrast ("Brush Contrast", Range(0.5, 2)) = 0.95
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _InkColor;
                half4 _LightColor;
                half4 _WashColor;
                float _BrushContrast;
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

            // 좌표를 일정한 난수 값으로 바꿔 외부 텍스처 없이 붓질 무늬를 만든다.
            float Hash(float2 coord)
            {
                coord = frac(coord * float2(123.34, 456.21));
                coord += dot(coord, coord + 45.32);
                return frac(coord.x * coord.y);
            }

            // 인접 격자의 난수를 보간해 연속적인 먹물 굴곡을 계산한다.
            float Noise(float2 coord)
            {
                float2 cell = floor(coord);
                float2 fraction = frac(coord);
                fraction = fraction * fraction * (3.0 - 2.0 * fraction);
                return lerp(lerp(Hash(cell), Hash(cell + float2(1, 0)), fraction.x),
                    lerp(Hash(cell + float2(0, 1)), Hash(cell + float2(1, 1)), fraction.x), fraction.y);
            }

            // 서로 다른 크기의 무늬를 합쳐 길게 찢어진 붓 자국을 계산한다.
            float Brush(float2 coord)
            {
                return Noise(coord) * 0.6 + Noise(coord * 2.07 + 7.3) * 0.28
                    + Noise(coord * 4.13 + 19.1) * 0.12;
            }

            // 검 궤적의 메시 위치와 색상 및 수명 알파를 전달한다.
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }

            // 검은 붓질과 찢어진 백색 틈 및 안쪽 잔광을 같은 궤적에서 합성한다.
            half4 Frag(Varyings input) : SV_Target
            {
                float u = input.uv.x;
                float v = input.uv.y;
                float life = saturate(input.color.a);
                float2 flow = float2(u * 5.0, v * 34.0);
                float brush = Brush(flow);
                float coarse = Noise(float2(u * 7.0, v * 8.0));
                float fine = Noise(float2(u * 52.0, v * 110.0));
                float start = smoothstep(0.0, 0.08, u);
                float taper = pow(saturate(u / 0.32), 0.55);
                float inner = 0.43 + (coarse - 0.5) * 0.13;
                float outer = 0.82 + (coarse - 0.5) * 0.10;
                inner = lerp(0.69, inner, taper);
                float band = smoothstep(inner, inner + 0.014, v)
                    * (1.0 - smoothstep(outer - 0.014, outer, v));
                float dryBrush = smoothstep(0.23, 0.31, brush + (fine - 0.5) * 0.15);
                float ink = band * dryBrush;

                float lip = 1.0 - smoothstep(0.015, 0.065, abs(v - (outer - 0.035)));
                lip *= smoothstep(0.25, 0.58, Noise(float2(u * 13.0, v * 6.0)));
                float tears = smoothstep(0.57, 0.72, brush * _BrushContrast)
                    * smoothstep(inner + 0.035, inner + 0.12, v);
                float light = saturate(lip * smoothstep(0.28, 0.5, brush) * 0.72 + tears);
                half3 inkColor = lerp(_InkColor.rgb, _LightColor.rgb * input.color.rgb, light);

                float washEdge = inner - 0.065 + (brush - 0.5) * 0.055;
                float washLine = exp2(-pow((v - washEdge) / 0.065, 2.0) * 2.0);
                float washBody = smoothstep(0.015, 0.15, v)
                    * (1.0 - smoothstep(inner - 0.11, inner, v));
                float washAlpha = saturate(washBody * 0.12 + washLine * 0.58);
                half3 washColor = lerp(_WashColor.rgb, _LightColor.rgb, washLine * 0.6) * input.color.rgb;

                float inkAlpha = ink * _InkColor.a;
                float alpha = saturate((inkAlpha + washAlpha * (1.0 - inkAlpha)) * life * start);
                half3 color = (inkColor * inkAlpha + washColor * washAlpha * (1.0 - inkAlpha))
                    / max(0.001, inkAlpha + washAlpha * (1.0 - inkAlpha));
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}
