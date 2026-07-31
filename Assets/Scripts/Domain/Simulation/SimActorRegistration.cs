/// <summary>Controller 持有的 SimulationWorld 注册句柄，用于对称注销。</summary>
public readonly struct SimActorRegistration
{
    /// <summary>无效注册句柄。</summary>
    public static SimActorRegistration Invalid => default;

    /// <summary>World 分配的稳定 Actor 标识。</summary>
    public SimActorId Id { get; }

    /// <summary>句柄当前是否表示有效注册。</summary>
    public bool IsValid => Id.IsValid;

    /// <summary>从 World 分配的稳定 Id 创建注册句柄。</summary>
    public SimActorRegistration(SimActorId id)
    {
        Id = id;
    }
}
