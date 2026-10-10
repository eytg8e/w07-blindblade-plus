using UnityEngine;

/// <summary>
    /// 관측자와 수신한 소리 위치로 윤곽선의 공간·시간 인지 영역을 셰이더에 전달한다.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SensoryManager))]
    public sealed class SensoryPerceptionMask : MonoBehaviour
    {
        private const int MAX_SOUND_REGIONS = 16;
        private static readonly int _observerRegionId = Shader.PropertyToID("_OutlineObserverRegion");
        private static readonly int _soundRegionsId = Shader.PropertyToID("_OutlineSoundRegions");
        private static readonly int _soundCountId = Shader.PropertyToID("_OutlineSoundCount");
        private static readonly int _soundRadiusId = Shader.PropertyToID("_OutlineSoundRadius");
        private static readonly int _soundLifetimeId = Shader.PropertyToID("_OutlineSoundLifetime");
        private static readonly int _edgeFadeId = Shader.PropertyToID("_OutlineEdgeFade");
        private static readonly int _perceptionTimeId = Shader.PropertyToID("_OutlinePerceptionTime");

        [Header("플레이어 주변 인지")]
        [SerializeField, Min(0f), Tooltip("관측자 주변을 상시 표시하는 반경(미터). 0이면 상시 표시하지 않는다.")]
        private float _playerRadius = 2f;

        [Header("소리 주변 인지")]
        [SerializeField, Min(0f), Tooltip("수신한 감각 사건 주변을 표시하는 반경(미터).")]
        private float _soundRadius = 4f;
        [SerializeField, Min(0.01f), Tooltip("소리 주변 윤곽선이 선형으로 사라지는 수명(초).")]
        private float _soundLifetime = 1f;
        private readonly Vector4[] _soundRegions = new Vector4[MAX_SOUND_REGIONS];
        private int _soundCount;
        private bool _footstepOnly;

        [Header("인지 경계")]
        [SerializeField, Min(0.01f), Tooltip("인지 반경 안쪽에서 가장자리까지 흐려지는 거리(미터).")]
        private float _edgeFade = 0.75f;
        private SensoryManager _sensoryManager;

        void Awake()
        {
            _sensoryManager = GetComponent<SensoryManager>();
        }

        void OnEnable()
        {
            _sensoryManager.PerceptionRequested += HandlePerceptionRequested;
        }

        void OnDisable()
        {
            _sensoryManager.PerceptionRequested -= HandlePerceptionRequested;
            ClearMask();
        }

        void LateUpdate()
        {
            PublishMask();
        }

        /// <summary>enabled로 착지 전용 표시를 전환하고 이전 영역을 비운다.</summary>
        public void SetFootstepOnly(bool enabled)
        {
            _footstepOnly = enabled;
            ClearMask();
            PublishMask();
        }

        /// <summary>착지 position의 주변을 잠깐 표시하고 기존 수명으로 페이드한다.</summary>
        public void RevealFootstep(Vector3 position)
        {
            if (_sensoryManager.IsReady)
                RegisterRegion(position);
        }

        /// <summary>
        /// 사건의 월드 위치와 발생 시간을 버퍼에 등록하고 가장 오래된 영역부터 교체한다.
        /// </summary>
        private void HandlePerceptionRequested(SensoryEvent sensoryEvent)
        {
            if (_footstepOnly)
                return;
            RegisterRegion(sensoryEvent.Position);
        }

        /// <summary>position에 임시 표시 영역을 등록하고 오래된 영역을 교체한다.</summary>
        private void RegisterRegion(Vector3 position)
        {
            if (_soundCount == MAX_SOUND_REGIONS)
            {
                for (int i = 1; i < _soundCount; i++)
                    _soundRegions[i - 1] = _soundRegions[i];
                _soundCount--;
            }

            _soundRegions[_soundCount++] = new Vector4(position.x, position.y, position.z, Time.time);
            PublishMask();
        }

        /// <summary>
        /// 만료된 사건을 제거하고 현재 관측자와 설정값 및 유효 사건을 셰이더에 전달한다.
        /// </summary>
        private void PublishMask()
        {
            if (!_sensoryManager.IsReady)
            {
                ClearMask();
                return;
            }

            float currentTime = Time.time;
            int remainingCount = 0;
            for (int i = 0; i < _soundCount; i++)
            {
                if (currentTime - _soundRegions[i].w < _soundLifetime)
                    _soundRegions[remainingCount++] = _soundRegions[i];
            }
            _soundCount = remainingCount;

            Vector3 observerPosition = _sensoryManager.Observer.position;
            Shader.SetGlobalVector(_observerRegionId,
                new Vector4(observerPosition.x, observerPosition.y, observerPosition.z, _footstepOnly ? 0f : _playerRadius));
            Shader.SetGlobalFloat(_soundRadiusId, _soundRadius);
            Shader.SetGlobalFloat(_soundLifetimeId, _soundLifetime);
            Shader.SetGlobalFloat(_edgeFadeId, _edgeFade);
            Shader.SetGlobalFloat(_perceptionTimeId, currentTime);
            Shader.SetGlobalInt(_soundCountId, _soundCount);
            if (_soundCount > 0)
                Shader.SetGlobalVectorArray(_soundRegionsId, _soundRegions);
        }

        /// <summary>
        /// 관측자 미연결 또는 세션 비활성 상태에서 모든 인지 영역을 숨기고 사건 버퍼를 비운다.
        /// </summary>
        private void ClearMask()
        {
            _soundCount = 0;
            Shader.SetGlobalVector(_observerRegionId, Vector4.zero);
            Shader.SetGlobalInt(_soundCountId, 0);
        }
    }
