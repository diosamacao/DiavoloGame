using System;
using System.Collections.Generic;
using UnityEditor.Search;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>创建入口共用的 Unity 原生 Search Picker 与选片预览，不写正式动作。</summary>
public sealed class ActionAnimationPickerPanel : IDisposable
{
    // 仅缓存完整扫描结果；资源变化后整批失效，避免取消搜索留下不完整动作库。
    static readonly Dictionary<string, LibraryEntry[]> libraries = new(StringComparer.Ordinal);
    static int libraryVersion;
    sealed class LibraryEntry
    {
        public AnimationClip Clip;
        public string Name, Path, Id;
    }
    static ActionAnimationPickerPanel()
    {
        EditorApplication.projectChanged += () => { libraries.Clear(); libraryVersion++; };
    }

    readonly EditorWindow owner;
    readonly ActionEditorPreviewViewport viewport = new();
    readonly ActionEditorPreviewSession session;

    Object scope;
    string rmPath, match;
    bool playing, preview, bound, hasPending;
    AnimationClip[] pending;
    readonly List<AnimationClip> sequence = new();
    UnityEditorInternal.ReorderableList sequenceList;

    AnimationClip selected;
    GameObject model;
    ActionDefinition draft;
    int frame;
    double lastTime;

    /// <summary>选择预览独占全局 AnimationMode；其他窗口保留状态并暂停采样。</summary>
    public static Object PreviewOwner { get; private set; }

    public ActionAnimationPickerPanel(EditorWindow owner)
    {
        this.owner = owner;
        session = new ActionEditorPreviewSession(owner);
        EditorApplication.projectChanged += Invalidate;
        EditorApplication.update += Update;
    }

    void Invalidate() { match = null; owner.Repaint(); }

    /// <summary>绑定角色或独立创建范围；切换范围清除候选、选中项及预览。</summary>
    public void Bind(Object context, GameObject prefab)
    {
        if (bound && scope == context) return;
        StopPreview(); bound = true; scope = context; model = prefab; selected = null;
        hasPending = false; match = null;
    }

    /// <summary>列出目录或单个导入文件中的真实 Clip 子资源；同名片段保持对象身份。</summary>
    public static List<AnimationClip> Collect(string path)
    {
        var paths = AssetDatabase.IsValidFolder(path)
            ? AssetDatabase.FindAssets("t:AnimationClip", new[] { path }).Select(AssetDatabase.GUIDToAssetPath).Distinct()
            : new[] { path };
        return paths.SelectMany(AssetDatabase.LoadAllAssetsAtPath).OfType<AnimationClip>()
            .Where(c => !c.name.StartsWith("__preview__", StringComparison.Ordinal))
            .Distinct().OrderBy(c => c.name, Comparer<string>.Create(EditorUtility.NaturalCompare)).ThenBy(AssetDatabase.GetAssetPath).ToList();
    }

    /// <summary>绘制原生多选入口与可重排清单，返回组成一个动作的有序动画段。</summary>
    public AnimationClip[] Draw(AnimationClip[] current)
    {
        sequence.Clear(); sequence.AddRange(current ?? Array.Empty<AnimationClip>());
        if (hasPending) { sequence.AddRange(pending); hasPending = false; GUI.changed = true; }
        string rm = CharacterAnimationSourcePreferences.Get(scope, true);
        if (rm != rmPath) { rmPath = rm; match = null; }
        CharacterAnimationSourcePreferences.DrawAnimationFolder(scope);
        DrawFolder("RM 目录", true, rmPath);
        var context = scope;
        var added = DrawClipField(EditorGUILayout.GetControlRect(), new GUIContent("添加动画（多选）"), null, scope,
            clip => { }, () => OpenSequencePicker(scope, clips =>
            { if (owner == null || scope != context) return; pending = clips; hasPending = true; owner.Repaint(); }));
        if (added != null) sequence.Add(added);
        EditorGUILayout.HelpBox("小圆圈打开动作库，Ctrl / Shift 多选后按 Enter 添加。下方顺序即同一个动作的播放顺序，可拖动排序；每段可单独预览。", MessageType.Info);
        if (sequenceList == null)
        {
            sequenceList = new UnityEditorInternal.ReorderableList(sequence, typeof(AnimationClip), true, true, false, true);
            sequenceList.drawHeaderCallback = r => EditorGUI.LabelField(r, "动画段 · 按顺序组成一个 ActionDefinition");
            sequenceList.drawElementCallback = (r, i, active, focused) =>
            {
                r.height = EditorGUIUtility.singleLineHeight;
                EditorGUI.LabelField(r, $"{i + 1}. {(sequence[i] != null ? sequence[i].name : "缺少动画")}");
            };
            sequenceList.onSelectCallback = list =>
            { StopPreview(); selected = sequence[list.index]; match = null; };
            sequenceList.onRemoveCallback = list =>
            {
                if (list.index < 0 || list.index >= sequence.Count) return;
                StopPreview(); sequence.RemoveAt(list.index); selected = null; match = null;
            };
        }
        sequenceList.DoLayoutList();
        if (!sequence.Contains(selected)) { StopPreview(); selected = sequence.FirstOrDefault(); match = null; }
        if (selected != null)
        {
            if (match == null) match = string.IsNullOrEmpty(rmPath) ? "未指定 RM 目录"
                : MotionClipPairMatcher.TryMatchSingle(selected, rmPath, out var pair, out string error)
                    ? "RM：" + pair.RootMotionClip.name : "RM：" + error;
            EditorGUILayout.LabelField(match, EditorStyles.wordWrappedMiniLabel);
        }
        var nextModel = (GameObject)EditorGUILayout.ObjectField("预览模型", model, typeof(GameObject), false);
        if (nextModel != model) { StopPreview(); model = nextModel; }
        using (new EditorGUI.DisabledScope(selected == null || model == null || EditorApplication.isPlayingOrWillChangePlaymode))
        {
            if (GUILayout.Button(preview ? "结束选片预览" : "预览所选动画"))
            { if (preview) StopPreview(); else StartPreview(); }
        }
        if (preview)
        {
            playing = GUILayout.Toggle(playing, "播放", "Button");
            frame = EditorGUILayout.IntSlider("预览帧", frame, 0, Mathf.Max(0, draft.TotalFrames - 1));
            viewport.Draw(GUILayoutUtility.GetRect(0, 220, GUILayout.ExpandWidth(true)), false, null, null);
        }
        return sequence.ToArray();
    }

