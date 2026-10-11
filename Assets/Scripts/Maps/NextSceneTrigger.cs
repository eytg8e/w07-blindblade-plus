using UnityEngine;

/// <summary>Loads the configured scene once when the player enters this trigger.</summary>
[RequireComponent(typeof(BoxCollider))]
[DisallowMultipleComponent]
public sealed class NextSceneTrigger : MonoBehaviour
{
    [Header("Scene Transition")]
    [SerializeField, Min(0)] private int _nextSceneBuildIndex = 2;
    private bool _loading;

    void OnTriggerEnter(Collider other)
    {
        if (_loading || other.GetComponentInParent<PlayerController>() == null)
            return;

        if (!Application.CanStreamedLevelBeLoaded(_nextSceneBuildIndex))
        {
            Debug.LogError("Assign a Next Scene Build Index available in the build scene list.", this);
            return;
        }

        Managers.Scene.LoadSceneAsync(_nextSceneBuildIndex);
        _loading = true;
    }
}
