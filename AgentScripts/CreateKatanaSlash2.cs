using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using UnityEngine;

using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>현재 1타의 5번 프레임에서 우하단 준비와 좌상단 베기를 연결한 2타 클립을 작성한다.</summary>
public static class CreateKatanaSlash2
{
    private const string SOURCE = "Assets/Animations/NewPlayer/Player@KatanaSlash_1.anim";
    private const string TARGET = "Assets/Animations/NewPlayer/Player@KatanaSlash_2.anim";
    private const string OUTPUT = "Temp/KatanaSlash2";
    private const float FPS = 60f;
    private const float STEP = 1f / 240f;

    /// <summary>
    /// 1타의 현재 커브와 기존 2타의 끝 자세를 읽어 준비·반전 베기·후속 복귀 커브를 저장한다.
    /// 기존 GUID와 Scale 및 이벤트를 보존하고 격리된 리그의 미리보기 경로를 반환한다.
    /// </summary>
    public static object Apply()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Play Mode를 종료해야 합니다.");
        AnimationClip source = AssetDatabase.LoadAssetAtPath<AnimationClip>(SOURCE);
        AnimationClip target = AssetDatabase.LoadAssetAtPath<AnimationClip>(TARGET);
        if (source == null || target == null) throw new InvalidOperationException("1타 또는 2타 클립이 없습니다.");
        Directory.CreateDirectory(OUTPUT);
        string backup = OUTPUT + "/before_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".anim";
        File.Copy(TARGET, backup);
        float duration = target.length;
        if (Mathf.Abs(duration - 10f / FPS) > 0.00001f || Mathf.Abs(source.frameRate - FPS) > 0.001f)
            throw new InvalidOperationException("현재 60fps·10프레임 기준과 클립 길이가 다릅니다.");
        Vector3 start = Read(source, "Sword", "m_LocalPosition", 5f / FPS);
        Vector3 ready = Mirror(Read(source, "Sword", "m_LocalPosition", 2f / FPS));
        Vector3 finish = Read(target, "Sword", "m_LocalPosition", duration);
        Quaternion startRotation = Quaternion.Euler(Read(source, "Sword", "localEulerAnglesRaw", 5f / FPS));
        Quaternion readyRotation = ReflectedRotation(source, 2f / FPS);
        Quaternion endRotation = ReadRotation(target, "Sword", duration);
        Vector3 cutEnd = Mirror(Read(source, "Sword", "m_LocalPosition", 5f / FPS));
        Quaternion cutEndRotation = ReflectedRotation(source, 5f / FPS);
        Undo.RegisterCompleteObjectUndo(target, "Create Katana upward second slash");
        foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(target)
            .Where(b => b.path == "Sword" && (b.propertyName.StartsWith("localEulerAngles") || b.propertyName.StartsWith("m_LocalRotation"))).ToArray())
            AnimationUtility.SetEditorCurve(target, binding, null);
        for (int axis = 0; axis < 3; axis++)
        {
            AnimationCurve original = Curve(source, "Sword", "m_LocalPosition." + "xyz"[axis]);
            float sign = axis == 0 ? -1f : 1f;
            float entryVelocity = sign * Derivative(original, 2f / FPS);
            float exitVelocity = sign * Derivative(original, 5f / FPS);
            List<Keyframe> keys = new List<Keyframe>
            {
                new Keyframe(0f, start[axis], 0f, 0f),
                new Keyframe(2f / FPS, ready[axis], entryVelocity, entryVelocity)
            };
            foreach (Keyframe k in original.keys.Where(k => k.time > 2f / FPS + 0.000001f && k.time < 5f / FPS - 0.000001f))
            {
                Keyframe key = k;
                key.value *= sign;
                key.inTangent *= sign;
                key.outTangent *= sign;
                keys.Add(key);
            }
            keys.Add(new Keyframe(5f / FPS, cutEnd[axis], exitVelocity, exitVelocity));
            // 베기 끝에서 감속하고 왼쪽에서 검을 낮추는 별도의 복귀 경로를 사용한다.
            keys.Add(new Keyframe(6f / FPS, cutEnd[axis] + exitVelocity / FPS * 0.5f, 0f, 0f));
            keys.Add(new Keyframe(8f / FPS, axis == 0 ? Mathf.Lerp(cutEnd.x, finish.x, 0.5f)
                : axis == 1 ? Mathf.Lerp(cutEnd.y, finish.y, 0.7f) : Mathf.Lerp(cutEnd.z, finish.z, 0.35f),
                (finish[axis] - cutEnd[axis]) / (4f / FPS), (finish[axis] - cutEnd[axis]) / (4f / FPS)));
            keys.Add(new Keyframe(duration, finish[axis], 0f, 0f));
            SetCurve(target, Binding("Sword", "m_LocalPosition." + "xyz"[axis]), keys);
        }

        List<Quaternion> rotations = new List<Quaternion>();
        for (int sample = 0; sample <= 40; sample++)
        {
            float time = sample * STEP;
            Quaternion rotation;
            if (time < 2f / FPS)
                rotation = Quaternion.Slerp(startRotation, readyRotation, Smooth(time / (2f / FPS)));
            else if (time <= 5f / FPS)
                rotation = ReflectedRotation(source, time);
            else if (time <= 6f / FPS)
                rotation = cutEndRotation;
            else
                rotation = Quaternion.Slerp(cutEndRotation, endRotation, Smooth((time - 6f / FPS) / (4f / FPS)));
            if (rotations.Count > 0 && Quaternion.Dot(rotations.Last(), rotation) < 0f)
                rotation = new Quaternion(-rotation.x, -rotation.y, -rotation.z, -rotation.w);
            rotations.Add(rotation);
        }
        for (int axis = 0; axis < 4; axis++)
        {
            List<Keyframe> keys = new List<Keyframe>();
            for (int sample = 0; sample < rotations.Count; sample++)
            {
                float tangent = sample == 0 || sample == rotations.Count - 1 ? 0f
                    : (rotations[sample + 1][axis] - rotations[sample - 1][axis]) / (2f * STEP);
                keys.Add(new Keyframe(sample * STEP, rotations[sample][axis], tangent, tangent));
            }
            SetCurve(target, Binding("Sword", "m_LocalRotation." + "xyzw"[axis]), keys);
        }
        foreach (string property in new[] { "m_LocalPosition", "localEulerAnglesRaw" })
        {
            Vector3 sayaStart = Read(source, "Saya", property, 5f / FPS);
            Vector3 sayaEnd = Read(target, "Saya", property, duration);
            for (int axis = 0; axis < 3; axis++)
                SetCurve(target, Binding("Saya", property + "." + "xyz"[axis]), new List<Keyframe>
                {
                    new Keyframe(0f, sayaStart[axis], 0f, 0f),
                    new Keyframe(6f / FPS, sayaStart[axis], 0f, 0f),
                    new Keyframe(duration, sayaEnd[axis], 0f, 0f)
                });
        }
        target.EnsureQuaternionContinuity();
        AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(target);
        settings.loopTime = false;
        AnimationUtility.SetAnimationClipSettings(target, settings);
        EditorUtility.SetDirty(target);
        AssetDatabase.SaveAssetIfDirty(target);
        return new { path = TARGET, target.length, target.frameRate, backup, preview = Render(source, target) };
    }

    /// <summary>value의 화면 좌우 방향인 X만 반전한 위치를 반환한다.</summary>
    private static Vector3 Mirror(Vector3 value) => new Vector3(-value.x, value.y, value.z);

    /// <summary>source의 time 시각 Euler 회전을 좌우 반전하여 칼날 장축을 반전한 Quaternion을 반환한다.</summary>
    private static Quaternion ReflectedRotation(AnimationClip source, float time)
    {
        Vector3 euler = Read(source, "Sword", "localEulerAnglesRaw", time);
        return Quaternion.Euler(euler.x, -euler.y, -euler.z);
    }

    /// <summary>clip의 path 회전을 time에서 평가한다. Euler 또는 Quaternion 커브 형식에 맞는 회전을 반환한다.</summary>
    private static Quaternion ReadRotation(AnimationClip clip, string path, float time)
    {
        if (Curve(clip, path, "localEulerAnglesRaw.x") != null)
            return Quaternion.Euler(Read(clip, path, "localEulerAnglesRaw", time));
        return new Quaternion(Curve(clip, path, "m_LocalRotation.x").Evaluate(time),
            Curve(clip, path, "m_LocalRotation.y").Evaluate(time),
            Curve(clip, path, "m_LocalRotation.z").Evaluate(time),
            Curve(clip, path, "m_LocalRotation.w").Evaluate(time)).normalized;
    }

    /// <summary>value를 0~1로 제한한 준비·복귀 보간율을 반환한다. 주된 베기 구간에는 적용하지 않는다.</summary>
    private static float Smooth(float value)
    {
        value = Mathf.Clamp01(value);
        return value * value * (3f - 2f * value);
    }

    /// <summary>path와 property로 Transform의 Float Curve 바인딩을 생성해 반환한다.</summary>
    private static EditorCurveBinding Binding(string path, string property) => EditorCurveBinding.FloatCurve(path, typeof(Transform), property);

    /// <summary>clip의 path와 property에 대응하는 기존 커브를 반환한다.</summary>
    private static AnimationCurve Curve(AnimationClip clip, string path, string property) => AnimationUtility.GetEditorCurve(clip, Binding(path, property));

    /// <summary>clip의 path와 property XYZ 커브를 time 시각에 평가하여 벡터를 반환한다.</summary>
    private static Vector3 Read(AnimationClip clip, string path, string property, float time)
    {
        return new Vector3(Curve(clip, path, property + ".x").Evaluate(time),
            Curve(clip, path, property + ".y").Evaluate(time), Curve(clip, path, property + ".z").Evaluate(time));
    }

    /// <summary>curve의 time 시각 속도를 중앙 차분으로 계산하여 준비와 베기 구간의 연결 속도를 반환한다.</summary>
    private static float Derivative(AnimationCurve curve, float time) => (curve.Evaluate(time + 0.00001f) - curve.Evaluate(time - 0.00001f)) / 0.00002f;

    /// <summary>keys의 편측 속도를 Free 설정으로 보존하고 clip의 binding에 커브를 저장한다.</summary>
    private static void SetCurve(AnimationClip clip, EditorCurveBinding binding, List<Keyframe> keys)
    {
        AnimationCurve curve = new AnimationCurve(keys.ToArray());
        for (int i = 0; i < keys.Count; i++)
        {
            AnimationUtility.SetKeyBroken(curve, i, true);
            AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.Free);
            AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.Free);
            curve.MoveKey(i, keys[i]);
        }
        AnimationUtility.SetEditorCurve(clip, binding, curve);
    }

    /// <summary>source의 5번 프레임과 target의 각 자세를 기존 Player의 격리 리그로 렌더링하고 PNG 저장 경로를 반환한다.</summary>
    private static string Render(AnimationClip source, AnimationClip target)
    {
        var scene = EditorSceneManager.NewPreviewScene();
        RenderTexture texture = new RenderTexture(960, 540, 24);
        Texture2D image = new Texture2D(960, 540, TextureFormat.RGB24, false);
        RenderTexture previous = RenderTexture.active;
        try
        {
            GameObject player = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Prefabs/Player.prefab");
            Transform original = player.transform.Find("Player Camera/KatanaRig");
            GameObject rig = UnityEngine.Object.Instantiate(original.gameObject);
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(rig, scene);
            rig.GetComponent<Animator>().enabled = false;
            GameObject cameraObject = new GameObject("Slash2PreviewCamera");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(cameraObject, scene);
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.CopyFrom(player.GetComponentInChildren<Camera>(true));
            camera.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            camera.scene = scene;
            camera.overrideSceneCullingMask = EditorSceneManager.GetSceneCullingMask(scene);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.targetTexture = texture;
            camera.enabled = false;
            for (int frame = -1; frame <= 20; frame++)
            {
                rig.transform.SetPositionAndRotation(original.localPosition, original.localRotation);
                rig.transform.localScale = original.localScale;
                (frame < 0 ? source : target).SampleAnimation(rig, frame < 0 ? 5f / FPS : frame / 120f);
                camera.Render();
                RenderTexture.active = texture;
                image.ReadPixels(new Rect(0, 0, 960, 540), 0, 0);
                image.Apply();
                File.WriteAllBytes(OUTPUT + "/" + (frame < 0 ? "source_frame5" : "second_" + frame.ToString("D2")) + ".png", image.EncodeToPNG());
            }
            return OUTPUT;
        }
        finally
        {
            RenderTexture.active = previous;
            UnityEngine.Object.DestroyImmediate(texture);
            UnityEngine.Object.DestroyImmediate(image);
            EditorSceneManager.ClosePreviewScene(scene);
        }
    }
}
