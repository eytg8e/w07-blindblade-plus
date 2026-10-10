using UnityEngine;

/// <summary>미전환 씬에서 GameSceneInitializer를 생성하는 기존 씬 호환 진입점이다.</summary>
public sealed class MainMapSession : MonoBehaviour
{
    void Start()
    {
        GameSceneInitializer existing = null;
        CorridorRoundController round = null;
        TerrainMemory memory = null;
        foreach (GameObject root in gameObject.scene.GetRootGameObjects())
        {
            if (existing == null) existing = root.GetComponentInChildren<GameSceneInitializer>(true);
            if (round == null) round = root.GetComponentInChildren<CorridorRoundController>(true);
            if (memory == null) memory = root.GetComponentInChildren<TerrainMemory>(true);
        }
        if (existing != null) return;
        foreach (GameObject root in gameObject.scene.GetRootGameObjects())
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                if (child.name == "GameOverCanvas") child.gameObject.SetActive(false);
        }
        GameObject prefab = Resources.Load<GameObject>("Prefabs/GameManagers/GameSceneInitializer");
        if (prefab == null)
        {
            Debug.LogError("게임 씬 초기화 프리팹이 없습니다.", this);
            enabled = false;
            return;
        }
        GameObject initializer = Instantiate(prefab);
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(initializer, gameObject.scene);
        initializer.GetComponent<GameSceneInitializer>().ConfigureLegacy(round, memory);
    }

}
