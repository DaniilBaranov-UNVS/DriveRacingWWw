using UnityEngine;

public class ButNextPanel : MonoBehaviour
{
    public GameObject NextPanel;
    public GameObject CurrentPanel;
    public void OnNextPanel()
    {
        CurrentPanel.SetActive(false);
        NextPanel.SetActive(true);
    }
}
