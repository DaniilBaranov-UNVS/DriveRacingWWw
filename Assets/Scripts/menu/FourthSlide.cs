using JetBrains.Annotations;
using UnityEngine;

public class FourthSlide : MonoBehaviour
{
    [Header("Панели обучения")]
    public GameObject thisTaskPanel;
    public GameObject nextTaskPanel;

    public CarControllerSample carController;
    private float delay = 0f;
    private bool taskComplited = false;

    void Start()
    {
        if (carController == null)
        {
            carController = FindFirstObjectByType<CarControllerSample>();
        }
        if (thisTaskPanel != null)
            thisTaskPanel.SetActive(true);

        if (nextTaskPanel != null)
            nextTaskPanel.SetActive(false);
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
        bool gasPressed = carController.inputControllerReader.Throttle>0.1f;
        if (gasPressed) {
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

        if (nextTaskPanel != null)
            nextTaskPanel.SetActive(true);
    }

}
