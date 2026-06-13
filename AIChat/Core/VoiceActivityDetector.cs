using System;
using System.Collections.Generic;
using AIChat.Utils;

namespace AIChat.Core
{
    /// <summary>
    /// 轻量客户端 VAD（Voice Activity Detection）。
    /// 基于短时 RMS 能量阈值，状态机：Idle -> Speech -> Idle。
    /// 当检测到一段有效语音结束时，触发 SpeechSegmentReady 事件返回 float 采样数组。
    /// </summary>
    public class VoiceActivityDetector
    {
        public enum VadState
        {
            Idle,
            Speech
        }

        public VadState State { get; private set; } = VadState.Idle;

        /// <summary>
        /// 当前帧的能量（RMS），范围大致 0.0 ~ 1.0。
        /// </summary>
        public float CurrentEnergy { get; private set; } = 0f;

        /// <summary>
        /// 检测到的有效语音段已准备就绪。
        /// 参数为 16kHz 单声道 float 采样数组（范围 -1.0 ~ 1.0）。
        /// </summary>
        public event Action<float[]> SpeechSegmentReady;

        private readonly int _sampleRate;
        private readonly float _threshold;
        private float _endThreshold;
        private readonly float _endThresholdRatio;
        private readonly int _minSpeechFrames;
        private readonly int _silenceFrames;
        private readonly int _maxSpeechFrames;
        private readonly int _frameSize;

        private int _speechFrames;
        private int _silenceFramesCount;
        private List<float> _speechBuffer;

        // 调试用：每约 1 秒输出一次当前能量和状态
        private int _frameCounter;
        private readonly int _logIntervalFrames;

        /// <summary>
        /// </summary>
        /// <param name="sampleRate">采样率，如 16000。</param>
        /// <param name="threshold">语音能量阈值（RMS），默认 0.02。</param>
        /// <param name="minSpeechSeconds">最短有效语音时长，过滤噪声。</param>
        /// <param name="silenceSeconds">停顿多久判定为说话结束。</param>
        /// <param name="maxSpeechSeconds">单段语音最大时长，超时强制结束。</param>
        /// <param name="frameSeconds">每帧时长，默认 0.02（20ms）。</param>
        /// <param name="endThresholdRatio">结束阈值相对阈值的比例（形成迟滞，避免抖动），默认 0.5。</param>
        public VoiceActivityDetector(
            int sampleRate,
            float threshold,
            float minSpeechSeconds,
            float silenceSeconds,
            float maxSpeechSeconds = 30f,
            float frameSeconds = 0.02f,
            float endThresholdRatio = 0.5f)
        {
            _sampleRate = sampleRate;
            _threshold = threshold;
            _endThresholdRatio = Math.Max(0.01f, Math.Min(1.0f, endThresholdRatio));
            // 结束阈值略低，形成迟滞，避免能量在阈值边缘抖动
            _endThreshold = threshold * _endThresholdRatio;
            _frameSize = Math.Max(1, (int)(sampleRate * frameSeconds));
            _minSpeechFrames = Math.Max(1, (int)(minSpeechSeconds / frameSeconds));
            _silenceFrames = Math.Max(1, (int)(silenceSeconds / frameSeconds));
            _maxSpeechFrames = Math.Max(1, (int)(maxSpeechSeconds / frameSeconds));
            // 约 1 秒输出一次调试日志（20ms 一帧 => 50 帧）
            _logIntervalFrames = Math.Max(10, (int)(1.0f / frameSeconds));
        }

        public void Reset()
        {
            State = VadState.Idle;
            CurrentEnergy = 0f;
            _speechFrames = 0;
            _silenceFramesCount = 0;
            _frameCounter = 0;
            _speechBuffer?.Clear();
        }

        /// <summary>
        /// 处理一帧采样。会在内部按 frameSeconds 分帧处理。
        /// </summary>
        public void Process(float[] samples)
        {
            if (samples == null || samples.Length == 0) return;

            int offset = 0;
            while (offset < samples.Length)
            {
                int remaining = samples.Length - offset;
                int chunkSize = Math.Min(remaining, _frameSize);
                ProcessFrame(samples, offset, chunkSize);
                offset += chunkSize;
            }
        }

        private void ProcessFrame(float[] samples, int offset, int length)
        {
            float sum = 0f;
            for (int i = 0; i < length; i++)
            {
                float v = samples[offset + i];
                sum += v * v;
            }
            CurrentEnergy = (length > 0) ? (float)Math.Sqrt(sum / length) : 0f;

            switch (State)
            {
                case VadState.Idle:
                    if (CurrentEnergy >= _threshold)
                    {
                        _speechFrames++;
                        if (_speechFrames >= _minSpeechFrames)
                        {
                            State = VadState.Speech;
                            _speechBuffer = new List<float>(_frameSize * _silenceFrames * 2);
                            _silenceFramesCount = 0;
                            Log.Info($"[VAD] 检测到语音开始，能量 {CurrentEnergy:F4} >= 阈值 {_threshold:F4}");
                            // 把触发前的有效帧也补进去（因为 minSpeechFrames 已经满足阈值）
                            AppendCurrentFrame(samples, offset, length);
                        }
                    }
                    else
                    {
                        _speechFrames = 0;
                    }
                    break;

                case VadState.Speech:
                    AppendCurrentFrame(samples, offset, length);
                    _speechFrames++;

                    if (CurrentEnergy < _endThreshold)
                    {
                        _silenceFramesCount++;
                        if (_silenceFramesCount >= _silenceFrames)
                        {
                            FinalizeSpeechSegment();
                        }
                    }
                    else
                    {
                        _silenceFramesCount = 0;
                    }

                    if (_speechFrames >= _maxSpeechFrames)
                    {
                        Log.Info("[VAD] 单段语音超过最大时长，强制结束");
                        FinalizeSpeechSegment();
                    }
                    break;
            }

            _frameCounter++;
            if (_frameCounter % _logIntervalFrames == 0)
            {
                Log.Info($"[VAD Debug] 状态={State}, 能量={CurrentEnergy:F4}, 阈值={_threshold:F4}");
            }
        }

        private void AppendCurrentFrame(float[] samples, int offset, int length)
        {
            if (_speechBuffer == null) return;
            for (int i = 0; i < length; i++)
            {
                _speechBuffer.Add(samples[offset + i]);
            }
        }

        private void FinalizeSpeechSegment()
        {
            State = VadState.Idle;
            _speechFrames = 0;
            _silenceFramesCount = 0;

            if (_speechBuffer != null && _speechBuffer.Count > 0)
            {
                float[] segment = _speechBuffer.ToArray();
                _speechBuffer.Clear();
                try
                {
                    SpeechSegmentReady?.Invoke(segment);
                }
                catch (Exception ex)
                {
                    Log.Error($"[VAD] SpeechSegmentReady 回调异常: {ex.Message}");
                }
            }
        }
    }
}
