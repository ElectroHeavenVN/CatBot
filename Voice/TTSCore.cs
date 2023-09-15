using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DiscordBot.Instance;
using DiscordBot.Music;
using DSharpPlus.Entities;
using DSharpPlus.SlashCommands;
using DSharpPlus.VoiceNext;
using Newtonsoft.Json.Linq;

namespace DiscordBot.Voice
{
    internal class TTSCore
    {
        internal double volume = 1;

        internal static async Task SpeakTTS(SnowflakeObject message, string tts, string voiceIDStr = "NamBac")
        {
            BotServerInstance serverInstance = BotServerInstance.GetBotServerInstance(message.TryGetChannel().Guild);
            serverInstance.lastChannel = message.TryGetChannel();
            if (serverInstance.isVoicePlaying)
            {
                await message.TryRespondAsync(new DiscordEmbedBuilder().WithTitle("Có người đang dùng lệnh rồi!").WithColor(DiscordColor.Yellow).WithFooter("Powered by Zalo AI", "https://cdn.discordapp.com/emojis/1124415235961393193.webp?quality=lossless").Build());  //You may need to change this
                return;
            }
            if (Enum.TryParse(voiceIDStr, true, out VoiceID voiceID))
            {
                new Thread(async () =>
                {
                    await serverInstance.textToSpeech.InternalSpeakTTS(message, tts, voiceID);
                }) { IsBackground = true }.Start();
            }
            else
                await message.TryRespondAsync(new DiscordEmbedBuilder().WithTitle("Giọng nói không hợp lệ!").WithColor(DiscordColor.Red).WithFooter("Powered by Zalo AI", "https://cdn.discordapp.com/emojis/1124415235961393193.webp?quality=lossless").Build());  //You may need to change this
        }

        internal static async Task SetVolume(SnowflakeObject obj, long volume)
        {
            try
            {
                BotServerInstance serverInstance = BotServerInstance.GetBotServerInstance(obj.TryGetChannel().Guild);
                if (volume == -1)
                {
                    await obj.TryRespondAsync("Âm lượng TTS hiện tại: " + (int)(serverInstance.textToSpeech.volume * 100));
                    return;
                }
                if (volume < 0 || volume > 250)
                {
                    await obj.TryRespondAsync("Âm lượng không hợp lệ!");
                    return;
                }
                serverInstance.textToSpeech.volume = volume / 100d;
                await obj.TryRespondAsync("Điều chỉnh âm lượng TTS thành: " + volume + "%!");
            }
            catch (Exception ex) { Utils.LogException(ex); }
        }

        async Task InternalSpeakTTS(SnowflakeObject message, string tts, VoiceID voiceId)
        {
            BotServerInstance serverInstance = BotServerInstance.GetBotServerInstance(this);
            if (!await serverInstance.InitializeVoiceNext(message))
                return;
            if (message is DiscordInteraction interaction)
                await interaction.DeferAsync();
            MusicPlayerCore musicPlayer = BotServerInstance.GetMusicPlayer(message.TryGetChannel().Guild);
            BotServerInstance.GetBotServerInstance(this).isVoicePlaying = true;
            byte[] buffer = new byte[serverInstance.currentVoiceNextConnection.GetTransmitSink().SampleLength];
            try
            {
                MemoryStream ttsStream = await GetTTSPCMStream(tts, voiceId);
                ttsStream.Position = 0;
                if (musicPlayer.isPlaying)
                {
                    byte[] data = new byte[ttsStream.Length + ttsStream.Length % 2];
                    ttsStream.Read(data, 0, (int)ttsStream.Length);
                    for (int i = 0; i < data.Length; i += 2)
                        Array.Copy(BitConverter.GetBytes((short)(BitConverter.ToInt16(data, i) * volume)), 0, data, i, sizeof(short));
                    musicPlayer.sfxData.AddRange(data);
                    while (musicPlayer.sfxData.Count != 0)
                        await Task.Delay(100);
                }
                else
                {
                    while (ttsStream.Read(buffer, 0, buffer.Length) != 0)
                    {
                        if (serverInstance.voiceChannelSFX.isStop)
                            break;
                        while (!serverInstance.canSpeak)
                            await Task.Delay(500);
                        for (int i = 0; i < buffer.Length; i += 2)
                            Array.Copy(BitConverter.GetBytes((short)(BitConverter.ToInt16(buffer, i) * volume)), 0, buffer, i, sizeof(short));
                        await serverInstance.WriteTransmitData(buffer);
                    }
                }
                if (serverInstance.voiceChannelSFX.isStop)
                    serverInstance.voiceChannelSFX.isStop = false;
                if (message is DiscordInteraction interaction2)
                    await interaction2.DeleteOriginalResponseAsync();
            }
            catch (Exception ex)
            {
                Utils.LogException(ex);
                if (message is DiscordInteraction interaction2)
                    await interaction2.CreateFollowupMessageAsync(new DiscordFollowupMessageBuilder().WithContent("```" + Environment.NewLine + ex + Environment.NewLine + "```"));
                else 
                    await message.TryRespondAsync("```" + Environment.NewLine + ex + Environment.NewLine + "```");
            }
            BotServerInstance.GetBotServerInstance(this).isVoicePlaying = false;
        }

        static async Task<MemoryStream> GetTTSPCMStream(string strToSpeak, VoiceID voiceId)
        {
            byte[] data = Encoding.ASCII.GetBytes($"input={Uri.EscapeUriString(strToSpeak.Replace(' ', '+'))}&speaker_id={(int)voiceId}&speed=1&dict_id=0&quality=1");
            HttpWebRequest webRequest = (HttpWebRequest)WebRequest.Create("https://zalo.ai/api/demo/v1/tts/synthesize");
            webRequest.Method = "POST";
            webRequest.ContentType = "application/x-www-form-urlencoded";
            webRequest.ContentLength = data.Length;
            webRequest.SetCookie(Config.ZaloAICookie, "/", ".zalo.ai");
            webRequest.UserAgent = Config.UserAgent;
            webRequest.Referer = "https://zalo.ai/products/text-to-audio-converter";
            webRequest.Headers.Add("authority", "zalo.ai");
            webRequest.Headers.Add("path", "/api/demo/v1/tts/synthesize");
            webRequest.Headers.Add(HttpRequestHeader.AcceptEncoding, "gzip, deflate, br");
            webRequest.Headers.Add(HttpRequestHeader.AcceptLanguage, "vi-VN,vi;q=0.9");
            webRequest.Headers.Add("origin", "https://zalo.ai");
            webRequest.Accept = "application/json, text/plain, */*";
            webRequest.GetRequestStream().Write(data, 0, data.Length);
            HttpWebResponse httpWebResponse = (HttpWebResponse)await webRequest.GetResponseAsync();
            JObject obj = JObject.Parse(new StreamReader(httpWebResponse.GetResponseStream()).ReadToEnd());
            if (!obj.ContainsKey("data"))
                throw new WebException(obj["error_message"].ToString(), null, WebExceptionStatus.UnknownError, httpWebResponse);
            await Task.Delay(3 * strToSpeak.Length);
            MemoryStream ttsStream = new MemoryStream();
            while (ttsStream.Length == 0)
                Utils.GetPCMStream(obj["data"]["url"].ToString()).CopyTo(ttsStream);
            return ttsStream;
        }
    }
}
