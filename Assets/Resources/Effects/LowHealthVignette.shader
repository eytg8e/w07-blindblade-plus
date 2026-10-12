Shader "BlindBlade/Low Health Vignette"
{
    Properties
    {
        [PerRendererData] _MainTex ("UI Texture", 2D) = "white" {}
        _Color ("색상", Color) = (1,0,0,1)
        _Opacity ("불투명도", Range(0,1)) = 0
        _Width ("화면 가장자리 두께", Range(0,1)) = 0.15
        _RippleStrength ("일렁임 진폭", Range(0,0.1)) = 0.02
        _RippleSpeed ("일렁임 속도", Float) = 0.8
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
            float _Opacity, _Width, _RippleStrength, _RippleSpeed;
            struct Attributes
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };
            struct Varyings
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };
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
                float2 centered = (input.uv - 0.5) * 2;
                float radius = length(centered);
                float phase = _Time.y * _RippleSpeed * 6.2831853;
                float ripple = sin(centered.x * 8 + phase)
                    * sin(centered.y * 7 - phase * 0.7);
                float width = saturate(_Width + ripple * _RippleStrength);
                float edge = smoothstep(1 - width, 1.25, radius);
                float shimmer = 1 + ripple * _RippleStrength * 3;
                return fixed4(input.color.rgb, saturate(input.color.a * _Opacity * edge * shimmer));
            }
            ENDCG
        }
    }
}
