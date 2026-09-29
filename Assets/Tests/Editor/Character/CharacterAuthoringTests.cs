using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>验证空角色创建和动作绑定的引用隔离、失败清理及身份约束。</summary>
public sealed class CharacterAuthoringCreationTests
{
    string folder;
    CharacterDefinition template;
    CharacterConfig config;
    CharacterLocomotionProfile locomotion;
    ActionGraph graph;
    ActionDefinition action;
    AnimationClip clip;

    [SetUp]
    public void Setup()
    {
        folder = "Assets/__CharacterAuthoringTests_" + Guid.NewGuid().ToString("N");
        AssetDatabase.CreateFolder("Assets", folder.Substring("Assets/".Length));
        template = Asset<CharacterDefinition>("Template");
        config = Asset<CharacterConfig>("Config");
        locomotion = Asset<CharacterLocomotionProfile>("Locomotion");
        graph = Asset<ActionGraph>("Graph");
        action = Asset<ActionDefinition>("Action");
        clip = new AnimationClip { name = "SharedClip", legacy = true };
        clip.SetCurve("", typeof(Transform), "localPosition.x", AnimationCurve.Linear(0, 0, 1, 1));
        AssetDatabase.CreateAsset(clip, folder + "/SharedClip.anim");
        Edit(template, so => { so.FindProperty("characterId").stringValue = "Owner_" + Guid.NewGuid().ToString("N"); so.FindProperty("characterConfig").objectReferenceValue = config; });
        Edit(config, so =>
        {
            SerializedProperty modes = so.FindProperty("combatModes.entries"); modes.arraySize = 1;
            modes.GetArrayElementAtIndex(0).FindPropertyRelative("actionGraph").objectReferenceValue = graph;
            modes.GetArrayElementAtIndex(0).FindPropertyRelative("locomotionProfile").objectReferenceValue = locomotion;
        });
        Edit(graph, so =>
        {
            SerializedProperty nodes = so.FindProperty("nodes"); nodes.arraySize = 1;
            SerializedProperty node = nodes.GetArrayElementAtIndex(0);
            node.FindPropertyRelative("nodeId").stringValue = "Attack";
            node.FindPropertyRelative("action").objectReferenceValue = action;
            node.FindPropertyRelative("isEntry").boolValue = true;
            node.FindPropertyRelative("intent").intValue = (int)GameplayIntentType.Attack;
        });
        Edit(action, so =>
        {
            SerializedProperty segments = so.FindProperty("animationSegments"); segments.arraySize = 1;
            segments.GetArrayElementAtIndex(0).FindPropertyRelative("clip").objectReferenceValue = clip;
            segments.GetArrayElementAtIndex(0).FindPropertyRelative("endFrame").intValue = -1;
        });
    }

    [TearDown]
    public void Cleanup() { if (folder != null) AssetDatabase.DeleteAsset(folder); }

    T Asset<T>(string name) where T : ScriptableObject
    {
        T value = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(value, folder + "/" + name + ".asset");
        return value;
    }
    static void Edit(Object value, Action<SerializedObject> edit)
    { var so = new SerializedObject(value); edit(so); so.ApplyModifiedPropertiesWithoutUndo(); }

