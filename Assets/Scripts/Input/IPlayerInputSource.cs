/// <summary>输入源抽象：本地设备、回放、网络均可实现并喂给 InputManager。</summary>
public interface IPlayerInputSource
{
    PlayerInputFrame CaptureFrame();
}
