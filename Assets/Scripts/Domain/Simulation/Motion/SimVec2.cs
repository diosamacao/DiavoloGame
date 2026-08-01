/// <summary>二维整数向量（毫米级水平位移用 X/Z）。</summary>
public readonly struct SimVec2
{
    /// <summary>本地右向毫米。</summary>
    public readonly int X;

    /// <summary>本地前向毫米。</summary>
    public readonly int Z;

    /// <summary>构造水平毫米位移。</summary>
    public SimVec2(int x, int z)
    {
        X = x;
        Z = z;
    }

    /// <summary>零位移。</summary>
    public static SimVec2 Zero => new(0, 0);
}
