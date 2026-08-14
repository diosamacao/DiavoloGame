using System;

/// <summary>
/// 预测位移的无引擎步进：InputFrame 本地轴 × MoveReferenceYaw → wish，
/// 再按 FollowInput（朝向 SmoothDamp 追 wish，位移沿更新后的朝向）写入 MotorSim。
/// 不重跑 Locomotion FSM / 烘焙根运动。
/// </summary>
public static class PredictedLocomotionMath
{
    /// <summary>与 InputManager.HasMoveIntent 对齐：量化轴平方和低于此值视为无移动。</summary>
    public const int MoveIntentMagnitudeSqMin = 162;

    /// <summary>
    /// 把 InputFrame 应用到电机：FollowInput 水平位移 + 竖直 Tick。
    /// planarSpeedMm&gt;0 时用该速度，否则按输入幅度在走/跑之间选择。
    /// </summary>
    public static void ApplyInput(
        CharacterMotorSim motor,
        in InputFrame input,
        in PredictedLocomotionConfig config,
        ref float facingVelocityDeg,
        int planarSpeedMm = 0)
    {
        if (motor == null)
            throw new ArgumentNullException(nameof(motor));

        int magSq = (int)input.MoveX * input.MoveX + (int)input.MoveY * input.MoveY;
        if (magSq >= MoveIntentMagnitudeSqMin)
        {
            int yawMilliDeg = input.MoveReferenceYawQuantized * 100;
            CharacterMotorSim.RotateLocalToWorld(
                yawMilliDeg,
                input.MoveX,
                input.MoveY,
                out int wishX,
                out int wishZ);

            double wishLen = Math.Sqrt((double)wishX * wishX + (double)wishZ * wishZ);
            if (wishLen >= 1.0)
            {
                double mag01 = Math.Min(1.0, Math.Sqrt(magSq) / InputQuantizer.AxisScale);
                int speedMm = planarSpeedMm > 0
                    ? planarSpeedMm
                    : (mag01 * 1000.0 >= config.RunThresholdMilli
                        ? config.RunSpeedMm
                        : config.WalkSpeedMm);
                int distMm = (int)Math.Round(
                    speedMm * mag01 / config.LogicHz,
                    MidpointRounding.AwayFromZero);

                float targetDeg = (float)(Math.Atan2(wishX, wishZ) * (180.0 / Math.PI));
                float currentDeg = MotionQuantization.MilliDegToDegrees(motor.FacingMilliDeg);
                float dt = 1f / config.LogicHz;
                currentDeg = SmoothDampAngle(
                    currentDeg,
                    targetDeg,
                    ref facingVelocityDeg,
                    config.RotationSmoothTimeSeconds,
                    dt);
                motor.SetFacingMilliDeg(MotionQuantization.DegreesToMilliDeg(currentDeg));

                // 与权威 FollowInput 相同：先转再沿朝向走，W→WD 走出弧线而不是瞬时横移
                double rad = currentDeg * (Math.PI / 180.0);
                int dx = (int)Math.Round(Math.Sin(rad) * distMm, MidpointRounding.AwayFromZero);
                int dz = (int)Math.Round(Math.Cos(rad) * distMm, MidpointRounding.AwayFromZero);
                motor.TryMoveWorldMm(dx, dz);
            }
        }

        motor.TickVertical();
    }

    /// <summary>水平毫米距离；用于纠偏阈值。</summary>
    public static int PlanarErrorMm(
        int axMm,
        int azMm,
        int bxMm,
        int bzMm)
    {
        long dx = axMm - bxMm;
        long dz = azMm - bzMm;
        return (int)Math.Round(Math.Sqrt(dx * dx + dz * dz), MidpointRounding.AwayFromZero);
    }

    /// <summary>最短角距后的 SmoothDamp，语义对齐 Unity SmoothDampAngle（无引擎依赖）。</summary>
    public static float SmoothDampAngle(
        float currentDeg,
        float targetDeg,
        ref float currentVelocityDeg,
        float smoothTimeSeconds,
        float deltaTimeSeconds)
    {
        if (smoothTimeSeconds <= 0.001f || deltaTimeSeconds <= 0f)
        {
            currentVelocityDeg = 0f;
            return currentDeg + DeltaAngle(currentDeg, targetDeg);
        }

        float wrappedTarget = currentDeg + DeltaAngle(currentDeg, targetDeg);
        return SmoothDamp(
            currentDeg,
            wrappedTarget,
            ref currentVelocityDeg,
            smoothTimeSeconds,
            deltaTimeSeconds);
    }

    static float DeltaAngle(float currentDeg, float targetDeg)
    {
        float diff = (targetDeg - currentDeg) % 360f;
        if (diff > 180f)
            diff -= 360f;
        if (diff < -180f)
            diff += 360f;
        return diff;
    }

    /// <summary>临界阻尼 SmoothDamp（Game Programming Gems / Unity 同族），供转向复用。</summary>
    static float SmoothDamp(
        float current,
        float target,
        ref float currentVelocity,
        float smoothTime,
        float deltaTime)
    {
        smoothTime = Math.Max(0.0001f, smoothTime);
        float omega = 2f / smoothTime;
        float x = omega * deltaTime;
        float exp = 1f / (1f + x + 0.48f * x * x + 0.235f * x * x * x);
        float change = current - target;
        float temp = (currentVelocity + omega * change) * deltaTime;
        currentVelocity = (currentVelocity - omega * temp) * exp;
        float output = target + (change + temp) * exp;

        // 越过目标则贴死，避免振荡
        if ((target - current > 0f) == (output > target))
        {
            output = target;
            currentVelocity = 0f;
        }

        return output;
    }
}
