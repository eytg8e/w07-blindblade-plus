using System;

using UnityEngine;
using UnityEngine.AI;

using Unity.AI.Navigation;

using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>
/// Unity CLI에서 DefenseTest의 공터, 대나무숲 및 PoC 연결을 저장한다.
/// </summary>
public static class DefensePocSceneBuilder
{
    private const string SCENE_PATH = "Assets/Scenes/TestScene/DefenseTest.unity";
    private const string BAMBOO_PATH = "Assets/Resources/Prefabs/Enviornment/Bamboo.prefab";
    private const string MELEE_PATH = "Assets/Resources/Enemies/Enemy_SingleMelee.prefab";
    private const string RANGED_PATH = "Assets/Resources/Enemies/Enemy_SingleRanged.prefab";
    private const string GROUND_MATERIAL_PATH = "Assets/Scenes/TestScene/DefensePocGround.mat";
    private const string BAMBOO_RIM_MATERIAL_PATH =
        "Assets/Scenes/TestScene/DefensePocBambooRim.mat";
    private const string NAVMESH_PATH = "Assets/Scenes/TestScene/DefensePocNavMesh.asset";

    /// <summary>
    /// 기존 DefenseTest 환경을 원형 공터와 대나무숲으로 교체한다.
    /// 플레이어 입력·집중과 적 생성 참조를 연결하고 NavMesh 및 씬을 저장한다.
    /// </summary>
    public static void Build()
    {
        UnityEngine.SceneManagement.Scene scene = EditorSceneManager.OpenScene(SCENE_PATH);
        RemoveOldEnvironment();
        GameObject arena = new GameObject("DefensePocArena");
        GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        floor.name = "CircularFloor";
        floor.transform.SetParent(arena.transform, false);
        floor.transform.localPosition = new Vector3(0f, -0.15f, 0f);
        floor.transform.localScale = new Vector3(40f, 0.15f, 40f);
        floor.GetComponent<MeshRenderer>().sharedMaterial = GetGroundMaterial();

        NavMeshSurface surface = arena.AddComponent<NavMeshSurface>();
        surface.collectObjects = CollectObjects.Children;
        surface.useGeometry = NavMeshCollectGeometry.RenderMeshes;
        surface.BuildNavMesh();
        SaveNavMesh(surface);

        PlaceBamboo();
        ConnectPlayerAndSpawner();
        EditorSceneManager.MarkSceneDirty(scene);
        SerializationMode previousSerialization = EditorSettings.serializationMode;
        try
        {
            EditorSettings.serializationMode = SerializationMode.ForceText;
            if (!EditorSceneManager.SaveScene(scene))
                throw new InvalidOperationException("DefenseTest 씬을 저장하지 못했습니다.");
            AssetDatabase.ForceReserializeAssets(new[] { SCENE_PATH },
                ForceReserializeAssetsOptions.ReserializeAssets);
        }
        finally
        {
            EditorSettings.serializationMode = previousSerialization;
        }
        Debug.Log("DefenseTest PoC 씬, 대나무숲 및 NavMesh 구성을 저장했습니다.");
    }

    /// <summary>
    /// surface가 구운 NavMeshData를 씬 밖의 고정 에셋에 저장한다.
    /// 씬에는 저장된 에셋 참조만 남기고 다음 빌드에서도 같은 경로를 유지한다.
    /// </summary>
    private static void SaveNavMesh(NavMeshSurface surface)
    {
        NavMeshData existing = AssetDatabase.LoadAssetAtPath<NavMeshData>(NAVMESH_PATH);
        NavMeshData baked = surface.navMeshData;
        if (baked == null) throw new InvalidOperationException("DefenseTest NavMesh를 굽지 못했습니다.");
        if (existing == null)
        {
            AssetDatabase.CreateAsset(baked, NAVMESH_PATH);
            return;
        }
        EditorUtility.CopySerialized(baked, existing);
        surface.navMeshData = existing;
        UnityEngine.Object.DestroyImmediate(baked);
        EditorUtility.SetDirty(existing);
    }

    /// <summary>
    /// 기존 사각 경기장과 보스 전용 환경 루트를 제거한다.
    /// 공유 Player, UIManager 및 GameSceneInitializer는 유지한다.
    /// </summary>
    private static void RemoveOldEnvironment()
    {
        string[] names = { "Arena", "SpatialLandmarks", "BossBattleHitEffects",
            "SceneTransition", "DefensePocArena", "DefensePocBamboo", "DefensePocSpawner" };
        foreach (string name in names)
        {
            GameObject root = GameObject.Find(name);
            if (root != null) UnityEngine.Object.DestroyImmediate(root);
        }
    }

