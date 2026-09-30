using System;
using System.Linq;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

/// <summary>隔离实例资源和音画扩展回归；测试仅创建独占临时目录。</summary>
public sealed class ActionEditorViewportTests
{
    string folder;
    GameObject prefab;
    ActionEditorPreviewViewport viewport;

    [SetUp] public void Setup()
    {
        folder = "Assets/__ActionEditorAlignmentTests_" + Guid.NewGuid().ToString("N");
        AssetDatabase.CreateFolder("Assets", folder.Substring("Assets/".Length));
        var root = new GameObject("AlignmentPreviewModel");
        root.AddComponent<Animator>();
        prefab = PrefabUtility.SaveAsPrefabAsset(root, folder + "/Model.prefab");
        Object.DestroyImmediate(root);
        viewport = new ActionEditorPreviewViewport();
    }

    [TearDown] public void Cleanup()
    {
        ActionEditorAnimationSampler.EndSession();
        viewport.Dispose();
        AssetDatabase.DeleteAsset(folder);
    }

    [Test] public void AdhesionPreviewMatchesSimulationAndResumesBaseOutsideWindow()
    {
        var action = ScriptableObject.CreateInstance<ActionDefinition>();
        var clip = new AnimationClip();
        clip.SetCurve("", typeof(Transform), "localPosition.z", AnimationCurve.Linear(0, 0, 1, 1));
        try
        {
            using (var so = new SerializedObject(action))
            {
                // TotalFrames 由有效动画段计算，不能直接写入后被 OnValidate 重算为 1。
                var segments = so.FindProperty("animationSegments");
                segments.arraySize = 1;
                var segment = segments.GetArrayElementAtIndex(0);
                segment.FindPropertyRelative("clip").objectReferenceValue = clip;
                segment.FindPropertyRelative("startFrame").intValue = 0;
                segment.FindPropertyRelative("endFrame").intValue = 10;
                so.FindProperty("executionPolicy.baseMotionMode").intValue = (int)ActionBaseMotionMode.BakedMotion;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            Assert.That(action.TotalFrames, Is.EqualTo(11));
            action.BakedMotion.CopyFrom(new ActionBakedMotion
            {
                frameCount = 11, bakeStatus = ActionBakedMotionStatus.Ok,
                positionDeltaMmX = new int[11], positionDeltaMmZ = Enumerable.Repeat(1000, 11).ToArray(),
                yawDeltaMilliDeg = new int[11]
            });
            var window = new ActionMotionAdhesionParams(0, 9, 1000, 500, 100000, 100000, 0);
            var state = new ActionMotionAdhesion.State();
            int x = 0, z = 0;
            for (int frame = 0; frame <= 10; frame++)
            {
                ActionMotionAdhesion.TryComputeDisplacementMm(ref state, x, z, 0, 0, 2000,
                    in window, frame, 0, 1000, ActionMotionAdhesion.BakedProgress(action.BakedMotion, frame, 9),
                    out int dx, out int dz);
                x += dx; z += dz;
                ActionMotionAdhesionSceneDrawing.SimulateThroughFrame(action, in window,
                    Vector3.zero, Quaternion.identity, new Vector3(0, 0, 2), 0, frame, null,
                    out var preview, out var desired, out _);
                Assert.That(preview.x, Is.EqualTo(x / 1000f).Within(.001));
                Assert.That(preview.z, Is.EqualTo(z / 1000f).Within(.001));
                Assert.That(desired, Is.EqualTo(new Vector3(-.5f, 0, 3)));
            }
            Assert.That(z, Is.EqualTo(4000)); // 窗外第 10 帧恢复原本的 1m 位移。
        }
        finally { Object.DestroyImmediate(action); Object.DestroyImmediate(clip); }
    }

    [Test] public void AnimationSourcesKeepRolesSeparateAndFollowFolderMoves()
    {
        var a = ScriptableObject.CreateInstance<CharacterConfig>();
        var b = ScriptableObject.CreateInstance<CharacterConfig>();
        AssetDatabase.CreateAsset(a, folder + "/A.asset");
        AssetDatabase.CreateAsset(b, folder + "/B.asset");
        AssetDatabase.CreateFolder(folder, "Clips");
        string key = "ACTGame.Authoring.RM." + Application.dataPath + "." + AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(a));
        EditorPrefs.SetString(key, folder + "/Clips");
        Assert.That(CharacterAnimationSourcePreferences.Get(a, true), Is.EqualTo(folder + "/Clips"));
        Assert.That(EditorPrefs.HasKey(key), Is.False);
        CharacterAnimationSourcePreferences.Set(a, false, folder + "/Clips");
        Assert.That(CharacterAnimationSourcePreferences.Get(b, false), Is.Empty);
        Assert.That(AssetDatabase.MoveAsset(folder + "/Clips", folder + "/Moved"), Is.Empty);
        Assert.That(CharacterAnimationSourcePreferences.Get(a, false), Is.EqualTo(folder + "/Moved"));
        Assert.That(CharacterAnimationSourcePreferences.Get(a, true), Is.EqualTo(folder + "/Moved"));
        AssetDatabase.DeleteAsset(folder + "/Moved");
        Assert.That(CharacterAnimationSourcePreferences.Get(a, true), Is.Empty);
    }

