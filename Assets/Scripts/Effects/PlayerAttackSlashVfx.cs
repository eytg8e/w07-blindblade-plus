using System.Collections.Generic;

using UnityEngine;

using UnityEngine.Rendering;

/// <summary>
/// 플레이어 공격 시작에 자식 검기 파티클을 재생하고 완료 후 비활성화한다.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerController))]
public sealed class PlayerAttackSlashVfx : MonoBehaviour
{
    [Header("공격 검기 연결")]
    [SerializeField] private PlayerController _player;
    [SerializeField, InspectorName("1타 VFX"), Tooltip("Player 프리팹 자식에 배치한 VFX의 루트 ParticleSystem을 연결합니다.")]
    private ParticleSystem _slashVfx;
    [SerializeField, InspectorName("2타 VFX")]
    private ParticleSystem _secondSlashVfx;
    [SerializeField, HideInInspector]
    private ParticleSystem _thirdSlashVfx;

    [Header("검기 표현")]
    [SerializeField, InspectorName("Outline Shader 사용"), Tooltip("켜면 검기를 OutlineVFX로 표시하고, 끄면 검기의 원래 재질로 표시합니다. 연결된 모든 공격 검기에 적용됩니다.")]
    private bool _useOutlineShader = true;
    private PlayerActionDriver _timing;
    private KatanaCombatCoordinator _combat;
    private ParticleSystem _pendingSlashVfx;
    private ParticleSystem _currentSlashVfx;
    private int _pendingAction;
    private PlayerAttackKind _pendingKind;
    private bool _pending;
    private bool _appliedUseOutlineShader;
    private readonly List<Material> _slashMaterials = new List<Material>();

    void Awake()
    {
        _timing = GetComponent<PlayerActionDriver>();
        _combat = GetComponent<KatanaCombatCoordinator>();
        ConfigureSlash(_slashVfx);
        ConfigureSlash(_secondSlashVfx);
        ConfigureSlash(_thirdSlashVfx);
        _appliedUseOutlineShader = _useOutlineShader;
        StopSlash();
    }

    void OnEnable()
    {
        _player.ActionEvent += HandleAction;
    }

    void OnDisable()
    {
        _player.ActionEvent -= HandleAction;
        StopSlash();
    }

    void OnDestroy()
    {
        foreach (Material material in _slashMaterials)
            Destroy(material);
    }

    void Update()
    {
        if (_appliedUseOutlineShader != _useOutlineShader)
        {
            ApplyOutlineSetting(_slashVfx);
            ApplyOutlineSetting(_secondSlashVfx);
            ApplyOutlineSetting(_thirdSlashVfx);
            _appliedUseOutlineShader = _useOutlineShader;
        }
        if (_pending && !_player.IsPaused)
        {
            if (_player.CurrentActionId != _pendingAction || (_player.CurrentState != PlayerActionState.NormalAttack
                && _player.CurrentState != PlayerActionState.IaidoSlash)) _pending = false;
            else
            {
                _timing.GetStrikeWindow(_pendingKind, out float start, out _);
                if (_timing.GetPresentationElapsed(_pendingAction) >= start)
                {
                    _pending = false;
                    _currentSlashVfx = _pendingSlashVfx;
                    _currentSlashVfx.gameObject.SetActive(true);
                    _currentSlashVfx.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                    _currentSlashVfx.Play(true);
                }
            }
        }
        if (_currentSlashVfx != null && _currentSlashVfx.gameObject.activeSelf && !_currentSlashVfx.IsAlive(true))
            _currentSlashVfx.gameObject.SetActive(false);
    }

