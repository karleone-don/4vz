using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;  // ← Обязательно для смены сцен!

[RequireComponent(typeof(Button))]
public class AutoPlay : MonoBehaviour
{
    void Awake()
    {
        GetComponent<Button>().onClick.AddListener(() =>
        {
            Debug.Log("Переход в игру!");
            SceneManager.LoadScene("SampleScene");  // ← Имя твоей игровой сцены!
        });
    }
}
