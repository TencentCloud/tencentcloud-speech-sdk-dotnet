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

using System.Text.Json;
var config_json = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(File.ReadAllBytes("config.json"));
var builder = new asr.ASRController.Builder();
builder.appid = config_json["appid"].GetString();
builder.secretid = config_json["secretid"].GetString();
builder.secretkey = config_json["secretkey"].GetString();
builder.needvad = 1;
var ctl = builder.build();
using FileStream fs = File.OpenRead("output.pcm");
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
    Console.WriteLine(msg);
  });
  await ctl.waitAsync();
}
catch (Exception e)
{
  Console.WriteLine(e.ToString());
}