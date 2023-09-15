using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.CommandsNext;
using DSharpPlus.VoiceNext;
using TagLib.Asf;
using System.Diagnostics;
using System.IO;
using System.Threading;
using DSharpPlus.Entities;
using DSharpPlus;

namespace DiscordBot.Instance
{
    public class GlobalBaseCommands : BaseCommandModule
    {
        [Command("volume"), Aliases("vol", "v"), Description("Xem hoặc chỉnh âm lượng tổng của bot")]
        public async Task SetVolume(CommandContext ctx, [Description("Âm lượng (0 - 250)")] long volume = -1) => await BotServerInstance.SetVolume(ctx.Message, volume);

        //[Command("test")]
        //public async Task Test(CommandContext ctx)
        //{
        //    new Thread(async () =>
        //    {
        //        BotServerInstance serverInstance = BotServerInstance.GetBotServerInstance(ctx.Guild);
        //        if (!await serverInstance.InitializeVoiceNext(ctx.Message))
        //            return;
        //        var voiceNextConnection = serverInstance.currentVoiceNextConnection;
        //        new Thread(async () =>
        //        {
        //            await Task.Delay(200);
        //            if (voiceNextConnection.TargetChannel.Type == ChannelType.Stage)
        //            {
        //                DiscordMember botMember = await ctx.Guild.GetMemberAsync(DiscordBotMain.botClient.CurrentUser.Id);
        //                if (botMember.VoiceState.IsSuppressed)
        //                    await botMember.UpdateVoiceStateAsync(voiceNextConnection.TargetChannel, false);
        //            }
        //        }).Start();
        //        Console.WriteLine("Connecting to channel");
        //        //stage: keep transmiting data
        //        var pcm = ConvertAudioToPcm(@"D:\Music\Xomu - Mannenzakura (クミ P Remix -- 初音ミク)_256k.mp3");
        //        //var pcm = ConvertAudioToPcm(@"C:\Users\EHVN\Downloads\Sample.mp3");
        //        //var pcm = new FileStream(@"SFX\2000YearsLater.pcm", FileMode.Open, FileAccess.Read);
        //        Console.WriteLine("Convert Music File to PCM");
        //        byte[] buffer = new byte[voiceNextConnection.GetTransmitSink().SampleLength];
        //        DateTime lastTime = DateTime.Now;
        //        while (pcm.Read(buffer, 0, buffer.Length) != 0)
        //        {
        //            await voiceNextConnection.GetTransmitSink().WriteAsync(new ReadOnlyMemory<byte>(buffer));
        //            if ((DateTime.Now - lastTime).TotalSeconds > 10)
        //            {
        //                pcm.Close();
        //                break;
        //            }
        //        }
        //        Console.WriteLine("File Played Success.");
        //        pcm.Dispose();
        //        Console.WriteLine("End Playing ....");
        //    }).Start();
        //}

        //static Stream ConvertAudioToPcm(string filePath)
        //{
        //    Console.WriteLine("We are in Convert Function");
        //    var ffmpeg = Process.Start(new ProcessStartInfo
        //    {
        //        FileName = "ffmpeg\\ffmpeg",
        //        Arguments = $@"-i ""{filePath}"" -ac 2 -f s16le -ar 48000 pipe:1",
        //        RedirectStandardOutput = true,
        //        UseShellExecute = false
        //    });
        //    Console.WriteLine("Proccess Was Done!");
        //    return ffmpeg.StandardOutput.BaseStream;
        //}
    }
}
