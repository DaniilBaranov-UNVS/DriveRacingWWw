using UnityEngine;
using UnityEngine.Audio;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [SerializeField] private AudioMixer audioMixer;
    private const string MASTER_VOLUME_PARAM = "MasterVolume";

    private const float MIN_DB = -80f;
    private const float MAX_DB = 0f;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            LoadVolume();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void SetMasterVolume(float volumePercent)
    {
        // volumePercent от 0 до 1
        float volumeDB = MIN_DB;

        if (volumePercent > 0.001f) 
        {
            // Логарифмическое преобразование для естественного восприятия
            volumeDB = Mathf.Log10(volumePercent) * 20f;
            volumeDB = Mathf.Clamp(volumeDB, MIN_DB, MAX_DB);
        }

        audioMixer.SetFloat(MASTER_VOLUME_PARAM, volumeDB);

        // Сохраняем настройку
        PlayerPrefs.SetFloat("MasterVolume", volumePercent);
        PlayerPrefs.Save();

        Debug.Log($"Volume set: {volumePercent} -> {volumeDB}dB");
    }

    private void LoadVolume()
    {
        float savedVolume = PlayerPrefs.GetFloat("MasterVolume", 0.7f);
        SetMasterVolume(savedVolume);
    }
}