using UnityEngine;
using Bhaptics.SDK2;
public class SimpleCarCollision : MonoBehaviour
{
    void OnCollisionEnter(Collision collision)
    {
        // Получаем точку удара в локальных координатах машины
        Vector3 localCollisionPoint = transform.InverseTransformPoint(collision.contacts[0].point);

        // Определяем удар спереди или сзади по Z-координате
        if (localCollisionPoint.z > 0)
        {
            BhapticsLibrary.Play(eventId: BhapticsEvent.FRONTCRASH, startMillis: 0, intensity: 1, duration: 1, angleX: 0, offsetY: 0);
            Debug.Log("Test Taktilki FRONTCRASH");
        }
        else
        {
            BhapticsLibrary.Play(eventId: BhapticsEvent.BACKCRASH, startMillis: 0, intensity: 1, duration: 1, angleX: 0, offsetY: 0);
            Debug.Log("Test Taktilki BACKCRASH");
        }
    }
}