namespace Framework.UIFramework
{
    public enum UIPanelLifeTime
    {
        SingletonCached,//始终只有一个实例，关闭后隐藏并缓存。
        Pooled,//允许多个实例，关闭后返回对象池。
        Transient//每次打开创建，关闭后销毁。
    }
}