    [Test] public void NativePickerSortsNamesNaturallyAndReusesCompletedLibraryScan()
    {
        AssetDatabase.CreateFolder(folder, "Library");
        foreach (string name in new[] { "Attack_10", "Attack_2", "Attack_1" })
            AssetDatabase.CreateAsset(new AnimationClip { name = name }, folder + "/Library/" + name + ".anim");
        var provider = new UnityEditor.Search.SearchProvider("test-library", "Library");
        using var context = UnityEditor.Search.SearchService.CreateContext(provider, "");
        var fetch = typeof(ActionAnimationPickerPanel).GetMethod("FetchLibrary", BindingFlags.Static | BindingFlags.NonPublic);
        System.Collections.Generic.List<UnityEditor.Search.SearchItem> Read(out int pauses)
        {
            var iterator = (IEnumerator)fetch.Invoke(null, new object[] { folder + "/Library", context, provider });
            var result = new System.Collections.Generic.List<UnityEditor.Search.SearchItem>();
            pauses = 0;
            while (iterator.MoveNext())
                if (iterator.Current is UnityEditor.Search.SearchItem item) result.Add(item);
                else pauses++;
            return result;
        }
        var cold = Read(out int coldPauses);
        var warm = Read(out int warmPauses);
        Assert.That(coldPauses, Is.GreaterThan(0), "冷加载必须给 Search 留出更新窗口的机会");
        Assert.That(warmPauses, Is.Zero, "重复打开应直接复用完整目录缓存");
        Assert.That(cold.Select(i => ((AnimationClip)i.data).name), Is.EqualTo(new[] { "Attack_1", "Attack_2", "Attack_10" }));
        Assert.That(cold.Select(i => i.score), Is.Ordered.Ascending);
        Assert.That(warm.Select(i => i.id), Is.EqualTo(cold.Select(i => i.id)));
    }

    [Test] public void BatchAnimationLibraryUsesCurrentRoleIncludesChildrenAndDeduplicates()
    {
        AssetDatabase.CreateFolder(folder, "Library");
        AssetDatabase.CreateFolder(folder + "/Library", "Child");
        var config = ScriptableObject.CreateInstance<CharacterConfig>();
        AssetDatabase.CreateAsset(config, folder + "/Config.asset");
        var inside = new AnimationClip { name = "Same" };
        var outside = new AnimationClip { name = "Same" };
        AssetDatabase.CreateAsset(inside, folder + "/Library/Child/In.anim");
        AssetDatabase.CreateAsset(outside, folder + "/Out.anim");
        CharacterAnimationSourcePreferences.Set(config, false, folder + "/Library");
        var window = ScriptableObject.CreateInstance<CharacterActionBatchWindow>();
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        try
        {
            typeof(CharacterActionBatchWindow).GetField("config", flags).SetValue(window, config);
            window.AppendFromLibrary(); window.AppendFromLibrary();
            var clips = (AnimationClip[])typeof(CharacterActionBatchWindow).GetField("clips", flags).GetValue(window);
            Assert.That(clips, Is.EqualTo(new[] { inside }));
            CharacterAnimationSourcePreferences.Set(config, false, "");
            window.AppendFromLibrary();
            Assert.That(typeof(CharacterActionBatchWindow).GetField("clips", flags).GetValue(window), Is.EqualTo(clips));
        }
        finally { Object.DestroyImmediate(window); }
    }

