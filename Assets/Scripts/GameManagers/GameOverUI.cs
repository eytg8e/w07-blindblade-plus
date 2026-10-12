using System.Collections;
using System.Collections.Generic;

using UnityEngine;

using UnityEngine.UI;

using TMPro;

/// <summary>게임오버 상태에 따른 화면 표시와 버튼 동작을 담당한다.</summary>
public sealed class GameOverUI : GameUI
{
    [Header("게임오버 UI")]
    [SerializeField] private GameObject _panel;
    [SerializeField] private Button _restartButton;
    private GameStateManager _state;
    private TMP_Text _restartText;

    [Header("재도전 텍스트 절단")]
    [SerializeField, Min(0.01f), Tooltip("절단 연출 시간(초)")] private float _sliceDuration = 0.2f;
    [SerializeField, Range(-90f, 90f), Tooltip("절단선 각도(도)")] private float _sliceAngle = -15f;
    [SerializeField, Min(0f), Tooltip("각 조각의 이동 거리(UI 로컬 단위)")] private float _sliceDistance = 28f;
    [SerializeField, Min(0.01f), Tooltip("빛나는 절단면 폭(UI 로컬 단위)")] private float _sliceGlowWidth = 2f;
    [SerializeField] private Color _sliceGlowColor = new Color(1f, 0.8f, 0.4f, 1f);
    [SerializeField, Min(0f), Tooltip("절단 조각이 튀어나가는 거리 배율")] private float _sliceKickMultiplier = 1.8f;
    [SerializeField, Min(1f), Tooltip("절단 순간 최대 확대 배율")] private float _slicePopScale = 1.18f;
    [SerializeField, Range(0f, 1f), Tooltip("사라질 때 최종 크기 배율")] private float _sliceEndScale = 0.65f;
    [SerializeField, Range(0f, 45f), Tooltip("각 조각이 반대로 회전하는 최종 각도(도)")] private float _sliceRotation = 12f;
    private readonly List<GameObject> _sliceObjects = new List<GameObject>();
    private readonly List<Vector3> _sliceOffsets = new List<Vector3>();
    private readonly List<Mesh> _sliceMeshes = new List<Mesh>();
    private readonly List<Material> _sliceMaterials = new List<Material>();
    private Shader _sliceShader;
    private Coroutine _sliceRoutine;
    private bool _isRestarting;

    /// <summary>Game의 상태 매니저를 캐싱하고 상태 구독과 버튼 및 초기 표시를 연결한다.</summary>
    public override void Initialize()
    {
        Shutdown();
        _restartText = _restartButton.GetComponentInChildren<TMP_Text>(true);
        _sliceShader = Resources.Load<Shader>("Effects/IntroTextSlice");
        _state = Game.Instance.GameStateManager;
        _state.StateChanged += HandleState;
        _restartButton.onClick.AddListener(Restart);
        HandleState(_state.State);
    }

    /// <summary>visible에 따라 게임오버 패널을 표시하고 재시작 버튼을 선택한다.</summary>
    public override void SetVisible(bool visible)
    {
        base.SetVisible(visible);
        _panel.SetActive(visible);
        if (visible)
        {
            _restartButton.interactable = true;
            _restartButton.Select();
        }
    }

    /// <summary>state가 게임오버이면 화면을 표시하고 그 외에는 숨긴다.</summary>
    private void HandleState(GameplayState state)
    {
        SetVisible(state == GameplayState.GameOver);
    }

    /// <summary>재도전 텍스트를 실제 시간으로 절단한 뒤 현재 상태 매니저에 재시작을 요청한다.</summary>
    private void Restart()
    {
        if (_isRestarting || _state.State != GameplayState.GameOver)
        {
            return;
        }
        if (_restartText == null || _sliceShader == null || !_sliceShader.isSupported)
        {
            Debug.LogError("게임오버 재도전 TMP 텍스트 또는 절단 셰이더를 확인해주세요.", this);
            _state.Restart();
            return;
        }

        _isRestarting = true;
        _restartButton.interactable = false;
        _restartText.ForceMeshUpdate();
        Vector3 center = _restartText.textBounds.center;
        float angle = _sliceAngle * Mathf.Deg2Rad;
        Vector4 normal = new Vector4(-Mathf.Sin(angle), Mathf.Cos(angle), 0f, 0f);
        Material[] materials = _restartText.fontSharedMaterials;
        for (int i = 0; i < _restartText.textInfo.materialCount; i++)
        {
            TMP_MeshInfo meshInfo = _restartText.textInfo.meshInfo[i];
            if (meshInfo.vertexCount == 0)
            {
                continue;
            }
            for (int side = -1; side <= 1; side += 2)
            {
                CreateSlicePiece(_restartText, meshInfo.mesh, materials[i], center, normal, side);
            }
        }
        _restartText.enabled = false;
        _sliceRoutine = StartCoroutine(AnimateRestart());
    }

    /// <summary>화면을 유지하고 재시작 버튼을 선택한다.</summary>
    private void Stay()
    {
        _restartButton.Select();
    }

