using UnityEngine;

/// <summary>Configurable test wall used only by PlayerTestWorldAdapter.</summary>
public sealed class PlayerTestWall : MonoBehaviour
{
    [SerializeField] private bool _isDestructible;
    [SerializeField] private bool _breakSucceeds = true;
    [SerializeField] private bool _isPassableAfterBreak = true;

    /// <summary>설정된 파괴 가능성, 성공 여부, 통과 여부를 적용해 맵 충돌 결과를 반환합니다.</summary>
    public PlayerWallResolution TryBreak(Collider wallCollider)
    {
        if (!_isDestructible)
            return new PlayerWallResolution(false, false, false);

        if (!_breakSucceeds)
            return new PlayerWallResolution(true, false, false);

        if (_isPassableAfterBreak && wallCollider != null)
            wallCollider.enabled = false;

        return new PlayerWallResolution(true, true, _isPassableAfterBreak);
    }
}
