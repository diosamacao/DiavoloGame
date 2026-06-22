/// <summary>架构级业务系统接口；系统由 ACTGameArchitecture 创建并初始化。</summary>
public interface IArchitectureSystem
{
    /// <summary>绑定所属架构入口，供系统内部发送事件或查询其他系统。</summary>
    void Initialize(ACTGameArchitecture architecture);
}
