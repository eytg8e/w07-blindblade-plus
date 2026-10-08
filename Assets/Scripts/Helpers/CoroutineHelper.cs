using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 코루틴 관련 유틸 기능을 제공하고 MonoBehaviour가 없는 클래스에서도 코루틴을 사용할 수 있도록 합니다.
/// </summary>

// MonoSingleton으로 만든 이유는 코루틴 실행 주체 게임오브젝트를 만들기 위해서, 사용시에는 CoroutineHelper.Delay 식으로 사용하면 됨
public class CoroutineHelper : MonoSingleton<CoroutineHelper>
{
    private static MonoBehaviour GetValidCoroutineHost(MonoBehaviour coroutineHost)
    {
        return coroutineHost ?? Instance;
    }

    /// <summary>
    /// 지정된 시간 후에 콜백을 실행하는 코루틴을 시작합니다. (게임 시간 기준)
    /// </summary>
    public static Coroutine Delay(float seconds, Action onComplete, MonoBehaviour coroutineHost = null)
    {
        return GetValidCoroutineHost(coroutineHost).StartCoroutine(DelayRoutine(seconds, onComplete, false));
    }

    /// <summary>
    /// 지정된 시간 후에 콜백을 실행하는 코루틴을 시작합니다. (실제 시간 기준)
    /// </summary>
    public static Coroutine DelayRealtime(float seconds, Action onComplete, MonoBehaviour coroutineHost = null)
    {
        return GetValidCoroutineHost(coroutineHost).StartCoroutine(DelayRoutine(seconds, onComplete, true));
    }

    private static IEnumerator DelayRoutine(float seconds, Action onComplete, bool isRealtime)
    {
        yield return isRealtime == true ? new WaitForSecondsRealtime(seconds) : new WaitForSeconds(seconds);
        onComplete?.Invoke();
    }

    /// <summary>
    /// 여러 코루틴을 순차적으로 실행하는 코루틴을 시작합니다.
    /// </summary>
    public static Coroutine Sequence(IEnumerable<IEnumerator> coroutines, MonoBehaviour coroutineHost = null)
    {
        var host = GetValidCoroutineHost(coroutineHost);
        return host.StartCoroutine(SequenceRoutine(coroutines, host));
    }

    private static IEnumerator SequenceRoutine(IEnumerable<IEnumerator> coroutines, MonoBehaviour coroutineHost)
    {
        foreach (var coroutine in coroutines)
        {
            yield return coroutineHost.StartCoroutine(coroutine);
        }
    }

    /// <summary>
    /// 조건이 true가 될 때까지 대기한 후 콜백을 실행하는 코루틴을 시작합니다.
    /// </summary>
    public static Coroutine WaitUntil(Func<bool> waitCondition, Action callBack, MonoBehaviour coroutineHost = null)
    {
        return GetValidCoroutineHost(coroutineHost)
            .StartCoroutine(WaitUntilOrWhileRoutine(waitCondition, callBack, false));
    }

    /// <summary>
    /// 조건이 false가 될 때까지 대기한 후 콜백을 실행하는 코루틴을 시작합니다.
    /// </summary>
    public static Coroutine WaitWhile(Func<bool> waitCondition, Action callBack, MonoBehaviour coroutineHost = null)
    {
        return GetValidCoroutineHost(coroutineHost)
            .StartCoroutine(WaitUntilOrWhileRoutine(waitCondition, callBack, true));
    }

    private static IEnumerator WaitUntilOrWhileRoutine(Func<bool> waitCondition, Action callBack, bool isWhile)
    {
        yield return isWhile == true ? new WaitWhile(waitCondition) : new WaitUntil(waitCondition);
        callBack?.Invoke();
    }
}