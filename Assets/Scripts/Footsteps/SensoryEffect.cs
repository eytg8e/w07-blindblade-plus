using System.Collections;

using UnityEngine;

/// <summary>
/// 독립 효과의 수명과 공통 가시성 입력 및 풀 반환 수명을 관리한다.
/// </summary>
public abstract class SensoryEffect : MonoBehaviour
{
    private Coroutine _routine;
    private float _elapsed;
    private float _duration;
    protected float Visibility { get; private set; }
    public int PlaybackVersion { get; private set; }
    public bool IsPlaying { get; private set; }

    protected virtual void OnDisable()
    {
        if (_routine != null)
        {
            StopCoroutine(_routine);
            _routine = null;
        }
        IsPlaying = false;
    }

    /// <summary>
    /// 공통 담당자의 거리 감쇠값으로 현재 효과 가시성을 즉시 갱신한다.
    /// </summary>
    public void SetVisibility(float visibility)
    {
        Visibility = Mathf.Clamp01(visibility);
        if (IsPlaying)
            ApplyFrame(_elapsed, Mathf.Clamp01(_elapsed / _duration));
    }

    /// <summary>
    /// 기존 재생을 중단하고 주어진 수명과 가시성으로 새 독립 재생을 시작한다.
    /// </summary>
    protected void BeginPlayback(float duration, float visibility)
    {
        if (_routine != null)
            StopCoroutine(_routine);
        PlaybackVersion++;
        _elapsed = 0f;
        _duration = Mathf.Max(0.01f, duration);
        Visibility = Mathf.Clamp01(visibility);
        IsPlaying = true;
        _routine = StartCoroutine(Animate());
    }

    /// <summary>
    /// 효과 수명 동안 프레임을 적용하고 종료 시 기존 풀 또는 객체 해제로 반환한다.
    /// </summary>
    private IEnumerator Animate()
    {
        while (_elapsed < _duration)
        {
            ApplyFrame(_elapsed, Mathf.Clamp01(_elapsed / _duration));
            yield return null;
            _elapsed += Time.deltaTime;
        }
        _routine = null;
        IsPlaying = false;
        if (Managers.IsInitialized)
            Managers.Resource.Destroy(gameObject);
        else
            Destroy(gameObject);
    }

    /// <summary>
    /// 경과 시간과 정규화 진행률을 사용하여 각 효과의 독립 표시 상태를 적용한다.
    /// </summary>
    protected abstract void ApplyFrame(float elapsed, float progress);
}