    [Test] public void AnimationPickerRetainsSameNamedSubAssetIdentityAndCleansPreview()
    {
        var clip = new AnimationClip { name = "Same", legacy = true };
        clip.SetCurve("", typeof(Transform), "localPosition.z", AnimationCurve.Linear(0, 0, 1, 1));
        AssetDatabase.CreateAsset(clip, folder + "/Clip.anim");
        var second = Object.Instantiate(clip); second.name = "Same";
        AssetDatabase.AddObjectToAsset(second, clip);
        var found = ActionAnimationPickerPanel.Collect(folder + "/Clip.anim");
        Assert.That(found.Count, Is.EqualTo(2));
        Assert.That(found, Does.Contain(clip)); Assert.That(found, Does.Contain(second));
        var window = ScriptableObject.CreateInstance<ActionDefinitionCreateWindow>();
        var panel = new ActionAnimationPickerPanel(window);
        int scenes = EditorSceneManager.previewSceneCount;
        try
        {
            panel.Bind(null, prefab);
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            typeof(ActionAnimationPickerPanel).GetField("selected", flags).SetValue(panel, clip);
            typeof(ActionAnimationPickerPanel).GetMethod("StartPreview", flags).Invoke(panel, null);
            typeof(ActionAnimationPickerPanel).GetMethod("Update", flags).Invoke(panel, null);
            Assert.That(ActionAnimationPickerPanel.PreviewOwner, Is.SameAs(window));
            Assert.That(EditorSceneManager.previewSceneCount, Is.EqualTo(scenes + 1));
        }
        finally { panel.Dispose(); Object.DestroyImmediate(window); }
        Assert.That(ActionAnimationPickerPanel.PreviewOwner, Is.Null);
        Assert.That(EditorSceneManager.previewSceneCount, Is.EqualTo(scenes));
        Assert.That(ActionEditorAnimationSampler.IsSessionActive, Is.False);
    }

    [Test] public void BindReusesOnlyItsIsolatedInstanceAndDisposesIt()
    {
        int scenes = EditorSceneManager.previewSceneCount;
        var first = viewport.Bind(prefab, Vector3.zero, Quaternion.identity);
        Assert.That(EditorSceneManager.IsPreviewSceneObject(first.gameObject), Is.True);
        Assert.That(viewport.Bind(prefab, Vector3.zero, Quaternion.identity), Is.SameAs(first));
        Assert.That(EditorSceneManager.previewSceneCount, Is.EqualTo(scenes + 1));
        Assert.That(viewport.Bind(prefab, Vector3.right, Quaternion.identity).position, Is.EqualTo(Vector3.right));
        viewport.Dispose();
        Assert.That(first == null, Is.True);
        Assert.That(EditorSceneManager.previewSceneCount, Is.EqualTo(scenes));
        Assert.That(prefab != null, Is.True);
    }

    [Test] public void SceneObjectIsRejectedWithoutMovingOrDestroyingIt()
    {
        var sceneModel = new GameObject("SceneModel");
        try
        {
            var scene = sceneModel.scene;
            Assert.Throws<ArgumentException>(() => viewport.Bind(sceneModel, Vector3.zero, Quaternion.identity));
            Assert.That(sceneModel.scene, Is.EqualTo(scene));
            Assert.That(sceneModel != null, Is.True);
        }
        finally { Object.DestroyImmediate(sceneModel); }
    }

