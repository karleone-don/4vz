using UnityEngine;

public class PauseManager : MonoBehaviour
{
    public GameObject pauseMenu; // Ссылка на объект паузы

    private bool isPaused = false;

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape)) // Нажатие клавиши Escape
        {
            TogglePause();
        }
    }

    public void TogglePause()
    {
        isPaused = !isPaused;
        pauseMenu.SetActive(isPaused);

        if (isPaused)
        {
            Time.timeScale = 0f; // Остановить время
        }
        else
        {
            Time.timeScale = 1f; // Возобновить время
        }
    }
}