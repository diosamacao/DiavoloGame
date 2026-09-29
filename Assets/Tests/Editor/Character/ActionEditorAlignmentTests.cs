using System;
using System.Collections.Generic;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

/// <summary>保护新工作区的原子帧编辑、磁吸、全轨道与只读选择契约。</summary>
public sealed class ActionEditorAlignmentTests
{
    ActionDefinition action;
    AnimationClip clip;
    SerializedObject so;

    [SetUp] public void Setup()
    {
        action = ScriptableObject.CreateInstance<ActionDefinition>();
        clip = new AnimationClip { legacy = true, name = "TestClip" };
        clip.SetCurve("", typeof(Transform), "localPosition.x", AnimationCurve.Linear(0, 0, 1, 1));
        so = new SerializedObject(action);
        ActionAnimationSegmentCommands.Insert(so, 0, new[] { clip });
    }

    [TearDown] public void Cleanup()
    {
        Undo.ClearUndo(action);
        so.Dispose(); Object.DestroyImmediate(action); Object.DestroyImmediate(clip);
    }

    [TestCase(1f, 7, 10)]
    [TestCase(4f, 7, 7)]
    [TestCase(2f, 6, 10)]
    public void MagnetUsesEightScreenPixels(float ppf, int input, int expected) =>
        Assert.That(ActionTimelineSnapping.Resolve(input, ppf, 59, new[] { (10, 1) }), Is.EqualTo(expected));

    [Test] public void TieUsesPriorityThenSmallerFrame()
    {
        Assert.That(ActionTimelineSnapping.Resolve(10, 1, 59, new[] { (8, 2), (12, 0) }), Is.EqualTo(12));
        Assert.That(ActionTimelineSnapping.Resolve(10, 1, 59, new[] { (12, 1), (8, 1) }), Is.EqualTo(8));
        Assert.That(ActionTimelineSnapping.Resolve(10, 1, 59, new[] { (11, 2), (12, 0) }), Is.EqualTo(11));
    }

    [Test] public void AltAndSingleFrameStillRespectBounds()
    {
        Assert.That(ActionTimelineSnapping.Resolve(8, 1, 59, new[] { (10, 0) }, true), Is.EqualTo(8));
        Assert.That(ActionTimelineSnapping.Resolve(-5, 1, 0, new[] { (-2, 0), (9, 0) }), Is.Zero);
        Assert.That(ActionTimelineSnapping.Resolve(80, 1, 59, new[] { (80, 0) }), Is.EqualTo(59));
    }

    [TestCase(-100, -10)] [TestCase(100, 19)] [TestCase(5, 5)]
    public void GroupClampKeepsSpacing(int delta, int expected) =>
        Assert.That(ActionTimelineSnapping.ClampGroupDelta(delta, 10, 40, 60), Is.EqualTo(expected));

    [Test] public void CandidatesExcludeSelectedWindows()
    {
        var selected = ActionTimelineCommands.AddWindow(so, ActionTimelineTrackKind.Hitbox, "Hitbox", 20, 60, 60);
        var selection = new ActionEditorSelectionSet(); selection.Set(selected);
        var candidates = new List<(int frame, int priority)>();
        ActionTimelineSnapping.Collect(so, action, selection, 7, candidates);
        Assert.That(candidates.Exists(c => c.frame == 20), Is.False);
        Assert.That(candidates.Exists(c => c.frame == 7 && c.priority == 0), Is.True);
    }

    [Test] public void TrimEndMinusOneAndUndoRestoreSourceRange()
    {
        Undo.IncrementCurrentGroup();
        Assert.That(action.AnimationSegments[0].endFrame, Is.EqualTo(-1));
        Assert.That(ActionAnimationSegmentCommands.Trim(so, 0, 10, 29, out _), Is.True);
        Assert.That(action.TotalFrames, Is.EqualTo(20));
        Undo.FlushUndoRecordObjects(); Undo.PerformUndo(); so.Update();
        Assert.That(action.TotalFrames, Is.EqualTo(60));
        Assert.That(action.AnimationSegments[0].endFrame, Is.EqualTo(-1));
        Undo.PerformRedo(); so.Update();
        Assert.That(action.TotalFrames, Is.EqualTo(20));
    }

    [Test] public void RejectedTrimDoesNotPartiallyClampWindows()
    {
        ActionTimelineCommands.AddWindow(so, ActionTimelineTrackKind.Sfx, "Sfx", 55, 60, 60);
        string before = EditorJsonUtility.ToJson(action);
        Assert.That(ActionAnimationSegmentCommands.Trim(so, 0, 0, 20, out string error), Is.False);
        Assert.That(error, Does.Contain("Sfx[0]"));
        Assert.That(EditorJsonUtility.ToJson(action), Is.EqualTo(before));
    }

    [Test] public void InspectorChangeUsesSameAtomicGuard()
    {
        ActionTimelineCommands.AddWindow(so, ActionTimelineTrackKind.Hitbox, "Hitbox", 50, 60, 60);
        string before = EditorJsonUtility.ToJson(action);
        so.FindProperty("animationSegments").GetArrayElementAtIndex(0).FindPropertyRelative("endFrame").intValue = 10;
        Assert.That(ActionAnimationSegmentCommands.ApplyPending(so, out _), Is.False);
        Assert.That(EditorJsonUtility.ToJson(action), Is.EqualTo(before));
        Assert.That(so.hasModifiedProperties, Is.False);
    }

