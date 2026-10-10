using UnityEngine;

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

        [Header("윤곽선 표현")]
        [SerializeField, Tooltip("검은 면 위에 표시할 선 색상. 알파는 선 밝기에 적용한다.")]
        private Color _lineColor = Color.white;
        [SerializeField, Range(1, 6), Tooltip("렌더링 해상도 기준 선 검출 폭(픽셀).")]
        private int _lineWidth = 1;
        [SerializeField, Range(1f, 200f), Tooltip("깊이 검출 민감도. 높을수록 작은 상대 깊이 차이도 선으로 표시한다.")]
        private float _depthSensitivity = 50f;
        [SerializeField, Range(1f, 20f), Tooltip("노멀 검출 민감도. 높을수록 작은 표면 방향 차이도 선으로 표시한다.")]
        private float _normalSensitivity = 4f;

        [Header("렌더링 연결")]
        [SerializeField, Tooltip("화면 공간 윤곽선 셰이더. PC_Renderer에 기본 연결되어 있다.")]
        private Shader _shader;
        private Material _material;
        private OutlinePass _pass;

        /// <summary>
        /// 연결된 셰이더로 전용 머티리얼과 깊이·노멀을 사용하는 윤곽선 패스를 생성한다.
        /// </summary>
        public override void Create()
        {
            CoreUtils.Destroy(_material);
            _material = null;
            _pass = null;

            if (_shader == null)
            {
                Debug.LogError("ScreenSpaceOutlineFeature에 윤곽선 셰이더를 연결해야 합니다.", this);
                return;
            }

            _material = CoreUtils.CreateEngineMaterial(_shader);
            _pass = new OutlinePass(_material);
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
            renderer.EnqueuePass(_pass);
        }

        /// <summary>
        /// 기능 제거 또는 재생 종료 시 이 기능이 생성한 머티리얼을 해제한다.
        /// </summary>
        protected override void Dispose(bool disposing)
        {
            CoreUtils.Destroy(_material);
            _material = null;
            _pass = null;
        }

        private sealed class OutlinePass : ScriptableRenderPass
        {
            private const string PASS_NAME = "Screen Space Outline";
            private readonly Material _material;

            private sealed class PassData
            {
                internal Material Material;
            }

            /// <summary>
            /// 머티리얼을 보관하고 투명 오브젝트 이전에 깊이·노멀 입력을 요청한다.
            /// </summary>
            public OutlinePass(Material material)
            {
                _material = material;
                renderPassEvent = RenderPassEvent.BeforeRenderingTransparents;
                ConfigureInput(ScriptableRenderPassInput.Depth | ScriptableRenderPassInput.Normal);
            }

            /// <summary>
            /// 프레임의 깊이·노멀을 읽어 카메라 색상에 검은 면과 선을 기록한다.
            /// </summary>
            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                UniversalResourceData resources = frameData.Get<UniversalResourceData>();
                using (IRasterRenderGraphBuilder builder =
                    renderGraph.AddRasterRenderPass<PassData>(PASS_NAME, out PassData passData))
                {
                    passData.Material = _material;
                    builder.UseTexture(resources.cameraDepthTexture, AccessFlags.Read);
                    builder.UseTexture(resources.cameraNormalsTexture, AccessFlags.Read);
                    builder.SetRenderAttachment(resources.activeColorTexture, 0, AccessFlags.WriteAll);
                    builder.SetRenderFunc<PassData>(DrawOutline);
                }
            }

            /// <summary>
            /// 패스 머티리얼로 전체 화면을 덮어 윤곽선 결과를 출력한다.
            /// </summary>
            private static void DrawOutline(PassData data, RasterGraphContext context)
            {
                Blitter.BlitTexture(context.cmd, new Vector4(1f, 1f, 0f, 0f), data.Material, 0);
            }

#pragma warning disable CS0618, CS0672
            /// <summary>
            /// 호환 모드에서는 현재 카메라 색상 대상으로 같은 윤곽선 셰이더를 실행한다.
            /// </summary>
            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                CommandBuffer commandBuffer = CommandBufferPool.Get(PASS_NAME);
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
