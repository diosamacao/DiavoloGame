using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;

/// <summary>验证自动过渡画布在重排、断线、分组和失效引用下的保存语义。</summary>
public sealed class ActionGraphAutomaticTransitionTests
{
    ActionGraph graph;
    ActionGraphView view;

    [SetUp]
    public void SetUp()
    {
        graph = ScriptableObject.CreateInstance<ActionGraph>();
        var so = new SerializedObject(graph);
        SerializedProperty nodes = so.FindProperty("nodes");
        nodes.arraySize = 2;
        // 与保存时的字典排序相反，覆盖 SerializedObject 原地重排。
        nodes.GetArrayElementAtIndex(0).FindPropertyRelative("nodeId").stringValue = "B";
        nodes.GetArrayElementAtIndex(1).FindPropertyRelative("nodeId").stringValue = "A";
        SerializedProperty rules = nodes.GetArrayElementAtIndex(1).FindPropertyRelative("automaticTransitions");
        rules.arraySize = 2;
        for (int i = 0; i < 2; i++)
        {
            SerializedProperty rule = rules.GetArrayElementAtIndex(i);
            rule.FindPropertyRelative("targetNodeId").stringValue = "B";
            rule.FindPropertyRelative("condition").enumValueIndex = (int)ActionTransitionCondition.AtFrame;
            rule.FindPropertyRelative("startFrame").intValue = 10 + i;
            rule.FindPropertyRelative("priority").intValue = 5 - i;
        }
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(graph);
    }

    void Load()
    {
        view = new ActionGraphView(graph);
        view.LoadFromAsset();
    }

    [Test]
    public void RoutedEdges_PortInitializationAndDetachedUpdatesDoNotComputeLayout()
    {
        Assert.DoesNotThrow(Load);
        var existing = view.edges.ToList()[0];
        var edge = new RoutedActionGraphEdge();
        Assert.DoesNotThrow(() => edge.output = existing.output);
        Assert.DoesNotThrow(() => edge.input = existing.input);
        Assert.That(edge.UpdateEdgeControl(), Is.False);
        Assert.DoesNotThrow(() => view.AddElement(edge));
        Assert.That(edge.UpdateEdgeControl(), Is.True);
        Assert.That(edge.edgeControl.edgeWidth, Is.EqualTo(3));
        view.RemoveElement(edge);
        Assert.That(edge.UpdateEdgeControl(), Is.False);
        Assert.DoesNotThrow(() => edge.output = null);
        Assert.DoesNotThrow(() => edge.input = null);
    }

    [Test]
    public void RoutedEdges_RoundTripKeepsSeparatePathsAndRemovesDeletedLayout()
    {
        Load();
        var edges = view.edges.ToList().Cast<RoutedActionGraphEdge>().ToArray();
        edges[0].Points.Add(new Vector2(120, 240));
        edges[1].Points.Add(new Vector2(420, 180));
        view.WriteToAsset();
        Assert.That(graph.EditorEdgeLayouts.Count, Is.EqualTo(2));
        Assert.That(graph.EditorEdgeLayouts.Select(x => x.key).Distinct().Count(), Is.EqualTo(2));
        var expected = graph.EditorEdgeLayouts.ToDictionary(x => x.key, x => x.points.ToArray());
        view.LoadFromAsset();
        foreach (var edge in view.edges.ToList().Cast<RoutedActionGraphEdge>())
            Assert.That(edge.Points, Is.EqualTo(expected[ActionGraphView.EdgeLayoutKey(edge)]));
        Assert.That(graph.Edges, Is.Empty);

        Edge removed = view.edges.ToList()[0];
        string removedKey = ActionGraphView.EdgeLayoutKey(removed);
        removed.output.Disconnect(removed);
        removed.input.Disconnect(removed);
        view.RemoveElement(removed);
        view.WriteToAsset();
        Assert.That(graph.EditorEdgeLayouts.Count, Is.EqualTo(1));
        Assert.That(graph.EditorEdgeLayouts.Any(x => x.key == removedKey), Is.False);
    }

    [Test]
    public void RoutedEdges_UndoRestoresPreviousPoints()
    {
        Load();
        var edge = (RoutedActionGraphEdge)view.edges.ToList()[0];
        edge.Points.Add(new Vector2(100, 200));
        view.WriteToAsset();
        Undo.IncrementCurrentGroup();
        edge.Points[0] = new Vector2(300, 400);
        view.PersistViewToAsset();
        Undo.FlushUndoRecordObjects();
        Undo.PerformUndo();
        view.LoadFromAsset();
        Assert.That(view.edges.ToList().Cast<RoutedActionGraphEdge>().Single(e => e.Points.Count > 0).Points[0],
            Is.EqualTo(new Vector2(100, 200)));
        Undo.ClearUndo(graph);
    }

