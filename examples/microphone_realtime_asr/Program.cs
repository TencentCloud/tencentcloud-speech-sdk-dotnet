using asr;
using System.Text.Json;

namespace microphone_realtime_asr
{
    class Program
    {
        private static ASRController ctl = null;
        private static Dictionary<string, JsonElement> config_json = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(File.ReadAllBytes("config.json"));

        static async Task Main()
        {

            _ = Task.Run(async () => { await AudioService.CreatePcmAudio(); });

            Console.WriteLine();

            // 消费者消费数据
            // 模式一：一次读一个
            while (await AudioService.channel.Reader.WaitToReadAsync())
            {
                ctl = GetCtl();
                while (AudioService.channel.Reader.TryRead(out var item))
                {
                    await ProcessFilesAsync(ctl, item);
                }
            }
        }


        static ASRController GetCtl()
        {
            var builder = new asr.ASRController.Builder();
            builder.appid = config_json["appid"].GetString();
            builder.secretid = config_json["secretid"].GetString();
            builder.secretkey = config_json["secretkey"].GetString();
            builder.needvad = 1;
            ctl = builder.build();
            return ctl;
        }


        /// <summary>麦克风生成的音频调用腾讯的asr接口实时听写</summary>
        static async Task ProcessFilesAsync(asr.ASRController ctl, string pcmPath)
        {
            var fi = new FileInfo(pcmPath);

            if (fi != null && fi.Exists)
            {
                Console.WriteLine($"current pcm is {fi.Name}");

                using (FileStream fs = File.OpenRead(fi.FullName))
                {
                    try
                    {
                        await ctl.startAsync(async (byte[] buf, CancellationToken token) =>
                        {
                            var count = await fs.ReadAsync(buf, 0, buf.Length, token);
                            if (count <= 0)
                            {
                                await ctl.stopAsync();
                            }
                        }, async (String msg) =>
                        {
                            await Console.Out.WriteLineAsync(msg);
                        });
                        await ctl.waitAsync();
                    }
                    catch (TaskCanceledException e)
                    {
                        //Console.WriteLine(e.ToString());
                    }
                }

                //识别完毕删除录音文件
                File.Delete(fi.FullName);

            }
        }
    }
}
