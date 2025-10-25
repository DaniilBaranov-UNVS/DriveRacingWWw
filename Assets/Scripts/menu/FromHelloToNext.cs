using UnityEngine;
using Bhaptics.SDK2;
public class FromHelloToNext : MonoBehaviour
{
    [Header("Панели обучения")]
    public GameObject currentslide;
    public GameObject nextTaskPanel;
    void Start()
    {
        if (currentslide != null)
            currentslide.SetActive(true);

        if (nextTaskPanel != null)
            nextTaskPanel.SetActive(false);
    }
    public void ShowNextPanel()
    {

        if (currentslide != null)
            currentslide.SetActive(false);

        if (nextTaskPanel != null)
        {
            nextTaskPanel.SetActive(true);
            BhapticsLibrary.Play(eventId: BhapticsEvent.BUTTERFLY, startMillis: 0, intensity: 1, duration: 1, angleX: 0, offsetY: 0);
            Debug.Log("Test Taktilki butterfly");
        }

    }
}
