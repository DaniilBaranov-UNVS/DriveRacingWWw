using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Bhaptics.SDK2;

public class CheckpointSystem : MonoBehaviour
{
    [Header("Настройки чекпоинтов")]
    public List<GameObject> checkpoints;
    public int currentCheckpointIndex = 0;

    [Header("Настройки машины")]
    public GameObject car;
    public float checkpointRadius = 5f;

    [Header("Настройки NPC изображений")]
    public List<Image> npcImages;

    [Header("Настройки окончания игры")]
    public GameObject finalText;

    [Header("Настройки меток")]
    public List<GameObject> marksImages;
    void Start()
    {
        InitializeCheckpoints();
        InitializeNPCImages();
        InitilizeMarks();
        finalText.SetActive(false);
    }

    void Update()
    {
        CheckForCheckpointCompletion();
    }

    void InitializeCheckpoints()
    {
        if (checkpoints.Count == 0)
        {
            Debug.LogError("Не добавлены чекпоинты в список!");
            return;
        }

        for (int i = 0; i < checkpoints.Count; i++)
        {
            checkpoints[i].SetActive(i == currentCheckpointIndex);
        }
    }

    void InitializeNPCImages()
    {
        for (int i = 0; i < npcImages.Count; i++)
        {
            if (npcImages[i] != null)
            {
                npcImages[i].gameObject.SetActive(i == 0);
            }
        }
    }

    void InitilizeMarks() 
    {
        for (int i = 0; i < marksImages.Count; i++) 
        {
            if (marksImages[i] != null)
            {
                marksImages[i].gameObject.SetActive(i == 0);
            }
        }
    }
       

    void CheckForCheckpointCompletion()
    {
        if (currentCheckpointIndex >= checkpoints.Count || car == null)
            return;

        float distance = Vector3.Distance(car.transform.position,
                                         checkpoints[currentCheckpointIndex].transform.position);

        if (distance <= checkpointRadius)
        {
            CompleteCheckpoint();
        }
    }

    void CompleteCheckpoint()
    {
        checkpoints[currentCheckpointIndex].SetActive(false);
        BhapticsLibrary.Play(eventId: BhapticsEvent.PROCESSCOMPLETEORDER, startMillis: 0, intensity: 1, duration: 1, angleX: 0, offsetY: 0);
        Debug.Log("Test Taktilki PROCESSCOMPLETEORDER");
        currentCheckpointIndex++;

        if (currentCheckpointIndex < checkpoints.Count)
        {
            checkpoints[currentCheckpointIndex].SetActive(true);
            UpdateNPCImages();
            UpdateMarks();
            Debug.Log("Переход к чекпоинту " + (currentCheckpointIndex + 1));
        }
        else
        {
            UpdateNPCImages();
            UpdateMarks();
            Debug.Log("Все чекпоинты пройдены! Уровень завершен.");
            finalText.SetActive(true);
            
        }
    }

    void UpdateNPCImages()
    {
        foreach (Image npcImage in npcImages)
        {
            if (npcImage != null)
            {
                npcImage.gameObject.SetActive(false);
            }
        }
        if (currentCheckpointIndex < npcImages.Count && npcImages[currentCheckpointIndex] != null)
        {
            npcImages[currentCheckpointIndex].gameObject.SetActive(true);
            BhapticsLibrary.Play(eventId: BhapticsEvent.MESSAGE, startMillis: 0, intensity: 1, duration: 1, angleX: 0, offsetY: 0);
            Debug.Log("Test Taktilki message");
        }
    }

    void UpdateMarks()
    {
        foreach (GameObject marks in marksImages)
        {
            if (marks != null)
            { 
                marks.gameObject.SetActive(false);
            }
        }
        if (currentCheckpointIndex < marksImages.Count && marksImages[currentCheckpointIndex] != null)
        {
            marksImages[currentCheckpointIndex].gameObject.SetActive(true);
        }
    }

    void OnDrawGizmosSelected()
    {
        if (checkpoints != null && currentCheckpointIndex < checkpoints.Count &&
            checkpoints[currentCheckpointIndex] != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(checkpoints[currentCheckpointIndex].transform.position, checkpointRadius);
        }
    }
}