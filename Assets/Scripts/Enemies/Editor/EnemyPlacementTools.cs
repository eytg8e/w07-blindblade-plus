using UnityEngine;
using UnityEngine.AI;

using UnityEditor;

namespace BlindBlade.Enemies.Editor
{
    public static class EnemyPlacementTools
    {
        /// <summary>
        /// command의 부모 문맥을 사용하여 단일 근접 적 프리팹을 현재 씬에 배치하고 선택한다.
        /// 생성된 오브젝트는 Undo로 취소할 수 있다.
        /// </summary>
        [MenuItem("GameObject/BlindBlade/Enemies/근접 적", false, 10)]
        private static void CreateMelee(MenuCommand command)
        {
            PlaceEnemy("Enemy_SingleMelee", command);
        }

        /// <summary>
        /// command의 부모 문맥을 사용하여 단일 원거리 적 프리팹을 현재 씬에 배치하고 선택한다.
        /// 생성된 오브젝트는 Undo로 취소할 수 있다.
        /// </summary>
        [MenuItem("GameObject/BlindBlade/Enemies/원거리 적", false, 11)]
        private static void CreateRanged(MenuCommand command)
        {
            PlaceEnemy("Enemy_SingleRanged", command);
        }

        /// <summary>
        /// command의 부모 문맥을 사용하여 다중 패턴 적 프리팹을 현재 씬에 배치하고 선택한다.
        /// 생성된 오브젝트는 Undo로 취소할 수 있다.
        /// </summary>
        [MenuItem("GameObject/BlindBlade/Enemies/다중 패턴 적", false, 12)]
        private static void CreateMultiPattern(MenuCommand command)
        {
            PlaceEnemy("Enemy_MultiPattern", command);
        }

        /// <summary>
        /// command의 부모 문맥을 사용하여 세 박자 돌진 적 프리팹을 현재 씬에 배치하고 선택한다.
        /// 생성된 오브젝트는 Undo로 취소할 수 있다.
        /// </summary>
        [MenuItem("GameObject/BlindBlade/Enemies/세 박자 돌진 적", false, 13)]
        private static void CreateRhythmCharge(MenuCommand command)
        {
            PlaceEnemy("Enemy_RhythmCharge", command);
        }

        /// <summary>
        /// prefabName의 적을 command의 부모 아래 Scene 뷰 중심에 생성하고 가까운 NavMesh에 배치한다.
        /// 프리팹이 없으면 오류를 알리고 생성하지 않으며 성공 시 Undo 등록과 선택 상태를 변경한다.
        /// </summary>
        private static void PlaceEnemy(string prefabName, MenuCommand command)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Enemies/" + prefabName + ".prefab");
            if (prefab == null)
            {
                Debug.LogError("적 프리팹을 찾을 수 없습니다: " + prefabName);
                return;
            }

            GameObject enemy = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            Undo.RegisterCreatedObjectUndo(enemy, "적 배치");
            GameObjectUtility.SetParentAndAlign(enemy, command.context as GameObject);
            Vector3 position = SceneView.lastActiveSceneView != null ? SceneView.lastActiveSceneView.pivot : Vector3.zero;
            NavMeshAgent agent = enemy.GetComponent<NavMeshAgent>();
            NavMeshQueryFilter filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
            if (NavMesh.SamplePosition(position, out NavMeshHit sampled, 10f, filter))
                position = sampled.position;
            enemy.transform.position = position;
            Selection.activeGameObject = enemy;
            EditorGUIUtility.PingObject(enemy);
        }
    }
}
