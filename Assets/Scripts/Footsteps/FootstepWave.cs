using System.Collections;

using UnityEngine;

/// <summary>
/// 발자국 지점에서 확산되는 작은 파동 링 시각 연출을 담당한다.
/// </summary>
public class FootstepWave : MonoBehaviour
{
    [Header("Wave Settings")]
    [SerializeField] private SpriteRenderer _waveRenderer;
    [SerializeField] private float _maxRadius = 2f;
    [SerializeField] private float _duration = 1f;
    [SerializeField] private AnimationCurve _fadeCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);

    private Coroutine _waveCoroutine;
    private Color _initialColor;

    void Awake()
    {
        if (_waveRenderer != null)
        {
            _initialColor = _waveRenderer.color;
        }
    }

    void OnDisable()
    {
        if (_waveCoroutine != null)
        {
            StopCoroutine(_waveCoroutine);
            _waveCoroutine = null;
        }
    }

    /// <summary>
    /// 지정된 파동 크기와 재생 시간으로 파동 확산 연출을 시작한다.
    /// duration과 maxRadius를 전달받아 내부 코루틴을 실행한다.
    /// </summary>
    public void PlayWave(float maxRadius = -1f, float duration = -1f)
    {
        if (_waveRenderer == null)
            return;

        if (_waveCoroutine != null)
        {
            StopCoroutine(_waveCoroutine);
        }

        float targetRadius = maxRadius > 0f ? maxRadius : _maxRadius;
        float targetDuration = duration > 0f ? duration : _duration;

        _waveCoroutine = StartCoroutine(CoWaveProcess(targetRadius, targetDuration));
    }

    /// <summary>
    /// 시간에 따라 스케일을 확장하고 알파값을 페이드아웃하는 코루틴을 처리한다.
    /// targetRadius와 targetDuration을 사용하여 보간한다.
    /// </summary>
    private IEnumerator CoWaveProcess(float targetRadius, float targetDuration)
    {
        float elapsed = 0f;
        transform.localScale = Vector3.zero;

        // 시간에 따라 링 크기를 확장하고 알파값을 감소시킨다.
        while (elapsed < targetDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / targetDuration);

            float currentScale = Mathf.Lerp(0f, targetRadius, t);
            transform.localScale = new Vector3(currentScale, currentScale, currentScale);

            float alphaRate = _fadeCurve.Evaluate(t);
            Color c = _initialColor;
            c.a = _initialColor.a * alphaRate;
            _waveRenderer.color = c;

            yield return null;
        }

        transform.localScale = Vector3.zero;
        Color finalColor = _initialColor;
        finalColor.a = 0f;
        _waveRenderer.color = finalColor;
        _waveCoroutine = null;
    }
}
