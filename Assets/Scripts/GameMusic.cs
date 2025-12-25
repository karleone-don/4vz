using UnityEngine;

public class GameMusic : MonoBehaviour
{
    private void Start()
    {
        if (SoundManager.Instance != null)
            SoundManager.Instance.PlayGameMusic();
    }
}