    [Test]
    public void CreateCharacter_IsEmptyIndependentAndUsesPurposePaths()
    {
        Selection.activeObject = template;
        string id = "New_" + Guid.NewGuid().ToString("N");
        var created = CharacterAuthoringService.CreateCharacter(id, folder);
        var body = created.CharacterConfig;
        string root = folder + "/" + id;
        Assert.That(created.Id.Value, Is.EqualTo(id));
        Assert.That(body, Is.Not.SameAs(config));
        Assert.That(body.CombatModes.Entries.Count, Is.EqualTo(1));
        var mode = body.CombatModes.Entries[0];
        Assert.That(mode.ActionGraph.Nodes, Is.Empty);
        Assert.That(CharacterAuthoringService.CollectActions(body), Is.Empty);
        Assert.That(new SerializedObject(body).FindProperty("modelPrefab").objectReferenceValue, Is.Null);
        Assert.That(new SerializedObject(mode.LocomotionProfile).FindProperty("animationEntries").arraySize, Is.Zero);
        Assert.That(AssetDatabase.GetAssetPath(created), Is.EqualTo(root + "/" + id + "_Character.asset"));
        Assert.That(AssetDatabase.GetAssetPath(body), Is.EqualTo(root + "/Config/" + id + "_Config.asset"));
        Assert.That(AssetDatabase.GetAssetPath(mode.ActionGraph), Is.EqualTo(root + "/Graphs/" + id + "_Default_Graph.asset"));
        Assert.That(AssetDatabase.GetAssetPath(mode.LocomotionProfile), Is.EqualTo(root + "/Locomotion/" + id + "_Default_Locomotion.asset"));
        Assert.That(AssetDatabase.IsValidFolder(root + "/Actions"), Is.True);
        Assert.That(AssetDatabase.IsValidFolder(root + "/Reactions"), Is.True);
        Assert.That(AssetDatabase.FindAssets("t:ScriptableObject", new[] { root }).Length, Is.EqualTo(4));
        Assert.That(graph.Nodes.Count, Is.EqualTo(1));
        var attack = CharacterAuthoringService.CreateAction(body, 0, new[] { clip }, "Attack_01", CombatActionType.Attack, GameplayIntentType.Attack, true, false, default);
        Assert.That(AssetDatabase.GetAssetPath(attack), Is.EqualTo(root + "/Actions/" + id + "_Attack_01.asset"));
        var hit = CharacterAuthoringService.CreateAction(body, 0, new[] { clip }, id + "_Hit_01", CombatActionType.Hit, GameplayIntentType.None, false, true, CharacterReactionType.Hit);
        Assert.That(AssetDatabase.GetAssetPath(hit), Is.EqualTo(root + "/Reactions/" + id + "_Hit_01.asset"));
    }

    [Test]
    public void CreateCharacter_ExistingDirectoryIsNotDeleted()
    {
        AssetDatabase.CreateFolder(folder, "Existing");
        Assert.Throws<InvalidOperationException>(() => CharacterAuthoringService.CreateCharacter("Existing", folder));
        Assert.That(AssetDatabase.IsValidFolder(folder + "/Existing"), Is.True);
    }

    [Test]
    public void CreateCharacter_DuplicateIdentityRejectedAcrossFoldersAndCase()
    {
        string id = "Unique_" + Guid.NewGuid().ToString("N");
        CharacterAuthoringService.CreateCharacter(id, folder);
        Assert.Throws<InvalidOperationException>(() => CharacterAuthoringService.CreateCharacter(id.ToUpperInvariant(), folder + "/Other"));
        Assert.That(AssetDatabase.IsValidFolder(folder + "/Other"), Is.False);
    }

    [TestCase("../Escape")]
    [TestCase("Invalid/Name")]
    [TestCase("CON")]
    [TestCase("")]
    public void CreateCharacter_InvalidIdDoesNotCreateDirectories(string id)
    {
        Assert.Throws<ArgumentException>(() => CharacterAuthoringService.CreateCharacter(id, folder + "/NewParent"));
        Assert.That(AssetDatabase.IsValidFolder(folder + "/NewParent"), Is.False);
    }

    [Test]
    public void CreateCharacter_RejectsParentTraversalAndFileCollision()
    {
        Assert.Throws<ArgumentException>(() => CharacterAuthoringService.CreateCharacter("New", folder + "/../Outside"));
        Assert.Throws<System.IO.IOException>(() => CharacterAuthoringService.CreateCharacter("New", folder + "/Config.asset"));
        Assert.That(AssetDatabase.LoadAssetAtPath<CharacterConfig>(folder + "/Config.asset"), Is.SameAs(config));
    }

    [Test]
    public void CreateAction_MultipleClipsPreserveOrderAndBindOneAction()
    {
        var tail = UnityEngine.Object.Instantiate(clip);
        tail.name = "Tail";
        AssetDatabase.CreateAsset(tail, folder + "/Tail.anim");
        var clips = new[] { tail, clip, tail };
        var created = CharacterAuthoringService.CreateAction(config, 0, clips, "Sequence_" + Guid.NewGuid().ToString("N"),
            CombatActionType.Attack, GameplayIntentType.Attack, false, false, default);
        Assert.That(created.AnimationSegments.Select(s => s.clip), Is.EqualTo(clips));
        Assert.That(created.TotalFrames, Is.EqualTo(created.AnimationSegments.Sum(s => s.GetFrameCount(60))));
        Assert.That(graph.Nodes.Count, Is.EqualTo(2));
        Assert.That(graph.Nodes[1].Action, Is.SameAs(created));
        var standalone = ActionDefinitionCreateUtility.Create("StandaloneSequence", clips, folder);
        Assert.That(standalone.AnimationSegments.Select(s => s.clip), Is.EqualTo(clips));
        Assert.That(standalone.TotalFrames, Is.EqualTo(created.TotalFrames));
    }

