using System.Collections;

using UnityEngine;

/// <summary>
/// 개별 발자국 오브젝트의 수명, 페이드아웃 및 풀 반환을 관리한다.
/// </summary>
public class FootstepItem : MonoBehaviour
{
    [Header("Rendering")]
    [SerializeField] private SpriteRenderer _leftFootRenderer;
    [SerializeField] private SpriteRenderer _rightFootRenderer;
    [SerializeField] private FootstepWave _waveEffect;

    [Header("LifeTime")]
    [SerializeField] private float _lifeTime = 5f;
    [SerializeField] private float _fadeDuration = 2f;

    private Coroutine _lifeCoroutine;
    private Color _leftInitialColor;
    private Color _rightInitialColor;

    void Awake()
    {
        if (_leftFootRenderer != null)
        {
            _leftInitialColor = _leftFootRenderer.color;
        }

        if (_rightFootRenderer != null)
        {
            _rightInitialColor = _rightFootRenderer.color;
        }
    }

    void OnDisable()
    {
        if (_lifeCoroutine != null)
        {
            StopCoroutine(_lifeCoroutine);
            _lifeCoroutine = null;
        }
    }

    /// <summary>
    /// 발자국의 좌/우 여부 및 초기 가시성 알파를 설정하고 생명주기를 시작한다.
    /// isLeftFoot 여부와 visibilityAlpha를 받아 렌더러를 갱신한다.
    /// </summary>
    public void Init(bool isLeftFoot, float visibilityAlpha = 1f, float waveRadius = 2f)
    {
        if (_leftFootRenderer != null)
        {
            _leftFootRenderer.gameObject.SetActive(isLeftFoot);
            Color c = _leftInitialColor;
            c.a = _leftInitialColor.a * visibilityAlpha;
            _leftFootRenderer.color = c;
        }

        if (_rightFootRenderer != null)
        {
            _rightFootRenderer.gameObject.SetActive(!isLeftFoot);
            Color c = _rightInitialColor;
            c.a = _rightInitialColor.a * visibilityAlpha;
            _rightFootRenderer.color = c;
        }

        if (_waveEffect != null)
        {
            _waveEffect.PlayWave(waveRadius);
        }

        if (_lifeCoroutine != null)
        {
            StopCoroutine(_lifeCoroutine);
        }

        _lifeCoroutine = StartCoroutine(CoLifeCycle(isLeftFoot, visibilityAlpha));
    }

    /// <summary>
    /// 유지 시간 동안 발자국을 표시한 뒤 점진적으로 페이드아웃하여 오브젝트를 반환한다.
    /// isLeftFoot과 startAlpha를 사용하여 알파값을 제어한다.
    /// </summary>
    private IEnumerator CoLifeCycle(bool isLeftFoot, float startAlpha)
    {
        float waitTime = Mathf.Max(0f, _lifeTime - _fadeDuration);
        yield return new WaitForSeconds(waitTime);

        float elapsed = 0f;
        SpriteRenderer activeRenderer = isLeftFoot ? _leftFootRenderer : _rightFootRenderer;
        Color baseColor = isLeftFoot ? _leftInitialColor : _rightInitialColor;

        // 페이드 지속 시간 동안 투명도를 0으로 수렴시킨다.
        while (elapsed < _fadeDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / _fadeDuration);

            if (activeRenderer != null)
            {
                Color c = baseColor;
                c.a = Mathf.Lerp(baseColor.a * startAlpha, 0f, t);
                activeRenderer.color = c;
            }

            yield return null;
        }

        _lifeCoroutine = null;

        // Managers 풀이 초기화되어 있다면 풀 반환을 시도하고, 없으면 파괴한다.
        if (Managers.IsInitialized)
        {
            Managers.Resource.Destroy(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }
}
