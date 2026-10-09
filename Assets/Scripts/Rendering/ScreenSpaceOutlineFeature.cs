using System.Collections.Generic;

using UnityEngine;
using UnityEngine.Experimental.Rendering;

using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

namespace BlindBlade.Rendering
{
    public sealed class ScreenSpaceOutlineFeature : ScriptableRendererFeature
    {
        private static readonly int _lineColorId = Shader.PropertyToID("_LineColor");
        private static readonly int _lineWidthId = Shader.PropertyToID("_LineWidth");
        private static readonly int _depthSensitivityId = Shader.PropertyToID("_DepthSensitivity");
        private static readonly int _normalSensitivityId = Shader.PropertyToID("_NormalSensitivity");
        private static readonly int _usePerceptionMaskId = Shader.PropertyToID("_UsePerceptionMask");
        private static readonly int _transparentThresholdId = Shader.PropertyToID("_TransparentThreshold");

        [Header("윤곽선 표현")]
        [SerializeField] private Color _lineColor = Color.white;
        [SerializeField, Range(1, 6)] private int _lineWidth = 1;
        [SerializeField, Range(1f, 200f)] private float _depthSensitivity = 50f;
        [SerializeField, Range(1f, 20f)] private float _normalSensitivity = 4f;
        [SerializeField, Range(0.001f, 0.5f), Tooltip("선택 VFX의 색상·알파 검출 기준이다.")]
        private float _transparentThreshold = 0.015f;

        [Header("렌더링 연결")]
        [SerializeField] private Shader _shader;
        [SerializeField] private Shader _transparentDepthShader;
        private Material _material;
        private Material _depthMaterial;
        private readonly OutlineVFXRouting _routing = new OutlineVFXRouting();
        private CapturePass _capture;
        private TransparentDepthPass _depth;
        private OutlinePass _outline;

        /// <summary>
        /// 연결된 셰이더로 색상 캡처·투명 깊이·최종 윤곽선 패스를 생성한다.
        /// 기존 패스 리소스를 해제하고 현재 설정으로 재생성한다.
        /// </summary>
        public override void Create()
        {
            ReleaseResources();
            if (_shader == null)
            {
                Debug.LogError("ScreenSpaceOutlineFeature에 윤곽선 셰이더를 연결해야 합니다.", this);
                return;
            }
            if (_transparentDepthShader == null)
                _transparentDepthShader = Shader.Find("Hidden/BlindBlade/OutlineTransparentDepth");
            if (_transparentDepthShader == null)
            {
                Debug.LogError("ScreenSpaceOutlineFeature에 투명 깊이 셰이더를 연결해야 합니다.", this);
                return;
            }
            _material = CoreUtils.CreateEngineMaterial(_shader);
            _depthMaterial = CoreUtils.CreateEngineMaterial(_transparentDepthShader);
            _depthMaterial.SetFloat("_DepthBlendOp", SystemInfo.usesReversedZBuffer ? 4f : 3f);
            _capture = new CapturePass();
            _depth = new TransparentDepthPass(_depthMaterial);
            _outline = new OutlinePass(_material, _capture, _depth);
            RenderPipelineManager.beginCameraRendering += BeginCamera;
            RenderPipelineManager.endCameraRendering += EndCamera;
        }

        /// <summary>
        /// 지원 카메라의 컬링 전에 선택 VFX의 임시 큐와 표시를 준비한다.
        /// 기능 비활성 상태에서는 원본 URP 렌더링을 유지한다.
        /// </summary>
        private void BeginCamera(ScriptableRenderContext context, Camera camera)
        {
            if (!isActive || _outline == null || (camera.cameraType != CameraType.Game
                && camera.cameraType != CameraType.SceneView))
                return;
            var data = camera.GetComponent<UniversalAdditionalCameraData>();
            if (data != null && data.renderType == CameraRenderType.Overlay)
                return;
            _routing.Prepare();
        }

        /// <summary>
        /// 카메라 실행 후 원본 렌더러 상태를 복원한다.
        /// </summary>
        private void EndCamera(ScriptableRenderContext context, Camera camera) { _routing.Restore(); }

        /// <summary>
        /// 기본 게임 카메라와 Scene View에 현재 설정을 전달하고 세 패스를 등록한다.
        /// Overlay·Preview·Reflection 카메라는 기존처럼 제외한다.
        /// </summary>
        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            var camera = renderingData.cameraData;
            if (_outline == null || camera.renderType == CameraRenderType.Overlay
                || (camera.cameraType != CameraType.Game && camera.cameraType != CameraType.SceneView))
                return;
            _material.SetColor(_lineColorId, _lineColor);
            _material.SetFloat(_lineWidthId, _lineWidth);
            _material.SetFloat(_depthSensitivityId, _depthSensitivity);
            _material.SetFloat(_normalSensitivityId, _normalSensitivity);
            _material.SetFloat(_transparentThresholdId, _transparentThreshold);
            _material.SetFloat(_usePerceptionMaskId, Application.isPlaying ? 1f : 0f);
            renderer.EnqueuePass(_capture);
            renderer.EnqueuePass(_depth);
            renderer.EnqueuePass(_outline);
        }

        /// <summary>
        /// 기능 제거 시 생성한 머티리얼과 호환 모드 타깃을 해제한다.
        /// disposing 값과 무관하게 이 기능의 리소스만 정리한다.
        /// </summary>
        protected override void Dispose(bool disposing) { ReleaseResources(); }

