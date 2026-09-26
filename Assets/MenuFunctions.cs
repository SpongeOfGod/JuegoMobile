using UnityEngine;
using UnityEngine.SceneManagement;

public class MenuFunctions : MonoBehaviour
{
    public void LoadSceneByName(string name) => SceneManager.LoadScene(name);
    public void LoadSceneByIndex(int index) => SceneManager.LoadScene(index);
}
