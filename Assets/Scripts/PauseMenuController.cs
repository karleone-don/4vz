using UnityEngine;
using UnityEngine.SceneManagement;

public class PauseMenuController : MonoBehaviour
{
    public GameObject pauseMenu; // Ссылка на префаб паузы

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

    public void ResumeGame()
    {
        isPaused = false;
        pauseMenu.SetActive(false);
        Time.timeScale = 1f; // Возобновить время
    }

    public void OpenSettings()
    {
        // Здесь можно открыть меню настроек
        Debug.Log("Открыть настройки");
    }

    public void ExitToMainMenu()
    {
        Time.timeScale = 1f; // Возобновить время перед выходом
        SceneManager.LoadScene("main_menu"); // Замените "MainMenu" на имя вашей сцены главного меню
    }
}