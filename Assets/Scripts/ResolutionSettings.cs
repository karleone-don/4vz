using UnityEngine;
using TMPro;

public class ResolutionSettings : MonoBehaviour
{
    public TextMeshProUGUI resolutionText;

    private Resolution[] resolutions;
    private int currentResolutionIndex;

    void Start()
    {
        resolutions = Screen.resolutions;

        // загрузка сохранённого разрешения
        currentResolutionIndex = PlayerPrefs.GetInt("ResolutionIndex", GetCurrentResolutionIndex());

        ApplyResolution();
        UpdateText();
    }

    int GetCurrentResolutionIndex()
    {
        for (int i = 0; i < resolutions.Length; i++)
        {
            if (resolutions[i].width == Screen.currentResolution.width &&
                resolutions[i].height == Screen.currentResolution.height)
            {
                return i;
            }
        }
        return resolutions.Length - 1;
    }

    public void NextResolution()
    {
        currentResolutionIndex++;
        if (currentResolutionIndex >= resolutions.Length)
            currentResolutionIndex = 0;

        ApplyResolution();
    }

    public void PreviousResolution()
    {
        currentResolutionIndex--;
        if (currentResolutionIndex < 0)
            currentResolutionIndex = resolutions.Length - 1;

        ApplyResolution();
    }

    void ApplyResolution()
    {
        Resolution res = resolutions[currentResolutionIndex];
        Screen.SetResolution(res.width, res.height, Screen.fullScreen);

        UpdateText();
        Save();
    }

    void UpdateText()
    {
        Resolution res = resolutions[currentResolutionIndex];
        resolutionText.text = $"{res.width} x {res.height}";
    }

    void Save()
    {
        PlayerPrefs.SetInt("ResolutionIndex", currentResolutionIndex);
        PlayerPrefs.Save();
    }
}
