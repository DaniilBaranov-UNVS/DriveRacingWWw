using UnityEngine;
using UnityEngine.UI;

public class VolumeSlider : MonoBehaviour
{
    [SerializeField] private Slider volumeSlider;

    private void Start()
    {
        float savedVolume = PlayerPrefs.GetFloat("MasterVolume", 0.7f);
        volumeSlider.value = savedVolume;

        volumeSlider.onValueChanged.AddListener(OnVolumeChanged);
    }

    private void OnVolumeChanged(float volume)
    {
        AudioManager.Instance.SetMasterVolume(volume);
    }
}