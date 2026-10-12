using UnityEngine;

public class ShurikenVisualRotation : MonoBehaviour
{
    [Header("수리검 모델 회전")]
    [SerializeField] private float _rotationSpeed = 720f;
    [SerializeField, Range(0f, 180f)] private float _randomAngle = 10f;

    void OnEnable()
    {
        transform.localRotation = Quaternion.Euler(
            Random.Range(-_randomAngle, _randomAngle),
            Random.Range(-_randomAngle, _randomAngle),
            Random.Range(-_randomAngle, _randomAngle));
    }

    void Update()
    {
        transform.Rotate(Vector3.up, _rotationSpeed * Time.deltaTime, Space.Self);
    }
}
