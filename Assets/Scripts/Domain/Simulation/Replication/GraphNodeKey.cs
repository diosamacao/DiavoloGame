/// <summary>Graph 节点稳定整数键；空名映射 0，非空名用与动作 Catalog 相同的 FNV-1a。</summary>
public static class GraphNodeKey
{
    /// <summary>把节点名哈希为稳定正整数；空串返回 0。</summary>
    public static int FromStableName(string nodeId)
    {
        if (string.IsNullOrEmpty(nodeId))
            return 0;

        unchecked
        {
            int hash = (int)2166136261u;
            for (int i = 0; i < nodeId.Length; i++)
                hash = (hash ^ nodeId[i]) * 16777619;
            if (hash == int.MinValue)
                hash = 1;
            int id = hash < 0 ? -hash : hash;
            return id == 0 ? 1 : id;
        }
    }
}
