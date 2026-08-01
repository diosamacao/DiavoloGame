using System;

/// <summary>InPlace ↔ RootMotion 文件/Clip 命名匹配规则（无 Unity 依赖）。</summary>
public static class MotionClipNameRules
{
    public const int MinStemLengthForPartial = 3;

    static readonly string[] InplaceSuffixes =
    {
        "_Inplace",
        "_InPlace",
        "_inplace",
    };

    /// <summary>从 InPlace Clip/文件名剥离后缀得到 stem；不合规返回 false。</summary>
    public static bool TryGetInplaceStem(string clipOrFileName, out string stem)
    {
        stem = null;
        if (string.IsNullOrWhiteSpace(clipOrFileName))
            return false;

        string name = clipOrFileName.Trim();
        int slash = Math.Max(name.LastIndexOf('/'), name.LastIndexOf('\\'));
        if (slash >= 0)
            name = name.Substring(slash + 1);

        int dot = name.LastIndexOf('.');
        if (dot > 0)
            name = name.Substring(0, dot);

        for (int i = 0; i < InplaceSuffixes.Length; i++)
        {
            string suffix = InplaceSuffixes[i];
            if (name.Length > suffix.Length
                && name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                stem = name.Substring(0, name.Length - suffix.Length);
                return stem.Length > 0;
            }
        }

        return false;
    }

    /// <summary>
    /// 匹配优先级：P0 全名相等；P1 以 |stem 结尾；P2 资源文件名等于 stem；
    /// P3 去掉角色前缀后 token 等于 stem。未命中返回 -1；歧义由调用方收集同级多命中处理。
    /// </summary>
    public static int GetMatchPriority(string stem, string rootMotionClipName, string rootMotionFileName)
    {
        if (string.IsNullOrEmpty(stem))
            return -1;

        string clipName = rootMotionClipName ?? string.Empty;
        string fileName = rootMotionFileName ?? string.Empty;

        if (string.Equals(clipName, stem, StringComparison.Ordinal))
            return 0;

        if (clipName.EndsWith("|" + stem, StringComparison.Ordinal))
            return 1;

        if (!string.IsNullOrEmpty(fileName)
            && string.Equals(fileName, stem, StringComparison.Ordinal))
            return 2;

        // 短 stem 禁止模糊 P3，避免误伤
        if (stem.Length < MinStemLengthForPartial)
            return -1;

        string token = clipName;
        int bar = token.LastIndexOf('|');
        if (bar >= 0 && bar + 1 < token.Length)
            token = token.Substring(bar + 1);

        if (string.Equals(token, stem, StringComparison.Ordinal))
            return 3;

        return -1;
    }
}
