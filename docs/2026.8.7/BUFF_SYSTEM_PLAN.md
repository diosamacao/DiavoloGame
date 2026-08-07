# Buff / 状态效果方案（终态对齐 GAS-lite）

> 制定：2026-08-07  
> **排期与实现真源：** [GAS_STYLE_COMBAT_REFACTOR_PLAN.md](./GAS_STYLE_COMBAT_REFACTOR_PLAN.md)（G2/G4）  
> 状态：独立 BuffSim **不做**；Effect 已在 GAS G2/G4 落地；G5 完成态无第二套 Buff 权威

---

## 1. 结论

1. **不实现**长期独立的 `CharacterBuffSim` / 第二套状态机。  
2. Buff / Debuff / DOT / 临时加减益一律为 **`EffectDefinition` + `EffectContainer`**。  
3. 字段、叠层、帧推进、与 Pipeline 的关系以 GAS 方案 §5～§6 为准；本文不另立阶段号。

---

## 2. 产品语义 → Effect

| 玩法说法 | Effect 策略 | 备注 |
|----------|-------------|------|
| 限时加攻 / 减伤 | Duration + Flat/Percent Modifier | 到期移除 Modifier |
| 中毒跳伤 | Periodic | **不**触发 Hit Reaction；**不** Grant 资源 |
| 一次性回能 / 扣费 | Instant | 通常由 `ActionResourceSpec` 编译，不单独做「Buff」资产 |
| 完美闪避反击缓冲 | **非 Effect** | `CombatContextFlags`（见 GAS §5.4） |
| 无敌帧 | **非 Effect** | Timeline `Invincible` 相位；Pipeline 早退 |

---

## 3. 禁止

- 新建 `Domain/Combat/Buff` 作为与 Numeric 并行的权威  
- Buff 与 Attribute 双写  
- 用 Periodic 实现接战回能（回能只走 `NumericSystem.Step`）

---

## 4. 一句话

Buff = Effect；无第二套 Buff 模拟器。细节与验收见 GAS-lite 方案 G2/G4/G5。