    /// <summary>保留原生 ObjectField 外观和拖放；已配置动作库时，小圆圈直接打开目录限定选择器。</summary>
    public static AnimationClip DrawClipField(Rect rect, GUIContent label, AnimationClip value,
        Object context, Action<AnimationClip> onSelected, Action openPicker = null)
    {
        var pickerRect = new Rect(rect.xMax - 19f, rect.y, 19f, rect.height);
        var e = Event.current;
        if (GUI.enabled && e.type == EventType.MouseDown && e.button == 0 && pickerRect.Contains(e.mousePosition)
            && !string.IsNullOrEmpty(CharacterAnimationSourcePreferences.Get(context, false)))
        {
            // 在 ObjectField 消费点击前替换其选片入口，其余拖放、定位、清空仍由 Unity 处理。
            e.Use();
            if (openPicker != null) openPicker(); else OpenPicker(context, onSelected);
        }
        return (AnimationClip)EditorGUI.ObjectField(rect, label, value, typeof(AnimationClip), false);
    }

    /// <summary>以角色动作库为固定范围打开 Unity 原生选择窗口；取消不写回。</summary>
    public static void OpenPicker(Object context, Action<AnimationClip> onSelected)
    {
        string path = CharacterAnimationSourcePreferences.Get(context, false);
        if (!AssetDatabase.IsValidFolder(path)) return;
        var provider = CreateProvider(path);
        var searchContext = SearchService.CreateContext(provider, "");
        var state = new SearchViewState(searchContext, (SearchItem item, bool cancelled) =>
        {
            if (!cancelled) onSelected(item?.data as AnimationClip);
        });
        state.title = "选择动作库动画";
        state.group = provider.id;
        state.hideAllGroup = true;
        SearchService.ShowPicker(state);
    }

    /// <summary>原生 Search 多选窗口；显式执行添加后才提交，按名称顺序初始化，可回到创建窗重排。</summary>
    public static void OpenSequencePicker(Object context, Action<AnimationClip[]> onSelected)
    {
        string path = CharacterAnimationSourcePreferences.Get(context, false);
        if (!AssetDatabase.IsValidFolder(path)) return;
        var provider = CreateProvider(path);
        ISearchView view = null;
        provider.actions.Add(new SearchAction(provider.id, "append", new GUIContent("添加所选动画"), (SearchItem[] items) =>
        {
            var clips = items.OrderBy(i => i.score).Select(i => i.data as AnimationClip).Where(c => c != null).ToArray();
            if (clips.Length == 0) return;
            onSelected(clips); view?.Close();
        }));
        var searchContext = SearchService.CreateContext(provider, "");
        searchContext.options |= SearchFlags.Multiselect;
        var state = new SearchViewState(searchContext) { title = "多选动画 · Enter 添加", group = provider.id, hideAllGroup = true };
        view = SearchService.ShowWindow(state);
        view.multiselect = true;
    }