    [UnityTest] public IEnumerator RepaintingPreviewPreservesHandlesAndPointEventsCanBeDragged()
    {
        viewport.Bind(prefab, Vector3.zero, Quaternion.identity);
        var action = ScriptableObject.CreateInstance<ActionDefinition>();
        using var so = new SerializedObject(action);
        var clip = new AnimationClip { legacy = true };
        clip.SetCurve("", typeof(Transform), "localPosition.x", AnimationCurve.Linear(0, 0, 1, 1));
        ActionAnimationSegmentCommands.Insert(so, 0, new[] { clip });
        ActionTimelineCommands.AddWindow(so, ActionTimelineTrackKind.Vfx, "VFX", 10, 60, 60);
        ActionTimelineCommands.AddWindow(so, ActionTimelineTrackKind.Sfx, "SFX", 20, 60, 60);
        ActionTimelineCommands.EnsureTracksFromWindows(so);
        var timeline = new ActionTimelineView();
        var selection = new ActionEditorSelectionSet();
        int frame = 0, repaints = 0;
        Exception error = null;
        var window = ScriptableObject.CreateInstance<EditorWindow>();
        var oldMatrix = Handles.matrix;
        var oldColor = Handles.color;
        var oldZTest = Handles.zTest;
        var expectedMatrix = Matrix4x4.Translate(new Vector3(2, 3, 4));
        try
        {
            window.position = new Rect(100, 100, 800, 520);
            window.rootVisualElement.Add(new IMGUIContainer(() =>
            {
                if (error != null) return;
                var previousCamera = Camera.current;
                var previousTarget = RenderTexture.active;
                Handles.matrix = expectedMatrix;
                Handles.color = Color.magenta;
                Handles.zTest = UnityEngine.Rendering.CompareFunction.LessEqual;
                try
                {
                    viewport.Draw(new Rect(0, 0, 800, 220), false, null, camera =>
                    {
                        Assert.That(Handles.matrix, Is.EqualTo(Matrix4x4.identity));
                        Assert.That(RenderTexture.active, Is.SameAs(camera.targetTexture));
                        Handles.DrawAAPolyLine(3, Vector3.zero, Vector3.forward);
                    });
                    Assert.That(Handles.matrix, Is.EqualTo(expectedMatrix), "视口不能清零其他画布的矩阵");
                    Assert.That(Handles.color, Is.EqualTo(Color.magenta));
                    Assert.That(Handles.zTest, Is.EqualTo(UnityEngine.Rendering.CompareFunction.LessEqual));
                    Assert.That(Camera.current, Is.SameAs(previousCamera));
                    Assert.That(RenderTexture.active, Is.SameAs(previousTarget));
                }
                catch (Exception e) { error = e; }
                finally { Handles.matrix = oldMatrix; Handles.color = oldColor; Handles.zTest = oldZTest; }
            }) { style = { height = 220 } });
            window.rootVisualElement.Add(new IMGUIContainer(() =>
            {
                if (error != null) return;
                bool repaint = Event.current.type == EventType.Repaint;
                try
                {
                    so.Update();
                    if (timeline.Draw(new Rect(0, 0, 520, 300), so, action, selection, ref frame, null))
                        so.ApplyModifiedProperties();
                    if (repaint) repaints++;
                }
                catch (Exception e) { error = e; }
            }) { style = { height = 300 } });
            window.ShowUtility();
            for (int i = 0; i < 60 && repaints < 3 && error == null; i++) { window.Repaint(); yield return null; }
            Assert.That(error, Is.Null);
            Assert.That(repaints, Is.GreaterThanOrEqualTo(3));
            // 两条通知轨位于 Animation 下方；坐标为时间轴画布内的 GUI 点。
            const float ppf = (520f - ActionEditorStyles.TrackHeaderWidth) / 60;
            foreach (var item in new[] { (ActionTimelineTrackKind.Vfx, 10, 92f), (ActionTimelineTrackKind.Sfx, 20, 122f) })
            {
                Vector2 start = new(ActionEditorStyles.TrackHeaderWidth + (item.Item2 + .25f) * ppf, item.Item3 + 220);
                // 制造 1 帧内的吸附候选，验证单击不会意外改写触发帧。
                frame = item.Item2 + 1;
                window.SendEvent(new Event { type = EventType.MouseDown, button = 0, mousePosition = start });
                window.SendEvent(new Event { type = EventType.MouseUp, button = 0, mousePosition = start });
                Assert.That(selection.Primary.Kind, Is.EqualTo(item.Item1));
                Assert.That(selection.Primary.ElementProperty.FindPropertyRelative("startFrame").intValue,
                    Is.EqualTo(item.Item2), "单击选中不应触发磁吸写回");
                foreach (var evt in new[]
                {
                    new Event { type = EventType.MouseDown, button = 0, mousePosition = start, modifiers = EventModifiers.Alt },
                    new Event { type = EventType.MouseDrag, button = 0, mousePosition = start + Vector2.right * ppf * 5, modifiers = EventModifiers.Alt },
                    new Event { type = EventType.MouseUp, button = 0, mousePosition = start + Vector2.right * ppf * 5, modifiers = EventModifiers.Alt },
                })
                {
                    window.SendEvent(evt);
                    window.Repaint(); yield return null;
                    Assert.That(error, Is.Null);
                }
                Assert.That(selection.Primary.Kind, Is.EqualTo(item.Item1));
                var element = selection.Primary.ElementProperty;
                Assert.That(element.FindPropertyRelative("startFrame").intValue, Is.EqualTo(item.Item2 + 5));
                Assert.That(element.FindPropertyRelative("endFrame").intValue, Is.EqualTo(item.Item2 + 5));
            }
            // 区间窗右边缘：磁吸线必须与绘制热区的 xMax 重合，包括动作末尾和最短一帧。
            ActionTimelineCommands.AddTrack(so, ActionTimelineTrackKind.Hitbox);
            var tracks = so.FindProperty("timeline.tracks");
            string trackName = tracks.GetArrayElementAtIndex(tracks.arraySize - 1).FindPropertyRelative("trackName").stringValue;
            var hitbox = ActionTimelineCommands.AddWindow(so, ActionTimelineTrackKind.Hitbox, trackName, 10, 60, 60);
            int initialEnd = hitbox.ElementProperty.FindPropertyRelative("endFrame").intValue;
            Vector2 rightEdge = new(ActionEditorStyles.TrackHeaderWidth + (initialEnd + 1) * ppf - 1, 152 + 220);
            window.SendEvent(new Event { type = EventType.MouseDown, button = 0, mousePosition = rightEdge });
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            foreach (int desiredEnd in new[] { initialEnd + 5, 59, 0 })
            {
                frame = desiredEnd;
                window.SendEvent(new Event { type = EventType.MouseDrag, button = 0,
                    mousePosition = rightEdge + Vector2.right * (desiredEnd - initialEnd) * ppf });
                int beforeRepaint = repaints;
                for (int i = 0; i < 60 && repaints == beforeRepaint && error == null; i++) { window.Repaint(); yield return null; }
                Assert.That(error, Is.Null);
                Assert.That(repaints, Is.GreaterThan(beforeRepaint));
                Assert.That(hitbox.ElementProperty.FindPropertyRelative("endFrame").intValue, Is.EqualTo(Mathf.Max(10, desiredEnd)));
                var cache = (IEnumerable)typeof(ActionTimelineView).GetField("_windowHitCache", flags).GetValue(timeline);
                Rect actualRect = default;
                foreach (object entry in cache)
                {
                    var entrySelection = (ActionEditorSelection)entry.GetType().GetField("Selection").GetValue(entry);
                    if (entrySelection.Kind == ActionTimelineTrackKind.Hitbox)
                        actualRect = (Rect)entry.GetType().GetField("HitRect").GetValue(entry);
                }
                float guideX = (float)typeof(ActionTimelineView).GetMethod("GetSnapGuideX", flags).Invoke(timeline, null);
                Assert.That(actualRect.width, Is.GreaterThan(0));
                Assert.That(guideX, Is.EqualTo(actualRect.xMax).Within(.01f), "吸附线应落在条块实际右边缘");
            }
            window.SendEvent(new Event { type = EventType.MouseUp, button = 0, mousePosition = rightEdge });
        }
        finally
        {
            window.Close();
            Handles.matrix = oldMatrix; Handles.color = oldColor; Handles.zTest = oldZTest;
            Undo.ClearUndo(action);
            Object.DestroyImmediate(action);
            Object.DestroyImmediate(clip);
        }
    }

