/// <summary>一次动作解析请求：只描述设备无关的玩法意图。</summary>
public readonly struct ActionRequest
{
    /// <summary>构造动作解析请求。</summary>
    public ActionRequest(GameplayIntentType intent)
    {
        Intent = intent;
    }

    /// <summary>设备无关的动作语义。</summary>
    public GameplayIntentType Intent { get; }

    /// <summary>None 不参与动作解析。</summary>
    public bool IsValid => Intent != GameplayIntentType.None;
}