    static SearchProvider CreateProvider(string path)
    {
        var provider = new SearchProvider("actgame-animation", "动作库")
        {
            filterId = "animation:",
            toObject = (item, type) => item.data as AnimationClip,
            fetchThumbnail = (item, ctx) => AssetPreview.GetMiniThumbnail(item.data as AnimationClip),
        };
        provider.fetchItems = (ctx, items, p) => FetchLibrary(path, ctx, p);
        return provider;
    }

    // Search 的异步枚举在资源之间让出执行权；首次打开先显示窗口，再加载 FBX 子动画。
    static System.Collections.IEnumerator FetchLibrary(string path, SearchContext context, SearchProvider provider)
    {
        if (!libraries.TryGetValue(path, out var entries))
        {
            yield return null;
            int version = libraryVersion;
            var found = new List<LibraryEntry>();
            var paths = AssetDatabase.FindAssets("t:AnimationClip", new[] { path })
                .Select(AssetDatabase.GUIDToAssetPath).Distinct();
            foreach (string assetPath in paths)
            {
                foreach (var clip in AssetDatabase.LoadAllAssetsAtPath(assetPath).OfType<AnimationClip>())
                {
                    if (clip.name.StartsWith("__preview__", StringComparison.Ordinal)) continue;
                    AssetDatabase.TryGetGUIDAndLocalFileIdentifier(clip, out string guid, out long localId);
                    found.Add(new LibraryEntry { Clip = clip, Name = clip.name, Path = assetPath, Id = guid + ":" + localId });
                }
                yield return null;
            }
            entries = found.OrderBy(e => e.Name, Comparer<string>.Create(EditorUtility.NaturalCompare))
                .ThenBy(e => e.Path, StringComparer.Ordinal).ThenBy(e => e.Id, StringComparer.Ordinal).ToArray();
            if (version == libraryVersion) libraries[path] = entries;
        }
        string query = context.searchQuery ?? "";
        for (int i = 0; i < entries.Length; i++)
        {
            var entry = entries[i];
            if (entry.Clip == null || (entry.Name.IndexOf(query, StringComparison.OrdinalIgnoreCase) < 0
                && entry.Path.IndexOf(query, StringComparison.OrdinalIgnoreCase) < 0)) continue;
            // Search 默认按 score/id 排序；显式名次保证可见顺序与动画名自然排序一致。
            yield return provider.CreateItem(context, entry.Id, i, entry.Name, entry.Path, null, entry.Clip);
        }
    }

    void DrawFolder(string label, bool rm, string path)
    {
        var value = AssetDatabase.LoadAssetAtPath<DefaultAsset>(path);
        EditorGUI.BeginChangeCheck();
        var next = (DefaultAsset)EditorGUILayout.ObjectField(label, value, typeof(DefaultAsset), false);
        if (EditorGUI.EndChangeCheck())
        { CharacterAnimationSourcePreferences.Set(scope, rm, AssetDatabase.GetAssetPath(next)); match = null; }
    }
    void StartPreview()
    {
        draft = ScriptableObject.CreateInstance<ActionDefinition>();
        draft.hideFlags = HideFlags.HideAndDontSave;
        using (var so = new SerializedObject(draft)) ActionAnimationSegmentCommands.Insert(so, 0, new[] { selected });
        PreviewOwner = owner;
        session.SetAction(draft);
        session.SetPreviewCharacter(viewport.Bind(model, Vector3.zero, Quaternion.identity));
        frame = 0; lastTime = EditorApplication.timeSinceStartup; preview = true;
    }

    void Update()
    {
        if (!preview) return;
        if (EditorApplication.isPlayingOrWillChangePlaymode || PreviewOwner != owner) { StopPreview(); return; }
        double now = EditorApplication.timeSinceStartup;
        if (playing) frame = (frame + (int)((now - lastTime) * ActionSim.LogicHz)) % Mathf.Max(1, draft.TotalFrames);
        if (!playing || now - lastTime >= 1d / ActionSim.LogicHz) lastTime = now;
        session.SetPreviewFrame(frame); session.Tick(); owner.Repaint();
    }

    void StopPreview()
    {
        if (!preview && draft == null) return;
        session.Dispose(); viewport.Dispose();
        if (draft != null) { Undo.ClearUndo(draft); Object.DestroyImmediate(draft); }
        draft = null; preview = playing = false;
        if (PreviewOwner == owner) PreviewOwner = null;
    }

    /// <summary>窗口关闭或重载时释放采样、模型及资源监听。</summary>
    public void Dispose()
    {
        StopPreview(); EditorApplication.projectChanged -= Invalidate; EditorApplication.update -= Update;
    }
}
