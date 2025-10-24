using UnityEngine;

public class FivethSlide : MonoBehaviour
{
    [Header("Панели обучения")]
    public GameObject thisTaskPanel;
    public CarControllerSample carController;
    
    private float delay = 0f;
    private bool taskComplited = false;
    void Start()
    {
        if(carController == null)
        {
            carController = FindFirstObjectByType<CarControllerSample>();
        }
        if (thisTaskPanel != null)
            thisTaskPanel.SetActive(true);
    }
    void Update()
    {
        if (carController != null && !taskComplited)
        {
            CheckTaskCompletion();
        }
    }
    void CheckTaskCompletion()
    {
        bool gasPressed = carController.inputControllerReader.Brake > 0.1f;
        if (gasPressed)
        {
            TaskComplited();
        }
    }
    void TaskComplited()
    {
        taskComplited = true;
        Invoke("ShowNextPanel", delay);
    }
    void ShowNextPanel()
    {
        if (thisTaskPanel != null)
            thisTaskPanel.SetActive(false);
    }
}
