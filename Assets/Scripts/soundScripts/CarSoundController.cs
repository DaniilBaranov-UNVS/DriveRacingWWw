using UnityEngine;
using System.Collections;

public class CarSoundController : MonoBehaviour
{
    [Header("Audio Sources")]
    public AudioSource engineStartSource;
    public AudioSource engineIdleSource;
    public AudioSource gearShiftSource;

    [Header("Audio Clips")]
    public AudioClip engineStartClip;
    public AudioClip engineIdleClip;
    public AudioClip gearShiftClip;

    [Header("Settings")]
    public float startToIdleDelay = 0.5f;

    private CarControllerSample carController;
    private int lastGear = 0;


    void Start()
    {
        engineIdleSource.Stop();
        engineIdleSource.clip = null;

        carController = GetComponent<CarControllerSample>();
        if (carController == null)
        {
            Debug.LogError("CarControllerSample не найден на этом объекте!");
        }

        StartCoroutine(PlayEngineSounds());
    }

    void Update()
    {
        if (carController != null)
        {
            int currentGear = carController.currentGear;

            if (currentGear != lastGear && lastGear != 0)
            {
                PlayGearShiftSound();
            }

            lastGear = currentGear;
        }
    }

    IEnumerator PlayEngineSounds()
    {
        engineStartSource.clip = engineStartClip;
        engineStartSource.Play();

        // Ждем до момента, когда нужно запустить idle (до окончания стартового звука)
        float waitTime = engineStartClip.length - startToIdleDelay;
        yield return new WaitForSeconds(waitTime);

        // Запускаем idle звук, пока еще играет стартовый
        engineIdleSource.clip = engineIdleClip;
        engineIdleSource.loop = true;
        engineIdleSource.Play();

        // Ждем окончание стартового звука
        yield return new WaitForSeconds(startToIdleDelay);

        // Останавливаем стартовый звук (теперь играет только idle)
        engineStartSource.Stop();
    }

    public void PlayGearShiftSound()
    {
        if (gearShiftSource != null && gearShiftClip != null)
        {
            gearShiftSource.clip = gearShiftClip;
            gearShiftSource.Play();
        }
        else
        {
            Debug.LogWarning("Источник звука или клип переключения передачи не назначены!");
        }
    }

    public void ManualGearShift()
    {
        PlayGearShiftSound();
    }
}