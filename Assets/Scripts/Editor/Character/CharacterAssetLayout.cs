using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;

/// <summary>角色作者资产的目录、稳定命名和安全目录创建规则。</summary>
public static class CharacterAssetLayout
{
    /// <summary>创建窗口默认的角色集合目录。</summary>
    public const string DefaultParent = "Assets/Data/Characters";
    /// <summary>每个空角色预建的配置用途目录。</summary>
    public static readonly string[] Subfolders = { "Config", "Graphs", "Locomotion", "Actions", "Reactions" };

    /// <summary>为已存在的角色根目录补齐基础骨架；不创建占位配置，不改变任何引用。</summary>
    public static void EnsureBaseFolders(string root)
    {
        if (!AssetDatabase.IsValidFolder(root) || !root.StartsWith("Assets/", StringComparison.Ordinal))
            throw new ArgumentException("请选择 Assets 下已有的角色根目录。");
        var created = new List<string>();
        var markers = new List<string>();
        try
        {
            foreach (string sub in Subfolders) EnsureFolder(root + "/" + sub, created);
            // Git 不记录空目录；保留标记让共享配置使用者的空用途目录也能随仓库同步。
            foreach (string sub in Subfolders)
            {
                string folder = root + "/" + sub;
                if (Directory.EnumerateFileSystemEntries(folder).Any()) continue;
                string marker = folder + "/.gitkeep";
                File.WriteAllText(marker, "");
                markers.Add(marker);
            }
        }
        catch
        {
            foreach (string marker in markers) File.Delete(marker);
            CleanupEmptyFolders(created);
            throw;
        }
    }

    /// <summary>稳定标识用英文字母开头，仅含字母、数字和下划线，避免路径及平台保留名。</summary>
    public static void ValidateKey(string value)
    {
        if (value == null || !Regex.IsMatch(value, @"\A[A-Za-z][A-Za-z0-9_]{0,63}\z")
            || Regex.IsMatch(value, @"\A(CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])\z", RegexOptions.IgnoreCase))
            throw new ArgumentException("名称须为 1–64 位英文字母、数字或下划线，以字母开头，且不能使用系统保留名。");
    }

    /// <summary>返回新角色根目录；父目录可尚未创建，但必须位于 Assets 内。</summary>
    public static string CharacterFolder(string parent, string id)
    {
        ValidateKey(id);
        parent = (parent ?? "").Replace('\\', '/').TrimEnd('/');
        if (!parent.StartsWith("Assets/", StringComparison.Ordinal)
            || parent.Split('/').Any(p => string.IsNullOrWhiteSpace(p) || p == "." || p == ".."
                || p != p.Trim() || p.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0))
            throw new ArgumentException("父目录必须是 Assets 下的有效路径，不能包含相对跳转。");
        return parent + "/" + id;
    }

    /// <summary>从身份引用确定前缀；无身份的身体配置使用自身资产名，不从旧模板推测前缀。</summary>
    public static string OwnerKey(CharacterConfig config)
    {
        var keys = AssetDatabase.FindAssets("t:CharacterDefinition")
            .Select(g => AssetDatabase.LoadAssetAtPath<CharacterDefinition>(AssetDatabase.GUIDToAssetPath(g)))
            .Where(c => c != null && c.CharacterConfig == config).Select(c => c.Id.Value).Distinct().ToArray();
        if (keys.Length > 1) throw new InvalidOperationException("身体配置被多个角色身份共享，请先拆分角色配置再创建私有动作。");
        if (keys.Length == 1) return keys[0];
        string directory = Path.GetDirectoryName(AssetDatabase.GetAssetPath(config))?.Replace('\\', '/');
        if (!string.IsNullOrEmpty(directory) && Path.GetFileName(directory) == "Config")
        {
            string key = Path.GetFileName(Path.GetDirectoryName(directory));
            if (config.name == key + "_Config") return key;
        }
        return config.name;
    }

    /// <summary>只追加一次当前角色前缀；用途由作者填写，不携带任何模板资产名。</summary>
    public static string ActionName(CharacterConfig config, string purpose)
    {
        ValidateKey(purpose);
        string prefix = OwnerKey(config) + "_";
        return purpose.StartsWith(prefix, StringComparison.Ordinal) ? purpose : prefix + purpose;
    }

    /// <summary>新布局从 Config 的上级定位角色根；已有平铺配置在自身目录下建立用途子目录。</summary>
    public static string ActionFolder(CharacterConfig config, bool reaction)
    {
        string directory = Path.GetDirectoryName(AssetDatabase.GetAssetPath(config)).Replace('\\', '/');
        string root = Path.GetFileName(directory) == "Config" ? Path.GetDirectoryName(directory).Replace('\\', '/') : directory;
        return root + (reaction ? "/Reactions" : "/Actions");
    }

    /// <summary>只创建缺失目录并记录所有权，供失败后按逆序清理空目录。</summary>
    public static void EnsureFolder(string path, List<string> created)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        if (File.Exists(path)) throw new IOException("目录位置已被文件占用：" + path);
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent, created);
        if (string.IsNullOrEmpty(AssetDatabase.CreateFolder(parent, Path.GetFileName(path))))
            throw new IOException("无法创建目录：" + path);
        created.Add(path);
    }

    /// <summary>仅清理由本次操作创建且目前为空的目录，不删除既有作者内容。</summary>
    public static void CleanupEmptyFolders(List<string> created)
    {
        for (int i = created.Count - 1; i >= 0; i--)
            if (Directory.Exists(created[i]) && !Directory.EnumerateFileSystemEntries(created[i]).Any())
                AssetDatabase.DeleteAsset(created[i]);
    }
}
