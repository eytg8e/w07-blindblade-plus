using System.Collections;

using UnityEngine;

using ReusableManagers;

[DisallowMultipleComponent]
[RequireComponent(typeof(Poolable))]
public sealed class FootstepWave : MonoBehaviour
{
    [Header("파동 표시")]
    [SerializeField] private SpriteRenderer _ringRenderer;
    [SerializeField, Min(0.01f), Tooltip("파동 확산 및 표시 수명(초)")]
    private float _duration = 0.6f;
    [SerializeField, Min(0f), Tooltip("시작 반경(미터), 원본 링 지름은 1미터 기준")]
    private float _startRadius = 0.05f;
    [SerializeField, Min(0f), Tooltip("종료 반경(미터)")]
    private float _endRadius = 1.5f;
    private Color _baseColor;
    private Coroutine _waveRoutine;

    void Awake()
    {
        if (_ringRenderer == null)
        {
            Debug.LogError("파동 프리팹에 링 SpriteRenderer를 연결해야 합니다.", this);
            enabled = false;
            return;
        }
        _baseColor = _ringRenderer.color;
        _ringRenderer.enabled = false;
    }

    void OnDisable()
    {
        if (_waveRoutine != null)
        {
            StopCoroutine(_waveRoutine);
            _waveRoutine = null;
        }
        if (_ringRenderer != null)
            _ringRenderer.enabled = false;
    }

    /// <summary>
    /// 파동을 지정한 가시성으로 재생하고 자체 수명 종료 후 풀로 반환한다.
    /// </summary>
    public void Play(float visibility = 1f)
    {
        if (!enabled)
        {
            Managers.Resource.Destroy(gameObject);
            return;
        }
        if (_waveRoutine != null)
            StopCoroutine(_waveRoutine);
        _ringRenderer.enabled = true;
        _waveRoutine = StartCoroutine(AnimateWave(Mathf.Clamp01(visibility)));
    }

    /// <summary>
    /// 경과 시간과 입력 가시성으로 링의 반경과 알파를 갱신하고 수명 종료 시 반환한다.
    /// </summary>
    private IEnumerator AnimateWave(float visibility)
    {
        float elapsed = 0f;
        float duration = Mathf.Max(0.01f, _duration);
        while (elapsed < duration)
        {
            float progress = Mathf.Clamp01(elapsed / duration);
            _ringRenderer.transform.localScale = Vector3.one
                * (Mathf.Lerp(_startRadius, _endRadius, progress) * 2f);
            Color color = _baseColor;
            color.a *= (1f - progress) * visibility;
            _ringRenderer.color = color;
            yield return null;
            elapsed += Time.deltaTime;
        }
        _waveRoutine = null;
        Managers.Resource.Destroy(gameObject);
    }
}
