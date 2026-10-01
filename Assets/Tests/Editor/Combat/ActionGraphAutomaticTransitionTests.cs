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
