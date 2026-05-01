using System;
using NAudio.Wave;

namespace RetroVU.Services;

internal class AudioDateEventArgs : EventArgs // 定义包含了音频瞬时能量的数据事件
{
    public float Power { get; set; }// 当前音频帧的整体能量(均方根平方)
}

internal class AudioAnalyzer : IDisposable
{
    private WasapiLoopbackCapture? _capture;

    // 当提取出音频音量数据时触发此事件
    public event EventHandler<AudioDateEventArgs>? AudioDataAvailable;

    private float _smoothedPower = 0f;

    private DateTime _lastPacketTime = DateTime.Now;

    public void StartCapturing()
    {
        // 初始化系统级音频捕获，获取当前电脑默认扬声器的发声
        _capture = new WasapiLoopbackCapture();

        _capture.DataAvailable += OnDataAvaliable;
        _capture.StartRecording();
    }

    public void StopCapturing()
    {
        if (_capture != null)
        {
            _capture.StopRecording();
            _capture.DataAvailable -= OnDataAvaliable;
            _capture.Dispose();
            _capture = null;
        }
    }
    private void OnDataAvaliable(object? sender, WaveInEventArgs e)
    {
        // 新增防残留逻辑：如果发现距离上一个包超过了 100 毫秒，说明发生了断流
        if ((DateTime.Now - _lastPacketTime).TotalMilliseconds > 100)
        {
            _smoothedPower = -1f;
        }
        _lastPacketTime = DateTime.Now;

        // e.Buffer 包含了原始音频字节流，e.BytesRecorded 表示读取到了多少字节
        // WasapiLoopbackCapture 默认捕获的是 32-bit IEEE Float 格式，并且通常是立体声 (Stereo)

        int bytesPerSample = 4;
        int sampleCount = e.BytesRecorded / bytesPerSample;

        if (sampleCount == 0)
        {
            return;
        }
        float sumSquare = 0f;

        // 遍历并读取每一个样本的 Float 值
        for (int i = 0; i < e.BytesRecorded; i += bytesPerSample)
        {
            // 将 4 个独立字节提取出浮点数
            float sample = BitConverter.ToSingle(e.Buffer, i);

            // 将样本音量进行平方，并累加
            sumSquare += sample * sample;

        }
        // 这里算出来的是几十毫秒内音频的“瞬时能量”
        // 计算当前包音频段段能量平均值 (Mean Square)
        float instantPower = 0f;
        if (sampleCount > 0)
        {
            instantPower = sumSquare / sampleCount;
        }

        // 【积分时间算法】模拟电容充放电
        // alpha系数决定了积分时间长短，值越小指针爬升越慢
        float alpha = 0.2f;

        // 将瞬时能量逐步融合进能量池
        _smoothedPower = _smoothedPower + alpha * (instantPower - _smoothedPower);


        // 将算好的最新音频帧能量发给 Avalonia 的 ViewModel
        AudioDataAvailable?.Invoke(this, new AudioDateEventArgs
        {
            Power = _smoothedPower
        });
    }

    public void Dispose()
    {
        StopCapturing();
    }
}