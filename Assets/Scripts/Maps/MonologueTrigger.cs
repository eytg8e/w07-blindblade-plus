using UnityEngine;

/// <summary>플레이어의 구역 진입으로 지정된 독백 데이터를 공통 Controller에 전달한다.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider), typeof(Rigidbody))]
public sealed class MonologueTrigger : MonoBehaviour
{
    [Header("독백")]
    [SerializeField] private MonologueData _data;
    [SerializeField] private bool _playOnce = true;

    private bool _hasPlayed;

    void Reset()
    {
        GetComponent<BoxCollider>().isTrigger = true;
        var body = GetComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;
    }

    void OnTriggerEnter(Collider other)
    {
        if ((_playOnce && _hasPlayed) || _data == null
            || other.GetComponentInParent<PlayerController>() == null || !Game.IsInitialized)
            return;
        var manager = Game.Instance.UIManager;
        if (manager == null) return;
        var controller = manager.GetComponent<MonologueController>();
        if (controller == null)
        {
            Debug.LogError("씬 UIManager에 MonologueController가 필요합니다.", this);
            return;
        }
        if (controller.TryPlay(_data)) _hasPlayed = true;
    }
}
