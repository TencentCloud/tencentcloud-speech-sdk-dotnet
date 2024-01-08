using NAudio.CoreAudioApi;
using NAudio.Wave;
using System.Threading.Channels;


namespace microphone_realtime_asr;


/// <summary>
/// 监听话筒,实时生成16k pcm音频
/// </summary>
public class AudioService
{
    private static WaveFileWriter waveWriter;
    private static WaveInEvent waveIn;

    /// <summary>定义一个100容量的Channel</summary>
    public static Channel<string> channel = Channel.CreateBounded<string>(
                                                new BoundedChannelOptions(100)
                                                {
                                                    FullMode = BoundedChannelFullMode.Wait
                                                });

    /// <summary>设置麦克风音量</summary>
    private static void SetCurrentMicVolume(int volume)
    {
        var enumerator = new MMDeviceEnumerator();
        IEnumerable<MMDevice> captureDevices = enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active).ToArray();
        if (captureDevices.Count() > 0)
        {
            MMDevice mMDevice = captureDevices.ToList()[0];
            mMDevice.AudioEndpointVolume.MasterVolumeLevelScalar = volume / 100.0f;
        }
    }

    /// <summary>
    /// pcms
    /// </summary>
    public static string pcmsPath = Path.Combine(Environment.CurrentDirectory, "pcms");

    public static async Task CreatePcmAudio()
    {
        SetCurrentMicVolume(100);

        #region 获取录音
        // 创建WaveInEvent实例
        waveIn = new WaveInEvent();

        // 设置音频参数
        waveIn.WaveFormat = new WaveFormat(16000, 16, 1); // 16 kHz, 16-bit, mono

        // 设置数据处理事件
        waveIn.DataAvailable += WaveIn_DataAvailable;
        waveIn.RecordingStopped += WaveIn_RecordingStopped;

        // 设置文件名和路径
        string outputPath = $"{DateTime.Now.ToString("yyMMddHHmmss")}.pcm";

        // 创建WaveFileWriter实例
        waveWriter = new WaveFileWriter(outputPath, waveIn.WaveFormat);

        // 设置录音时长，以1秒为例
        int recordingDuration = 1000 * 2; // in milliseconds

        // 开始录音
        waveIn.StartRecording();


        int i = 0;
        // 循环监听
        while (true)
        {
            //i++;
            //Console.WriteLine(i);

            // 等待指定时间
            await Task.Delay(recordingDuration);

            // 停止录音
            waveIn.StopRecording();


            #region 音频复制到其他地方, 解决文件死锁问题
            var finame = waveWriter.Filename;
            var sourceFinamePath = Path.Combine(Environment.CurrentDirectory, finame);

            if (!Directory.Exists(pcmsPath))
            {
                Directory.CreateDirectory(pcmsPath);
            }
            var copyPath = Path.Combine(pcmsPath, finame);
            File.Copy(sourceFinamePath, copyPath);

            //写入channel
            channel.Writer.TryWrite(copyPath);

            #endregion

            waveIn = new WaveInEvent();
            waveIn.WaveFormat = new WaveFormat(16000, 16, 1); // 16 kHz, 16-bit, mono
            waveIn.DataAvailable += WaveIn_DataAvailable;
            waveIn.RecordingStopped += WaveIn_RecordingStopped;

            waveWriter = new WaveFileWriter($"{DateTime.Now.ToString("yyMMddHHmmss")}.pcm", waveIn.WaveFormat);
            waveIn.StartRecording();

            //Console.WriteLine($"Recording... {recordingDuration / 1000} seconds passed.");
        }
        #endregion

    }

    private static void WaveIn_RecordingStopped(object sender, StoppedEventArgs e)
    {
        waveWriter?.Flush();
        waveWriter?.Dispose();
        waveIn.DataAvailable -= WaveIn_DataAvailable;
        waveIn.RecordingStopped -= WaveIn_RecordingStopped;
        waveIn?.Dispose();
    }


    // 处理音频数据的方法
    private static void WaveIn_DataAvailable(object sender, WaveInEventArgs e)
    {
        // 在这里处理音频数据，e.Buffer包含音频数据
        //Console.WriteLine($"Captured {e.BytesRecorded} bytes of audio data.");

        if (waveWriter.CanWrite)
        {
            waveWriter.Write(e.Buffer, 0, e.BytesRecorded);
        }

    }
}