    [Test] public void BakePanelWritesCurrentActionPreservesTableOnFailureAndSupportsUndo()
    {
        var inplace = new AnimationClip { legacy = true, name = "BakeProbe_Inplace" };
        inplace.SetCurve("", typeof(Transform), "localPosition.z", AnimationCurve.Linear(0, 0, 1, 0));
        AssetDatabase.CreateAsset(inplace, folder + "/BakeProbe_Inplace.anim");
        var rm = new AnimationClip { legacy = true, name = "BakeProbe" };
        rm.SetCurve("", typeof(Transform), "localPosition.z", AnimationCurve.Linear(0, 0, 1, 2));
        AssetDatabase.CreateAsset(rm, folder + "/BakeProbe.anim");
        var action = ScriptableObject.CreateInstance<ActionDefinition>();
        AssetDatabase.CreateAsset(action, folder + "/Action.asset");
        using var so = new SerializedObject(action);
        ActionAnimationSegmentCommands.Insert(so, 0, new[] { inplace });
        var panel = new ActionMotionBakePanel();
        panel.Bind(action);
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        typeof(ActionMotionBakePanel).GetField("folder", flags).SetValue(panel, AssetDatabase.LoadAssetAtPath<DefaultAsset>(folder));
        typeof(ActionMotionBakePanel).GetField("mode", flags).SetValue(panel, ActionMotionPlanarMode.FullPlanar);
        bool committed = false;
        Undo.IncrementCurrentGroup();
        Assert.That(panel.Bake(() => committed = true), Is.True);
        Assert.That(committed, Is.True);
        Assert.That(action.BakedMotion.IsReady, Is.True);
        Assert.That(action.BakedMotion.frameCount, Is.EqualTo(action.TotalFrames));
        // 每帧独立量化到毫米，累计误差上限为每帧半毫米。
        Assert.That(action.BakedMotion.positionDeltaMmZ.Sum(), Is.EqualTo(2000).Within(action.TotalFrames * .5f));
        Assert.That(ActionMotionDirtyUtility.IsDirty(action, folder, ActionSim.LogicHz), Is.False);
        var reopened = new ActionMotionBakePanel(); reopened.Bind(action);
        Assert.That(typeof(ActionMotionBakePanel).GetField("mode", flags).GetValue(reopened), Is.EqualTo(ActionMotionPlanarMode.FullPlanar));
        string baked = EditorJsonUtility.ToJson(action);
        AssetDatabase.CreateFolder(folder, "EmptyRM");
        typeof(ActionMotionBakePanel).GetField("folder", flags).SetValue(panel,
            AssetDatabase.LoadAssetAtPath<DefaultAsset>(folder + "/EmptyRM"));
        Assert.That(panel.Bake(), Is.False);
        Assert.That(EditorJsonUtility.ToJson(action), Is.EqualTo(baked), "失败不得覆盖已有烘焙");
        Undo.FlushUndoRecordObjects();
        Undo.PerformUndo();
        Assert.That(action.BakedMotion.IsReady, Is.False);
        Undo.ClearUndo(action);
    }

