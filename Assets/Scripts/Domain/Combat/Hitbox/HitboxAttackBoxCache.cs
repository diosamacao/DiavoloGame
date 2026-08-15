using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 按 MotorSim 逻辑根解析攻击盒；跟随挂点每帧重建，世界空间盒在进入窗时冻结。
/// Hitbox 收集与预测卡肉共用，禁止两套构图。
/// </summary>
public sealed class HitboxAttackBoxCache
{
    readonly Transform _root;
    readonly CharacterMotorSim _motorSim;
    readonly CharacterAttachPointResolver _attachPoints;
    readonly Dictionary<int, HitboxOrientedBox> _frozenWorldBoxes = new();
    readonly List<int> _staleFrozenKeys = new();

    /// <summary>绑定角色根、逻辑电机与挂点解析。</summary>
    public HitboxAttackBoxCache(
        Transform root,
        CharacterMotorSim motorSim,
        CharacterAttachPointResolver attachPoints)
    {
        _root = root;
        _motorSim = motorSim ?? throw new System.ArgumentNullException(nameof(motorSim));
        _attachPoints = attachPoints;
    }

    /// <summary>新招或切实例时丢掉冻结盒。</summary>
    public void Clear() => _frozenWorldBoxes.Clear();

    /// <summary>解析攻击盒：跟随挂点则每帧重建；世界空间则进入窗口时冻结。</summary>
    public HitboxOrientedBox Resolve(int hitboxIndex, HitboxNotifyState hitbox)
    {
        if (hitbox.ParentToAttachPoint)
        {
            _frozenWorldBoxes.Remove(hitboxIndex);
            return BuildFollowAttachBox(hitbox);
        }

        if (_frozenWorldBoxes.TryGetValue(hitboxIndex, out HitboxOrientedBox frozen))
            return frozen;

        HitboxOrientedBox captured = BuildFollowAttachBox(hitbox);
        _frozenWorldBoxes[hitboxIndex] = captured;
        return captured;
    }

    /// <summary>窗口已退出的世界空间盒丢弃，下次进入重新捕获。</summary>
    public void Prune(ActionDefinition action, int frame)
    {
        if (_frozenWorldBoxes.Count == 0)
            return;

        HitboxNotifyState[] hitboxes = action != null ? action.HitboxStates : null;
        _staleFrozenKeys.Clear();
        foreach (KeyValuePair<int, HitboxOrientedBox> pair in _frozenWorldBoxes)
        {
            int index = pair.Key;
            if (hitboxes == null
                || index < 0
                || index >= hitboxes.Length
                || hitboxes[index] == null
                || !hitboxes[index].IsActiveAtFrame(frame)
                || hitboxes[index].ParentToAttachPoint)
            {
                _staleFrozenKeys.Add(index);
            }
        }

        for (int i = 0; i < _staleFrozenKeys.Count; i++)
            _frozenWorldBoxes.Remove(_staleFrozenKeys[i]);
    }

    /// <summary>按当前逻辑根 + 挂点局部 TRS 构建跟随盒。</summary>
    public HitboxOrientedBox BuildFollowAttachBox(HitboxNotifyState hitbox)
    {
        float heightY = _root != null ? _root.position.y : 0f;
        SimCombatPose attackerPose = SimCombatPose.FromMotor(_motorSim, heightY);
        return HitboxMath.BuildFromHitboxLogical(
            in attackerPose,
            ResolveAttachLocalPosition(hitbox),
            ResolveAttachLocalRotation(hitbox),
            hitbox);
    }

    Vector3 ResolveAttachLocalPosition(HitboxNotifyState hitbox)
    {
        Transform anchor = ResolveHitboxAnchor(hitbox);
        if (_root == null || anchor == null || anchor == _root)
            return Vector3.zero;

        return _root.InverseTransformPoint(anchor.position);
    }

    Quaternion ResolveAttachLocalRotation(HitboxNotifyState hitbox)
    {
        Transform anchor = ResolveHitboxAnchor(hitbox);
        if (_root == null || anchor == null || anchor == _root)
            return Quaternion.identity;

        return Quaternion.Inverse(_root.rotation) * anchor.rotation;
    }

    Transform ResolveHitboxAnchor(HitboxNotifyState hitbox)
    {
        if (_attachPoints == null)
            return _root;

        return _attachPoints.Resolve(hitbox != null ? hitbox.AttachPointId : null);
    }
}
