Shader "Hidden/BlindBlade/ScreenSpaceOutline"
{
    Properties
    {
        _LineColor ("Line Color", Color) = (1, 1, 1, 1)
        _LineWidth ("Line Width", Float) = 1
        _DepthSensitivity ("Depth Sensitivity", Float) = 50
        _NormalSensitivity ("Normal Sensitivity", Float) = 4
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
            CBUFFER_END

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
            float _OutlineMemoryEnabled;
            float4 _OutlineMemoryColor;

            // 영역 반경의 안쪽 경계부터 가장자리까지 공간 가시성을 부드럽게 줄인다.
            float RegionVisibility(float3 position, float3 center, float radius)
            {
                if (radius <= 0.0)
                    return 0.0;
                float innerRadius = max(0.0, radius - max(_OutlineEdgeFade, 0.0001));
                return 1.0 - smoothstep(innerRadius, radius, distance(position, center));
            }

            // 현재 관측자·소리·적 공개 영역의 최대 가시성을 반환한다.
            float3 PerceptionVisibility(float2 uv, float rawDepth, float hasSurface)
            {
                if (hasSurface == 0.0)
                    return 0.0;
                if (_UsePerceptionMask < 0.5)
                    return _LineColor.rgb * _LineColor.a;

                #if !UNITY_REVERSED_Z
                    rawDepth = lerp(UNITY_NEAR_CLIP_VALUE, 1.0, rawDepth);
                #endif
                float3 position = ComputeWorldSpacePosition(uv, rawDepth, UNITY_MATRIX_I_VP);
                float visibility = RegionVisibility(
                    position, _OutlineObserverRegion.xyz, _OutlineObserverRegion.w);
                float memoryVisibility = 0.0;
                if (_OutlineMemoryEnabled > 0.5)
                {
                    float2 memoryUv = (position.xz - _OutlineMemoryBounds.xy) / _OutlineMemoryBounds.zw;
                    if (all(memoryUv >= 0.0) && all(memoryUv <= 1.0))
                        memoryVisibility = SAMPLE_TEXTURE2D_LOD(_OutlineTerrainMemory, sampler_OutlineTerrainMemory, memoryUv, 0).r;
                }
                [loop]
                for (int i = 0; i < _OutlineSoundCount; i++)
                {
                    float age = max(0.0, _OutlinePerceptionTime - _OutlineSoundRegions[i].w);
                    float timeVisibility = saturate(1.0 - age / max(_OutlineSoundLifetime, 0.0001));
                    float soundVisibility = RegionVisibility(
                        position, _OutlineSoundRegions[i].xyz, _OutlineSoundRadius) * timeVisibility;
                    visibility = max(visibility, soundVisibility);
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
                        PerceptionVisibility(firstUv, firstRawDepth, firstSurface),
                        PerceptionVisibility(secondUv, secondRawDepth, secondSurface));
                }
                if (firstSurface == 0.0)
                    return 0.0;

                float firstDepth = LinearEyeDepth(firstRawDepth, _ZBufferParams);
                float secondDepth = LinearEyeDepth(secondRawDepth, _ZBufferParams);
                if (unity_OrthoParams.w > 0.5)
                {
                    #if UNITY_REVERSED_Z
                        firstDepth = lerp(_ProjectionParams.y, _ProjectionParams.z, 1.0 - firstRawDepth);
                        secondDepth = lerp(_ProjectionParams.y, _ProjectionParams.z, 1.0 - secondRawDepth);
                    #else
                        firstDepth = lerp(_ProjectionParams.y, _ProjectionParams.z, firstRawDepth);
                        secondDepth = lerp(_ProjectionParams.y, _ProjectionParams.z, secondRawDepth);
                    #endif
                }
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
                    PerceptionVisibility(firstUv, firstRawDepth, firstSurface),
                    PerceptionVisibility(secondUv, secondRawDepth, secondSurface));
            }

            // 대각선 픽셀 차이로 선을 생성하고 면과 배경을 검게 출력한다.
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
                // 넓고 평평한 방문 바닥에 이동 거리를 읽을 수 있는 옅은 격자를 표시한다.
                if (_OutlineMemoryEnabled > 0.5)
                {
                    float depth = SampleSceneDepth(uv);
                    if (HasSurface(depth) > 0.5 && SampleSceneNormals(uv).y > 0.9)
                    {
                        float worldDepth = depth;
                        #if !UNITY_REVERSED_Z
                            worldDepth = lerp(UNITY_NEAR_CLIP_VALUE, 1.0, depth);
                        #endif
                        float3 world = ComputeWorldSpacePosition(uv, worldDepth, UNITY_MATRIX_I_VP);
                        float2 grid = world.xz * 0.5;
                        float2 lineDistance = abs(frac(grid - 0.5) - 0.5) / max(fwidth(grid), 0.0001);
                        float gridLine = 1.0 - saturate(min(lineDistance.x, lineDistance.y));
                        if (abs(world.y) < 0.1)
                            edge = max(edge, gridLine * 0.22 * PerceptionVisibility(uv, depth, 1.0));
                    }
                }
                return half4(edge, 1.0);
            }
            ENDHLSL
        }
    }
}
