Shader "Hidden/BlindBlade/ScreenSpaceOutline"
{
    Properties
    {
        _LineColor ("Line Color", Color) = (1, 1, 1, 1)
        _LineWidth ("Line Width", Float) = 1
        _DepthSensitivity ("Depth Sensitivity", Float) = 50
        _NormalSensitivity ("Normal Sensitivity", Float) = 4
        _TransparentThreshold ("Transparent Coverage Threshold", Float) = 0.015
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "ScreenSpaceOutline"
            ZWrite Off
            ZTest Always
            Cull Off
            Blend Off

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareNormalsTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _LineColor;
                float _LineWidth;
                float _DepthSensitivity;
                float _NormalSensitivity;
                float _UsePerceptionMask;
                float _TransparentThreshold;
            CBUFFER_END

            TEXTURE2D_X(_AlwaysVisibleOutlineMask);
            TEXTURE2D_X(_OutlineOpaqueColor);
            TEXTURE2D_X(_OutlineTransparentDepth);

            #define MAX_SOUND_REGIONS 16
            float4 _OutlineObserverRegion;
            float4 _OutlineSoundRegions[MAX_SOUND_REGIONS];
            int _OutlineSoundCount;
            float _OutlineSoundRadius;
            float _OutlineSoundLifetime;
            float _OutlineEdgeFade;
            float _OutlinePerceptionTime;
            TEXTURE2D(_OutlineTerrainMemory);
            SAMPLER(sampler_OutlineTerrainMemory);
            float4 _OutlineMemoryBounds;
            float4 _OutlineMemoryColor;
            float _OutlineMemoryEnabled;

            // 영역 반경의 안쪽 경계부터 가장자리까지 공간 가시성을 부드럽게 줄인다.
            float RegionVisibility(float3 position, float3 center, float radius)
            {
                if (radius <= 0.0)
                    return 0.0;
                float innerRadius = max(0.0, radius - max(_OutlineEdgeFade, 0.0001));
                return 1.0 - smoothstep(innerRadius, radius, distance(position, center));
            }

            // 현재 인지를 선 색상으로 표시하고 일반 지형에만 누적 기억 색상을 합성한다.
            float3 PerceptionColor(float2 uv, float rawDepth, float hasSurface, float includeMemory)
            {
                if (hasSurface == 0.0)
                    return 0.0;
                if (SAMPLE_TEXTURE2D_X(_AlwaysVisibleOutlineMask, sampler_PointClamp, uv).r > 0.5)
                    return _LineColor.rgb * _LineColor.a;
                if (_UsePerceptionMask < 0.5)
                    return _LineColor.rgb * _LineColor.a;

                #if !UNITY_REVERSED_Z
                    rawDepth = lerp(UNITY_NEAR_CLIP_VALUE, 1.0, rawDepth);
                #endif
                float3 position = ComputeWorldSpacePosition(uv, rawDepth, UNITY_MATRIX_I_VP);
                float visibility = RegionVisibility(
                    position, _OutlineObserverRegion.xyz, _OutlineObserverRegion.w);
                [loop]
                for (int i = 0; i < _OutlineSoundCount; i++)
                {
                    float age = max(0.0, _OutlinePerceptionTime - _OutlineSoundRegions[i].w);
                    float timeVisibility = saturate(1.0 - age / max(_OutlineSoundLifetime, 0.0001));
                    float soundVisibility = RegionVisibility(
                        position, _OutlineSoundRegions[i].xyz, _OutlineSoundRadius) * timeVisibility;
                    visibility = max(visibility, soundVisibility);
                }
                float memoryVisibility = 0.0;
                if (includeMemory > 0.5 && _OutlineMemoryEnabled > 0.5)
                {
                    float2 memoryUv = (position.xz - _OutlineMemoryBounds.xy) / _OutlineMemoryBounds.zw;
                    if (all(memoryUv >= 0.0) && all(memoryUv <= 1.0))
                        memoryVisibility = SAMPLE_TEXTURE2D_LOD(_OutlineTerrainMemory,
                            sampler_OutlineTerrainMemory, memoryUv, 0).r;
                }
                float3 memoryColor = _OutlineMemoryColor.rgb * _OutlineMemoryColor.a * memoryVisibility;
                return memoryColor * (1.0 - visibility) + _LineColor.rgb * _LineColor.a * visibility;
            }

            // 깊이의 배경값을 구분하여 하늘의 노멀로 생기는 선을 제외한다.
            float HasSurface(float depth)
            {
                #if UNITY_REVERSED_Z
                    return depth > 0.000001;
                #else
                    return depth < 0.999999;
                #endif
            }

            // 원근 및 직교 카메라의 장치 깊이를 같은 눈 공간 거리로 변환한다.
            float EyeDepth(float rawDepth)
            {
                if (unity_OrthoParams.w > 0.5)
                {
                    #if UNITY_REVERSED_Z
                        return lerp(_ProjectionParams.y, _ProjectionParams.z, 1.0 - rawDepth);
                    #else
                        return lerp(_ProjectionParams.y, _ProjectionParams.z, rawDepth);
                    #endif
                }
                return LinearEyeDepth(rawDepth, _ZBufferParams);
            }

            // 두 픽셀의 상대 깊이와 노멀 차이를 사용해 경계 강도를 반환한다.
            float3 EdgeBetween(float2 firstUv, float2 secondUv)
            {
                float firstRawDepth = SampleSceneDepth(firstUv);
                float secondRawDepth = SampleSceneDepth(secondUv);
                float firstSurface = HasSurface(firstRawDepth);
                float secondSurface = HasSurface(secondRawDepth);
                if (firstSurface != secondSurface)
                {
                    return max(
                        PerceptionColor(firstUv, firstRawDepth, firstSurface, 1.0),
                        PerceptionColor(secondUv, secondRawDepth, secondSurface, 1.0));
                }
                if (firstSurface == 0.0)
                    return 0.0;

                float firstDepth = EyeDepth(firstRawDepth);
                float secondDepth = EyeDepth(secondRawDepth);
                float depthDifference = abs(firstDepth - secondDepth)
                    / max(min(firstDepth, secondDepth), 0.0001);
                float normalDifference = length(
                    SampleSceneNormals(firstUv) - SampleSceneNormals(secondUv));

                float edgeStrength = max(
                    depthDifference * _DepthSensitivity,
                    normalDifference * _NormalSensitivity);
                float edge = smoothstep(1.0, 1.25, edgeStrength);
                if (edge <= 0.0)
                    return 0.0;
                return edge * max(
                    PerceptionColor(firstUv, firstRawDepth, firstSurface, 1.0),
                    PerceptionColor(secondUv, secondRawDepth, secondSurface, 1.0));
            }

            // 선택 VFX의 실제 RGB 기여에서 모양을 검출하고 보이지 않는 알파 면은 제외한다.
            float TransparentCoverage(float2 uv)
            {
                float4 source = SAMPLE_TEXTURE2D_X(_OutlineOpaqueColor, sampler_LinearClamp, uv);
                float difference = max(abs(source.r), max(abs(source.g), abs(source.b)));
                float depth = SAMPLE_TEXTURE2D_X(_OutlineTransparentDepth, sampler_PointClamp, uv).r;
                return smoothstep(_TransparentThreshold, _TransparentThreshold * 2.0, difference) * HasSurface(depth);
            }

            // 보이는 RGB 모양의 경계만 표시하며 기하 깊이는 인지 위치 계산에만 사용한다.
            float3 TransparentEdgeBetween(float2 firstUv, float2 secondUv)
            {
                float first = TransparentCoverage(firstUv);
                float second = TransparentCoverage(secondUv);
                float firstDepth = SAMPLE_TEXTURE2D_X(_OutlineTransparentDepth, sampler_PointClamp, firstUv).r;
                float secondDepth = SAMPLE_TEXTURE2D_X(_OutlineTransparentDepth, sampler_PointClamp, secondUv).r;
                float edge = saturate(abs(first - second) * 1.5);
                return edge * max(PerceptionColor(firstUv, firstDepth, first, 0.0),
                    PerceptionColor(secondUv, secondDepth, second, 0.0));
            }

            // 불투명 경계와 투명 이펙트 경계를 합성하고 모든 면과 배경은 검게 출력한다.
            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 texelSize = rcp(_ScaledScreenParams.xy);
                float lowerOffset = floor(_LineWidth * 0.5);
                float upperOffset = ceil(_LineWidth * 0.5);
                float2 uv = input.texcoord;
                float2 firstUv = saturate(uv + float2(-lowerOffset, -lowerOffset) * texelSize);
                float2 secondUv = saturate(uv + float2(upperOffset, upperOffset) * texelSize);
                float2 thirdUv = saturate(uv + float2(-lowerOffset, upperOffset) * texelSize);
                float2 fourthUv = saturate(uv + float2(upperOffset, -lowerOffset) * texelSize);
                float3 edge = max(EdgeBetween(firstUv, secondUv), EdgeBetween(thirdUv, fourthUv));
                edge *= 1.0 - TransparentCoverage(uv);
                float3 transparentEdge = max(TransparentEdgeBetween(firstUv, secondUv),
                    TransparentEdgeBetween(thirdUv, fourthUv));
                edge = max(edge, transparentEdge);
                return half4(edge, 1.0);
            }
            ENDHLSL
        }
    }
}
