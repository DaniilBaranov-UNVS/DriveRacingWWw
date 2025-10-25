using UnityEngine;
using Bhaptics.SDK2;
public class test : MonoBehaviour
{
    public void TestTaktil()
    {
        BhapticsLibrary.Play(eventId: BhapticsEvent.TEST, startMillis: 0, intensity: 1, duration: 1, angleX: 0, offsetY: 0);
        Debug.Log("Test Taktilki");
    }
}