    [Test] public void PreviewLightingFollowsCameraAndSuppliesAmbientFill()
    {
        viewport.Bind(prefab, Vector3.zero, Quaternion.identity);
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var render = (PreviewRenderUtility)typeof(ActionEditorPreviewViewport).GetField("render", flags).GetValue(viewport);
        var configure = typeof(ActionEditorPreviewViewport).GetMethod("ConfigureLighting", flags);
        foreach (float yaw in new[] { 0f, 90f, 155f, 270f })
        {
            var rotation = Quaternion.Euler(10, yaw, 0);
            configure.Invoke(viewport, new object[] { rotation });
            foreach (var light in render.lights)
            {
                Assert.That(Vector3.Dot(rotation * Vector3.forward, light.transform.forward), Is.GreaterThan(.6f));
                Assert.That(light.color.maxColorComponent * light.intensity, Is.GreaterThan(.5f));
            }
            Assert.That(render.ambientColor.grayscale, Is.GreaterThan(.3f));
        }
    }

    [Test] public void SameSessionSamplesSceneAndIsolationEquallyAndRestoresAnimator()
    {
        var controller = UnityEditor.Animations.AnimatorController.CreateAnimatorControllerAtPath(folder + "/Test.controller");
        var isolated = viewport.Bind(prefab, Vector3.zero, Quaternion.identity);
        isolated.GetComponent<Animator>().runtimeAnimatorController = controller;
        var sceneModel = new GameObject("SceneParityModel");
        var animator = sceneModel.AddComponent<Animator>(); animator.runtimeAnimatorController = controller;
        var action = ScriptableObject.CreateInstance<ActionDefinition>();
        var clip = new AnimationClip();
        AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(Transform), "m_LocalPosition.x"), AnimationCurve.Linear(0, 0, 1, 3));
        using var so = new SerializedObject(action);
        ActionAnimationSegmentCommands.Insert(so, 0, new[] { clip });
        var session = new ActionEditorPreviewSession(action);
        try
        {
            session.SetAction(action); session.SetPreviewCharacter(sceneModel.transform); session.SetPreviewFrame(30); session.Tick();
            float sceneX = sceneModel.transform.localPosition.x;
            Assert.That(animator.enabled, Is.False);
            session.SetPreviewCharacter(isolated);
            Assert.That(animator.enabled, Is.True);
            Assert.That(sceneModel.transform.localPosition.x, Is.EqualTo(0).Within(.001));
            session.Tick();
            Assert.That(isolated.localPosition.x, Is.EqualTo(sceneX).Within(.001));
            var watch = System.Diagnostics.Stopwatch.StartNew();
            for (int i = 0; i < 600; i++) { session.SetPreviewFrame(i % 60); session.Tick(); }
            watch.Stop();
            TestContext.WriteLine($"600 preview ticks: {watch.Elapsed.TotalMilliseconds:0.0} ms ({watch.Elapsed.TotalMilliseconds / 600:0.000} ms/tick), scene x={sceneX}");
            session.SetAction(null);
            Assert.That(ActionEditorAnimationSampler.IsSessionActive, Is.False);
            Assert.That(isolated.GetComponent<Animator>().enabled, Is.True);
        }
        finally
        {
            session.Dispose(); Object.DestroyImmediate(sceneModel); Object.DestroyImmediate(action); Object.DestroyImmediate(clip);
        }
    }

    [Test] public void WorldVfxStaysInPreviewSceneAndTenScrubLoopsLeaveNoInstances()
    {
        var root = new GameObject("AlignmentVfx"); root.AddComponent<ParticleSystem>();
        var vfxPrefab = PrefabUtility.SaveAsPrefabAsset(root, folder + "/Vfx.prefab");
        Object.DestroyImmediate(root);
        var action = ScriptableObject.CreateInstance<ActionDefinition>();
        var clip = new AnimationClip { legacy = true };
        clip.SetCurve("", typeof(Transform), "localPosition.x", AnimationCurve.Linear(0, 0, 1, 1));
        var so = new SerializedObject(action);
        ActionAnimationSegmentCommands.Insert(so, 0, new[] { clip });
        var item = ActionTimelineCommands.AddWindow(so, ActionTimelineTrackKind.Vfx, "Vfx", 5, 60, 60).ElementProperty;
        item.FindPropertyRelative("prefab").objectReferenceValue = vfxPrefab;
        item.FindPropertyRelative("parentToAttachPoint").boolValue = false;
        so.ApplyModifiedProperties();
        var target = viewport.Bind(prefab, Vector3.zero, Quaternion.identity);
        var extension = new ActionEditorVfxPreviewExtension();
        extension.Bind(() => so.FindProperty("timeline.playVfxNotifies"));
        int baseline = PreviewEffects().Length;
        try
        {
            for (int i = 0; i < 10; i++)
            {
                var context = new ActionEditorPreviewContext(action, target, target, 20);
                extension.OnPreviewUpdate(context);
                var effects = PreviewEffects();
                Assert.That(effects.Length, Is.EqualTo(baseline + 1));
                Assert.That(effects.Any(effect => effect.scene == target.gameObject.scene), Is.True);
                context = new ActionEditorPreviewContext(action, target, target, 0);
                extension.OnPreviewUpdate(context);
                Assert.That(PreviewEffects().Length, Is.EqualTo(baseline));
            }
        }
        finally
        {
            extension.SetEnabled(false);
            so.Dispose(); Object.DestroyImmediate(action); Object.DestroyImmediate(clip);
        }
        Assert.That(PreviewEffects().Length, Is.EqualTo(baseline));
    }

    static GameObject[] PreviewEffects() => Resources.FindObjectsOfTypeAll<GameObject>()
        .Where(go => go.name.StartsWith("[VFX Preview") && !EditorUtility.IsPersistent(go)).ToArray();
}
