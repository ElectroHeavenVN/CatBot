using DiscordBot.Instance;
using DiscordBot.Music;
using DSharpPlus.Entities;
using DSharpPlus.VoiceNext;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DiscordBot.Voice
{
    internal class TTSCore
    {
        internal static async Task SpeakTTS(SnowflakeObject message, string tts, string voiceIDStr = "NamBac")
        {
            BotServerInstance serverInstance = BotServerInstance.GetBotServerInstance(message.TryGetChannel().Guild);
            serverInstance.lastChannel = message.TryGetChannel();
            if (serverInstance.isVoicePlaying)
            {
                await message.TryRespondAsync(new DiscordEmbedBuilder().WithTitle("Có người đang dùng lệnh rồi!").WithColor(DiscordColor.Yellow).WithFooter("Powered by Zalo AI", "https://cdn.discordapp.com/emojis/1124415235961393193.webp?quality=lossless").Build());
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
                await message.TryRespondAsync(new DiscordEmbedBuilder().WithTitle("Giọng nói không hợp lệ!").WithColor(DiscordColor.Red).WithFooter("Powered by Zalo AI", "https://cdn.discordapp.com/emojis/1124415235961393193.webp?quality=lossless").Build());
        }

        async Task InternalSpeakTTS(SnowflakeObject message, string tts, VoiceID voiceId)
        {
            BotServerInstance serverInstance = BotServerInstance.GetBotServerInstance(this);
            if (!await serverInstance.InitializeVoiceNext(message))
                return;
            if (message is DiscordInteraction interaction)
                await interaction.DeferAsync();
            MusicPlayerCore musicPlayer = BotServerInstance.GetMusicPlayer(message.TryGetChannel().Guild);
            bool isPaused = musicPlayer.isPaused;
            musicPlayer.isPaused = true;
            VoiceTransmitSink transmitSink = serverInstance.currentVoiceNextConnection.GetTransmitSink();
            BotServerInstance.GetBotServerInstance(this).isVoicePlaying = true;
            byte[] buffer = new byte[transmitSink.SampleLength];
            try
            {
                MemoryStream ttsStream = await GetTTSPCMStream(tts, voiceId);
                ttsStream.Position = 0;
                while (ttsStream.Read(buffer, 0, buffer.Length) != 0)
                {
                    if (serverInstance.voiceChannelSFX.isStop)
                        break;
                    await transmitSink.WriteAsync(new ReadOnlyMemory<byte>(buffer));
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
            musicPlayer.isPaused = isPaused;
            BotServerInstance.GetBotServerInstance(this).isVoicePlaying = false;
        }

        static async Task<MemoryStream> GetTTSPCMStream(string strToSpeak, VoiceID voiceId)
        {
            byte[] data = Encoding.ASCII.GetBytes($"input={Uri.EscapeUriString(strToSpeak.Replace(' ', '+'))}&speaker_id={(int)voiceId}&speed=1&dict_id=0&quality=1");
            HttpWebRequest webRequest = (HttpWebRequest)WebRequest.Create("https://zalo.ai/api/demo/v1/tts/synthesize");
            webRequest.Method = "POST";
            webRequest.ContentType = "application/x-www-form-urlencoded";
            webRequest.ContentLength = data.Length;
            webRequest.CookieContainer = new CookieContainer();
            webRequest.CookieContainer.Add(new Cookie("zai_did", "8k9uAj3FNiTevcSSryzXoYYo6433oM_2BhmNHZ8m", "/", ".zalo.ai"));
            webRequest.CookieContainer.Add(new Cookie("_zlang", "vn", "/", ".zalo.ai"));
            webRequest.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/114.0.0.0 Safari/537.36";
            webRequest.Referer = "https://zalo.ai/products/text-to-audio-converter";
            webRequest.Headers.Add("authority", "zalo.ai");
            webRequest.Headers.Add("path", "/api/demo/v1/tts/synthesize");
            webRequest.Headers.Add(HttpRequestHeader.AcceptEncoding, "gzip, deflate, br");
            webRequest.Headers.Add(HttpRequestHeader.AcceptLanguage, "vi-VN,vi;q=0.9");
            webRequest.Headers.Add("origin", "https://zalo.ai");
            webRequest.Headers.Add("sec-ch-ua", "\"Not.A / Brand\";v=\"8\", \"Chromium\";v=\"114\", \"Google Chrome\";v=\"114\"");
            webRequest.Headers.Add("sec-ch-ua-mobile", "?0");
            webRequest.Headers.Add("sec-ch-ua-platform", "\"Windows\"");
            webRequest.Headers.Add("sec-fetch-dest", "empty");
            webRequest.Headers.Add("sec-fetch-mode", "cors");
            webRequest.Headers.Add("sec-fetch-site", "same-origin");
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
