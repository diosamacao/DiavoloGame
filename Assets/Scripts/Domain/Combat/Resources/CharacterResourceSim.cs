using System;

/// <summary>
/// 玩法资源权威：Energy / Decibel / DodgeCharges；仅逻辑帧 Step，禁止 Time.deltaTime。
/// </summary>
public sealed class CharacterResourceSim
{
    readonly CharacterResourceConfig _config;
    int _energyMilli;
    int _decibel;
    int _dodgeCharges;
    int _dodgeRechargeFramesLeft;
    int _inCombatHoldFrames;

    /// <summary>按配置创建满能量、满闪避、零喧响的资源模拟。</summary>
    public CharacterResourceSim(CharacterResourceConfig config)
    {
        _config = config ?? CharacterResourceConfig.Default;
        _energyMilli = _config.MaxEnergy * 1000;
        _decibel = 0;
        _dodgeCharges = _config.MaxDodgeCharges;
        _dodgeRechargeFramesLeft = 0;
        _inCombatHoldFrames = 0;
    }

    public int EnergyMilli => _energyMilli;
    public int EnergyPoints => _energyMilli / 1000;
    public int MaxEnergy => _config.MaxEnergy;
    public int Decibel => _decibel;
    public int MaxDecibel => _config.MaxDecibel;
    public int DodgeCharges => _dodgeCharges;
    public int MaxDodgeCharges => _config.MaxDodgeCharges;
    public int DodgeRechargeFramesLeft => _dodgeRechargeFramesLeft;
    public int InCombatHoldFrames => _inCombatHoldFrames;
    public bool IsInCombat => _inCombatHoldFrames > 0;
    public int EnergyRegenMilliPerFrame => _config.EnergyRegenMilliPerFrame;

    /// <summary>标记接战（动作/受击/命中），刷新被动回能门闩。</summary>
    public void NotifyInCombat()
    {
        _inCombatHoldFrames = _config.CombatHoldFrames;
    }

    /// <summary>是否负担得起价签（不扣费）。</summary>
    public bool CanAfford(ActionResourceSpec spec)
    {
        if (spec == null)
            return true;

        if (spec.EnergyCost > 0 && EnergyPoints < spec.EnergyCost)
            return false;

        if (spec.ConsumeDodgeCharge && _dodgeCharges <= 0)
            return false;

        if (spec.RequiresDecibelFull && _decibel < _config.MaxDecibel)
            return false;

        return true;
    }

    /// <summary>起手扣费；调用前须 CanAfford。会刷新接战门闩。</summary>
    public void CommitCost(ActionResourceSpec spec)
    {
        if (spec == null)
            return;

        if (spec.EnergyCost > 0)
            _energyMilli = Math.Max(0, _energyMilli - spec.EnergyCost * 1000);

        if (spec.ConsumeDodgeCharge && _dodgeCharges > 0)
        {
            _dodgeCharges--;
            if (_dodgeCharges < _config.MaxDodgeCharges && _dodgeRechargeFramesLeft <= 0)
                _dodgeRechargeFramesLeft = _config.DodgeRechargeFrames;
        }

        if (spec.ClearsDecibelOnStart)
            _decibel = 0;

        NotifyInCombat();
    }

    /// <summary>ConfirmHit 后回填；挥空不得调用。</summary>
    public void GrantOnHit(ActionResourceSpec spec)
    {
        if (spec == null)
            return;

        if (spec.EnergyGrantOnHit > 0)
        {
            _energyMilli = Math.Min(
                _config.MaxEnergy * 1000,
                _energyMilli + spec.EnergyGrantOnHit * 1000);
        }

        if (spec.DecibelGrantOnHit > 0)
            _decibel = Math.Min(_config.MaxDecibel, _decibel + spec.DecibelGrantOnHit);

        NotifyInCombat();
    }

    /// <summary>
    /// 推进 1 逻辑帧：接战回能、闪避充能。
    /// freezeFrames&gt;0 时应由调用方跳过本方法（卡肉暂停回复）。
    /// </summary>
    public void Step()
    {
        if (_inCombatHoldFrames > 0)
        {
            _inCombatHoldFrames--;
            if (_config.EnergyRegenMilliPerFrame > 0 && _energyMilli < _config.MaxEnergy * 1000)
            {
                _energyMilli = Math.Min(
                    _config.MaxEnergy * 1000,
                    _energyMilli + _config.EnergyRegenMilliPerFrame);
            }
        }

        if (_dodgeCharges >= _config.MaxDodgeCharges)
        {
            _dodgeRechargeFramesLeft = 0;
            return;
        }

        if (_dodgeRechargeFramesLeft > 0)
        {
            _dodgeRechargeFramesLeft--;
            if (_dodgeRechargeFramesLeft <= 0)
            {
                _dodgeCharges++;
                if (_dodgeCharges < _config.MaxDodgeCharges)
                    _dodgeRechargeFramesLeft = _config.DodgeRechargeFrames;
            }
        }
    }
}
