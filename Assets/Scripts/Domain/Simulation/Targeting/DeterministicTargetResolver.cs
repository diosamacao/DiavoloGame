using System;
using System.Collections.Generic;

/// <summary>仅基于量化逻辑快照维护唯一目标，并按稳定方位规则切换。</summary>
public static class DeterministicTargetResolver
{
    const double FullCircle = 360d;
    const double AngleEpsilon = 0.000001d;

    /// <summary>解析本帧 SelectedTargetId；候选传入顺序不会改变结果。</summary>
    public static SimActorId Resolve(
        in SimTargetResolveRequest request,
        IReadOnlyList<SimTargetCandidate> candidates)
    {
        if (candidates == null || candidates.Count == 0)
            return SimActorId.Invalid;

        bool currentValid = TryFindEligible(
            request.CurrentSelectedTargetId,
            request.RetainRangeMm,
            in request,
            candidates,
            out SimTargetCandidate current);
        if (!currentValid)
            return AcquireNearest(in request, candidates);

        if (request.SwitchDirection == TargetSwitchDirection.None)
            return current.ActorId;

        return SwitchFromCurrent(in request, in current, candidates);
    }

    /// <summary>当前目标无效时按距离平方、ActorId 获取稳定最近目标。</summary>
    static SimActorId AcquireNearest(
        in SimTargetResolveRequest request,
        IReadOnlyList<SimTargetCandidate> candidates)
    {
        SimActorId bestId = SimActorId.Invalid;
        long bestDistanceSq = long.MaxValue;
        int rangeMm = Math.Max(0, request.AcquireRangeMm);

        for (int i = 0; i < candidates.Count; i++)
        {
            SimTargetCandidate candidate = candidates[i];
            if (!TryGetEligibleDistanceSq(in candidate, rangeMm, in request, out long distanceSq))
                continue;

            if (distanceSq < bestDistanceSq
                || (distanceSq == bestDistanceSq
                    && (!bestId.IsValid || candidate.ActorId.CompareTo(bestId) < 0)))
            {
                bestId = candidate.ActorId;
                bestDistanceSq = distanceSq;
            }
        }

        return bestId;
    }

    /// <summary>以当前目标角为起点沿指定方向环绕，按角差、距离、ActorId 选下一目标。</summary>
    static SimActorId SwitchFromCurrent(
        in SimTargetResolveRequest request,
        in SimTargetCandidate current,
        IReadOnlyList<SimTargetCandidate> candidates)
    {
        double referenceYaw = InputQuantizer.DequantizeYaw(request.MoveReferenceYawQuantized);
        double currentAngle = ResolveRelativeAngle(in current, in request, referenceYaw);
        SimActorId bestId = current.ActorId;
        double bestAngularDelta = double.MaxValue;
        long bestDistanceSq = long.MaxValue;
        int rangeMm = Math.Max(0, request.AcquireRangeMm);

        for (int i = 0; i < candidates.Count; i++)
        {
            SimTargetCandidate candidate = candidates[i];
            if (candidate.ActorId == current.ActorId
                || !TryGetEligibleDistanceSq(in candidate, rangeMm, in request, out long distanceSq))
            {
                continue;
            }

            double candidateAngle = ResolveRelativeAngle(in candidate, in request, referenceYaw);
            double angularDelta = request.SwitchDirection == TargetSwitchDirection.Right
                ? NormalizePositive(candidateAngle - currentAngle)
                : NormalizePositive(currentAngle - candidateAngle);
            if (angularDelta <= AngleEpsilon)
                angularDelta = FullCircle;

            if (angularDelta < bestAngularDelta - AngleEpsilon
                || (Math.Abs(angularDelta - bestAngularDelta) <= AngleEpsilon
                    && (distanceSq < bestDistanceSq
                        || (distanceSq == bestDistanceSq
                            && candidate.ActorId.CompareTo(bestId) < 0))))
            {
                bestId = candidate.ActorId;
                bestAngularDelta = angularDelta;
                bestDistanceSq = distanceSq;
            }
        }

        return bestId;
    }

    /// <summary>查找并验证指定目标是否仍处于保持范围。</summary>
    static bool TryFindEligible(
        SimActorId actorId,
        int rangeMm,
        in SimTargetResolveRequest request,
        IReadOnlyList<SimTargetCandidate> candidates,
        out SimTargetCandidate result)
    {
        for (int i = 0; i < candidates.Count; i++)
        {
            SimTargetCandidate candidate = candidates[i];
            if (candidate.ActorId != actorId
                || !TryGetEligibleDistanceSq(in candidate, rangeMm, in request, out _))
            {
                continue;
            }

            result = candidate;
            return true;
        }

        result = default;
        return false;
    }

    /// <summary>统一排除自身、同阵营、死亡、无效身份与超距候选。</summary>
    static bool TryGetEligibleDistanceSq(
        in SimTargetCandidate candidate,
        int rangeMm,
        in SimTargetResolveRequest request,
        out long distanceSq)
    {
        long dx = (long)candidate.XMm - request.OriginXMm;
        long dz = (long)candidate.ZMm - request.OriginZMm;
        distanceSq = dx * dx + dz * dz;
        long rangeSq = (long)Math.Max(0, rangeMm) * Math.Max(0, rangeMm);
        return candidate.ActorId.IsValid
            && candidate.ActorId != request.RequesterId
            && candidate.TeamId != request.RequesterTeamId
            && candidate.IsAlive
            && distanceSq > 0
            && distanceSq <= rangeSq;
    }

    /// <summary>返回候选相对 MoveReferenceYaw 的有符号水平角（右为正）。</summary>
    static double ResolveRelativeAngle(
        in SimTargetCandidate candidate,
        in SimTargetResolveRequest request,
        double referenceYawDegrees)
    {
        double worldAngle = Math.Atan2(
            (long)candidate.XMm - request.OriginXMm,
            (long)candidate.ZMm - request.OriginZMm) * (180d / Math.PI);
        return NormalizeSigned(worldAngle - referenceYawDegrees);
    }

    static double NormalizePositive(double degrees)
    {
        double normalized = degrees % FullCircle;
        return normalized < 0d ? normalized + FullCircle : normalized;
    }

    static double NormalizeSigned(double degrees)
    {
        double normalized = NormalizePositive(degrees);
        return normalized >= 180d ? normalized - FullCircle : normalized;
    }
}
