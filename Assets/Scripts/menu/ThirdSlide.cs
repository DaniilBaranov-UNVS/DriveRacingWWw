using UnityEngine;
using UnityEngine.UI;

public class ThirdSlide : MonoBehaviour
{
    [Header("Панели обучения")]
    public GameObject firstTaskPanel;
    public GameObject nextTaskPanel;  

    [Header("Ссылка на контроллер машины")]
    public CarController carController;

    [Header("Настройки")]
    private float delayBeforeNextPanel = 0f;

    private bool taskCompleted = false;

    void Start()
    {
        
        if (carController == null)
        {
            carController = FindObjectOfType<CarController>();
        }
        if (firstTaskPanel != null)
            firstTaskPanel.SetActive(true);

        if (nextTaskPanel != null)
            nextTaskPanel.SetActive(false);
    }

    void Update()
    {
        if (!taskCompleted && carController != null)
        {
            CheckFirstTaskCompletion();
        }
    }

    void CheckFirstTaskCompletion()
    {
        bool clutchPressed = Input.GetKey(carController.clutchKey) || Input.GetKey(KeyCode.LeftShift);
        bool firstGearEngaged = carController.currentGear == 1;

        if (clutchPressed && firstGearEngaged)
        {
            TaskCompleted();
        }
    }

    void TaskCompleted()
    {
        taskCompleted = true;
        Invoke("ShowNextPanel", delayBeforeNextPanel);

        // Можно добавить звук или анимацию здесь
        Debug.Log("Первое задание обучения выполнено!");
    }

    void ShowNextPanel()
    {

        if (firstTaskPanel != null)
            firstTaskPanel.SetActive(false);

        if (nextTaskPanel != null)
            nextTaskPanel.SetActive(true);

    }
}