    [Test] public void ThreeClipsInsertInOrderWithSingleUndo()
    {
        var b = Object.Instantiate(clip); var c = Object.Instantiate(clip);
        try
        {
            Undo.IncrementCurrentGroup();
            ActionAnimationSegmentCommands.Insert(so, 0, new[] { c, b, clip });
            Assert.That(action.AnimationSegments[0].clip, Is.SameAs(c));
            Assert.That(action.AnimationSegments[1].clip, Is.SameAs(b));
            Assert.That(action.AnimationSegments[2].clip, Is.SameAs(clip));
            Assert.That(action.TotalFrames, Is.EqualTo(240));
            Undo.FlushUndoRecordObjects(); Undo.PerformUndo(); so.Update();
            Assert.That(action.AnimationSegments.Length, Is.EqualTo(1));
        }
        finally { Object.DestroyImmediate(b); Object.DestroyImmediate(c); }
    }

    [Test] public void AllTrackKindsKeepAddEditDeleteUndo()
    {
        foreach (ActionTimelineTrackKind kind in Enum.GetValues(typeof(ActionTimelineTrackKind)))
        {
            if (kind == ActionTimelineTrackKind.Animation) continue;
            ActionTimelineCommands.AddTrack(so, kind);
            var selection = ActionTimelineCommands.AddWindow(so, kind, kind.ToString(), 10, 60, 60);
            Assert.That(selection.IsValid, Is.True, kind.ToString());
            ActionTimelineCommands.MoveWindow(selection.ElementProperty, 2, action.TotalFrames);
            so.ApplyModifiedProperties();
            Assert.That(selection.ElementProperty.FindPropertyRelative("startFrame").intValue, Is.EqualTo(12));
            Undo.IncrementCurrentGroup();
            ActionTimelineCommands.RemoveWindow(so, selection);
            Undo.FlushUndoRecordObjects(); Undo.PerformUndo(); so.Update();
            Assert.That(so.FindProperty("timeline." + ActionTimelineCommands.GetArrayPropertyName(kind)).arraySize, Is.EqualTo(1), kind.ToString());
        }
    }

    [Test] public void SelectingActionDoesNotChangeSerializedContent()
    {
        var window = ScriptableObject.CreateInstance<ActionEditorWindow>();
        try
        {
            string before = EditorJsonUtility.ToJson(action);
            typeof(ActionEditorWindow).GetMethod("SelectAction", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(window, new object[] { action });
            Assert.That(EditorJsonUtility.ToJson(action), Is.EqualTo(before));
        }
        finally { Object.DestroyImmediate(window); }
    }

    [UnityTest] public IEnumerator WorkspaceFitsTwoTargetResolutions()
    {
        var window = ScriptableObject.CreateInstance<ActionEditorWindow>();
        try
        {
            window.ShowUtility();
            foreach (var size in new[] { new Vector2(1280, 720), new Vector2(1920, 1080) })
            {
                window.rootVisualElement.style.width = size.x;
                window.rootVisualElement.style.height = size.y;
                yield return null; yield return null;
                var splits = window.rootVisualElement.Query<TwoPaneSplitView>().ToList();
                Assert.That(splits.Count, Is.EqualTo(3));
                foreach (var split in splits)
                {
                    Assert.That(split.resolvedStyle.width, Is.GreaterThan(0));
                    Assert.That(split.resolvedStyle.height, Is.GreaterThan(0));
                }
                Assert.That(splits[0].resolvedStyle.width, Is.EqualTo(size.x).Within(1));
                Assert.That(splits[0].resolvedStyle.height, Is.LessThanOrEqualTo(size.y));
                TestContext.WriteLine($"Workspace layout {size.x}×{size.y}: three split panels fit root.");
            }
        }
        finally { window.Close(); }
    }

    [Test] public void SfxPreviewApiExistsOnSupportedUnity()
    {
        var field = typeof(ActionEditorSfxPreview).GetField("Play", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.That(field.GetValue(null), Is.Not.Null);
        using var preview = new ActionEditorSfxPreview();
        preview.Dispose(); // 未播放时不会停止其他音源。
    }

    [Test] public void ReturningToEditModeRestoresSerializedAction()
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var window = ScriptableObject.CreateInstance<ActionEditorWindow>();
        try
        {
            typeof(ActionEditorWindow).GetMethod("SelectAction", flags).Invoke(window, new object[] { action });
            var field = typeof(ActionEditorWindow).GetField("_serializedObject", flags);
            ((SerializedObject)field.GetValue(window)).Dispose();
            field.SetValue(window, null); // 域重载不序列化 SerializedObject。
            typeof(ActionEditorWindow).GetMethod("OnPlayModeChanged", flags).Invoke(window,
                new object[] { PlayModeStateChange.EnteredEditMode });
            var restored = (SerializedObject)field.GetValue(window);
            Assert.That(restored, Is.Not.Null);
            Assert.That(restored.targetObject, Is.SameAs(action));
        }
        finally { Object.DestroyImmediate(window); }
    }
}