    [Test]
    public void RoutedEdges_RectangleSelectionIncludesCrossingSegments()
    {
        var rect = new Rect(0, 0, 10, 10);
        Assert.That(RoutedActionGraphEdge.SegmentOverlaps(rect, new Vector2(-10, 5), new Vector2(20, 5)), Is.True);
        Assert.That(RoutedActionGraphEdge.SegmentOverlaps(rect, new Vector2(-10, 15), new Vector2(20, 15)), Is.False);
        Assert.That(RoutedActionGraphEdge.SegmentOverlaps(rect, Vector2.one, Vector2.one), Is.True);
        Assert.That(RoutedActionGraphEdge.DistanceToSegment(new Vector2(5, 3), Vector2.zero, new Vector2(10, 0)), Is.EqualTo(3));
    }

    [Test]
    public void RoutedEdges_SnapPrefersNearestAnchorPerAxisThenGrid()
    {
        var anchors = new[] { new Vector2(33, 80), new Vector2(90, 51), new Vector2(36, 100) };
        Assert.That(RoutedActionGraphEdge.SnapPoint(new Vector2(35, 54), anchors, 1, false),
            Is.EqualTo(new Vector2(36, 51)));
        Assert.That(RoutedActionGraphEdge.SnapPoint(new Vector2(-27, 147), anchors, 1, false),
            Is.EqualTo(new Vector2(-20, 140)));
        Assert.That(RoutedActionGraphEdge.SnapPoint(new Vector2(35, 54), anchors, 1, true),
            Is.EqualTo(new Vector2(35, 54)));
    }

    [TestCase(.5f, 113f)]
    [TestCase(1f, 100f)]
    [TestCase(2f, 100f)]
    public void RoutedEdges_AlignmentToleranceUsesScreenPixels(float zoom, float expectedX)
    {
        Assert.That(RoutedActionGraphEdge.SnapPoint(new Vector2(100, 200),
            new[] { new Vector2(113, 200) }, zoom, false).x, Is.EqualTo(expectedX));
    }

