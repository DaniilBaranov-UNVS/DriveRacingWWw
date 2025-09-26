using UnityEngine;
using UnityEngine.SceneManagement;

public class ButFromMaintoMenu : MonoBehaviour
{
    public int gameSceneIndex2 = 0;

    public void BackMenu()
    {
        SceneManager.LoadScene(gameSceneIndex2);
    }
}
