using UnityEngine;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace MaykerStudio.Demo
{

    public class CameraOrbit : MonoBehaviour
    {
        [Header("Settings")]
        public float rotationSpeed = 5f;
        public float zoomSpeed = 5f;
        public float verticalAngleMin = -80f;
        public float verticalAngleMax = 80f;

        [Header("References")]
        public Transform cameraTransform;

        private Vector3 _initialOffset;
        private float _currentZoom;
        private Vector2 _currentRotation;

        public void Start()
        {
            // Store initial camera offset
            _initialOffset = cameraTransform.localPosition;
            _currentZoom = -_initialOffset.z;
            _currentRotation = transform.eulerAngles;
        }

        void Update()
        {
            HandleRotation();
        }

        /// <summary>
        /// 현재 입력 백엔드의 마우스 이동을 읽어 카메라 피벗을 회전한다.
        /// 왼쪽 버튼 드래그에 회전 속도와 수직 제한을 적용하고 커서 잠금을 갱신한다.
        /// </summary>
        private void HandleRotation()
        {
#if ENABLE_INPUT_SYSTEM
            if ((EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject())
                && Mouse.current != null && Mouse.current.leftButton.isPressed)
#else
            if (!EventSystem.current.IsPointerOverGameObject() && Input.GetMouseButton(0))
#endif
            {
                Cursor.lockState = CursorLockMode.Locked;

                // Get mouse input
#if ENABLE_INPUT_SYSTEM
                Vector2 delta = Mouse.current.delta.ReadValue();
                float mouseX = delta.x * rotationSpeed * 0.05f;
                float mouseY = delta.y * rotationSpeed * 0.05f;
#else
                float mouseX = Input.GetAxis("Mouse X") * rotationSpeed;
                float mouseY = Input.GetAxis("Mouse Y") * rotationSpeed;
#endif

                // Calculate new rotation
                _currentRotation.x -= mouseY;
                _currentRotation.y += mouseX;

                // Clamp vertical rotation
                _currentRotation.x = Mathf.Clamp(
                    _currentRotation.x,
                    verticalAngleMin,
                    verticalAngleMax
                );

                // Apply rotation to pivot
                transform.rotation = Quaternion.Euler(_currentRotation.x, _currentRotation.y, 0);
            }
            else
            {
                Cursor.lockState = CursorLockMode.None;
            }
        }
    }
}