    /// <summary>
    /// slash의 개별 재질을 복제해 벽 가림을 없애고 조명과 배경 왜곡을 끈다.
    /// 검기에만 깊이·배경 샘플을 지정하며 공유 재질과 화면 렌더링 설정은 유지한다.
    /// </summary>
    private void ConfigureSlash(ParticleSystem slash)
    {
        if (slash == null) return;
        ApplyOutlineSetting(slash);
        foreach (ParticleSystem particle in slash.GetComponentsInChildren<ParticleSystem>(true))
        {
            var lights = particle.lights;
            lights.enabled = false;
        }
        foreach (ParticleSystemRenderer renderer in slash.GetComponentsInChildren<ParticleSystemRenderer>(true))
        {
            renderer.allowOcclusionWhenDynamic = false;
            if (renderer.sharedMaterial.shader.name == "Shader Graphs/Slash World")
            {
                renderer.enabled = false;
                continue;
            }
            Material[] materials = renderer.sharedMaterials;
            for (int i = 0; i < materials.Length; i++)
            {
                Material material = new Material(materials[i]);
                material.name = materials[i].name + " (Player Slash)";
                material.renderQueue = Mathf.Max(materials[i].renderQueue, (int)RenderQueue.Transparent);
                material.SetFloat("_ZTest", (float)CompareFunction.Always);
                material.SetFloat("_ZWrite", 0f);
                if (material.HasProperty("_Emission")) material.SetFloat("_Emission", 1f);
                _slashMaterials.Add(material);
                materials[i] = material;
            }
            renderer.sharedMaterials = materials;
            var properties = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(properties);
            properties.SetTexture("_CameraDepthTexture",
                SystemInfo.usesReversedZBuffer ? Texture2D.blackTexture : Texture2D.whiteTexture);
            properties.SetTexture("_CameraOpaqueTexture", Texture2D.blackTexture);
            renderer.SetPropertyBlock(properties);
        }
    }

    /// <summary>
    /// slash와 자식의 OutlineVFX 활성 여부를 Inspector의 검기 표현 선택으로 변경한다.
    /// 비어 있는 검기 슬롯은 건너뛰며 재질과 파티클 재생 상태는 변경하지 않는다.
    /// </summary>
    private void ApplyOutlineSetting(ParticleSystem slash)
    {
        if (slash == null) return;
        foreach (OutlineVFX outline in slash.GetComponentsInChildren<OutlineVFX>(true))
            outline.enabled = _useOutlineShader;
    }

    /// <summary>
    /// action의 공격 시작이면 공통 베기 구간에 검기 재생을 예약한다.
    /// 사망 상태로 변경되면 예약과 파티클을 지우고 검기를 비활성화한다.
    /// </summary>
    private void HandleAction(PlayerActionEvent action)
    {
        if ((action.Stage == PlayerActionStage.AttackStarted && action.State == PlayerActionState.NormalAttack)
            || action.Stage == PlayerActionStage.SlashStarted)
        {
            StopSlash();
            _pendingAction = action.ActionId;
            _pendingKind = action.Stage == PlayerActionStage.SlashStarted ? PlayerAttackKind.IaidoFinisher : PlayerAttackKind.Normal;
            _pendingSlashVfx = GetSlashForAttack(_pendingKind);
            _pending = _pendingSlashVfx != null;
        }
        else if (action.Stage == PlayerActionStage.StateChanged && action.State == PlayerActionState.Dead)
            StopSlash();
    }

    /// <summary>
    /// kind와 현재 연계 타수로 연결된 자식 VFX를 반환한다.
    /// 돌진 연계도 타수별 VFX를 사용하고 충전 발도·패링 단발 반격은 1타 VFX를 사용한다. 빈 슬롯은 검기를 생략한다.
    /// </summary>
    private ParticleSystem GetSlashForAttack(PlayerAttackKind kind)
    {
        if (_combat == null || _combat.IsCounterAttack
            || kind == PlayerAttackKind.IaidoFinisher && _player.TargetedComboStrike == 0)
            return _slashVfx;
        return _combat.Strike == 2 ? _secondSlashVfx : _slashVfx;
    }

    /// <summary>
    /// 세 타의 연결된 파티클 방출·잔상과 현재 예약 상태를 정리한다.
    /// 외부 입력 없이 각 VFX 자식을 비활성화하며 사용자가 설정한 Transform은 유지한다.
    /// </summary>
    private void StopSlash()
    {
        _pending = false;
        _pendingSlashVfx = null;
        _currentSlashVfx = null;
        StopParticle(_slashVfx);
        StopParticle(_secondSlashVfx);
        StopParticle(_thirdSlashVfx);
    }

    /// <summary>
    /// slash의 자식 파티클까지 정지하고 VFX GameObject를 비활성화한다.
    /// 비어 있는 Inspector 슬롯은 처리하지 않는다.
    /// </summary>
    private void StopParticle(ParticleSystem slash)
    {
        if (slash == null) return;
        slash.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        slash.gameObject.SetActive(false);
    }
}
