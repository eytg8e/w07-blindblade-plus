using System.Collections;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>지정된 모든 보스의 실제 사망을 확인하고 지연과 페이드 후 다음 빌드 씬으로 이동한다.</summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(210)]
public sealed class BossSceneExit : MonoBehaviour
{
    [Header("보스 및 화면")]
    [SerializeField] private EnemyHealth[] _bosses;
    [SerializeField] private SceneTransitionFade _fade;

    [Header("전환 시간")]
    [SerializeField, Min(0f)] private float _transitionDelay = 4f;
    [SerializeField, Min(.01f)] private float _fadeOutDuration = 2f;
    private readonly HashSet<EnemyHealth> _dead = new HashSet<EnemyHealth>();
    private bool _transitioning;
    private GameStateManager _state;
    private InputManager _input;

    void Start()
    {
        if (_bosses == null || _bosses.Length == 0 || _fade == null)
        {
            Debug.LogError("보스 사망 전환의 Bosses와 Fade를 연결하세요.", this);
            enabled = false;
            return;
        }
        foreach (EnemyHealth boss in _bosses)
        {
            if (boss == null)
            {
                Debug.LogError("보스 사망 전환에 누락된 보스가 있습니다.", this);
                enabled = false;
                return;
            }
        }
        _state = Game.IsInitialized ? Game.Instance.GameStateManager : null;
        _input = Game.IsInitialized ? Game.Instance.InputManager : null;
        foreach (EnemyHealth boss in _bosses) boss.Died += HandleDeath;
    }

    void OnDisable()
    {
        StopAllCoroutines();
        if (_bosses != null)
            foreach (EnemyHealth boss in _bosses)
                if (boss != null) boss.Died -= HandleDeath;
    }

    /// <summary>health의 사망을 기록하고 모든 지정 보스가 죽었을 때 전환을 한 번 시작한다.</summary>
    private void HandleDeath(EnemyHealth health)
    {
        _dead.Add(health);
        if (_transitioning || _dead.Count != _bosses.Length) return;
        _transitioning = true;
        StartCoroutine(Transition());
    }

    /// <summary>지연의 마지막 구간에 페이드아웃을 포함하고 다음 빌드 인덱스로 이동한다. 게임오버이면 취소한다.</summary>
    private IEnumerator Transition()
    {
        int next = gameObject.scene.buildIndex + 1;
        if (gameObject.scene.buildIndex < 0 || !Application.CanStreamedLevelBeLoaded(next))
        {
            Debug.LogError("현재 씬 다음의 활성 빌드 씬이 없습니다.", this);
            yield break;
        }
        float fadeDuration = Mathf.Min(_fadeOutDuration, _transitionDelay);
        float wait = Mathf.Max(0f, _transitionDelay - fadeDuration);
        float elapsed = 0f;
        while (elapsed < wait)
        {
            if (!CanContinue()) yield break;
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
        if (!CanContinue()) yield break;
        if (_input != null) _input.SetGameplayInputEnabled(false);
        yield return _fade.FadeOut(fadeDuration);
        if (!CanContinue())
        {
            _fade.Clear();
            yield break;
        }
        SceneTransitionFade.RequestEntry(next);
        Time.timeScale = 1f;
        SceneManager.LoadSceneAsync(next);
    }

    /// <summary>게임오버 또는 재시작이 아니라면 승리 후 씬 이동을 계속할 수 있다.</summary>
    private bool CanContinue()
    {
        return _state == null || _state.State == GameplayState.Playing;
    }
}