    [Test]
    public void CreateAction_BindsNodeWithoutInheritingPreviousPolicy()
    {
        ActionDefinition created = CharacterAuthoringService.CreateAction(config, 0, new[] { clip }, "Action_" + Guid.NewGuid().ToString("N"),
            CombatActionType.Attack, GameplayIntentType.Attack, false, false, CharacterReactionType.Hit);
        Assert.That(graph.Nodes.Count, Is.EqualTo(2));
        Assert.That(graph.Nodes[1].Action, Is.SameAs(created));
        Assert.That(graph.Nodes[1].IsEntry, Is.False);
        Assert.That(graph.Nodes[1].AutomaticTransitions, Is.Empty);
        Assert.That(created.SampleRate, Is.EqualTo(60));
    }

    [Test]
    public void CreateReaction_DuplicateDefaultRollsBackNewAsset()
    {
        CharacterAuthoringService.CreateAction(config, 0, new[] { clip }, "Action_" + Guid.NewGuid().ToString("N"),
            CombatActionType.Hit, GameplayIntentType.None, false, true, CharacterReactionType.Hit);
        string name = "Hit_" + Guid.NewGuid().ToString("N");
        int count = CharacterAuthoringService.CollectActions(config).Count;
        Assert.Throws<InvalidOperationException>(() => CharacterAuthoringService.CreateAction(config, 0, new[] { clip }, name,
            CombatActionType.Hit, GameplayIntentType.None, false, true, CharacterReactionType.Hit));
        Assert.That(AssetDatabase.LoadAssetAtPath<ActionDefinition>(CharacterAssetLayout.ActionFolder(config, true) + "/" + CharacterAssetLayout.ActionName(config, name) + ".asset"), Is.Null);
        Assert.That(CharacterAuthoringService.CollectActions(config).Count, Is.EqualTo(count));
    }

    [Test]
    public void Batch_FailureRestoresGraphAndDeletesOnlyNewActions()
    {
        string prefix = "Attack_" + Guid.NewGuid().ToString("N");
        var existing = Asset<ActionDefinition>(CharacterAssetLayout.ActionName(config, prefix + "_02"));
        int before = graph.Nodes.Count;
        Assert.Throws<InvalidOperationException>(() => CharacterAuthoringService.CreateActions(config, 0,
            new[] { clip, clip }, prefix, CombatActionType.Attack, GameplayIntentType.Attack));
        Assert.That(graph.Nodes.Count, Is.EqualTo(before));
        Assert.That(AssetDatabase.IsValidFolder(folder + "/Actions"), Is.False);
        Assert.That(AssetDatabase.LoadAssetAtPath<ActionDefinition>(CharacterAssetLayout.ActionFolder(config, false) + "/" + CharacterAssetLayout.ActionName(config, prefix + "_01") + ".asset"), Is.Null);
        Assert.That(AssetDatabase.LoadAssetAtPath<ActionDefinition>(folder + "/" + CharacterAssetLayout.ActionName(config, prefix + "_02") + ".asset"), Is.SameAs(existing));
    }
}