        /// <summary>
        /// 현재 패스의 머티리얼·타깃과 참조를 정리한다.
        /// 재생성 또는 제거 이후 이전 리소스를 재사용하지 않는다.
        /// </summary>
        private void ReleaseResources()
        {
            RenderPipelineManager.beginCameraRendering -= BeginCamera;
            RenderPipelineManager.endCameraRendering -= EndCamera;
            _routing.Dispose();
            CoreUtils.Destroy(_material);
            CoreUtils.Destroy(_depthMaterial);
            _capture?.Release();
            _depth?.Release();
            _outline?.Release();
            _material = null;
            _depthMaterial = null;
            _capture = null;
            _depth = null;
            _outline = null;
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
            private readonly Material _material;
            private readonly CapturePass _capture;
            private readonly TransparentDepthPass _depth;
            private RTHandle _legacyResult;

            private sealed class PassData
            {
                public Material Material { get; set; }
                public TextureHandle Source { get; set; }
                public TextureHandle Opaque { get; set; }
                public TextureHandle TransparentDepth { get; set; }
                public TextureHandle SceneDepth { get; set; }
                public TextureHandle Normals { get; set; }
            }

            /// <summary>
            /// material과 앞선 캡처 패스를 연결해 투명 합성 후 윤곽선을 출력하게 한다.
            /// 깊이·노멀 입력과 중간 카메라 타깃을 요청한다.
            /// </summary>
            public OutlinePass(Material material, CapturePass capture, TransparentDepthPass depth)
            {
                _material = material;
                _capture = capture;
                _depth = depth;
                renderPassEvent = RenderPassEvent.BeforeRenderingTransparents;
                requiresIntermediateTexture = true;
                ConfigureInput(ScriptableRenderPassInput.Depth | ScriptableRenderPassInput.Normal);
            }

            /// <summary>
            /// 최종 색상·불투명 색상·투명 깊이를 명시적으로 읽어 별도 합성 타깃에 출력한다.
            /// 같은 텍스처의 동시 읽기·쓰기를 피한 뒤 카메라 색상으로 복사한다.
            /// </summary>
            public override void RecordRenderGraph(RenderGraph graph, ContextContainer frameData)
            {
                var resources = frameData.Get<UniversalResourceData>();
                var frame = frameData.Get<FrameTextures>();
                var desc = resources.activeColorTexture.GetDescriptor(graph);
                desc.name = "Outline Composite";
                desc.depthBufferBits = DepthBits.None;
                desc.msaaSamples = MSAASamples.None;
                var result = graph.CreateTexture(desc);
                using (var builder = graph.AddRasterRenderPass<PassData>("Screen Space Outline", out var data))
                {
                    data.Material = _material;
                    data.Source = resources.activeColorTexture;
                    data.Opaque = frame.Opaque;
                    data.TransparentDepth = frame.TransparentDepth;
                    data.SceneDepth = resources.cameraDepthTexture;
                    data.Normals = resources.cameraNormalsTexture;
                    builder.UseTexture(data.Source, AccessFlags.Read);
                    builder.UseTexture(data.Opaque, AccessFlags.Read);
                    builder.UseTexture(data.TransparentDepth, AccessFlags.Read);
                    builder.UseTexture(data.SceneDepth, AccessFlags.Read);
                    builder.UseTexture(data.Normals, AccessFlags.Read);
                    builder.SetRenderAttachment(result, 0, AccessFlags.WriteAll);
                    builder.SetRenderFunc<PassData>(DrawOutline);
                }
                graph.AddBlitPass(result, resources.activeColorTexture, Vector2.one, Vector2.zero);
            }

            /// <summary>
            /// 현재 프레임의 모든 텍스처를 속성 블록에 연결해 전체 화면 윤곽선을 그린다.
            /// </summary>
            private static void DrawOutline(PassData data, RasterGraphContext context)
            {
                var properties = context.renderGraphPool.GetTempMaterialPropertyBlock();
                properties.SetTexture("_BlitTexture", data.Source);
                properties.SetVector("_BlitScaleBias", new Vector4(1, 1, 0, 0));
                properties.SetTexture("_OutlineOpaqueColor", data.Opaque);
                properties.SetTexture("_OutlineTransparentDepth", data.TransparentDepth);
                properties.SetTexture("_CameraDepthTexture", data.SceneDepth);
                properties.SetTexture("_CameraNormalsTexture", data.Normals);
                context.cmd.DrawProcedural(Matrix4x4.identity, data.Material, 0, MeshTopology.Triangles, 3, 1, properties);
            }

            /// <summary>
            /// 호환 모드에서 할당한 합성 결과 타깃을 해제한다.
            /// </summary>
            public void Release() { _legacyResult?.Release(); }

#pragma warning disable CS0618, CS0672
            /// <summary>
            /// 호환 모드에서 저장한 색상과 투명 깊이로 최종 윤곽선을 합성한다.
            /// </summary>
            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                var desc = renderingData.cameraData.cameraTargetDescriptor;
                desc.depthBufferBits = 0;
                desc.msaaSamples = 1;
                RenderingUtils.ReAllocateHandleIfNeeded(ref _legacyResult, desc, name: "Outline Composite");
                _material.SetTexture("_OutlineOpaqueColor", _capture.LegacyColor);
                _material.SetTexture("_OutlineTransparentDepth", _depth.LegacyDepth);
                var cmd = CommandBufferPool.Get("Screen Space Outline");
                Blitter.BlitCameraTexture(cmd, renderingData.cameraData.renderer.cameraColorTargetHandle,
                    _legacyResult, _material, 0);
                Blitter.BlitCameraTexture(cmd, _legacyResult, renderingData.cameraData.renderer.cameraColorTargetHandle);
                context.ExecuteCommandBuffer(cmd);
                CommandBufferPool.Release(cmd);
            }
#pragma warning restore CS0618, CS0672
        }
    }
}