    /// <summary>
    /// 원형 바닥에 사용할 URP 재질을 읽거나 만들고 반환한다.
    /// 기존 재질이 있으면 색상과 에셋을 다시 만들지 않는다.
    /// </summary>
    private static Material GetGroundMaterial()
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(GROUND_MATERIAL_PATH);
        if (material != null) return material;
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        material = new Material(shader);
        material.name = "DefensePocGround";
        material.SetColor("_BaseColor", new Color(0.23f, 0.28f, 0.16f, 1f));
        AssetDatabase.CreateAsset(material, GROUND_MATERIAL_PATH);
        return material;
    }

    /// <summary>
    /// 공터 반지름 20m부터 바깥 2m까지 대나무 프리팹을 육각 격자로 배치한다.
    /// 줄기 중심 간격 0.6m로 최소 0.5m를 넘기고 인스턴스를 씬에 저장한다.
    /// </summary>
    private static void PlaceBamboo()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BAMBOO_PATH);
        if (prefab == null) throw new InvalidOperationException("Bamboo 프리팹을 찾지 못했습니다.");
        GameObject forest = new GameObject("DefensePocBamboo");
        const float SPACING = 0.6f;
        float rowSpacing = SPACING * Mathf.Sqrt(3f) * 0.5f;
        int count = 0;
        for (int row = -43; row <= 43; row++)
        {
            float z = row * rowSpacing;
            float offset = (row & 1) == 0 ? 0f : SPACING * 0.5f;
            for (int column = -37; column <= 37; column++)
            {
                float x = column * SPACING + offset;
                float radius = Mathf.Sqrt(x * x + z * z);
                if (radius < 20f || radius > 22f) continue;
                GameObject bamboo = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                bamboo.transform.SetParent(forest.transform, true);
                bamboo.transform.position = new Vector3(x, prefab.transform.position.y, z);
                bamboo.name = "Bamboo_" + count;
                count++;
            }
        }
        Debug.Log("DefenseTest Bamboo 배치 수: " + count);
        DefensePocBambooWind wind = forest.AddComponent<DefensePocBambooWind>();
        SerializedObject serializedWind = new SerializedObject(wind);
        serializedWind.FindProperty("_forest").objectReferenceValue = forest.transform;
        serializedWind.FindProperty("_outlineMaterial").objectReferenceValue = GetBambooRimMaterial();
        serializedWind.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>
    /// DefenseTest 대나무 흰 윤곽선 재질을 읽거나 만들고 반환한다.
    /// 기존 재질이 있으면 씬 전용 설정과 에셋을 유지한다.
    /// </summary>
    private static Material GetBambooRimMaterial()
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(BAMBOO_RIM_MATERIAL_PATH);
        if (material != null) return material;
        Shader shader = Shader.Find("BlindBlade/DefensePocBambooRim");
        if (shader == null) throw new InvalidOperationException("대나무 바람 윤곽선 셰이더를 찾지 못했습니다.");
        material = new Material(shader);
        material.name = "DefensePocBambooRim";
        AssetDatabase.CreateAsset(material, BAMBOO_RIM_MATERIAL_PATH);
        return material;
    }

    /// <summary>
    /// 기존 Player 씬 인스턴스에 PoC 조작과 게이지를 추가한다.
    /// 기존 공격 판정의 검·검기 참조 및 두 적 프리팹을 생성기에 연결한다.
    /// </summary>
    private static void ConnectPlayerAndSpawner()
    {
        PlayerController player = UnityEngine.Object.FindFirstObjectByType<PlayerController>();
        if (player == null) throw new InvalidOperationException("DefenseTest Player를 찾지 못했습니다.");
        EnemyCombatService combatService = player.GetComponent<EnemyCombatService>();
        PlayerAttackSlashVfx slashVfx = player.GetComponent<PlayerAttackSlashVfx>();
        if (combatService == null || slashVfx == null)
            throw new InvalidOperationException("Player 전투·검기 연결을 찾지 못했습니다.");

        DefensePocPlayer poc = player.GetComponent<DefensePocPlayer>();
        if (poc == null) poc = player.gameObject.AddComponent<DefensePocPlayer>();
        SerializedObject serializedPoc = new SerializedObject(poc);
        serializedPoc.FindProperty("_player").objectReferenceValue = player;
        serializedPoc.FindProperty("_motor").objectReferenceValue = player.GetComponent<PlayerMotor>();
        serializedPoc.FindProperty("_combat").objectReferenceValue = player.GetComponent<KatanaCombatCoordinator>();
        serializedPoc.FindProperty("_bladeBase").objectReferenceValue =
            new SerializedObject(combatService).FindProperty("_bladeBase").objectReferenceValue;
        serializedPoc.FindProperty("_normalSlash").objectReferenceValue =
            new SerializedObject(slashVfx).FindProperty("_slashVfx").objectReferenceValue;
        serializedPoc.ApplyModifiedPropertiesWithoutUndo();
        if (player.GetComponent<DefensePocFocusBar>() == null)
            player.gameObject.AddComponent<DefensePocFocusBar>();

        GameObject spawnerObject = new GameObject("DefensePocSpawner");
        DefensePocSpawner spawner = spawnerObject.AddComponent<DefensePocSpawner>();
        SerializedObject serializedSpawner = new SerializedObject(spawner);
        serializedSpawner.FindProperty("_player").objectReferenceValue = player.transform;
        serializedSpawner.FindProperty("_meleePrefab").objectReferenceValue =
            AssetDatabase.LoadAssetAtPath<GameObject>(MELEE_PATH).GetComponent<EnemyHealth>();
        serializedSpawner.FindProperty("_rangedPrefab").objectReferenceValue =
            AssetDatabase.LoadAssetAtPath<GameObject>(RANGED_PATH).GetComponent<EnemyHealth>();
        serializedSpawner.ApplyModifiedPropertiesWithoutUndo();
    }
}
