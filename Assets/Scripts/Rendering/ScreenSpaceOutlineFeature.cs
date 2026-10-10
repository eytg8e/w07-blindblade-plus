using System.Collections.Generic;

using UnityEngine;
using UnityEngine.Experimental.Rendering;

using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

public sealed class ScreenSpaceOutlineFeature : ScriptableRendererFeature
    {
        private static readonly int _lineColorId = Shader.PropertyToID("_LineColor");
        private static readonly int _lineWidthId = Shader.PropertyToID("_LineWidth");
        private static readonly int _depthSensitivityId = Shader.PropertyToID("_DepthSensitivity");
        private static readonly int _normalSensitivityId = Shader.PropertyToID("_NormalSensitivity");
        private static readonly int _usePerceptionMaskId = Shader.PropertyToID("_UsePerceptionMask");
        private static readonly int _transparentThresholdId = Shader.PropertyToID("_TransparentThreshold");

        [Header("윤곽선 표현")]
        [SerializeField, Tooltip("검은 면 위에 표시할 선 색상. 알파는 선 밝기에 적용한다.")]
        private Color _lineColor = Color.white;
        [SerializeField, Range(1, 6), Tooltip("렌더링 해상도 기준 선 검출 폭(픽셀).")]
        private int _lineWidth = 1;
        [SerializeField, Range(1f, 200f), Tooltip("깊이 검출 민감도. 높을수록 작은 상대 깊이 차이도 선으로 표시한다.")]
        private float _depthSensitivity = 50f;
        [SerializeField, Range(1f, 20f), Tooltip("노멀 검출 민감도. 높을수록 작은 표면 방향 차이도 선으로 표시한다.")]
        private float _normalSensitivity = 4f;

        [SerializeField, Range(0.001f, 0.5f), Tooltip("선택 VFX의 RGB 밝기 검출 기준이다.")]
        private float _transparentThreshold = 0.015f;

        [Header("렌더링 연결")]
        [SerializeField, Tooltip("화면 공간 윤곽선 셰이더. PC_Renderer에 기본 연결되어 있다.")]
        private Shader _shader;
        [SerializeField] private Shader _alwaysVisibleMaskShader;
        [SerializeField] private Shader _transparentDepthShader;
        private Material _material;
        private Material _maskMaterial;
        private OutlinePass _pass;
        private Material _depthMaterial;
        private readonly OutlineVFXRouting _routing = new OutlineVFXRouting();
        private CapturePass _capture;
        private TransparentDepthPass _depth;

        /// <summary>
        /// 연결된 셰이더로 전용 머티리얼과 깊이·노멀을 사용하는 윤곽선 패스를 생성한다.
        /// </summary>
        public override void Create()
        {
            ReleaseResources();

            if (_shader == null || _alwaysVisibleMaskShader == null || _transparentDepthShader == null)
            {
                Debug.LogError("ScreenSpaceOutlineFeature에 윤곽선 셰이더를 연결해야 합니다.", this);
                return;
            }

            _material = CoreUtils.CreateEngineMaterial(_shader);
            _maskMaterial = CoreUtils.CreateEngineMaterial(_alwaysVisibleMaskShader);
            _depthMaterial = CoreUtils.CreateEngineMaterial(_transparentDepthShader);
            _depthMaterial.SetFloat("_DepthBlendOp", SystemInfo.usesReversedZBuffer ? 4f : 3f);
            _capture = new CapturePass();
            _depth = new TransparentDepthPass(_depthMaterial);
            _pass = new OutlinePass(_material, _maskMaterial, _capture, _depth);
            RenderPipelineManager.beginCameraRendering += BeginCamera;
            RenderPipelineManager.endCameraRendering += EndCamera;
        }

        /// <summary>
        /// 기본 게임 카메라와 씬 뷰에 인스펙터 설정을 적용하고 윤곽선 패스를 등록한다.
        /// </summary>
        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            CameraData cameraData = renderingData.cameraData;
            if (_material == null
                || cameraData.renderType == CameraRenderType.Overlay
                || (cameraData.cameraType != CameraType.Game
                    && cameraData.cameraType != CameraType.SceneView))
            {
                return;
            }

            _material.SetColor(_lineColorId, _lineColor);
            _material.SetFloat(_lineWidthId, _lineWidth);
            _material.SetFloat(_depthSensitivityId, _depthSensitivity);
            _material.SetFloat(_normalSensitivityId, _normalSensitivity);
            _material.SetFloat(_usePerceptionMaskId, Application.isPlaying ? 1f : 0f);
            _material.SetFloat(_transparentThresholdId, _transparentThreshold);
            renderer.EnqueuePass(_capture);
            renderer.EnqueuePass(_depth);
            renderer.EnqueuePass(_pass);
        }

        /// <summary>
        /// 기능 제거 또는 재생 종료 시 이 기능이 생성한 머티리얼을 해제한다.
        /// </summary>
        protected override void Dispose(bool disposing)
        {
            ReleaseResources();
        }

        /// <summary>
        /// context와 camera를 사용해 지원 카메라의 컬링 전에 선택 VFX의 임시 큐를 준비한다.
        /// 기본 게임 카메라와 씬 뷰의 렌더러 상태만 변경한다.
        /// </summary>
        private void BeginCamera(ScriptableRenderContext context, Camera camera)
        {
            if (!isActive || _pass == null || (camera.cameraType != CameraType.Game
                && camera.cameraType != CameraType.SceneView))
                return;
            var data = camera.GetComponent<UniversalAdditionalCameraData>();
            if (data != null && data.renderType == CameraRenderType.Overlay)
                return;
            _routing.Prepare();
        }

        /// <summary>
        /// context와 camera의 렌더링 종료 시 임시 VFX 머티리얼과 렌더링 마스크를 원복한다.
        /// 원본 에셋과 다음 카메라에 임시 상태를 남기지 않는다.
        /// </summary>
        private void EndCamera(ScriptableRenderContext context, Camera camera)
        {
            _routing.Restore();
        }

        /// <summary>
        /// 추가 입력 없이 카메라 구독, 임시 머티리얼 및 패스 타깃을 해제한다.
        /// 재생성이나 기능 제거 이후 이전 렌더링 상태와 리소스를 남기지 않는다.
        /// </summary>
        private void ReleaseResources()
        {
            RenderPipelineManager.beginCameraRendering -= BeginCamera;
            RenderPipelineManager.endCameraRendering -= EndCamera;
            _routing.Dispose();
            CoreUtils.Destroy(_material);
            CoreUtils.Destroy(_maskMaterial);
            CoreUtils.Destroy(_depthMaterial);
            _capture?.Release();
            _depth?.Release();
            _pass?.Dispose();
            _material = null;
            _maskMaterial = null;
            _depthMaterial = null;
            _capture = null;
            _depth = null;
            _pass = null;
        }

        private sealed class FrameTextures : ContextItem
        {
            public TextureHandle Opaque { get; set; }
            public TextureHandle TransparentDepth { get; set; }

            /// <summary>
            /// 프레임 종료 시 두 핸들을 비워 다른 카메라나 프레임에 남기지 않는다.
            /// </summary>
            public override void Reset()
            {
                Opaque = TextureHandle.nullHandle;
                TransparentDepth = TextureHandle.nullHandle;
            }
        }

        private sealed class CapturePass : ScriptableRenderPass
        {
            private readonly List<ShaderTagId> _tags = new List<ShaderTagId>
            {
                new ShaderTagId("UniversalForward"), new ShaderTagId("UniversalForwardOnly"),
                new ShaderTagId("SRPDefaultUnlit")
            };
            private readonly FilteringSettings _filter = new FilteringSettings(RenderQueueRange.all,
                -1, OutlineVFXRouting.SELECTED_MASK);
            private sealed class ColorData { public RendererListHandle Renderers { get; set; } }
            private RTHandle _legacyColor;
            public RTHandle LegacyColor => _legacyColor;

            /// <summary>
            /// 선택 VFX의 색상·알파 타깃과 읽기 가능한 중간 카메라 타깃을 준비한다.
            /// </summary>
            public CapturePass()
            {
                renderPassEvent = RenderPassEvent.BeforeRenderingTransparents;
                requiresIntermediateTexture = true;
            }

            /// <summary>
            /// activeColorTexture 형식의 빈 타깃에 선택 VFX만 원래 셰이더로 기록한다.
            /// </summary>
            public override void RecordRenderGraph(RenderGraph graph, ContextContainer frameData)
            {
                var resources = frameData.Get<UniversalResourceData>();
                var desc = resources.activeColorTexture.GetDescriptor(graph);
                desc.name = "Outline Opaque Color";
                desc.depthBufferBits = DepthBits.None;
                desc.msaaSamples = MSAASamples.None;
                desc.clearBuffer = true;
                desc.clearColor = Color.clear;
                var copy = graph.CreateTexture(desc);
                var drawing = RenderingUtils.CreateDrawingSettings(_tags, frameData.Get<UniversalRenderingData>(),
                    frameData.Get<UniversalCameraData>(), frameData.Get<UniversalLightData>(), SortingCriteria.CommonTransparent);
                var parameters = new RendererListParams(frameData.Get<UniversalRenderingData>().cullResults, drawing, _filter);
                using var builder = graph.AddRasterRenderPass<ColorData>("Outline Selected VFX", out var data);
                data.Renderers = graph.CreateRendererList(parameters);
                builder.UseRendererList(data.Renderers);
                builder.AllowPassCulling(false);
                builder.UseAllGlobalTextures(true);
                builder.SetRenderAttachment(copy, 0, AccessFlags.Write);
                builder.SetRenderAttachmentDepth(resources.activeDepthTexture, AccessFlags.ReadWrite);
                builder.SetRenderFunc<ColorData>(DrawColor);
                frameData.GetOrCreate<FrameTextures>().Opaque = copy;
            }

            /// <summary>
            /// 선택한 VFX를 원래 셰이더로 별도 투명 타깃에 그려 알파 모양을 보관한다.
            /// </summary>
            private static void DrawColor(ColorData data, RasterGraphContext context)
            {
                context.cmd.DrawRendererList(data.Renderers);
            }

            /// <summary>
            /// 호환 모드에서 할당한 색상 타깃을 해제한다.
            /// </summary>
            public void Release() { _legacyColor?.Release(); }

#pragma warning disable CS0618, CS0672
            /// <summary>
            /// 호환 모드에서 선택 VFX만 원래 셰이더로 기록한다.
            /// </summary>
            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                var desc = renderingData.cameraData.cameraTargetDescriptor;
                desc.depthBufferBits = 0;
                desc.msaaSamples = 1;
                RenderingUtils.ReAllocateHandleIfNeeded(ref _legacyColor, desc, name: "Outline Opaque Color");
                var cmd = CommandBufferPool.Get("Outline Opaque Capture");
                CoreUtils.SetRenderTarget(cmd, LegacyColor, renderingData.cameraData.renderer.cameraDepthTargetHandle,
                    ClearFlag.Color, Color.clear);
                context.ExecuteCommandBuffer(cmd);
                CommandBufferPool.Release(cmd);
                var drawing = CreateDrawingSettings(_tags, ref renderingData, SortingCriteria.CommonTransparent);
                var filter = _filter;
                context.DrawRenderers(renderingData.cullResults, ref drawing, ref filter);
            }
