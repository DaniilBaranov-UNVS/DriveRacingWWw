using System.Collections.Generic;
using UnityEngine;

public class CheckpointSystem : MonoBehaviour
{
    [Header("Настройки чекпоинтов")]
    public List<GameObject> checkpoints; 
    public int currentCheckpointIndex = 0; 

    [Header("Настройки машины")]
    public GameObject car; 
    public float checkpointRadius = 5f; 

    void Start()
    {
        InitializeCheckpoints();
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
            if (i == currentCheckpointIndex)
            {
                checkpoints[i].SetActive(true);
            }
            else
            {
                checkpoints[i].SetActive(false);
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

        currentCheckpointIndex++;

        if (currentCheckpointIndex < checkpoints.Count)
        {
            checkpoints[currentCheckpointIndex].SetActive(true);
            Debug.Log("Переход к чекпоинту " + (currentCheckpointIndex + 1));
        }
        else
        {
            Debug.Log("Все чекпоинты пройдены! Уровень завершен.");
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