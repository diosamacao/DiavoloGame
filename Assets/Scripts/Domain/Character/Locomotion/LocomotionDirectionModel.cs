using UnityEngine;

/// <summary>
/// 本地移动意图 → MoveCardinal；死区内 None，对角线取主导轴。
/// 不含滞回（滞回在 L-DIR2 Gait 循环接入）。
/// </summary>
public static class LocomotionDirectionModel
{
    /// <summary>默认死区：低于此幅度视为无向。</summary>
    public const float DefaultEpsilon = 0.2f;

    /// <summary>由本地 xz 意图解析 cardinal；|intent|&lt;ε → None。</summary>
    public static MoveCardinal Resolve(Vector2 localMoveIntent, float epsilon = DefaultEpsilon)
    {
        float eps = Mathf.Max(0.01f, epsilon);
        float ax = Mathf.Abs(localMoveIntent.x);
        float ay = Mathf.Abs(localMoveIntent.y);
        if (ax < eps && ay < eps)
            return MoveCardinal.None;

        // 主导轴：相等时优先前后（与旧横向「须 ax≥ay」一致：横向需严格主导才 Left/Right）
        if (ax > ay)
            return localMoveIntent.x < 0f ? MoveCardinal.Left : MoveCardinal.Right;

        return localMoveIntent.y < 0f ? MoveCardinal.Back : MoveCardinal.Forward;
    }

    /// <summary>
    /// 世界 wish → 角色本地移动意图（x=右、y=前）；供 FaceTarget/FaceCamera 选片。
    /// </summary>
    public static Vector2 ToLocalMoveIntent(Vector3 worldWish, Vector3 facingForward)
    {
        worldWish.y = 0f;
        facingForward.y = 0f;
        if (worldWish.sqrMagnitude < 0.0001f || facingForward.sqrMagnitude < 0.0001f)
            return Vector2.zero;

        Vector3 forward = facingForward.normalized;
        Vector3 right = Vector3.Cross(Vector3.up, forward);
        if (right.sqrMagnitude < 0.0001f)
            return Vector2.zero;
        right.Normalize();

        float x = Vector3.Dot(worldWish.normalized, right);
        float y = Vector3.Dot(worldWish.normalized, forward);
        return new Vector2(x, y);
    }
}