#pragma warning restore CS0618, CS0672
        }

        private sealed class TransparentDepthPass : ScriptableRenderPass
        {
            private readonly Material _material;
            private readonly List<ShaderTagId> _tags = new List<ShaderTagId>
            {
                new ShaderTagId("UniversalForward"), new ShaderTagId("UniversalForwardOnly"),
                new ShaderTagId("SRPDefaultUnlit")
            };
            private readonly FilteringSettings _filter = new FilteringSettings(RenderQueueRange.all,
                -1, OutlineVFXRouting.SELECTED_MASK);
            private RTHandle _legacyDepth;
            public RTHandle LegacyDepth => _legacyDepth;

            private sealed class DepthData { public RendererListHandle Renderers { get; set; } }

            /// <summary>
            /// material로 투명 기하 깊이를 수집하도록 합성 후 실행 순서를 지정한다.
            /// </summary>
            public TransparentDepthPass(Material material)
            {
                _material = material;
                renderPassEvent = RenderPassEvent.BeforeRenderingTransparents;
            }

            /// <summary>
            /// 카메라 깊이와 같은 크기의 타깃에 투명 기하의 최근접 깊이를 기록한다.
            /// 불투명 깊이는 읽기만 하며 결과 핸들은 현재 프레임에만 보관한다.
            /// </summary>
            public override void RecordRenderGraph(RenderGraph graph, ContextContainer frameData)
            {
                var resources = frameData.Get<UniversalResourceData>();
                var desc = resources.activeDepthTexture.GetDescriptor(graph);
                desc.name = "Outline Transparent Depth";
                desc.depthBufferBits = DepthBits.None;
                desc.colorFormat = GraphicsFormat.R32_SFloat;
                desc.clearBuffer = true;
                desc.clearColor = SystemInfo.usesReversedZBuffer ? Color.black : Color.white;
                var target = graph.CreateTexture(desc);
                var drawing = RenderingUtils.CreateDrawingSettings(_tags, frameData.Get<UniversalRenderingData>(),
                    frameData.Get<UniversalCameraData>(), frameData.Get<UniversalLightData>(), SortingCriteria.CommonTransparent);
                drawing.overrideMaterial = _material;
                drawing.overrideMaterialPassIndex = 0;
                var parameters = new RendererListParams(frameData.Get<UniversalRenderingData>().cullResults, drawing, _filter);
                using var builder = graph.AddRasterRenderPass<DepthData>("Outline Transparent Depth", out var data);
                data.Renderers = graph.CreateRendererList(parameters);
                builder.UseRendererList(data.Renderers);
                builder.AllowPassCulling(false);
                builder.SetRenderAttachment(target, 0, AccessFlags.Write);
                builder.SetRenderAttachmentDepth(resources.activeDepthTexture, AccessFlags.Read);
                builder.SetRenderFunc<DepthData>(DrawDepth);
                frameData.GetOrCreate<FrameTextures>().TransparentDepth = target;
            }

            /// <summary>
            /// 현재 카메라의 투명 렌더러 목록을 깊이 전용 머티리얼로 출력한다.
            /// </summary>
            private static void DrawDepth(DepthData data, RasterGraphContext context)
            {
                context.cmd.DrawRendererList(data.Renderers);
            }

            /// <summary>
            /// 호환 모드에서 할당한 투명 깊이 타깃을 해제한다.
            /// </summary>
            public void Release() { _legacyDepth?.Release(); }

#pragma warning disable CS0618, CS0672
            /// <summary>
            /// 호환 모드에서 불투명 표면 앞의 투명 기하 깊이를 별도 색상 타깃에 기록한다.
            /// </summary>
            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                var desc = renderingData.cameraData.cameraTargetDescriptor;
                desc.depthBufferBits = 0;
                desc.graphicsFormat = GraphicsFormat.R32_SFloat;
                RenderingUtils.ReAllocateHandleIfNeeded(ref _legacyDepth, desc, FilterMode.Point, name: "Outline Transparent Depth");
                var cmd = CommandBufferPool.Get("Outline Transparent Depth");
                CoreUtils.SetRenderTarget(cmd, LegacyDepth, renderingData.cameraData.renderer.cameraDepthTargetHandle,
                    ClearFlag.Color, SystemInfo.usesReversedZBuffer ? Color.black : Color.white);
                context.ExecuteCommandBuffer(cmd);
                CommandBufferPool.Release(cmd);
                var drawing = CreateDrawingSettings(_tags, ref renderingData, SortingCriteria.CommonTransparent);
                drawing.overrideMaterial = _material;
                drawing.overrideMaterialPassIndex = 0;
                var filter = _filter;
                context.DrawRenderers(renderingData.cullResults, ref drawing, ref filter);
            }
