using UnityEngine;

public class BillboardWithScale : MonoBehaviour
{
    public Transform targetCamera;
    public float baseScale = 1f;        // базовый размер маркера
    public float scaleFactor = 0.05f;   // коэффициент "увеличения с расстоянием"
    public float minScale = 0.5f;       // минимальный размер
    public float maxScale = 3f;         // максимальный размер

    void LateUpdate()
    {
        if (targetCamera == null)
            targetCamera = Camera.main.transform;

        // Поворот к камере
        Vector3 direction = targetCamera.position - transform.position;
        direction.y = 0;
        transform.rotation = Quaternion.LookRotation(direction) * Quaternion.Euler(0, 180, 0);

        // Масштаб в зависимости от расстояния
        float distance = Vector3.Distance(transform.position, targetCamera.position);
        float scale = Mathf.Clamp(baseScale + distance * scaleFactor, minScale, maxScale);

        transform.localScale = new Vector3(scale, scale, scale);
    }
}