/// <summary>覆盖动画映射实时编辑、派生时序与作者时序的边界。</summary>
public sealed class CharacterAuthoringTimingTests
{
    CharacterLocomotionProfile profile;
    AnimationClip clip;
    [SetUp]
    public void Setup()
    {
        profile = ScriptableObject.CreateInstance<CharacterLocomotionProfile>();
        clip = new AnimationClip { legacy = true };
        clip.SetCurve("", typeof(Transform), "localPosition.x", AnimationCurve.Linear(0, 0, 1, 1));
        var so = new SerializedObject(profile);
        SerializedProperty entries = so.FindProperty("animationEntries"); entries.arraySize = 1;
        entries.GetArrayElementAtIndex(0).FindPropertyRelative("Key").intValue = (int)AnimationKey.Run;
        entries.GetArrayElementAtIndex(0).FindPropertyRelative("Clip").objectReferenceValue = clip;
        so.ApplyModifiedPropertiesWithoutUndo();
    }
    [TearDown]
    public void Cleanup() { Object.DestroyImmediate(profile); Object.DestroyImmediate(clip); }
    [Test]
    public void Bake_PreservesAuthorExitAndHandoff()
    {
        profile.SetClipTimings(new[] { new LocomotionClipTiming(AnimationKey.Run, 60, false, 40, 23) });
        LocomotionTimingBaker.Bake(profile);
        LocomotionClipTiming timing = profile.RequireClipTiming(AnimationKey.Run);
        Assert.That(timing.ExitFrame, Is.EqualTo(40));
        Assert.That(timing.HandoffFrame, Is.EqualTo(23));
    }
    [Test]
    public void Bake_ShorterClipRejectsWithoutOverwritingAuthorTiming()
    {
        profile.SetClipTimings(new[] { new LocomotionClipTiming(AnimationKey.Run, 120, false, 90, 80) });
        Assert.Throws<InvalidOperationException>(() => LocomotionTimingBaker.Bake(profile));
        Assert.That(profile.RequireClipTiming(AnimationKey.Run).ExitFrame, Is.EqualTo(90));
    }
    [Test]
    public void MappingEditInvalidatesPreviouslyResolvedClipWithoutReload()
    {
        Assert.That(profile.GetClip(AnimationKey.Run), Is.SameAs(clip));
        var so = new SerializedObject(profile);
        so.FindProperty("animationEntries").GetArrayElementAtIndex(0).FindPropertyRelative("Clip").objectReferenceValue = null;
        so.ApplyModifiedPropertiesWithoutUndo();
        Assert.That(profile.GetClip(AnimationKey.Run), Is.Null);
    }

    [Test]
    public void Bake_DuplicateTimingDoesNotOverwriteAuthoredValues()
    {
        var timing = new LocomotionClipTiming(AnimationKey.Run, 60, false, 40, 23);
        profile.SetClipTimings(new[] { timing, timing });
        Assert.Throws<InvalidOperationException>(() => LocomotionTimingBaker.Bake(profile));
        Assert.That(profile.EditorClipTimings.Count, Is.EqualTo(2));
        Assert.That(profile.EditorClipTimings[0].HandoffFrame, Is.EqualTo(23));
    }

    [Test]
    public void ClipFingerprint_ChangesWhenCurveChangesAtSameDuration()
    {
        string before = RootMotionBakeUtility.ComputeClipContentHash(clip);
        clip.SetCurve("", typeof(Transform), "localPosition.x", AnimationCurve.Linear(0, 0, 1, 2));
        Assert.That(RootMotionBakeUtility.ComputeClipContentHash(clip), Is.Not.EqualTo(before));
    }
}

/// <summary>统一图校验拒绝不确定拓扑，同时允许终结动作和按 Id 请求的 AI 入口。</summary>
public sealed class ActionGraphValidatorTests
{
    ActionGraph graph;
    ActionDefinition action;
    [SetUp]
    public void Setup()
    {
        graph = ScriptableObject.CreateInstance<ActionGraph>();
        action = ScriptableObject.CreateInstance<ActionDefinition>();
        var so = new SerializedObject(graph);
        so.FindProperty("nodes").arraySize = 1;
        SerializedProperty node = so.FindProperty("nodes").GetArrayElementAtIndex(0);
        node.FindPropertyRelative("nodeId").stringValue = "A";
        node.FindPropertyRelative("action").objectReferenceValue = action;
        node.FindPropertyRelative("isEntry").boolValue = true;
        node.FindPropertyRelative("intent").intValue = (int)GameplayIntentType.Attack;
        so.ApplyModifiedPropertiesWithoutUndo();
    }
    [TearDown]
    public void Cleanup() { Object.DestroyImmediate(graph); Object.DestroyImmediate(action); }
    [Test]
    public void TerminalActionDoesNotRequireCancelWindow() => Assert.That(ActionGraphValidator.Validate(graph), Is.Empty);
    [TestCase(GameplayIntentType.None, false)]
    [TestCase(GameplayIntentType.Attack, true)]
    [TestCase(GameplayIntentType.Special, false)]
    public void MultipleEntries_OnlyConflictingInputIntentsAreRejected(GameplayIntentType intent, bool conflict)
    {
        SetTwoEntries(intent);
        var issues = ActionGraphValidator.Validate(graph);
        if (conflict) Assert.That(issues.Single().Code, Is.EqualTo("Entry.DuplicateIntent"));
        else Assert.That(issues, Is.Empty);
    }

