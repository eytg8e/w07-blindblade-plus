using UnityEngine;

namespace BlindBlade.Enemies
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EnemyController))]
    public sealed class EnemyDebugView : MonoBehaviour
    {
        [Header("편집기 상태 표시")]
        [SerializeField] private bool _showLabels = true;
        [SerializeField] private Vector3 _labelOffset = new Vector3(0f, 2.7f, 0f);
        private EnemyController _controller;
        private EnemyAttackSelector _selector;
        private Camera _camera;
        private GUIStyle _labelStyle;

        void Awake()
        {
            _controller = GetComponent<EnemyController>();
            _selector = GetComponent<EnemyAttackSelector>();
            _camera = Camera.main;
        }

        void OnGUI()
        {
            if (!Application.isEditor || !_showLabels || _camera == null)
                return;

            Vector3 position = _camera.WorldToScreenPoint(transform.position + _labelOffset);
            if (position.z <= 0f)
                return;

            if (_labelStyle == null)
                _labelStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 14 };

            EnemyAttackBehaviour attack = _selector.CurrentAttack;
            string pattern = _selector.IsBusy ? $"{attack.PatternId} / {attack.Phase}" : "";
            _labelStyle.normal.textColor = _selector.IsBusy ? Color.yellow : Color.white;
            GUI.Label(new Rect(position.x - 150f, Screen.height - position.y - 25f, 300f, 55f),
                $"{gameObject.name}\n{_controller.State} {pattern}", _labelStyle);
        }
    }
}