#pragma warning restore CS0618, CS0672
        }

        private sealed class OutlinePass : ScriptableRenderPass
        {
            private const string PASS_NAME = "Screen Space Outline";
            private readonly Material _material;
            private readonly Material _maskMaterial;
            private readonly CapturePass _capture;
            private readonly TransparentDepthPass _depth;
            private RTHandle _maskTarget;
            private static readonly int MaskId = Shader.PropertyToID("_AlwaysVisibleOutlineMask");
            private static readonly List<ShaderTagId> Tags = new List<ShaderTagId>
            {
                new ShaderTagId("UniversalForward"), new ShaderTagId("UniversalForwardOnly"),
                new ShaderTagId("SRPDefaultUnlit")
            };
            private const uint ALWAYS_VISIBLE_LAYER = 1u << 30;

            private sealed class MaskPassData
            {
                internal RendererListHandle Renderers;
            }

            private sealed class PassData
            {
                internal Material Material;
                internal TextureHandle Opaque;
                internal TextureHandle TransparentDepth;
                internal TextureHandle SceneDepth;
                internal TextureHandle Normals;
                internal TextureHandle Mask;
            }

            /// <summary>
            /// 머티리얼을 보관하고 투명 오브젝트 이전에 깊이·노멀 입력을 요청한다.
            /// </summary>
            public OutlinePass(Material material, Material maskMaterial, CapturePass capture, TransparentDepthPass depth)
            {
                _material = material;
                _maskMaterial = maskMaterial;
                _capture = capture;
                _depth = depth;
                renderPassEvent = RenderPassEvent.BeforeRenderingTransparents;
                ConfigureInput(ScriptableRenderPassInput.Depth | ScriptableRenderPassInput.Normal);
            }

            /// <summary>
            /// 프레임의 깊이·노멀을 읽어 카메라 색상에 검은 면과 선을 기록한다.
            /// </summary>
            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                UniversalResourceData resources = frameData.Get<UniversalResourceData>();
                UniversalRenderingData rendering = frameData.Get<UniversalRenderingData>();
                UniversalCameraData camera = frameData.Get<UniversalCameraData>();
                UniversalLightData light = frameData.Get<UniversalLightData>();
                TextureDesc description = renderGraph.GetTextureDesc(resources.activeColorTexture);
                description.name = "Always Visible Outline Mask";
                description.colorFormat = UnityEngine.Experimental.Rendering.GraphicsFormat.R8_UNorm;
                description.depthBufferBits = DepthBits.None;
                description.clearBuffer = true;
                description.clearColor = Color.clear;
                TextureHandle mask = renderGraph.CreateTexture(description);
                DrawingSettings drawing = RenderingUtils.CreateDrawingSettings(Tags, rendering, camera, light,
                    SortingCriteria.CommonOpaque);
                drawing.overrideMaterial = _maskMaterial;
                drawing.overrideMaterialPassIndex = 0;
                FilteringSettings filter = new FilteringSettings(RenderQueueRange.opaque, -1, ALWAYS_VISIBLE_LAYER);
                using (IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass<MaskPassData>(
                    "Always Visible Outline Mask", out MaskPassData data))
                {
                    data.Renderers = renderGraph.CreateRendererList(new RendererListParams(rendering.cullResults, drawing, filter));
                    builder.UseRendererList(data.Renderers);
                    builder.SetRenderAttachment(mask, 0, AccessFlags.Write);
                    builder.SetRenderAttachmentDepth(resources.activeDepthTexture, AccessFlags.Read);
                    builder.SetGlobalTextureAfterPass(mask, MaskId);
                    builder.SetRenderFunc<MaskPassData>(DrawMask);
                }
                using (IRasterRenderGraphBuilder builder =
                    renderGraph.AddRasterRenderPass<PassData>(PASS_NAME, out PassData passData))
                {
                    FrameTextures frame = frameData.Get<FrameTextures>();
                    passData.Material = _material;
                    passData.Opaque = frame.Opaque;
                    passData.TransparentDepth = frame.TransparentDepth;
                    passData.SceneDepth = resources.cameraDepthTexture;
                    passData.Normals = resources.cameraNormalsTexture;
                    passData.Mask = mask;
                    builder.UseTexture(passData.Opaque, AccessFlags.Read);
                    builder.UseTexture(passData.TransparentDepth, AccessFlags.Read);
                    builder.UseTexture(passData.SceneDepth, AccessFlags.Read);
                    builder.UseTexture(passData.Normals, AccessFlags.Read);
                    builder.UseTexture(passData.Mask, AccessFlags.Read);
                    builder.SetRenderAttachment(resources.activeColorTexture, 0, AccessFlags.WriteAll);
                    builder.SetRenderFunc<PassData>(DrawOutline);
                }
            }

            /// <summary>선택 렌더러 목록을 깊이 검사한 흰색 마스크로 그린다.</summary>
            private static void DrawMask(MaskPassData data, RasterGraphContext context)
            {
                context.cmd.DrawRendererList(data.Renderers);
            }

            /// <summary>호환 모드에서 사용하는 임시 마스크 타깃을 해제한다.</summary>
            internal void Dispose()
            {
                _maskTarget?.Release();
                _maskTarget = null;
            }

            /// <summary>
            /// 패스 머티리얼로 전체 화면을 덮어 윤곽선 결과를 출력한다.
            /// </summary>
            private static void DrawOutline(PassData data, RasterGraphContext context)
            {
                var properties = context.renderGraphPool.GetTempMaterialPropertyBlock();
                properties.SetVector("_BlitScaleBias", new Vector4(1f, 1f, 0f, 0f));
                properties.SetTexture("_OutlineOpaqueColor", data.Opaque);
                properties.SetTexture("_OutlineTransparentDepth", data.TransparentDepth);
                properties.SetTexture("_CameraDepthTexture", data.SceneDepth);
                properties.SetTexture("_CameraNormalsTexture", data.Normals);
                properties.SetTexture("_AlwaysVisibleOutlineMask", data.Mask);
                context.cmd.DrawProcedural(Matrix4x4.identity, data.Material, 0, MeshTopology.Triangles, 3, 1, properties);
            }

