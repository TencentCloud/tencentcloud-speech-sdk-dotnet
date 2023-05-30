/*
 * Copyright (c) 2017-2018 THL A29 Limited, a Tencent company. All Rights Reserved.
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *    http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

namespace asr;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Security.Cryptography;

public class ASRController
{
  public class Builder
  {
    [JsonIgnore]
    public String appid { get; set; } = "";
    public String secretid { get; set; } = "";
    [JsonIgnore]
    public String secretkey { get; set; } = "";
    public String engine_model_type { get; set; } = "16k_zh";
    public int voice_format { get; set; } = 1;
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? needvad { get; set; } = null;
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public String? hotword_id { get; set; } = null;
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? reinforce_hotword { get; set; } = null;
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public String? customization_id { get; set; } = null;
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? filter_dirty { get; set; } = null;
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? filter_modal { get; set; } = null;
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? filter_punc { get; set; } = null;
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? filter_empty_result { get; set; } = null;
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? convert_num_mode { get; set; } = null;
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? word_info { get; set; } = null;
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? vad_silence_time { get; set; } = null;
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public float? noise_threshold { get; set; } = null;
    public ASRController build()
    {
      var text = JsonSerializer.Serialize(this);
      var query = JsonSerializer.Deserialize<SortedDictionary<string, object>>(text);
      if (query == null)
      {
        throw new ArgumentNullException();
      }
      query.Add("timestamp", 0);
      query.Add("expired", 0);
      query.Add("nonce", 0);
      query.Add("voice_id", "");
      var ins = new ASRController(query);
      ins.app_id_ = appid;
      ins.secret_id_ = secretid;
      ins.secret_key_ = secretkey;
      return ins;
    }
  }
  ASRController(SortedDictionary<string, object> val)
  {
    query_ = val;
  }
  private SortedDictionary<string, object> query_;
  private String host_ { get; } = "asr.cloud.tencent.com";
  private String app_id_ = "";
  private String secret_id_ = "";
  private String secret_key_ = "";
  private CancellationTokenSource source_ = new CancellationTokenSource();
  private ClientWebSocket? client_;
  private Task? process_;
  private int interval_ = 40;
  public delegate Task DataSource(byte[] buffer, CancellationToken token);
  public delegate Task MessageHandler(String msg);

  public async Task startAsync(DataSource datasource, MessageHandler handler)
  {
    client_ = new ClientWebSocket();
    var uri = await url();
    await client_.ConnectAsync(uri, source_.Token);
    var read_process = read(handler);
    var write_process = write(datasource);
    process_ = Task.WhenAll(read_process, write_process);
  }

  public async Task cancelAsync()
  {
    source_.Cancel();
  }

  public async Task stopAsync()
  {
    await client_!.SendAsync(Encoding.UTF8.GetBytes("{\"type\":\"end\"}"), WebSocketMessageType.Text, true, source_.Token);
  }

  public async Task waitAsync()
  {
    await process_;
  }

  private async Task read(MessageHandler handler)
  {
    try
    {
      while (!source_.Token.IsCancellationRequested)
      {
        var content = new byte[1000];
        var res = await client_!.ReceiveAsync(content, source_.Token);
        if (res.EndOfMessage && res.MessageType == WebSocketMessageType.Text)
        {
          var result = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(new MemoryStream(content, 0, res.Count));
          await handler(Encoding.UTF8.GetString(content, 0, res.Count));
          if (result["code"].GetInt32() != 0)
          {
            source_.Cancel();
            break;
          }
          if (result.ContainsKey("final") && result["final"].GetInt32() == 1)
          {
            source_.Cancel();
            break;
          }
        }
      }
    }
    catch (TaskCanceledException e)
    {
    }
  }

  private async Task write(DataSource datasource)
  {
    try
    {
      while (!source_.Token.IsCancellationRequested)
      {
        await Task.Delay(interval_, source_.Token);
        var content = new byte[interval_ * 16 * 2];
        await datasource(content, source_.Token);
        await client_.SendAsync(content, WebSocketMessageType.Binary, true, source_.Token);
      }
    }
    catch (TaskCanceledException e)
    {

    }
  }

  private async Task<Uri> url()
  {
    query_["timestamp"] = (int)DateTime.UtcNow.Subtract(DateTime.UnixEpoch).TotalSeconds;
    query_["expired"] = (int)DateTime.UtcNow.AddDays(10).Subtract(DateTime.UnixEpoch).TotalSeconds;
    query_["nonce"] = new Random().Next(1000000000);
    query_["voice_id"] = Guid.NewGuid().ToString();
    var plain_text_builder = new StringBuilder();
    plain_text_builder.AppendFormat("{0}/asr/v2/{1}?", host_, app_id_);
    foreach (var item in query_)
    {
      plain_text_builder.AppendFormat("{0}={1}", item.Key, item.Value);
      plain_text_builder.Append("&");
    }
    plain_text_builder.Length--;
    var sign = Convert.ToBase64String(
    await new HMACSHA1(Encoding.UTF8.GetBytes(secret_key_)).ComputeHashAsync(new MemoryStream(Encoding.UTF8.GetBytes(plain_text_builder.ToString())), source_.Token));
    plain_text_builder.AppendFormat("&{0}={1}", "signature", Uri.EscapeDataString(sign));
    plain_text_builder.Insert(0, "wss://");
    return new Uri(plain_text_builder.ToString());
  }

}
