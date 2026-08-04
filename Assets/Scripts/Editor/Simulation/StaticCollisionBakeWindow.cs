using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 从当前打开场景的 Collider 烘焙：地面薄板→GroundY，墙体→水平 AABB；运行时不读 Physics。
/// </summary>
public sealed class StaticCollisionBakeWindow : EditorWindow
{
    StaticCollisionBake _target;
    LayerMask _includeLayers = ~0;
    bool _includeTriggers;
    bool _autoGroundFromFloors = true;
    float _groundYMeters;
    float _maxFloorHeightMeters = SimStaticColliderClassify.DefaultMaxFloorHeightMeters;
    Vector2 _scroll;
    string _log = string.Empty;

    /// <summary>打开静态碰撞烘焙窗口。</summary>
    [MenuItem("ACTGame/Collision/Bake Static From Scene...")]
    public static void Open()
    {
        var window = GetWindow<StaticCollisionBakeWindow>("Static Collision Bake");
        window.minSize = new Vector2(420f, 360f);
        window.Show();
    }

    void OnGUI()
    {
        EditorGUILayout.LabelField("场景静态碰撞烘焙", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "地面（薄板 / 名含 Floor·Ground·Terrain）只写入 GroundY，不进水平硬挡。"
            + " 墙体投影为 XZ AABB。若曾把 Floor 烘成障碍，请重新 Bake。",
            MessageType.Info);

        _target = (StaticCollisionBake)EditorGUILayout.ObjectField(
            "Bake Asset",
            _target,
            typeof(StaticCollisionBake),
            false);
        _includeLayers = LayerMaskField("Include Layers", _includeLayers);
        _includeTriggers = EditorGUILayout.Toggle("Include Triggers", _includeTriggers);
        _maxFloorHeightMeters = EditorGUILayout.FloatField(
            "Max Floor Height (m)",
            _maxFloorHeightMeters);
        _autoGroundFromFloors = EditorGUILayout.Toggle(
            "Auto Ground Y From Floors",
            _autoGroundFromFloors);
        using (new EditorGUI.DisabledScope(_autoGroundFromFloors))
        {
            _groundYMeters = EditorGUILayout.FloatField("Ground Y (meters)", _groundYMeters);
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Preview", GUILayout.Height(28f)))
                RunPreview();
            if (GUILayout.Button("Bake Into Asset", GUILayout.Height(28f)))
                RunBake();
        }

        if (GUILayout.Button("Create New Bake Asset..."))
            CreateNewAsset();

        EditorGUILayout.Space();
        _scroll = EditorGUILayout.BeginScrollView(_scroll);
        EditorGUILayout.TextArea(_log, GUILayout.ExpandHeight(true));
        EditorGUILayout.EndScrollView();
    }

    void RunPreview()
    {
        BakeCollectResult collect = CollectFromScene();
        _log = FormatCollectLog("Preview", collect);
    }

    void RunBake()
    {
        if (_target == null)
        {
            EditorUtility.DisplayDialog("Static Collision Bake", "请指定或创建 StaticCollisionBake 资产。", "OK");
            return;
        }

        BakeCollectResult collect = CollectFromScene();
        var data = new StaticCollisionBakeData
        {
            groundYMm = MotionQuantization.MetersToMm(collect.GroundYMeters),
            aabbs = collect.Obstacles.ToArray(),
            sourceSceneName = SceneManager.GetActiveScene().name,
            bakedUtcTicks = DateTime.UtcNow.Ticks,
        };

        Undo.RecordObject(_target, "Bake Static Collision");
        _target.EditorSetData(data);
        EditorUtility.SetDirty(_target);
        AssetDatabase.SaveAssets();

        _log = FormatCollectLog($"Baked into {_target.name}", collect);
        Debug.Log("Static Collision Bake\n" + _log);
        EditorUtility.DisplayDialog(
            "Static Collision Bake",
            _log.Length > 1200 ? _log.Substring(0, 1200) + "…" : _log,
            "OK");
    }

    void CreateNewAsset()
    {
        string path = EditorUtility.SaveFilePanelInProject(
            "Create Static Collision Bake",
            "StaticCollisionBake",
            "asset",
            "选择保存路径",
            "Assets/Data");
        if (string.IsNullOrEmpty(path))
            return;

        var asset = CreateInstance<StaticCollisionBake>();
        AssetDatabase.CreateAsset(asset, path);
        AssetDatabase.SaveAssets();
        _target = asset;
        _log = $"Created {path}";
    }

    BakeCollectResult CollectFromScene()
    {
        var result = new BakeCollectResult
        {
            GroundYMeters = _groundYMeters,
            Obstacles = new List<SimStaticAabb>(64),
            FloorNames = new List<string>(8),
            ObstacleNames = new List<string>(32),
        };

        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid())
            return result;

        float bestFloorArea = -1f;
        float bestFloorTopY = _groundYMeters;
        GameObject[] roots = scene.GetRootGameObjects();
        for (int r = 0; r < roots.Length; r++)
            CollectInTransform(roots[r].transform, result, ref bestFloorArea, ref bestFloorTopY);

        if (_autoGroundFromFloors && result.FloorCount > 0)
            result.GroundYMeters = bestFloorTopY;

