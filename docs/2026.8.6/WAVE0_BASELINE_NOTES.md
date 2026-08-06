# Wave 0 / Wave 1 基准样例记录（人工）

> 用于 Wave 1（ForwardSigned）与 Wave 2（残差拆分）前后对照横摆是否仍进 `MotorSim`。

## 操作

1. 菜单 `ACTGame/Action/Validate Motion Sources`，归档 Conflict 列表。
2. 选 1～2 条正式普攻/连招（如 Attack5）。
3. Play：开 `CombatDebugHudController`，打完整招式，记下 `ActionLateralPeakMm`。
4. Scene 勾选 Action「Show Baked Trajectory」，对照橙/青轨与 Motor 绿圆是否同步横跳。
5. **Wave 1：** Planar Mode = `ForwardSigned` → Bake → 再记一次 `ActionLateralPeakMm`（期望 ≈ 0）。
6. 菜单 `ACTGame/Action/Migrate Base Motion Mode`（可先 Dry-Run）。

## 记录表

| 日期 | Action 资产名 | PlanarMode | ActionLateralPeakMm | 备注 |
|------|---------------|------------|---------------------|------|
| | Attack5 | FullPlanar? | | Wave0 基线 |
| | Attack5 | ForwardSigned | | Wave1 重烘焙后 |
