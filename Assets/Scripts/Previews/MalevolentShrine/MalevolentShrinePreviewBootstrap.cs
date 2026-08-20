using UnityEngine;

/// <summary>
/// 预览场景入口：Play 时重建世界并开播。
/// 菜单 ACT / Combat / Preview / Malevolent Shrine 会创建带此组件的场景。
/// </summary>
[DisallowMultipleComponent]
public sealed class MalevolentShrinePreviewBootstrap : MonoBehaviour
{
    public MalevolentShrinePreviewSettings settings = MalevolentShrinePreviewSettings.CreateDefault();
    public bool buildOnAwake = true;
    public bool playOnStart = true;

    MalevolentShrinePreviewDirector _director;
    MalevolentShrinePreviewRefs _refs;

    public MalevolentShrinePreviewDirector Director => _director;

    public void Rebuild()
    {
        if (settings == null)
            settings = MalevolentShrinePreviewSettings.CreateDefault();

        MalevolentShrineMaterials oldMaterials = _refs != null ? _refs.materials : null;
        _refs = MalevolentShrineSceneFactory.Build(transform, settings);
        if (oldMaterials != null)
            oldMaterials.DestroyCreated();
        _director = GetComponent<MalevolentShrinePreviewDirector>();
        if (_director == null)
            _director = gameObject.AddComponent<MalevolentShrinePreviewDirector>();
        _director.Bind(_refs, settings);

        if (!Application.isPlaying)
            PoseForEditMode();
    }

    void PoseForEditMode()
    {
        if (_refs.shrine != null)
            _refs.shrine.localPosition = Vector3.zero;
        if (_refs.ring != null)
            _refs.ring.localScale = new Vector3(settings.radius, 1f, settings.radius);
    }

    void Awake()
    {
        if (buildOnAwake)
            Rebuild();
    }

    void Start()
    {
        if (playOnStart && _director != null)
            _director.Restart();
    }

    void OnDestroy()
    {
        if (_refs != null && _refs.materials != null)
            _refs.materials.DestroyCreated();
    }
}