        result.Obstacles.Sort(CompareAabb);
        return result;
    }

    void CollectInTransform(
        Transform t,
        BakeCollectResult result,
        ref float bestFloorArea,
        ref float bestFloorTopY)
    {
        Collider[] colliders = t.GetComponents<Collider>();
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider c = colliders[i];
            if (c == null)
            {
                result.Skipped++;
                continue;
            }

            if (!c.enabled)
            {
                // 常见坑：Cube 有 Mesh 但 BoxCollider 被关掉，视觉在却烘不进去
                result.Skipped++;
                result.SkipReasons.Add($"{c.gameObject.name}: Collider disabled");
                continue;
            }

            if (!_includeTriggers && c.isTrigger)
            {
                result.Skipped++;
                result.SkipReasons.Add($"{c.gameObject.name}: trigger excluded");
                continue;
            }

            if (((1 << c.gameObject.layer) & _includeLayers.value) == 0)
            {
                result.Skipped++;
                result.SkipReasons.Add($"{c.gameObject.name}: layer excluded");
                continue;
            }

            if (c is CharacterController)
            {
                result.Skipped++;
                result.SkipReasons.Add($"{c.gameObject.name}: CharacterController skipped");
                continue;
            }

            Bounds b = c.bounds;
            string name = c.gameObject.name;
            bool floorLike =
                SimStaticColliderClassify.IsFloorLikeName(name)
                || SimStaticColliderClassify.IsFloorLikeBounds(
                    b.size.x,
                    b.size.y,
                    b.size.z,
                    _maxFloorHeightMeters);

            if (floorLike)
            {
                // 地面只贡献高度，绝不进水平硬挡
                result.FloorCount++;
                result.FloorNames.Add(
                    $"{name} topY={b.max.y:F3} size=({b.size.x:F2},{b.size.y:F2},{b.size.z:F2})");
                float area = Mathf.Abs(b.size.x * b.size.z);
                if (area >= bestFloorArea)
                {
                    bestFloorArea = area;
                    bestFloorTopY = b.max.y;
                }

                continue;
            }

            result.Obstacles.Add(new SimStaticAabb(
                MotionQuantization.MetersToMm(b.min.x),
                MotionQuantization.MetersToMm(b.max.x),
                MotionQuantization.MetersToMm(b.min.z),
                MotionQuantization.MetersToMm(b.max.z)));
            result.ObstacleNames.Add(
                $"{name} x=[{b.min.x:F2},{b.max.x:F2}] z=[{b.min.z:F2},{b.max.z:F2}]");
        }

        for (int i = 0; i < t.childCount; i++)
            CollectInTransform(t.GetChild(i), result, ref bestFloorArea, ref bestFloorTopY);
    }

    static string FormatCollectLog(string title, BakeCollectResult collect)
    {
        var sb = new System.Text.StringBuilder(512);
        sb.AppendLine(
            $"{title}: floors={collect.FloorCount}, obstacles={collect.Obstacles.Count}, "
            + $"skipped={collect.Skipped}, groundY={collect.GroundYMeters:F3}m");

        sb.AppendLine("Floors (GroundY only):");
        int floorN = Math.Min(collect.FloorNames.Count, 20);
        for (int i = 0; i < floorN; i++)
            sb.AppendLine("  " + collect.FloorNames[i]);
        if (collect.FloorNames.Count > floorN)
            sb.AppendLine($"  … +{collect.FloorNames.Count - floorN} more");

        sb.AppendLine("Obstacles (horizontal block):");
        int obsN = Math.Min(collect.ObstacleNames.Count, 40);
        for (int i = 0; i < obsN; i++)
            sb.AppendLine("  " + collect.ObstacleNames[i]);
        if (collect.ObstacleNames.Count > obsN)
            sb.AppendLine($"  … +{collect.ObstacleNames.Count - obsN} more");

        if (collect.SkipReasons.Count > 0)
        {
            sb.AppendLine("Skipped:");
            int skipN = Math.Min(collect.SkipReasons.Count, 30);
            for (int i = 0; i < skipN; i++)
                sb.AppendLine("  " + collect.SkipReasons[i]);
            if (collect.SkipReasons.Count > skipN)
                sb.AppendLine($"  … +{collect.SkipReasons.Count - skipN} more");
        }

        return sb.ToString();
    }

    static int CompareAabb(SimStaticAabb a, SimStaticAabb b)
    {
        int cmp = a.MinXMm.CompareTo(b.MinXMm);
        if (cmp != 0)
            return cmp;
        cmp = a.MinZMm.CompareTo(b.MinZMm);
        if (cmp != 0)
            return cmp;
        cmp = a.MaxXMm.CompareTo(b.MaxXMm);
        return cmp != 0 ? cmp : a.MaxZMm.CompareTo(b.MaxZMm);
    }

    static LayerMask LayerMaskField(string label, LayerMask selected)
    {
        var layers = new List<string>(32);
        var layerNumbers = new List<int>(32);
        for (int i = 0; i < 32; i++)
        {
            string name = LayerMask.LayerToName(i);
            if (string.IsNullOrEmpty(name))
                continue;
            layers.Add(name);
            layerNumbers.Add(i);
        }

        int maskWithoutEmpty = 0;
        for (int i = 0; i < layerNumbers.Count; i++)
        {
            if (((1 << layerNumbers[i]) & selected.value) != 0)
                maskWithoutEmpty |= 1 << i;
        }

        maskWithoutEmpty = EditorGUILayout.MaskField(label, maskWithoutEmpty, layers.ToArray());
        int mask = 0;
        for (int i = 0; i < layerNumbers.Count; i++)
        {
            if ((maskWithoutEmpty & (1 << i)) != 0)
                mask |= 1 << layerNumbers[i];
        }

        selected.value = mask;
        return selected;
    }

    /// <summary>单次场景扫描结果。</summary>
    sealed class BakeCollectResult
    {
        public float GroundYMeters;
        public List<SimStaticAabb> Obstacles;
        public List<string> FloorNames;
        public List<string> ObstacleNames;
        public List<string> SkipReasons = new(16);
        public int FloorCount;
        public int Skipped;
    }
}
