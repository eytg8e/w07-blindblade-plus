using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using UnityEngine;

using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>사용자의 시작·끝 자세를 유지하고 중간 구간을 원본 Iaido의 같은 시각 자세로 복원한다.</summary>
public static class CorrectKatanaSlashCoordinates
{
    private const string TARGET = "Assets/Animations/NewPlayer/Player@KatanaSlash_1.anim";
    private const string SOURCE = "Assets/Animations/KatanaV2/PoseAligned/Clips/IaidoDoubleSlash.anim";
    private const string OUTPUT = "Temp/KatanaSlashEndpointRevision";
    private const float EDGE_DURATION = 2f / 60f;

    /// <summary>
    /// 현재 클립의 시작·끝 값과 원본 커브를 읽어 양 끝을 연결하고 중간의 원본 값·속도를 복원한다.
    /// 대상 Sword·Saya 위치·Euler 회전만 저장하고 경계 보존·중간 절대 자세 및 렌더 결과를 반환한다.
    /// </summary>
    public static object Apply()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Play Mode를 종료해야 합니다.");
        AnimationClip source = AssetDatabase.LoadAssetAtPath<AnimationClip>(SOURCE);
        AnimationClip target = AssetDatabase.LoadAssetAtPath<AnimationClip>(TARGET);
        GameObject player = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Prefabs/Player.prefab");
        Transform originalRig = player.transform.Find("Player Camera/KatanaRig");
        Transform sceneRig = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects()
            .First(g => g.name == "Player").transform.Find("Player Camera/KatanaRig");
        if (Vector3.Distance(sceneRig.localPosition, originalRig.localPosition) > 0.00001f
            || Quaternion.Angle(sceneRig.localRotation, originalRig.localRotation) > 0.001f
            || Vector3.Distance(sceneRig.localScale, originalRig.localScale) > 0.00001f)
            throw new InvalidOperationException("현재 리그의 부모 자세가 원본과 달라 별도의 절대 좌표 변환이 필요합니다.");
        if (Mathf.Abs(target.length - source.length) > 0.000001f)
            throw new InvalidOperationException("현재 클립과 원본의 길이가 다릅니다.");
        AnimationClip before = UnityEngine.Object.Instantiate(target);
        try
        {
            Directory.CreateDirectory(OUTPUT);
            if (!File.Exists(OUTPUT + "/before.anim")) File.Copy(TARGET, OUTPUT + "/before.anim");
            Dictionary<EditorCurveBinding, AnimationCurve> preserved = AnimationUtility.GetCurveBindings(target)
                .Where(b => b.path != "Sword" && b.path != "Saya" || b.propertyName.StartsWith("m_LocalScale"))
                .ToDictionary(b => b, b => AnimationUtility.GetEditorCurve(target, b));
            List<object> checks = new List<object>();
            float middleEnd = source.length - EDGE_DURATION;
            Undo.RegisterCompleteObjectUndo(target, "Restore Iaido middle poses");
            foreach (string path in new[] { "Sword", "Saya" })
            {
                foreach (string property in new[] { "m_LocalPosition", "localEulerAnglesRaw" })
                {
                    for (int axis = 0; axis < 3; axis++)
                    {
                        EditorCurveBinding binding = Binding(path, property + "." + "xyz"[axis]);
                        AnimationCurve original = AnimationUtility.GetEditorCurve(source, binding);
                        AnimationCurve existing = AnimationUtility.GetEditorCurve(before, binding);
                        if (existing == null) throw new InvalidOperationException("현재 위치·Euler 커브가 누락되었습니다.");
                        Keyframe first = existing.keys.First();
                        Keyframe last = existing.keys.Last();
                        float offset = property == "localEulerAnglesRaw"
                            ? 360f * Mathf.Round((first.value - original.Evaluate(0f)) / 360f) : 0f;
                        List<Keyframe> keys = new List<Keyframe> { new Keyframe(0f, first.value, 0f, 0f) };
                        keys.Add(new Keyframe(EDGE_DURATION, original.Evaluate(EDGE_DURATION) + offset,
                            Derivative(original, EDGE_DURATION), Derivative(original, EDGE_DURATION)));
                        foreach (Keyframe originalKey in original.keys
                            .Where(k => k.time > EDGE_DURATION + 0.000001f && k.time < middleEnd - 0.000001f))
                        {
                            Keyframe key = originalKey;
                            key.value += offset;
                            keys.Add(key);
                        }
                        keys.Add(new Keyframe(middleEnd, original.Evaluate(middleEnd) + offset,
                            Derivative(original, middleEnd), Derivative(original, middleEnd)));
                        keys.Add(new Keyframe(source.length, last.value, 0f, 0f));
                        AnimationCurve curve = new AnimationCurve(keys.ToArray());
                        SetFree(curve);
                        AnimationUtility.SetEditorCurve(target, binding, curve);
                    }
                }
                float positionError = 0f;
                float rotationError = 0f;
                for (int sample = 0; sample <= 1000; sample++)
                {
                    float t = Mathf.Lerp(EDGE_DURATION, middleEnd, sample / 1000f);
                    positionError = Mathf.Max(positionError, Vector3.Distance(
                        Vector(source, path, "m_LocalPosition", t), Vector(target, path, "m_LocalPosition", t)));
                    rotationError = Mathf.Max(rotationError, AngularError(
                        Quaternion.Euler(Vector(source, path, "localEulerAnglesRaw", t)),
                        Quaternion.Euler(Vector(target, path, "localEulerAnglesRaw", t))));
                }
                bool endpointsPreserved = new[] { "m_LocalPosition", "localEulerAnglesRaw" }.All(property =>
                    Vector(before, path, property, 0f) == Vector(target, path, property, 0f)
                    && Vector(before, path, property, source.length) == Vector(target, path, property, source.length));
                checks.Add(new { path, endpointsPreserved, positionError, rotationError });
            }
            bool otherCurvesPreserved = preserved.All(p =>
                AnimationUtility.GetEditorCurve(target, p.Key).keys.SequenceEqual(p.Value.keys));
            EditorUtility.SetDirty(target);
            AssetDatabase.SaveAssetIfDirty(target);
            object preview = RenderComparison(source, before, target, sceneRig);
            return new { target.length, target.frameRate, middleStart = EDGE_DURATION, middleEnd,
                otherCurvesPreserved, checks = checks.ToArray(), preview };
        }
        finally { UnityEngine.Object.DestroyImmediate(before); }
    }

    /// <summary>원본·수정 전·복원본을 같은 리그에서 렌더링하고 사용자 씬에 영향을 주지 않는 비교 PNG를 저장한다.</summary>
    private static object RenderComparison(AnimationClip source, AnimationClip before, AnimationClip target, Transform sceneRig)
    {
        UnityEngine.SceneManagement.Scene scene = EditorSceneManager.NewPreviewScene();
        RenderTexture texture = new RenderTexture(960, 535, 24);
        Texture2D image = new Texture2D(960, 535, TextureFormat.RGB24, false);
        try
        {
            GameObject player = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Prefabs/Player.prefab");
            Transform original = player.transform.Find("Player Camera/KatanaRig");
            GameObject rig = UnityEngine.Object.Instantiate(original.gameObject);
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(rig, scene);
            rig.GetComponent<Animator>().enabled = false;
            foreach (string path in new[] { "Sword", "Saya" })
                rig.transform.Find(path).localScale = sceneRig.Find(path).localScale;
            GameObject cameraObject = new GameObject("KatanaEndpointPreviewCamera");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(cameraObject, scene);
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.CopyFrom(player.GetComponentInChildren<Camera>());
            camera.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            camera.scene = scene;
            camera.overrideSceneCullingMask = EditorSceneManager.GetSceneCullingMask(scene);
            camera.targetTexture = texture;
            camera.enabled = false;
            Directory.CreateDirectory(OUTPUT + "/Preview");
            for (int frame = 0; frame <= 20; frame++)
            {
                float t = source.length * frame / 20f;
                foreach (string mode in new[] { "original", "before", "corrected" })
                {
                    rig.transform.SetPositionAndRotation(original.localPosition, original.localRotation);
                    rig.transform.localScale = original.localScale;
                    AnimationClip clip = mode == "original" ? source : mode == "before" ? before : target;
                    clip.SampleAnimation(rig, t);
                    camera.Render();
                    RenderTexture previous = RenderTexture.active;
                    RenderTexture.active = texture;
                    image.ReadPixels(new Rect(0f, 0f, 960f, 535f), 0, 0);
                    image.Apply();
                    RenderTexture.active = previous;
                    File.WriteAllBytes(OUTPUT + "/Preview/" + mode + "_" + frame.ToString("D3") + ".png", image.EncodeToPNG());
                }
            }
            return new { framesPerMode = 21 };
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(texture);
            UnityEngine.Object.DestroyImmediate(image);
            EditorSceneManager.ClosePreviewScene(scene);
        }
    }

    /// <summary>curve의 time 시각에서 Hermite 곡선의 정확한 기울기를 반환하여 원본과 연결 속도를 맞춘다.</summary>
    private static float Derivative(AnimationCurve curve, float time)
    {
        Keyframe[] keys = curve.keys;
        int segment = 0;
        while (segment < keys.Length - 2 && time > keys[segment + 1].time) segment++;
        Keyframe a = keys[segment];
        Keyframe b = keys[segment + 1];
        float duration = b.time - a.time;
        float u = (time - a.time) / duration;
        return (6f * u * u - 6f * u) * a.value / duration
            + (3f * u * u - 4f * u + 1f) * a.outTangent
            + (-6f * u * u + 6f * u) * b.value / duration
            + (3f * u * u - 2f * u) * b.inTangent;
    }

    /// <summary>expected와 actual의 정규화 내적을 double로 계산하여 작은 회전 차이를 도 단위로 반환한다.</summary>
    private static float AngularError(Quaternion expected, Quaternion actual)
    {
        double dot = (double)expected.x * actual.x + (double)expected.y * actual.y
            + (double)expected.z * actual.z + (double)expected.w * actual.w;
        double a = (double)expected.x * expected.x + (double)expected.y * expected.y
            + (double)expected.z * expected.z + (double)expected.w * expected.w;
        double b = (double)actual.x * actual.x + (double)actual.y * actual.y
            + (double)actual.z * actual.z + (double)actual.w * actual.w;
        return (float)(2d * Math.Acos(Math.Min(1d, Math.Abs(dot) / Math.Sqrt(a * b))) * 180d / Math.PI);
    }

    /// <summary>path와 property에 대응하는 Transform 커브 바인딩을 반환한다.</summary>
    private static EditorCurveBinding Binding(string path, string property)
    {
        return EditorCurveBinding.FloatCurve(path, typeof(Transform), property);
    }

    /// <summary>clip의 path·property XYZ 커브를 time에서 평가한 값을 반환한다.</summary>
    private static Vector3 Vector(AnimationClip clip, string path, string property, float time)
    {
        float[] v = "xyz".Select(axis => AnimationUtility.GetEditorCurve(clip,
            Binding(path, property + "." + axis)).Evaluate(time)).ToArray();
        return new Vector3(v[0], v[1], v[2]);
    }

    /// <summary>curve의 값과 편측 탄젠트를 보존하고 Free 보간으로 설정해 원본 중간 속도를 유지한다.</summary>
    private static void SetFree(AnimationCurve curve)
    {
        Keyframe[] originals = curve.keys;
        for (int i = 0; i < originals.Length; i++)
        {
            AnimationUtility.SetKeyBroken(curve, i, true);
            AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.Free);
            AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.Free);
            Keyframe key = curve.keys[i];
            key.inTangent = originals[i].inTangent;
            key.outTangent = originals[i].outTangent;
            curve.MoveKey(i, key);
        }
    }
}
