using System;

using UnityEngine;

namespace BlindBlade.Gameplay
{
    /// <summary>게임플레이 입력 허용 정책과 변경 알림을 관리하며 UI 입력은 유지한다.</summary>
    public sealed class InputManager : MonoBehaviour
    {
        private GameStateManager _state;
        public bool IsInitialized => _state != null;
        public bool IsGameplayInputEnabled { get; private set; }
        public event Action<bool> GameplayInputEnabledChanged;

        /// <summary>state의 상태 변경을 구독하고 게임플레이 입력을 차단한다.</summary>
        public void Initialize(GameStateManager state)
        {
            Shutdown();
            _state = state;
            _state.StateChanged += HandleState;
            SetGameplayInputEnabled(false);
        }

        /// <summary>state가 플레이 중일 때만 게임플레이 입력을 허용한다.</summary>
        private void HandleState(GameplayState state)
        {
            SetGameplayInputEnabled(state == GameplayState.Playing);
        }

        /// <summary>allowed로 입력 정책을 변경하고 변경된 허용 상태를 구독자에게 알린다.</summary>
        public void SetGameplayInputEnabled(bool allowed)
        {
            if (IsGameplayInputEnabled == allowed) return;
            IsGameplayInputEnabled = allowed;
            GameplayInputEnabledChanged?.Invoke(allowed);
        }

        /// <summary>게임플레이 입력을 차단하고 상태 이벤트 구독을 해제한다.</summary>
        public void Shutdown()
        {
            SetGameplayInputEnabled(false);
            if (_state != null) _state.StateChanged -= HandleState;
            _state = null;
        }

        void OnDestroy()
        {
            Shutdown();
        }
    }
}
