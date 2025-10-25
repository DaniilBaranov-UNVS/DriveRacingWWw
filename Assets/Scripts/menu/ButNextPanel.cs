using UnityEngine;
using Bhaptics.SDK2;
public class ButNextPanel : MonoBehaviour
{
    public GameObject NextPanel;
    public GameObject CurrentPanel;
    public void OnNextPanel()
    {
        CurrentPanel.SetActive(false);
        NextPanel.SetActive(true);
        BhapticsLibrary.Play(eventId: BhapticsEvent.RIGHTHAND, startMillis: 0, intensity: 1, duration: 1, angleX: 0, offsetY: 0);
        Debug.Log("Test Taktilki righthand");
    }
}
