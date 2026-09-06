using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>弹刀卡肉帧数只认招架窗，不读进攻盒 Feedback。</summary>
public sealed class AssistParryHitStopTests
{
    /// <summary>空窗回退默认帧。</summary>
    [Test]
    public void ResolveFrames_NullWindow_UsesDefault()
    {
        Assert.That(
            AssistParryHitStop.ResolveFrames((AssistParryWindowNotifyState)null),
            Is.EqualTo(AssistParryHitStop.DefaultFrames));
    }

    /// <summary>窗上配置的帧原样返回；0 表示不冻。</summary>
    [Test]
    public void ResolveFrames_Window_UsesConfiguredFrames()
    {
        Assert.That(AssistParryHitStop.ResolveFrames(CreateWindow(5)), Is.EqualTo(5));
        Assert.That(AssistParryHitStop.ResolveFrames(CreateWindow(0)), Is.Zero);
    }

    /// <summary>招在该帧有窗时读窗；无窗回退默认。</summary>
    [Test]
    public void ResolveFrames_ActionAtFrame_ReadsActiveWindow()
    {
        ActionDefinition action = CreateActionWithWindow(hitStopFrames: 11);
        try
        {
            Assert.That(AssistParryHitStop.ResolveFrames(action, 0), Is.EqualTo(11));
            Assert.That(
                AssistParryHitStop.ResolveFrames((ActionDefinition)null, 0),
                Is.EqualTo(AssistParryHitStop.DefaultFrames));
        }
        finally
        {
            DestroyAction(action);
        }
    }

    static AssistParryWindowNotifyState CreateWindow(int frames)
    {
        var window = new AssistParryWindowNotifyState();
        FieldInfo field = typeof(AssistParryWindowNotifyState).GetField(
            "hitStopFrames",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null);
        field.SetValue(window, frames);
        return window;
    }

    static ActionDefinition CreateActionWithWindow(int hitStopFrames)
    {
        ActionDefinition action = ScriptableObject.CreateInstance<ActionDefinition>();
        AnimationClip clip = new AnimationClip { name = "ParryWindowClip", legacy = true };
        var so = new SerializedObject(action);
        so.FindProperty("sampleRate").intValue = ActionSim.LogicHz;
        so.FindProperty("totalFrames").intValue = 12;
        SerializedProperty segments = so.FindProperty("animationSegments");
        segments.arraySize = 1;
        segments.GetArrayElementAtIndex(0).FindPropertyRelative("clip").objectReferenceValue = clip;
        SerializedProperty windows = so.FindProperty("timeline").FindPropertyRelative("assistParryWindowStates");
        windows.arraySize = 1;
        SerializedProperty window = windows.GetArrayElementAtIndex(0);
        window.FindPropertyRelative("startFrame").intValue = 0;
        window.FindPropertyRelative("endFrame").intValue = 11;
        window.FindPropertyRelative("hitStopFrames").intValue = hitStopFrames;
        so.ApplyModifiedPropertiesWithoutUndo();
        return action;
    }

    static void DestroyAction(ActionDefinition action)
    {
        if (action == null)
            return;

        AnimationClip clip = action.HasAnimation ? action.AnimationSegments[0].clip : null;
        UnityEngine.Object.DestroyImmediate(action);
        if (clip != null)
            UnityEngine.Object.DestroyImmediate(clip);
    }
}
