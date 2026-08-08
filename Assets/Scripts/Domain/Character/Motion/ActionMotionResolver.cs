using UnityEngine;

/// <summary>
/// Wave 4：把 MotionCommand 解析为 Motor 可提交的落点/朝向。
/// 不读表现骨骼；目标仅经 SimActorId + 逻辑 Pose。
/// </summary>
public static class ActionMotionResolver
{
    /// <summary>解析并（可选）直接写入 MotorSim；成功时返回 Applied。</summary>
    public static ActionMotionResolveResult ExecuteCommand(
        MotionCommandNotify command,
        CharacterMotorSim motor,
        ISimCollisionWorld collision,
        in SimCombatPose actorPose,
        SimActorId actionTargetId,
        SimActorId currentLockId,
        IActionMotionWorldQuery worldQuery)
    {
        if (command == null || motor == null || collision == null || worldQuery == null)
            return ActionMotionResolveResult.NotApplied;

        if (command.CommandType == MotionCommandType.SnapFacingToTarget)
            return ExecuteSnapFacing(
                command,
                motor,
                in actorPose,
                actionTargetId,
                currentLockId,
                worldQuery);

        if (!TryResolveTargetPose(
                command.TargetSource,
                actionTargetId,
                currentLockId,
                worldQuery,
                out SimCombatPose targetPose))
        {
            return TryFallback(command, motor, collision, in actorPose);
        }

        SimVec2 from = motor.PositionMm;
        if (!TryBuildDesiredMm(command, in targetPose, out int desiredX, out int desiredZ))
            return ActionMotionResolveResult.NotApplied;

        if (!ActionMotionRelocation.TryResolve(
                motor,
                collision,
                desiredX,
                desiredZ,
                command.CollisionPolicy,
                out SimVec2 resolved))
        {
            return TryFallback(command, motor, collision, in actorPose);
        }

        float facing = ActionMotionFacing.ResolveDegrees(
            command.FacingPolicy,
            actorPose.YawDegrees,
            in actorPose,
            in targetPose,
            from,
            resolved);

        Commit(motor, resolved, facing, command.PreserveVertical);

        int suppress = command.CollisionPolicy == MotionCollisionPolicy.IgnoreCharacters
            || command.CollisionPolicy == MotionCollisionPolicy.IgnoreAll
            ? command.SoftBodySuppressFrames
            : 0;

        return new ActionMotionResolveResult(true, resolved, facing, suppress);
    }

    static ActionMotionResolveResult ExecuteSnapFacing(
        MotionCommandNotify command,
        CharacterMotorSim motor,
        in SimCombatPose actorPose,
        SimActorId actionTargetId,
        SimActorId currentLockId,
        IActionMotionWorldQuery worldQuery)
    {
        if (!TryResolveTargetPose(
                command.TargetSource,
                actionTargetId,
                currentLockId,
                worldQuery,
                out SimCombatPose targetPose))
        {
            return ActionMotionResolveResult.NotApplied;
        }

        float facing = ActionMotionFacing.ResolveDegrees(
            MotionFacingPolicy.FaceTarget,
            actorPose.YawDegrees,
            in actorPose,
            in targetPose,
            motor.PositionMm,
            motor.PositionMm);
        motor.SetFacingDegrees(facing);
        return new ActionMotionResolveResult(true, motor.PositionMm, facing, 0);
    }

    static ActionMotionResolveResult TryFallback(
        MotionCommandNotify command,
        CharacterMotorSim motor,
        ISimCollisionWorld collision,
        in SimCombatPose actorPose)
    {
        if (command.FallbackPolicy != MotionFallbackPolicy.UseForwardOffset)
            return ActionMotionResolveResult.NotApplied;

        Vector3 forward = actorPose.Rotation * Vector3.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f)
            return ActionMotionResolveResult.NotApplied;

        forward.Normalize();
        int dist = command.ForwardFallbackMm;
        int desiredX = motor.PositionMm.X + Mathf.RoundToInt(forward.x * dist);
        int desiredZ = motor.PositionMm.Z + Mathf.RoundToInt(forward.z * dist);

