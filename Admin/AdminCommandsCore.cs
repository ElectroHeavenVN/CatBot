using DiscordBot.Instance;
using DiscordBot.Voice;
using DSharpPlus.Entities;
using DSharpPlus.SlashCommands;
using DSharpPlus.VoiceNext;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.Admin
{
    internal class AdminCommandsCore
    {
        internal static async Task JoinVoiceChannel(InteractionContext ctx, string channelIDstr)
        {
            await ctx.DeferAsync(true);
            if (!Config.BotAuthorsID.Contains(ctx.Member.Id))
            {
                await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder().WithContent("Bạn không có quyền sử dụng lệnh này!").AsEphemeral());
                return;
            }
            if (!ulong.TryParse(channelIDstr, out ulong channelID))
            {
                await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder().WithContent("Dữ liệu nhập vào không hợp lệ!").AsEphemeral());
                return;
            }
            KeyValuePair<DiscordChannel, VoiceNextConnection> keyValuePair;
            try
            {
                keyValuePair = await BotServerInstance.JoinVoiceChannel(channelID);
            }
            catch (Exception ex)
            {
                await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder().WithContent($"```\r\n{ex}\r\n```").AsEphemeral());
                Utils.LogException(ex);
                return;
            }
            if (keyValuePair.Key == null)
                await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder().WithContent($"Không tìm thấy kênh thoại với ID {channelID}!").AsEphemeral());
            else if (keyValuePair.Value == null)
                await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder().WithContent($"Không thể kết nối với kênh thoại <#{keyValuePair.Key.Id}>!").AsEphemeral());
            else
                await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder().WithContent($"Đã kết nối với kênh thoại <#{keyValuePair.Key.Id}>!").AsEphemeral());
        }

        internal static async Task LeaveVoiceChannel(InteractionContext ctx, string serverIDstr)
        {
            await ctx.DeferAsync(true);
            if (!Config.BotAuthorsID.Contains(ctx.Member.Id))
            {
                await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder().WithContent("Bạn không có quyền sử dụng lệnh này!").AsEphemeral());
                return;
            }
            if (!ulong.TryParse(serverIDstr, out ulong serverID))
            {
                await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder().WithContent("Dữ liệu nhập vào không hợp lệ!").AsEphemeral());
                return;
            }
            BotServerInstance serverInstance = BotServerInstance.GetBotServerInstance(ctx.Guild);
            serverInstance.suppressOnVoiceStateUpdatedEvent = true;
            KeyValuePair<DiscordChannel, bool> result = BotServerInstance.LeaveVoiceChannel(serverID).Result;
            if (!result.Value)
                await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder().WithContent($"Không thể ngắt kết nối kênh thoại <#{result.Key.Id}>!").AsEphemeral());
            else 
                await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder().WithContent($"Đã ngắt kết nối kênh thoại <#{result.Key.Id}>!").AsEphemeral());
            serverInstance.suppressOnVoiceStateUpdatedEvent = false;
        }

        internal static async Task AddSFX(DiscordMessage message, string sfxName, bool isSpecial)
        {
            if (!Config.BotAuthorsID.Contains(message.Author.Id))
            {
                await message.RespondAsync("Bạn không có quyền sử dụng lệnh này!");
                return;
            }
            if (message.Attachments.Count == 0)
            {
                await message.RespondAsync("Bạn chưa đính kèm file!");
                return;
            }
            if (message.Attachments.Count > 1)
            {
                await message.RespondAsync("Chỉ được đính kèm 1 file!");
                return;
            }
            string sfxUrl = message.Attachments[0].Url;
            string path = Config.SFXFolder;
            if (string.IsNullOrWhiteSpace(sfxName))
                sfxName = message.Attachments[0].FileName;
            if (isSpecial)
                path = Config.SFXFolderSpecial;
            try
            {
                string tempPath = Path.GetTempFileName();
                new WebClient().DownloadFile(sfxUrl, tempPath);
                MemoryStream pcmStream = new MemoryStream();
                Utils.GetPCMStream(tempPath).CopyTo(pcmStream);
                pcmStream.Position = 0;
                byte[] buffer = new byte[pcmStream.Length];
                pcmStream.Read(buffer, 0, buffer.Length);
                pcmStream.Close();
                File.WriteAllBytes(Path.Combine(path, sfxName + ".pcm"), buffer);
                File.Delete(tempPath);
                await message.RespondAsync("Đã thêm SFX thành công!");
            }
            catch (Exception ex)
            {
                await message.RespondAsync("Có lỗi xảy ra!");
                Utils.LogException(ex);
            }
        }

        internal static async Task DeleteSFX(DiscordMessage message, string sfxName, string isSpecialStr)
        {
            if (!Config.BotAuthorsID.Contains(message.Author.Id))
            {
                await message.RespondAsync("Bạn không có quyền sử dụng lệnh này!");
                return;
            }
            string path = Config.SFXFolder;
            if (isSpecialStr.Equals("special", StringComparison.InvariantCultureIgnoreCase))
                path = Config.SFXFolderSpecial;
            if (File.Exists(Path.Combine(path, sfxName + ".pcm")))
            {
                File.Delete(Path.Combine(path, sfxName + ".pcm"));
                await message.RespondAsync("Đã xóa SFX thành công!");
            }
            else 
                await message.RespondAsync("SFX không tồn tại!");
        }

        internal static async Task DownloadMusic(DiscordMessage message)
        {
            if (!Config.BotAuthorsID.Contains(message.Author.Id))
            {
                await message.RespondAsync("Bạn không có quyền sử dụng lệnh này!");
                return;
            }
            if (message.Attachments.Count == 0)
            {
                await message.RespondAsync("Bạn chưa đính kèm file!");
                return;
            }
            WebClient webClient = new WebClient();
            foreach (DiscordAttachment attachment in message.Attachments.Where(a => Path.GetExtension(a.FileName) == ".mp3"))
                webClient.DownloadFile(new Uri(attachment.Url), Path.Combine(Config.MusicFolder, attachment.FileName));
            await message.RespondAsync("Đã tải nhạc về bộ nhớ!");
        }

        internal static async Task ResetBotServerInstance(InteractionContext ctx, string serverIDstr)
        {
            if (!Config.BotAuthorsID.Contains(ctx.Member.Id))
            {
                await ctx.CreateResponseAsync(new DiscordInteractionResponseBuilder().WithContent("Bạn không có quyền sử dụng lệnh này!").AsEphemeral());
                return;
            }
            if (ulong.TryParse(serverIDstr, out ulong serverID))
                await BotServerInstance.RemoveBotServerInstance(serverID);
            else if (serverIDstr == "this")
                await BotServerInstance.RemoveBotServerInstance(ctx.Guild.Id);
            BotServerInstance.GetBotServerInstance(ctx.Guild);
            await ctx.CreateResponseAsync(new DiscordInteractionResponseBuilder().WithContent("Đã đặt lại instance bot của server!").AsEphemeral());
        }

        internal static async Task SetBotStatus(InteractionContext ctx, string name, ActivityType activityType)
        {
            if (!Config.BotAuthorsID.Contains(ctx.Member.Id))
            {
                await ctx.CreateResponseAsync(new DiscordInteractionResponseBuilder().WithContent("Bạn không có quyền sử dụng lệnh này!").AsEphemeral());
                return;
            }
            if (string.IsNullOrEmpty(name))
            {
                DiscordBotMain.activity = null;
                await ctx.CreateResponseAsync(new DiscordInteractionResponseBuilder().WithContent("Đã đặt lại trạng thái bot!").AsEphemeral());
            }
            else
            {
                DiscordBotMain.activity = new DiscordActivity(name, activityType);
                await ctx.CreateResponseAsync(new DiscordInteractionResponseBuilder().WithContent("Đã đặt trạng thái bot!").AsEphemeral());
            }
        }
    }
}
