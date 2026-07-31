using System;

/// <summary>将固定模拟帧稳定换算为动作采样帧，不使用浮点累计时间。</summary>
public struct ActionFrameClock
{
    int _currentFrame;
    int _sampleRemainder;

    /// <summary>当前动作帧；等于 totalFrames 时表示完整时长已经结束。</summary>
    public int CurrentFrame => _currentFrame;

    /// <summary>重置到动作第 0 帧，并清空跨模拟帧的采样余数。</summary>
    public void Reset()
    {
        _currentFrame = 0;
        _sampleRemainder = 0;
    }

    /// <summary>
    /// 推进一个模拟帧并返回跨过的动作帧数；整数余数保证低采样率动作保持原时长。
    /// </summary>
    public int Advance(int actionSampleRate, int simulationRate, int totalFrames)
    {
        if (actionSampleRate <= 0)
            throw new ArgumentOutOfRangeException(nameof(actionSampleRate));
        if (simulationRate <= 0)
            throw new ArgumentOutOfRangeException(nameof(simulationRate));
        if (totalFrames <= 0 || _currentFrame >= totalFrames)
            return 0;

        _sampleRemainder += actionSampleRate;
        int advancedFrames = _sampleRemainder / simulationRate;
        _sampleRemainder %= simulationRate;
        if (advancedFrames <= 0)
            return 0;

        int previousFrame = _currentFrame;
        _currentFrame = Math.Min(totalFrames, _currentFrame + advancedFrames);
        return _currentFrame - previousFrame;
    }
}
