Shader "BlindBlade/Dash Vignette"
{
    Properties
    {
        [PerRendererData] _MainTex ("UI Texture", 2D) = "white" {}
        _Color ("집중선 색상", Color) = (1,1,1,0.6)
        _Intensity ("강도", Range(0,1)) = 0
        _LineCount ("집중선 수", Float) = 48
        _InnerRadius ("중앙 여백", Range(0.2,0.9)) = 0.24
        _StrokeLength ("집중선 길이", Range(0.1,1)) = 0.6
        _Travel ("바깥 이동 진행도", Range(0,1)) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Cull Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "UnityCG.cginc"
            fixed4 _Color;
            float _Intensity, _LineCount, _InnerRadius, _StrokeLength, _Travel;
            struct Attributes { float4 vertex : POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; };
            struct Varyings { float4 vertex : SV_POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.uv = input.uv;
                output.color = input.color * _Color;
                return output;
            }
            fixed4 Frag(Varyings input) : SV_Target
            {
                float2 p = (input.uv - 0.5) * 2;
                float radius = length(p);
                float angle = (atan2(p.y, p.x) + 3.14159265) / 6.2831853;
                float sector = angle * _LineCount;
                float id = floor(sector);
                float random = frac(sin(id * 127.1) * 43758.5453);
                float variation = frac(sin((id + 13) * 269.5) * 18341.735);
                float start = _InnerRadius + random * 0.16 + _Travel * 0.65;
                float length = _StrokeLength * lerp(0.45, 1.15, variation);
                float stroke = saturate((radius - start) / max(length, 0.01));
                float taper = pow(saturate(sin(stroke * 3.14159265)), 0.7);
                float width = lerp(0.012, 0.07, random) * taper;
                float offset = lerp(0.25, 0.75, variation);
                float angularDistance = abs(frac(sector) - offset);
                float lineMask = 1 - smoothstep(width, width + max(fwidth(sector) * 0.5, 0.005), angularDistance);
                float ends = smoothstep(0.0, 0.18, stroke) * (1 - smoothstep(0.65, 1.0, stroke));
                float lineAlpha = saturate(input.color.a * _Intensity * lineMask * ends
                    * lerp(0.45, 1.0, variation));
                return fixed4(input.color.rgb, lineAlpha);
            }
            ENDCG
        }
    }
}