        if (!ActionMotionRelocation.TryResolve(
                motor,
                collision,
                desiredX,
                desiredZ,
                MotionCollisionPolicy.FindNearestValid,
                out SimVec2 resolved))
        {
            return ActionMotionResolveResult.NotApplied;
        }

        float facing = ActionMotionFacing.ResolveDegrees(
            command.FacingPolicy,
            actorPose.YawDegrees,
            in actorPose,
            in actorPose,
            motor.PositionMm,
            resolved);
        Commit(motor, resolved, facing, command.PreserveVertical);
        return new ActionMotionResolveResult(true, resolved, facing, 0);
    }

    static bool TryBuildDesiredMm(
        MotionCommandNotify command,
        in SimCombatPose targetPose,
        out int desiredXMm,
        out int desiredZMm)
    {
        desiredXMm = 0;
        desiredZMm = 0;
        Vector3 targetPos = targetPose.Position;
        Quaternion targetRot = targetPose.Rotation;

        Vector3 desiredWorld;
        switch (command.CommandType)
        {
            case MotionCommandType.RelocateBehindTarget:
            {
                // 目标朝向的反方向（一元负号不能作用在 Quaternion 上）
                Vector3 behind = -(targetRot * Vector3.forward);
                behind.y = 0f;
                if (behind.sqrMagnitude < 0.0001f)
                    behind = Vector3.back;
                behind.Normalize();
                Vector3 side = targetRot * Vector3.right;
                side.y = 0f;
                if (side.sqrMagnitude > 0.0001f)
                    side.Normalize();
                else
                    side = Vector3.zero;

                // localOffsetMm：x=目标右向附加，z=沿背后方向附加
                float behindM = MotionQuantization.MmToMeters(command.BehindDistanceMm);
                float sideM = MotionQuantization.MmToMeters(Mathf.RoundToInt(command.LocalOffsetMm.x));
                float alongBehindM = MotionQuantization.MmToMeters(Mathf.RoundToInt(command.LocalOffsetMm.z));
                desiredWorld = targetPos
                    + behind * (behindM + alongBehindM)
                    + side * sideM;
                break;
            }

            case MotionCommandType.RelocateToTargetOffset:
            {
                Vector3 localM = new(
                    MotionQuantization.MmToMeters(Mathf.RoundToInt(command.LocalOffsetMm.x)),
                    0f,
                    MotionQuantization.MmToMeters(Mathf.RoundToInt(command.LocalOffsetMm.z)));
                desiredWorld = targetPos + targetRot * localM;
                break;
            }

            default:
                return false;
        }

        desiredXMm = MotionQuantization.MetersToMm(desiredWorld.x);
        desiredZMm = MotionQuantization.MetersToMm(desiredWorld.z);
        return true;
    }

    static bool TryResolveTargetPose(
        MotionTargetSource source,
        SimActorId actionTargetId,
        SimActorId currentLockId,
        IActionMotionWorldQuery worldQuery,
        out SimCombatPose pose)
    {
        pose = default;
        SimActorId id = source == MotionTargetSource.CurrentLock ? currentLockId : actionTargetId;
        if (!id.IsValid)
        {
            // ActionTarget 无效时回退 CurrentLock，便于未固化也能试招
            if (source == MotionTargetSource.ActionTarget && currentLockId.IsValid)
                id = currentLockId;
            else
                return false;
        }

        return worldQuery.TryGetCommittedCombatPose(id, out pose);
    }

    static void Commit(
        CharacterMotorSim motor,
        SimVec2 resolved,
        float facingDegrees,
        bool preserveVertical)
    {
        if (preserveVertical)
            motor.TeleportMm(resolved.X, resolved.Z);
        else
            motor.TeleportMm(resolved.X, motor.YMm, resolved.Z);

        motor.SetFacingDegrees(facingDegrees);
    }
}
