using UnityEngine;

public class FivethSlide : MonoBehaviour
{
    [Header("Панели обучения")]
    public GameObject thisTaskPanel;
    public CarController carController;
    
    private float delay = 0f;
    private bool taskComplited = false;
    void Start()
    {
        if(carController == null)
        {
            carController = FindFirstObjectByType<CarController>();
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
        bool gasPressed = Input.GetKey(KeyCode.S);
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