    [TestCase(CancelWindowType.Normal)]
    [TestCase(CancelWindowType.Perfect)]
    public void CancelEntry_PreservesRoutePriorityIncludingOverlappingWindows(CancelWindowType window)
    {
        var action = ScriptableObject.CreateInstance<ActionDefinition>();
        var clip = new AnimationClip();
        try
        {
            clip.SetCurve("", typeof(Transform), "localPosition.x", AnimationCurve.Linear(0, 0, 1, 0));
            using (var actionSo = new SerializedObject(action))
            {
                var segments = actionSo.FindProperty("animationSegments");
                segments.arraySize = 1;
                segments.GetArrayElementAtIndex(0).FindPropertyRelative("clip").objectReferenceValue = clip;
                segments.GetArrayElementAtIndex(0).FindPropertyRelative("endFrame").intValue = -1;
                actionSo.FindProperty("totalFrames").intValue = 60;
                actionSo.ApplyModifiedPropertiesWithoutUndo();
            }
            Assert.That(action.IsSimulationReady, Is.True);
            using var so = new SerializedObject(graph);
            var nodes = so.FindProperty("nodes");
            nodes.arraySize = 3;
            for (int i = 0; i < 3; i++)
            {
                var node = nodes.GetArrayElementAtIndex(i);
                node.FindPropertyRelative("nodeId").stringValue = new[] { "A", "B", "C" }[i];
                node.FindPropertyRelative("action").objectReferenceValue = action;
                node.FindPropertyRelative("intent").intValue = (int)GameplayIntentType.Attack;
                node.FindPropertyRelative("isEntry").boolValue = i == 1;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            var request = new ActionRequest(GameplayIntentType.Attack);
            var context = new ActionResolveContext(ActionResolveOrigin.CancelWindow, action, null, null, window, "A", true);
            Assert.That(graph.TryResolveCancel(in request, in context, out var result), Is.True);
            Assert.That(result.NodeId, Is.EqualTo("B"));
            var intents = new System.Collections.Generic.HashSet<GameplayIntentType>();
            graph.CollectCancelCandidateIntents("A", window, intents);
            Assert.That(intents, Does.Contain(GameplayIntentType.Attack));
            var closed = new ActionResolveContext(ActionResolveOrigin.CancelWindow, action, null, null, window, "A", false);
            Assert.That(graph.TryResolveCancel(in request, in closed, out _), Is.False);
            var missing = new ActionResolveContext(ActionResolveOrigin.CancelWindow, action, null, null, window, "Missing", true);
            Assert.That(graph.TryResolveCancel(in request, in missing, out _), Is.False);

            // 仅有 Dodge 出边时，Attack 不得使用 Entry；失效目标同样不能放宽出口。
            so.Update();
            so.FindProperty("nodes").GetArrayElementAtIndex(2).FindPropertyRelative("intent").intValue = (int)GameplayIntentType.Dodge;
            var restrictedEdges = so.FindProperty("edges");
            restrictedEdges.arraySize = 1;
            var restricted = restrictedEdges.GetArrayElementAtIndex(0);
            restricted.FindPropertyRelative("fromNodeId").stringValue = "A";
            restricted.FindPropertyRelative("toNodeId").stringValue = "C";
            restricted.FindPropertyRelative("routeKind").intValue = (int)window;
            so.ApplyModifiedPropertiesWithoutUndo();
            Assert.That(graph.TryResolveCancel(in request, in context, out _), Is.False);
            graph.CollectCancelCandidateIntents("A", window, intents);
            Assert.That(intents.Contains(GameplayIntentType.Attack), Is.False);
            Assert.That(intents, Does.Contain(GameplayIntentType.Dodge));
            var dodge = new ActionRequest(GameplayIntentType.Dodge);
            Assert.That(graph.TryResolveCancel(in dodge, in context, out result), Is.True);
            Assert.That(result.NodeId, Is.EqualTo("C"));
            var otherWindow = window == CancelWindowType.Normal ? CancelWindowType.Perfect : CancelWindowType.Normal;
            var otherContext = new ActionResolveContext(ActionResolveOrigin.CancelWindow, action, null, null, otherWindow, "A", true);
            Assert.That(graph.TryResolveCancel(in request, in otherContext, out result), Is.True,
                "另一个没有连线的通道仍允许 Entry。");
            so.Update();
            so.FindProperty("edges").GetArrayElementAtIndex(0).FindPropertyRelative("toNodeId").stringValue = "Missing";
            so.ApplyModifiedPropertiesWithoutUndo();
            Assert.That(graph.TryResolveCancel(in request, in context, out _), Is.False);
            graph.CollectCancelCandidateIntents("A", window, intents);
            Assert.That(intents.Contains(GameplayIntentType.Attack), Is.False);
            so.Update();
            so.FindProperty("edges").arraySize = 0;
            so.FindProperty("nodes").GetArrayElementAtIndex(2).FindPropertyRelative("intent").intValue = (int)GameplayIntentType.Attack;
            so.ApplyModifiedPropertiesWithoutUndo();
            Assert.That(graph.TryResolveCancel(in request, in context, out result), Is.True);
            Assert.That(result.NodeId, Is.EqualTo("B"));

            so.Update();
            var routes = so.FindProperty("sharedRoutes");
            routes.arraySize = 1;
            var route = routes.GetArrayElementAtIndex(0);
            route.FindPropertyRelative("intent").intValue = (int)GameplayIntentType.Attack;
            route.FindPropertyRelative("routeKind").intValue = (int)window;
            route.FindPropertyRelative("toNodeId").stringValue = "C";
            so.ApplyModifiedPropertiesWithoutUndo();
            Assert.That(graph.TryResolveCancel(in request, in context, out result), Is.True);
            Assert.That(result.NodeId, Is.EqualTo("C"));

            so.Update();
            var explicitEdges = so.FindProperty("edges");
            explicitEdges.arraySize = 1;
            var explicitEdge = explicitEdges.GetArrayElementAtIndex(0);
            explicitEdge.FindPropertyRelative("fromNodeId").stringValue = "A";
            explicitEdge.FindPropertyRelative("toNodeId").stringValue = "A";
            explicitEdge.FindPropertyRelative("routeKind").intValue = (int)window;
            so.ApplyModifiedPropertiesWithoutUndo();
            Assert.That(graph.TryResolveCancel(in request, in context, out result), Is.True);
            Assert.That(result.NodeId, Is.EqualTo("A"));

            using (var actionSo = new SerializedObject(action))
            {
                var windows = actionSo.FindProperty("timeline.cancelWindowStates");
                windows.arraySize = 2;
                for (int i = 0; i < 2; i++)
                {
                    var item = windows.GetArrayElementAtIndex(i);
                    item.FindPropertyRelative("windowType").intValue = i;
                    item.FindPropertyRelative("startFrame").intValue = 10;
                    item.FindPropertyRelative("endFrame").intValue = i == 0 ? 30 : 40;
                }
                actionSo.ApplyModifiedPropertiesWithoutUndo();
            }
            var bridge = new ActionSimResolverBridge(new ActionResolverService(new GraphMode(graph)), null, null);
            var overlap = new ActionSimSnapshot(action, graph, "A", 20, 1, false, true, 0);
            so.Update();
            so.FindProperty("sharedRoutes").arraySize = 0;
            var normalEdge = so.FindProperty("edges").GetArrayElementAtIndex(0);
            normalEdge.FindPropertyRelative("routeKind").intValue = (int)CancelWindowType.Normal;
            normalEdge.FindPropertyRelative("toNodeId").stringValue = "C";
            so.ApplyModifiedPropertiesWithoutUndo();
            Assert.That(bridge.TryResolveNext(GameplayIntentType.Attack, CancelWindowType.Perfect, in overlap, out _), Is.False,
                "Perfect 未命中必须留给 Normal 显式边，不能提前回 Entry。");
            Assert.That(bridge.TryResolveNext(GameplayIntentType.Attack, CancelWindowType.Normal, in overlap, out var simResult), Is.True);
            Assert.That(simResult.NodeId, Is.EqualTo("C"));

            so.Update();
            so.FindProperty("edges").arraySize = 0;
            var shared = so.FindProperty("sharedRoutes");
            shared.arraySize = 1;
            shared.GetArrayElementAtIndex(0).FindPropertyRelative("routeKind").intValue = (int)CancelWindowType.Normal;
            shared.GetArrayElementAtIndex(0).FindPropertyRelative("intent").intValue = (int)GameplayIntentType.Attack;
            shared.GetArrayElementAtIndex(0).FindPropertyRelative("toNodeId").stringValue = "C";
            so.ApplyModifiedPropertiesWithoutUndo();
            Assert.That(bridge.TryResolveNext(GameplayIntentType.Attack, CancelWindowType.Perfect, in overlap, out _), Is.False);
            Assert.That(bridge.TryResolveNext(GameplayIntentType.Attack, CancelWindowType.Normal, in overlap, out simResult), Is.True);
            Assert.That(simResult.NodeId, Is.EqualTo("C"), "Normal 共享路由也必须优先于 Entry。");

            so.Update();
            so.FindProperty("sharedRoutes").arraySize = 0;
            so.ApplyModifiedPropertiesWithoutUndo();
            Assert.That(bridge.TryResolveNext(GameplayIntentType.Attack, CancelWindowType.Perfect, in overlap, out _), Is.False);
            Assert.That(bridge.TryResolveNext(GameplayIntentType.Attack, CancelWindowType.Normal, in overlap, out simResult), Is.True);
            Assert.That(simResult.NodeId, Is.EqualTo("B"));
            var perfectOnly = new ActionSimSnapshot(action, graph, "A", 35, 1, false, true, 0);
            Assert.That(bridge.TryResolveNext(GameplayIntentType.Attack, CancelWindowType.Perfect, in perfectOnly, out simResult), Is.True);
            Assert.That(simResult.NodeId, Is.EqualTo("B"), "只有 Perfect 开启时仍允许 Entry。");

            so.Update();
            var perfectEdges = so.FindProperty("edges");
            perfectEdges.arraySize = 1;
            perfectEdges.GetArrayElementAtIndex(0).FindPropertyRelative("fromNodeId").stringValue = "A";
            perfectEdges.GetArrayElementAtIndex(0).FindPropertyRelative("toNodeId").stringValue = "C";
            perfectEdges.GetArrayElementAtIndex(0).FindPropertyRelative("routeKind").intValue = (int)CancelWindowType.Perfect;
            so.ApplyModifiedPropertiesWithoutUndo();
            Assert.That(bridge.TryResolveNext(GameplayIntentType.Attack, CancelWindowType.Perfect, in overlap, out simResult), Is.True);
            Assert.That(simResult.NodeId, Is.EqualTo("C"), "Perfect 显式边仍保持优先。");
        }
        finally
        {
            Object.DestroyImmediate(action);
            Object.DestroyImmediate(clip);
        }
    }

    /// <summary>为解析桥提供固定图，避免测试依赖角色资产与场景。</summary>
    sealed class GraphMode : ICombatModeService
    {
        public GraphMode(ActionGraph graph) => ActiveGraph = graph;
        public ActionGraph ActiveGraph { get; }
        public CombatModeType CurrentMode => default;
        public event System.Action<CombatModeType, CombatModeType> ModeChanged { add { } remove { } }
        public CombatModeSwitchResult TrySetMode(CombatModeType mode,
            CombatModeSwitchPolicy policy = CombatModeSwitchPolicy.Immediate, bool isActionPlaying = false) => default;
        public void ApplyPendingModeIfReady() { }
    }

    [Test]
    public void RoundTrip_PreservesDistinctRulesAndDoesNotCreateCancelEdges()
    {
        Load();
        Assert.That(view.edges.ToList().Count, Is.EqualTo(2));
        view.WriteToAsset();
        var rules = graph.Nodes.Single(n => n.NodeId == "A").AutomaticTransitions;
        Assert.That(rules.Select(r => r.TargetNodeId), Is.EqualTo(new[] { "B", "B" }));
        Assert.That(rules.Select(r => r.StartFrame), Is.EqualTo(new[] { 10, 11 }));
        Assert.That(rules.Select(r => r.Priority), Is.EqualTo(new[] { 5, 4 }));
        Assert.That(rules.All(r => r.Condition == ActionTransitionCondition.AtFrame), Is.True);
        Assert.That(graph.Edges, Is.Empty);
        Assert.That(graph.Nodes.Single(n => n.NodeId == "B").AutomaticTransitions, Is.Empty);
        view.LoadFromAsset();
        Assert.That(view.edges.ToList().Count, Is.EqualTo(2));
    }

    [Test]
    public void Disconnect_EndsActionWithoutRemovingRule()
    {
        Load();
        Edge edge = view.edges.ToList()[0];
        edge.output.Disconnect(edge);
        edge.input.Disconnect(edge);
        view.RemoveElement(edge);
        view.WriteToAsset();
        var rules = graph.Nodes.Single(n => n.NodeId == "A").AutomaticTransitions;
        Assert.That(rules.Count, Is.EqualTo(2));
        Assert.That(rules.Count(r => string.IsNullOrEmpty(r.TargetNodeId)), Is.EqualTo(1));
        Assert.That(rules.Count(r => r.TargetNodeId == "B"), Is.EqualTo(1));
    }

    [Test]
    public void GroupedRules_CanConnectWithinGroupFromEitherDirection()
    {
        var so = new SerializedObject(graph);
        SerializedProperty groups = so.FindProperty("nodeGroups");
        groups.arraySize = 1;
        SerializedProperty group = groups.GetArrayElementAtIndex(0);
        group.FindPropertyRelative("groupId").stringValue = "Sequence";
        SerializedProperty children = group.FindPropertyRelative("childNodeIds");
        children.arraySize = 2;
        children.GetArrayElementAtIndex(0).stringValue = "A";
        children.GetArrayElementAtIndex(1).stringValue = "B";
        so.ApplyModifiedPropertiesWithoutUndo();
        Load();
        Edge edge = view.edges.ToList()[0];
        Assert.That(edge.input.node, Is.SameAs(edge.output.node));
        Assert.That(view.GetCompatiblePorts(edge.output, new NodeAdapter()), Does.Contain(edge.input));
        Assert.That(view.GetCompatiblePorts(edge.input, new NodeAdapter()), Does.Contain(edge.output));
        view.WriteToAsset();
        Assert.That(graph.Nodes.Single(n => n.NodeId == "A").AutomaticTransitions
            .All(r => r.TargetNodeId == "B"), Is.True);
        Assert.That(graph.Nodes.Single(n => n.NodeId == "B").AutomaticTransitions, Is.Empty);
    }

    [Test]
    public void MissingTarget_IsPreservedForValidationUntilReconnected()
    {
        var so = new SerializedObject(graph);
        so.FindProperty("nodes").GetArrayElementAtIndex(1)
            .FindPropertyRelative("automaticTransitions").GetArrayElementAtIndex(0)
            .FindPropertyRelative("targetNodeId").stringValue = "Missing";
        so.ApplyModifiedPropertiesWithoutUndo();
        Load();
        view.WriteToAsset();
        Assert.That(graph.Nodes.Single(n => n.NodeId == "A").AutomaticTransitions[0].TargetNodeId,
            Is.EqualTo("Missing"));
        Port output = view.ports.ToList().Single(p => p.userData is string id && id == "Missing");
        Port input = view.nodes.ToList().OfType<ActionGraphNodeView>().Single(n => n.NodeId == "B").InputPort;
        view.AddElement(output.ConnectTo(input));
        view.WriteToAsset();
        Assert.That(graph.Nodes.Single(n => n.NodeId == "A").AutomaticTransitions[0].TargetNodeId,
            Is.EqualTo("B"));
    }
}
