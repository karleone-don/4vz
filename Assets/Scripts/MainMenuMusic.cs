using UnityEngine;

public class MainMenuMusic : MonoBehaviour
{
    private void Start()
    {
        if (SoundManager.Instance != null)
            SoundManager.Instance.PlayMenuMusic();
    }
}
