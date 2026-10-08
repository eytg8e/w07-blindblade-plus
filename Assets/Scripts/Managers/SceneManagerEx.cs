using System;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneManagerEx
{
    public Scene CurrentScene => SceneManager.GetActiveScene();

    public void LoadScene(string name, LoadSceneMode mode = LoadSceneMode.Single)
    {
        Validate(name);
        SceneManager.LoadScene(name, mode);
    }

    public void LoadScene(int buildIndex, LoadSceneMode mode = LoadSceneMode.Single)
    {
        Validate(buildIndex);
        SceneManager.LoadScene(buildIndex, mode);
    }

    public AsyncOperation LoadSceneAsync(string name, LoadSceneMode mode = LoadSceneMode.Single)
    {
        Validate(name);
        return SceneManager.LoadSceneAsync(name, mode);
    }

    public AsyncOperation LoadSceneAsync(int buildIndex, LoadSceneMode mode = LoadSceneMode.Single)
    {
        Validate(buildIndex);
        return SceneManager.LoadSceneAsync(buildIndex, mode);
    }

    private static void Validate(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || !Application.CanStreamedLevelBeLoaded(name))
            throw new ArgumentException("Scene is not available. Check the build scene list: " + name, nameof(name));
    }

    private static void Validate(int buildIndex)
    {
        if (buildIndex < 0 || !Application.CanStreamedLevelBeLoaded(buildIndex))
            throw new ArgumentOutOfRangeException(nameof(buildIndex), "Scene is not in the build scene list.");
    }
}