    /// <summary>버튼 요청 연결과 현재 상태 참조를 해제한다.</summary>
    public override void Shutdown()
    {
        CancelSlice();
        if (_state == null) return;
        _state.StateChanged -= HandleState;
        _restartButton.onClick.RemoveListener(Restart);
        _state = null;
    }

    /// <summary>
    /// text의 로컬 좌표를 유지하는 메시와 전용 머티리얼을 생성하고 side 쪽 절반을 표시한다.
    /// </summary>
    private void CreateSlicePiece(
        TMP_Text text, Mesh sourceMesh, Material sourceMaterial,
        Vector3 center, Vector4 normal, int side)
    {
        GameObject piece = new GameObject("GameOverTextSlice", typeof(RectTransform), typeof(CanvasRenderer));
        piece.layer = text.gameObject.layer;
        RectTransform rect = (RectTransform)piece.transform;
        RectTransform sourceRect = text.rectTransform;
        // 선택한 재도전 텍스트 내부에 조각을 배치하여 기존 UI 위치를 유지한다.
        rect.SetParent(sourceRect, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = sourceRect.pivot;
        rect.sizeDelta = Vector2.zero;
        rect.anchoredPosition3D = Vector3.zero;
        rect.localRotation = Quaternion.identity;
        rect.localScale = Vector3.one;

        Mesh mesh = Instantiate(sourceMesh);
        Vector3[] vertices = mesh.vertices;
        List<Vector4> uvs = new List<Vector4>(vertices.Length);
        mesh.GetUVs(0, uvs);
        // UI 배칭의 좌표 변환에 영향받지 않도록 로컬 절단 거리를 UV에 저장한다.
        for (int i = 0; i < vertices.Length; i++)
        {
            Vector4 uv = uvs[i];
            uv.z = (vertices[i].x - center.x) * normal.x
                + (vertices[i].y - center.y) * normal.y;
            uvs[i] = uv;
        }
        mesh.SetUVs(0, uvs);

        Material material = new Material(sourceMaterial);
        material.shader = _sliceShader;
        material.SetFloat("_SliceSide", side);
        material.SetFloat("_SliceProgress", 0f);
        material.SetFloat("_SliceGlowWidth", _sliceGlowWidth);
        material.SetColor("_SliceGlowColor", _sliceGlowColor);
        CanvasRenderer renderer = piece.GetComponent<CanvasRenderer>();
        renderer.materialCount = 1;
        renderer.SetMaterial(material, 0);
        renderer.SetTexture(material.mainTexture);
        renderer.SetMesh(mesh);
        _sliceObjects.Add(piece);
        _sliceOffsets.Add(new Vector3(normal.x, normal.y, 0f) * side * _sliceDistance);
        _sliceMeshes.Add(mesh);
        _sliceMaterials.Add(material);
    }

    /// <summary>시간 배율과 무관하게 절단·페이드를 진행한 뒤 기존 상태 매니저로 씬을 재시작한다.</summary>
    private IEnumerator AnimateRestart()
    {
        float elapsed = 0f;
        while (elapsed < _sliceDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, _sliceDuration));
            float remaining = 1f - progress;
            float kick = 1f - remaining * remaining * remaining;
            float scale = progress < 0.15f
                ? Mathf.Lerp(1f, _slicePopScale, progress / 0.15f)
                : Mathf.Lerp(_slicePopScale, _sliceEndScale, (progress - 0.15f) / 0.85f);
            for (int i = 0; i < _sliceMaterials.Count; i++)
            {
                _sliceMaterials[i].SetFloat("_SliceProgress", progress);
                Transform piece = _sliceObjects[i].transform;
                piece.localPosition = _sliceOffsets[i] * (kick * _sliceKickMultiplier);
                piece.localScale = Vector3.one * scale;
                float side = i % 2 == 0 ? -1f : 1f;
                piece.localRotation = Quaternion.Euler(0f, 0f, side * _sliceRotation * kick);
            }
            yield return null;
        }
        _sliceRoutine = null;
        ClearSlicePieces();
        _state.Restart();
    }

    /// <summary>진행 중 절단 코루틴을 중지하고 리소스 및 재도전 UI 상태를 정리한다.</summary>
    private void CancelSlice()
    {
        if (_sliceRoutine != null)
        {
            StopCoroutine(_sliceRoutine);
            _sliceRoutine = null;
        }
        ClearSlicePieces();
        if (_restartText != null)
        {
            _restartText.enabled = true;
        }
        _isRestarting = false;
    }

    /// <summary>생성한 절단 조각·메시·전용 머티리얼을 해제한다.</summary>
    private void ClearSlicePieces()
    {
        for (int i = 0; i < _sliceObjects.Count; i++)
        {
            if (_sliceObjects[i] != null)
            {
                _sliceObjects[i].SetActive(false);
                Destroy(_sliceObjects[i]);
            }
            Destroy(_sliceMeshes[i]);
            Destroy(_sliceMaterials[i]);
        }
        _sliceObjects.Clear();
        _sliceOffsets.Clear();
        _sliceMeshes.Clear();
        _sliceMaterials.Clear();
    }

    void OnDisable()
    {
        CancelSlice();
    }

    void OnDestroy()
    {
        Shutdown();
    }
}
