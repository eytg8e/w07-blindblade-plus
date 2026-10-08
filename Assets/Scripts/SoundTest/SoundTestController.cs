using UnityEngine;
using UnityEngine.InputSystem;

namespace SoundTest
{
    public class SoundTestController : MonoBehaviour
    {
        [Header("Connections")]
        [SerializeField] private Transform _listener;
        [SerializeField] private CharacterController _characterController;
        [SerializeField] private BgmPlaybackController _bgm;
        [SerializeField] private FootstepSoundEmitter _footstep;
        [SerializeField] private Renderer _sourceRenderer;

        [Header("Test Controls")]
        [SerializeField, Min(0.1f)] private float _stepInterval = 0.7f;
        [SerializeField, Min(0.1f)] private float _moveSpeed = 3f;
        [SerializeField, Min(1f)] private float _turnSpeed = 90f;
        [SerializeField, Min(0.1f)] private float _orbitRadius = 5f;
        private bool _automaticSteps;
        private bool _movingSource;
        private float _nextStepTime;
        private float _orbitAngle;

        void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null)
                ProcessInput(keyboard);
            if (_movingSource)
            {
                _orbitAngle += Time.deltaTime;
                _footstep.transform.position = new Vector3(Mathf.Sin(_orbitAngle) * _orbitRadius, 0.1f, Mathf.Cos(_orbitAngle) * _orbitRadius);
            }
            if (_automaticSteps && Time.time >= _nextStepTime)
            {
                _nextStepTime = Time.time + _stepInterval;
                _footstep.PlayFootstep();
            }
        }

        /// <summary>
        /// keyboard의 입력 상태를 읽어 BGM, 발음체 및 청취 캐릭터를 조작한다.
        /// CharacterController로 이동을 처리하고 재생 상태, 위치, 방향 및 발음체 표시 여부를 변경한다.
        /// </summary>
        private void ProcessInput(Keyboard keyboard)
        {
            if (keyboard.bKey.wasPressedThisFrame) _bgm.PlayBgm();
            if (keyboard.nKey.wasPressedThisFrame) _bgm.StopBgm();
            if (keyboard.spaceKey.wasPressedThisFrame) _footstep.PlayFootstep();
            if (keyboard.tKey.wasPressedThisFrame)
            {
                _automaticSteps = !_automaticSteps;
                _nextStepTime = Time.time + _stepInterval;
            }
            if (keyboard.mKey.wasPressedThisFrame) _movingSource = !_movingSource;
            if (keyboard.vKey.wasPressedThisFrame) _sourceRenderer.enabled = !_sourceRenderer.enabled;
            if (keyboard.digit1Key.wasPressedThisFrame) SetSourcePosition(new Vector3(0f, 0.1f, 3f));
            if (keyboard.digit2Key.wasPressedThisFrame) SetSourcePosition(new Vector3(-3f, 0.1f, 0f));
            if (keyboard.digit3Key.wasPressedThisFrame) SetSourcePosition(new Vector3(3f, 0.1f, 0f));
            if (keyboard.digit4Key.wasPressedThisFrame) SetSourcePosition(new Vector3(0f, 0.1f, 1f));
            if (keyboard.digit5Key.wasPressedThisFrame) SetSourcePosition(new Vector3(0f, 0.1f, 15f));
            if (keyboard.rKey.wasPressedThisFrame)
            {
                _characterController.enabled = false;
                _listener.SetPositionAndRotation(new Vector3(0f, 1f, 0f), Quaternion.identity);
                _characterController.enabled = true;
                SetSourcePosition(new Vector3(0f, 0.1f, 3f));
            }
            float turn = (keyboard.rightArrowKey.isPressed ? 1f : 0f) - (keyboard.leftArrowKey.isPressed ? 1f : 0f);
            _listener.Rotate(Vector3.up, turn * _turnSpeed * Time.deltaTime, Space.World);
            Vector3 move = Vector3.zero;
            if (keyboard.wKey.isPressed) move += _listener.forward;
            if (keyboard.sKey.isPressed) move -= _listener.forward;
            if (keyboard.dKey.isPressed) move += _listener.right;
            if (keyboard.aKey.isPressed) move -= _listener.right;
            _characterController.Move(move.normalized * _moveSpeed * Time.deltaTime);
        }

        /// <summary>
        /// position을 발음체의 월드 위치로 설정한다.
        /// 자동 이동을 정지하여 고정된 방향 및 거리에서 발소리를 비교할 수 있게 한다.
        /// </summary>
        private void SetSourcePosition(Vector3 position)
        {
            _movingSource = false;
            _footstep.transform.position = position;
        }

        void OnDrawGizmos()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(Vector3.zero, 1f);
            Gizmos.DrawWireSphere(Vector3.zero, 15f);
            Gizmos.DrawLine(Vector3.zero, Vector3.forward * 15f);
            Gizmos.DrawLine(Vector3.left * 3f, Vector3.right * 3f);
        }
    }
}
