using UnityEngine;

namespace BlindBlade.Enemies
{
    public enum EnemySoundAction
    {
        None,
        Melee,
        Ranged,
        Charge,
        Footstep,
        RhythmCharge
    }

    public enum EnemySoundStage
    {
        Telegraph,
        Action
    }

    [CreateAssetMenu(fileName = "EnemySoundSettings", menuName = "BlindBlade/Enemies/Sound Settings")]
    public sealed class EnemySoundSettings : ScriptableObject
    {
        private const string RESOURCE_PATH = "Enemies/EnemySoundSettings";
        private static EnemySoundSettings _shared;
        private static bool _loaded;

        [Header("근접 공격")]
        [SerializeField] private SoundId _meleeTelegraph = SoundId.Footstep;
        [SerializeField] private SoundId _meleeAction = SoundId.Footstep;

        [Header("원거리 공격")]
        [SerializeField] private SoundId _rangedTelegraph = SoundId.Footstep;
        [SerializeField] private SoundId _rangedAction = SoundId.Footstep;

        [Header("돌진 공격")]
        [SerializeField] private SoundId _chargeTelegraph = SoundId.Footstep;
        [SerializeField] private SoundId _chargeAction = SoundId.Footstep;

        [Header("발걸음")]
        [SerializeField] private SoundId _footstep = SoundId.Footstep;

        [Header("세 박자 돌진")]
        [SerializeField] private SoundId _rhythmChargeTelegraph = SoundId.Footstep;
        [SerializeField] private SoundId _rhythmChargeAction = SoundId.Footstep;

        /// <summary>
        /// action과 stage에 대응하는 공통 설정의 SoundId를 반환한다.
        /// 설정 에셋을 최초 요청 시 한 번 불러오며 없거나 미지원 행동이면 None을 반환한다.
        /// </summary>
        public static SoundId GetSound(EnemySoundAction action, EnemySoundStage stage)
        {
            if (!_loaded)
            {
                _shared = Resources.Load<EnemySoundSettings>(RESOURCE_PATH);
                _loaded = true;
                if (_shared == null)
                    Debug.LogError("Assets/Resources/Enemies/EnemySoundSettings.asset 공통 사운드 설정이 필요합니다.");
            }

            return _shared == null ? SoundId.None : _shared.ResolveSound(action, stage);
        }

        /// <summary>
        /// action과 stage를 공통 설정 필드에 대응시켜 사용할 SoundId를 반환한다.
        /// 발걸음은 단계 구분 없이 같은 소리를 반환하며 등록되지 않은 행동은 None을 반환한다.
        /// </summary>
        private SoundId ResolveSound(EnemySoundAction action, EnemySoundStage stage)
        {
            bool telegraph = stage == EnemySoundStage.Telegraph;
            switch (action)
            {
                case EnemySoundAction.Melee:
                    return telegraph ? _meleeTelegraph : _meleeAction;
                case EnemySoundAction.Ranged:
                    return telegraph ? _rangedTelegraph : _rangedAction;
                case EnemySoundAction.Charge:
                    return telegraph ? _chargeTelegraph : _chargeAction;
                case EnemySoundAction.Footstep:
                    return _footstep;
                case EnemySoundAction.RhythmCharge:
                    return telegraph ? _rhythmChargeTelegraph : _rhythmChargeAction;
                default:
                    return SoundId.None;
            }
        }

        /// <summary>
        /// 플레이 시작 시 입력값 없이 공통 설정 캐시와 로딩 상태를 초기화한다.
        /// Domain Reload를 끈 경우에도 다음 요청에서 설정 에셋을 다시 불러오게 한다.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetCache()
        {
            _shared = null;
            _loaded = false;
        }
    }
}
