using System;

/// <summary>单 Actor 单逻辑帧的量化输入；固定布局供模拟、回放与网络共用。</summary>
public readonly struct InputFrame : IEquatable<InputFrame>
{
    /// <summary>逻辑帧号。</summary>
    public long Frame { get; }

    /// <summary>输入所属稳定 Actor。</summary>
    public SimActorId ActorId { get; }

    /// <summary>量化移动横轴，范围 [-127, 127]。</summary>
    public sbyte MoveX { get; }

    /// <summary>量化移动纵轴，范围 [-127, 127]。</summary>
    public sbyte MoveY { get; }

    /// <summary>本逻辑帧按下边沿 bitset。</summary>
    public ulong ButtonsPressed { get; }

    /// <summary>本逻辑帧保持状态 bitset。</summary>
    public ulong ButtonsHeld { get; }

    /// <summary>本逻辑帧松开边沿 bitset。</summary>
    public ulong ButtonsReleased { get; }

    /// <summary>把本地移动轴旋转到世界平面的参考偏航，单位 0.1 度，范围 [0,3599]。</summary>
    public ushort MoveReferenceYawQuantized { get; }

    /// <summary>构造完整量化输入帧。</summary>
    public InputFrame(
        long frame,
        SimActorId actorId,
        sbyte moveX,
        sbyte moveY,
        ulong buttonsPressed,
        ulong buttonsHeld,
        ulong buttonsReleased,
        ushort moveReferenceYawQuantized = 0)
    {
        Frame = frame;
        ActorId = actorId;
        MoveX = moveX;
        MoveY = moveY;
        ButtonsPressed = buttonsPressed;
        ButtonsHeld = buttonsHeld;
        ButtonsReleased = buttonsReleased;
        MoveReferenceYawQuantized = moveReferenceYawQuantized;
    }

    /// <summary>创建指定 Actor 与逻辑帧的空输入。</summary>
    public static InputFrame Empty(long frame, SimActorId actorId) =>
        new(frame, actorId, 0, 0, 0ul, 0ul, 0ul);

    /// <summary>查询按钮是否在本逻辑帧按下。</summary>
    public bool WasPressed(InputButton button) =>
        (ButtonsPressed & InputButtonMask.Of(button)) != 0ul;

    /// <summary>查询按钮当前是否保持按住。</summary>
    public bool IsHeld(InputButton button) =>
        (ButtonsHeld & InputButtonMask.Of(button)) != 0ul;

    /// <summary>查询按钮是否在本逻辑帧松开。</summary>
    public bool WasReleased(InputButton button) =>
        (ButtonsReleased & InputButtonMask.Of(button)) != 0ul;

    /// <summary>改写逻辑帧与 Actor，保留轴与按钮；权威用它把远端命令对齐到下一帧。</summary>
    public InputFrame WithIdentity(long frame, SimActorId actorId) =>
        new(
            frame,
            actorId,
            MoveX,
            MoveY,
            ButtonsPressed,
            ButtonsHeld,
            ButtonsReleased,
            MoveReferenceYawQuantized);

    /// <summary>移除指定按钮的按下、保持与松开位；座位级命令消费后不得再进入角色意图。</summary>
    public InputFrame WithoutButton(InputButton button)
    {
        ulong keepMask = ~InputButtonMask.Of(button);
        return new InputFrame(
            Frame,
            ActorId,
            MoveX,
            MoveY,
            ButtonsPressed & keepMask,
            ButtonsHeld & keepMask,
            ButtonsReleased & keepMask,
            MoveReferenceYawQuantized);
    }

    /// <summary>把连续状态延续到下一逻辑帧，并清除 Pressed/Released 边沿。</summary>
    public InputFrame CarryForward(long targetFrame) =>
        new(
            targetFrame,
            ActorId,
            MoveX,
            MoveY,
            0ul,
            ButtonsHeld,
            0ul,
            MoveReferenceYawQuantized);

    /// <summary>合并同一目标逻辑帧的多次渲染采样；边沿 OR，连续状态取最后样本。</summary>
    public InputFrame MergeSample(in InputFrame latest)
    {
        if (Frame != latest.Frame || ActorId != latest.ActorId)
            throw new InvalidOperationException("只能合并同一 Actor、同一逻辑帧的输入样本。");

        return new InputFrame(
            Frame,
            ActorId,
            latest.MoveX,
            latest.MoveY,
            ButtonsPressed | latest.ButtonsPressed,
            latest.ButtonsHeld,
            ButtonsReleased | latest.ButtonsReleased,
            latest.MoveReferenceYawQuantized);
    }

    /// <summary>比较所有序列化字段是否一致。</summary>
    public bool Equals(InputFrame other) =>
        Frame == other.Frame
        && ActorId == other.ActorId
        && MoveX == other.MoveX
        && MoveY == other.MoveY
        && ButtonsPressed == other.ButtonsPressed
        && ButtonsHeld == other.ButtonsHeld
        && ButtonsReleased == other.ButtonsReleased
        && MoveReferenceYawQuantized == other.MoveReferenceYawQuantized;

    /// <summary>比较对象是否为相同输入帧。</summary>
    public override bool Equals(object obj) => obj is InputFrame other && Equals(other);

    /// <summary>按固定字段顺序生成输入帧哈希。</summary>
    public override int GetHashCode()
    {
        unchecked
        {
            int hash = Frame.GetHashCode();
            hash = (hash * 397) ^ ActorId.GetHashCode();
            hash = (hash * 397) ^ MoveX;
            hash = (hash * 397) ^ MoveY;
            hash = (hash * 397) ^ ButtonsPressed.GetHashCode();
            hash = (hash * 397) ^ ButtonsHeld.GetHashCode();
            hash = (hash * 397) ^ ButtonsReleased.GetHashCode();
            hash = (hash * 397) ^ MoveReferenceYawQuantized;
            return hash;
        }
    }
}