    void SetTwoEntries(GameplayIntentType intent)
    {
        var so = new SerializedObject(graph);
        var nodes = so.FindProperty("nodes"); nodes.arraySize = 2;
        for (int i = 0; i < 2; i++)
        {
            var node = nodes.GetArrayElementAtIndex(i);
            node.FindPropertyRelative("nodeId").stringValue = i == 0 ? "A" : "B";
            node.FindPropertyRelative("intent").intValue = (int)intent;
            node.FindPropertyRelative("isEntry").boolValue = true;
            node.FindPropertyRelative("action").objectReferenceValue = action;
        }
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    [Test]
    public void NoneEntries_ResolveByIdButNeverByInputIntent()
    {
        SetTwoEntries(GameplayIntentType.None);
        var clip = new AnimationClip { legacy = true };
        try
        {
            clip.SetCurve("", typeof(Transform), "localPosition.x", AnimationCurve.Linear(0, 0, 1, 1));
            var so = new SerializedObject(action);
            var segments = so.FindProperty("animationSegments"); segments.arraySize = 1;
            segments.GetArrayElementAtIndex(0).FindPropertyRelative("clip").objectReferenceValue = clip;
            segments.GetArrayElementAtIndex(0).FindPropertyRelative("endFrame").intValue = -1;
            so.FindProperty("totalFrames").intValue = 60;
            so.ApplyModifiedPropertiesWithoutUndo();
            var context = default(ActionResolveContext);
            foreach (string id in new[] { "A", "B" })
            {
                Assert.That(graph.TryResolveEntry(id, in context, out var result), Is.True);
                Assert.That(result.NodeId, Is.EqualTo(id));
                Assert.That(result.Action, Is.SameAs(action));
            }
            var request = new ActionRequest(GameplayIntentType.Attack);
            Assert.That(graph.TryResolveStart(in request, in context, out _), Is.False);
            Assert.That(graph.TryResolveEntry("Missing", in context, out _), Is.False);
        }
        finally { Object.DestroyImmediate(clip); }
    }

    [Test]
    public void GraphWithoutEntryIsStillRejected()
    {
        var so = new SerializedObject(graph);
        so.FindProperty("nodes").GetArrayElementAtIndex(0).FindPropertyRelative("isEntry").boolValue = false;
        so.ApplyModifiedPropertiesWithoutUndo();
        Assert.That(ActionGraphValidator.Validate(graph).Any(i => i.Code == "Graph.NoEntry"), Is.True);
    }
    [Test]
    public void EdgeWithoutWindowHasStableLocation()
    {
        var so = new SerializedObject(graph);
        SerializedProperty edges = so.FindProperty("edges"); edges.arraySize = 1;
        edges.GetArrayElementAtIndex(0).FindPropertyRelative("fromNodeId").stringValue = "A";
        edges.GetArrayElementAtIndex(0).FindPropertyRelative("toNodeId").stringValue = "A";
        so.ApplyModifiedPropertiesWithoutUndo();
        Assert.That(ActionGraphValidator.Validate(graph).Any(i => i.Code == "Edge.MissingWindow" && i.NodeId == "A"), Is.True);
    }
    [Test]
    public void DuplicateNodeIdIsRejected()
    {
        var so = new SerializedObject(graph); so.FindProperty("nodes").arraySize = 2; so.ApplyModifiedPropertiesWithoutUndo();
        Assert.That(ActionGraphValidator.Validate(graph).Any(i => i.Code == "Node.InvalidId"), Is.True);
    }

    [Test]
    public void SpecialCancel_AllowsEnergyFormCandidates()
    {
        var so = new SerializedObject(graph);
        var nodes = so.FindProperty("nodes"); nodes.arraySize = 3;
        for (int i = 1; i < 3; i++)
        {
            var node = nodes.GetArrayElementAtIndex(i);
            node.FindPropertyRelative("nodeId").stringValue = "Special" + i;
            node.FindPropertyRelative("action").objectReferenceValue = action;
            node.FindPropertyRelative("intent").intValue = (int)GameplayIntentType.Special;
        }
        var edges = so.FindProperty("edges"); edges.arraySize = 2;
        for (int i = 0; i < 2; i++)
        {
            edges.GetArrayElementAtIndex(i).FindPropertyRelative("fromNodeId").stringValue = "A";
            edges.GetArrayElementAtIndex(i).FindPropertyRelative("toNodeId").stringValue = "Special" + (i + 1);
        }
        so.ApplyModifiedPropertiesWithoutUndo();
        Assert.That(ActionGraphValidator.Validate(graph).Any(i => i.Code == "Edge.Conflict"), Is.False);
    }
}
