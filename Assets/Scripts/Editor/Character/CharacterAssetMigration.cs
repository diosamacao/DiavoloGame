using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

/// <summary>按显式清单迁移配置，保留 GUID 和内容引用；整批预检并在失败时逆序回滚。</summary>
public static class CharacterAssetMigration
{
    /// <summary>仅在空闲 Editor 执行；禁止覆盖目的文件、未保存修改或清单生成后的源修改。</summary>
    public static void Execute(CharacterAssetMigrationManifest manifest)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            throw new InvalidOperationException("请在 Editor 空闲时迁移。");
        var entries = manifest?.entries;
        if (entries == null || entries.Length == 0) throw new ArgumentException("迁移清单为空。");
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var sources = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var originalBytes = new Dictionary<string, byte[]>();
        foreach (var item in entries)
        {
            ValidatePath(item.oldPath); ValidatePath(item.newPath);
            if (!paths.Add(item.newPath) || !sources.Add(item.oldPath)) throw new InvalidOperationException("清单含重复路径。");
            if (File.Exists(item.newPath) || Directory.Exists(item.newPath) || File.Exists(item.newPath + ".meta"))
                throw new IOException("目标已存在：" + item.newPath);
            if (AssetDatabase.AssetPathToGUID(item.oldPath) != item.guid || Hash(item.oldPath) != item.sha256)
                throw new InvalidOperationException("源内容已改变：" + item.oldPath);
            var asset = AssetDatabase.LoadMainAssetAtPath(item.oldPath);
            if (!(asset is ScriptableObject) || asset.name != item.oldName || EditorUtility.IsDirty(asset))
                throw new InvalidOperationException("资产类型、名称或未保存状态不匹配：" + item.oldPath);
            if (Path.GetFileNameWithoutExtension(item.newPath) != item.newName)
                throw new ArgumentException("目的文件名与资产名不一致。");
            CharacterAssetLayout.ValidateKey(item.newName);
            byte[] bytes = File.ReadAllBytes(item.oldPath);
            if (!Regex.IsMatch(Encoding.UTF8.GetString(bytes), @"(?m)^  m_Name: [^\r\n]*"))
                throw new ArgumentException("仅支持含名称字段的 Unity 文本资产。");
            originalBytes.Add(item.oldPath, bytes);
        }
        var moved = new List<CharacterAssetMigrationEntry>();
        var folders = new List<string>();
        try
        {
            foreach (var item in entries)
            {
                CharacterAssetLayout.EnsureFolder(Path.GetDirectoryName(item.newPath).Replace('\\', '/'), folders);
                string error = AssetDatabase.MoveAsset(item.oldPath, item.newPath);
                if (!string.IsNullOrEmpty(error)) throw new IOException(error);
                moved.Add(item);
                // 只修改名称行；避免 SaveAsset 重序列化时补写默认值或清掉未参与本次迁移的旧字段。
                var pattern = new Regex(@"(?m)^  m_Name: [^\r\n]*");
                string payload = Encoding.UTF8.GetString(originalBytes[item.oldPath]);
                payload = pattern.Replace(payload, _ => "  m_Name: " + item.newName, 1);
                File.WriteAllBytes(item.newPath, Encoding.UTF8.GetBytes(payload));
                AssetDatabase.ImportAsset(item.newPath, ImportAssetOptions.ForceUpdate);
                if (AssetDatabase.LoadMainAssetAtPath(item.newPath).name != item.newName)
                    throw new IOException("导入后的资产名称不一致。");
                if (AssetDatabase.AssetPathToGUID(item.newPath) != item.guid) throw new IOException("迁移 GUID 不一致。");
            }
        }
        catch (Exception original)
        {
            var failures = new List<Exception> { original };
            foreach (var item in moved.AsEnumerable().Reverse())
            {
                try
                {
                    string error = AssetDatabase.MoveAsset(item.newPath, item.oldPath);
                    if (!string.IsNullOrEmpty(error)) throw new IOException(error);
                    File.WriteAllBytes(item.oldPath, originalBytes[item.oldPath]);
                    AssetDatabase.ImportAsset(item.oldPath, ImportAssetOptions.ForceUpdate);
                }
                catch (Exception rollback) { failures.Add(rollback); }
            }
            CharacterAssetLayout.CleanupEmptyFolders(folders);
            throw new AggregateException("迁移失败；已尝试回滚全部已移动资产。", failures);
        }
    }

    static void ValidatePath(string path)
    {
        if (string.IsNullOrEmpty(path) || !path.StartsWith("Assets/Data/", StringComparison.Ordinal)
            || !path.EndsWith(".asset", StringComparison.Ordinal) || path.Contains('\\')
            || path.Split('/').Any(p => string.IsNullOrWhiteSpace(p) || p == "." || p == ".." || p.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0))
            throw new ArgumentException("迁移仅接受 Assets/Data 内的明确 .asset 路径。");
    }

    static string Hash(string path)
    {
        using var sha = SHA256.Create();
        return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-", "").ToLowerInvariant();
    }
}
