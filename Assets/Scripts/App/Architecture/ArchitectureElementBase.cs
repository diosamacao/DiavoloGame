/// <summary>架构对象基类，统一保存所属 ACTGameArchitecture 引用。</summary>
public abstract class ArchitectureElementBase : IBelongToArchitecture, ICanSetArchitecture
{
    ACTGameArchitecture _architecture;

    /// <summary>当前对象所属的架构入口；尚未注入时回退到全局入口。</summary>
    protected ACTGameArchitecture Architecture => GetArchitecture();

    /// <summary>返回当前对象所属的架构入口。</summary>
    public ACTGameArchitecture GetArchitecture()
    {
        return _architecture ?? ACTGameArchitecture.Interface;
    }

    /// <summary>绑定当前对象所属的架构入口。</summary>
    public void SetArchitecture(ACTGameArchitecture architecture)
    {
        _architecture = architecture;
    }
}
