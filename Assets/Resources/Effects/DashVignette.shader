Shader "BlindBlade/Dash Vignette"
{
    Properties
    {
        [PerRendererData] _MainTex ("UI Texture", 2D) = "white" {}
        _Color ("집중선 색상", Color) = (1,1,1,0.6)
        _Intensity ("강도", Range(0,1)) = 0
        _LineCount ("집중선 수", Float) = 64
        _InnerRadius ("중앙 여백", Range(0.2,0.9)) = 0.85
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
            float _Intensity, _LineCount, _InnerRadius;
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
                float random = frac(sin(floor(sector) * 127.1) * 43758.5453);
                float start = lerp(_InnerRadius, min(0.98, _InnerRadius + 0.25), random);
                float outerRadius = radius / max(max(abs(p.x), abs(p.y)), 0.0001);
                float taper = saturate((radius - start) / max(outerRadius - start, 0.01));
                float width = lerp(0.025, 0.13, random) * taper * 0.6;
                float angularDistance = abs(frac(sector) - 0.5);
                float lineMask = 1 - smoothstep(width, width + max(fwidth(sector), 0.01), angularDistance);
                float edge = smoothstep(0.0, 1.0, taper);
                float lineAlpha = saturate(input.color.a * _Intensity * lineMask * edge);
                return fixed4(input.color.rgb, lineAlpha);
            }
            ENDCG
        }
    }
}
