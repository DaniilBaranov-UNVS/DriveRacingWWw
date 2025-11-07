using UnityEngine;
using System.Collections;

[RequireComponent(typeof(CarController))]
public class CarSoundController : MonoBehaviour
{
    [Header("Audio Sources")]
    public AudioSource engineStartSource;   // Звук запуска двигателя
    public AudioSource engineIdleSource;    // Idle loop (работа на месте)
    public AudioSource engineDriveSource;   // Rev loop (разгон)

    [Header("Audio Clips")]
    public AudioClip engineStartClip;
    public AudioClip engineIdleClip;
    public AudioClip engineDriveClip;

    [Header("Настройки громкости и тональности")]
    [Range(0.1f, 1f)] public float idleVolume = 0.6f;
    [Range(0.5f, 1.5f)] public float driveMinVolume = 0.3f;
    [Range(0.5f, 1.5f)] public float driveMaxVolume = 1.0f;
    [Range(0.8f, 1.5f)] public float driveMinPitch = 1.0f;
    [Range(1.0f, 3.0f)] public float driveMaxPitch = 2.0f;
    [Range(0.5f, 5f)] public float smoothSpeed = 3f;

    [Header("Условия для перехода из idle в rev")]
    [Tooltip("Порог скорости, при котором звук переходит с idle на drive")]
    public float speedThresholdKmH = 3f;
    [Tooltip("RPM, после которых звук начинает кроссфейдиться на rev")]
    public float rpmThreshold = 1200f;
    [Tooltip("Минимальное значение газа, при котором начинается звук drive")]
    public float throttleThreshold = 0.15f;

    private CarController carControllerSample;
    private Rigidbody rb;
    private int lastGear = 0;
    private float drivePitchSmooth;
    private float driveVolumeSmooth;
    private float idleVolumeSmooth;

    void Start()
    {
        carControllerSample = GetComponent<CarController>();
        rb = GetComponent<Rigidbody>();

        if (carControllerSample == null)
        {
            Debug.LogError("CarController не найден на объекте!");
            enabled = false;
            return;
        }

        SetupAudioSource(engineIdleSource, engineIdleClip, true);
        SetupAudioSource(engineDriveSource, engineDriveClip, true);

        if (engineStartClip != null && engineStartSource != null)
            StartCoroutine(PlayStartSequence());
        else
            StartEngineSounds();
    }

    void SetupAudioSource(AudioSource source, AudioClip clip, bool loop)
    {
        if (source == null) return;
        source.clip = clip;
        source.loop = loop;
        source.playOnAwake = false;
        source.volume = 0f;
    }

    IEnumerator PlayStartSequence()
    {
        engineStartSource.clip = engineStartClip;
        engineStartSource.Play();

        yield return new WaitForSeconds(engineStartClip.length * 0.8f);

        StartEngineSounds();

        yield return new WaitForSeconds(engineStartClip.length * 0.2f);
        engineStartSource.Stop();
    }

    void StartEngineSounds()
    {
        if (engineIdleSource != null && !engineIdleSource.isPlaying)
        {
            engineIdleSource.volume = idleVolume;
            engineIdleSource.Play();
        }

        if (engineDriveSource != null && !engineDriveSource.isPlaying)
        {
            engineDriveSource.volume = 0f;
            engineDriveSource.Play();
        }
    }

    void Update()
    {
        if (carControllerSample == null) return;
        UpdateEngineSound();

        // Проверка смены передачи
        int currentGear = carControllerSample.currentGear;
        
        lastGear = currentGear;
    }

    void UpdateEngineSound()
    {
        if (engineIdleSource == null || engineDriveSource == null) return;

        float rpm = carControllerSample.engineRPM;
        float rpmNormalized = Mathf.InverseLerp(carControllerSample.rpmIdle, carControllerSample.rpmRedline, rpm);
        float speedKmH = rb.linearVelocity.magnitude * 3.6f;
        float throttle = Input.GetAxis("Vertical");

        bool isRevving = (speedKmH > speedThresholdKmH) ||
                         (rpm > rpmThreshold) ||
                         (Mathf.Abs(throttle) > throttleThreshold);
        // Pitch и volume для drive
        float targetPitch = Mathf.Lerp(driveMinPitch, driveMaxPitch, rpmNormalized);
        float targetVolume = Mathf.Lerp(driveMinVolume, driveMaxVolume, rpmNormalized);

        // Сглаживание
        drivePitchSmooth = Mathf.Lerp(drivePitchSmooth, targetPitch, Time.deltaTime * smoothSpeed);
        driveVolumeSmooth = Mathf.Lerp(driveVolumeSmooth, targetVolume, Time.deltaTime * smoothSpeed);

        if (isRevving)
        {
            // Плавный переход от idle к drive
            idleVolumeSmooth = Mathf.Lerp(idleVolumeSmooth, 0f, Time.deltaTime * smoothSpeed);
            driveVolumeSmooth = Mathf.Lerp(driveVolumeSmooth, targetVolume, Time.deltaTime * smoothSpeed);
        }
        else
        {
            // Плавный переход обратно на idle
            idleVolumeSmooth = Mathf.Lerp(idleVolumeSmooth, idleVolume, Time.deltaTime * smoothSpeed);
            driveVolumeSmooth = Mathf.Lerp(driveVolumeSmooth, 0f, Time.deltaTime * smoothSpeed);
        }

        // Применяем параметры
        engineDriveSource.pitch = drivePitchSmooth;
        engineDriveSource.volume = driveVolumeSmooth;
        engineIdleSource.volume = idleVolumeSmooth;
    }
}