#pragma warning disable CS0618, CS0672
            /// <summary>
            /// 호환 모드에서는 현재 카메라 색상 대상으로 같은 윤곽선 셰이더를 실행한다.
            /// </summary>
            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                CommandBuffer commandBuffer = CommandBufferPool.Get(PASS_NAME);
                RenderTextureDescriptor descriptor = renderingData.cameraData.cameraTargetDescriptor;
                descriptor.graphicsFormat = UnityEngine.Experimental.Rendering.GraphicsFormat.R8_UNorm;
                descriptor.depthBufferBits = 0;
                RenderingUtils.ReAllocateHandleIfNeeded(ref _maskTarget, descriptor, FilterMode.Point,
                    TextureWrapMode.Clamp, name: "Always Visible Outline Mask");
                CoreUtils.SetRenderTarget(commandBuffer, _maskTarget,
                    renderingData.cameraData.renderer.cameraDepthTargetHandle, ClearFlag.Color, Color.clear);
                context.ExecuteCommandBuffer(commandBuffer);
                commandBuffer.Clear();
                DrawingSettings drawing = CreateDrawingSettings(Tags, ref renderingData, SortingCriteria.CommonOpaque);
                drawing.overrideMaterial = _maskMaterial;
                FilteringSettings filter = new FilteringSettings(RenderQueueRange.opaque, -1, ALWAYS_VISIBLE_LAYER);
                context.DrawRenderers(renderingData.cullResults, ref drawing, ref filter);
                commandBuffer.SetGlobalTexture(MaskId, _maskTarget);
                _material.SetTexture("_OutlineOpaqueColor", _capture.LegacyColor);
                _material.SetTexture("_OutlineTransparentDepth", _depth.LegacyDepth);
                CoreUtils.SetRenderTarget(
                    commandBuffer,
                    renderingData.cameraData.renderer.cameraColorTargetHandle,
                    ClearFlag.None);
                Blitter.BlitTexture(commandBuffer, new Vector4(1f, 1f, 0f, 0f), _material, 0);
                context.ExecuteCommandBuffer(commandBuffer);
                CommandBufferPool.Release(commandBuffer);
            }
#pragma warning restore CS0618, CS0672
        }
    }
