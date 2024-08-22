using System.Net;
using CatBot.Instance;
using DSharpPlus;
using DSharpPlus.Commands.Processors.SlashCommands;
using DSharpPlus.Entities;
using DSharpPlus.VoiceNext;

namespace CatBot.Admin
{
    internal class AdminCommandsCore
    {
        internal static async Task JoinVoiceChannel(SlashCommandContext ctx, string channelIDstr)
        {
            await ctx.DeferResponseAsync(true);
            if (!Utils.IsBotOwner(ctx.Member.Id))
            {
                await ctx.FollowupAsync(new DiscordFollowupMessageBuilder().WithContent("Bạn không có quyền sử dụng lệnh này!").AsEphemeral());
                return;
            }
            if (!ulong.TryParse(channelIDstr, out ulong channelID))
            {
                await ctx.FollowupAsync(new DiscordFollowupMessageBuilder().WithContent("Dữ liệu nhập vào không hợp lệ!").AsEphemeral());
                return;
            }
            KeyValuePair<DiscordChannel, VoiceNextConnection> keyValuePair = await BotServerInstance.JoinVoiceChannel(channelID);
            if (keyValuePair.Key == null)
                await ctx.FollowupAsync(new DiscordFollowupMessageBuilder().WithContent($"Không tìm thấy kênh thoại với ID {channelID}!").AsEphemeral());
            else if (keyValuePair.Value == null)
                await ctx.FollowupAsync(new DiscordFollowupMessageBuilder().WithContent($"Không thể kết nối với kênh thoại <#{keyValuePair.Key.Id}>!").AsEphemeral());
            else
                await ctx.FollowupAsync(new DiscordFollowupMessageBuilder().WithContent($"Đã kết nối với kênh thoại <#{keyValuePair.Key.Id}>!").AsEphemeral());
        }

        internal static async Task LeaveVoiceChannel(SlashCommandContext ctx, string serverIDstr)
        {
            await ctx.DeferResponseAsync(true);
            if (!Utils.IsBotOwner(ctx.Member.Id))
            {
                await ctx.FollowupAsync(new DiscordFollowupMessageBuilder().WithContent("Bạn không có quyền sử dụng lệnh này!").AsEphemeral());
                return;
            }
            if (!ulong.TryParse(serverIDstr, out ulong serverID))
            {
                await ctx.FollowupAsync(new DiscordFollowupMessageBuilder().WithContent("Dữ liệu nhập vào không hợp lệ!").AsEphemeral());
                return;
            }
            BotServerInstance serverInstance = BotServerInstance.GetBotServerInstance(ctx.Guild);
            serverInstance.suppressOnVoiceStateUpdatedEvent = true;
            KeyValuePair<DiscordChannel, bool> result = BotServerInstance.LeaveVoiceChannel(serverID).Result;
            if (!result.Value)
                await ctx.FollowupAsync(new DiscordFollowupMessageBuilder().WithContent($"Không thể ngắt kết nối kênh thoại <#{result.Key.Id}>!").AsEphemeral());
            else 
                await ctx.FollowupAsync(new DiscordFollowupMessageBuilder().WithContent($"Đã ngắt kết nối kênh thoại <#{result.Key.Id}>!").AsEphemeral());
            serverInstance.suppressOnVoiceStateUpdatedEvent = false;
        }

        internal static async Task AddSFX(DiscordMessage message, string sfxName, bool isSpecial)
        {
            if (!Utils.IsBotOwner(message.Author.Id))
            {
                await message.RespondAsync("Bạn không có quyền sử dụng lệnh này!");
                return;
            }
            if (message.Attachments.Count == 0)
            {
                await message.RespondAsync("Bạn chưa đính kèm file!");
                return;
            }
            if (message.Attachments.Count > 1 && !string.IsNullOrWhiteSpace(sfxName))
            {
                await message.RespondAsync("Chỉ được đính kèm 1 file!");
                return;
            }
            using HttpClient client = new HttpClient();
            foreach (DiscordAttachment attachment in message.Attachments)
            {
                string sfxUrl = attachment.Url;
                string path = Config.gI().SFXFolder;
                if (isSpecial)
                    path = Config.gI().SFXFolderSpecial;
                try
                {
                    string tempPath = Path.GetTempFileName();
                    byte[] fileBytes = await client.GetByteArrayAsync(sfxUrl);
                    await File.WriteAllBytesAsync(tempPath, fileBytes);
                    MemoryStream pcmStream = new MemoryStream();
                    Utils.GetPCMStream(tempPath).CopyTo(pcmStream);
                    pcmStream.Position = 0;
                    byte[] buffer = new byte[pcmStream.Length];
                    pcmStream.Read(buffer, 0, buffer.Length);
                    pcmStream.Close();
                    File.WriteAllBytes(Path.Combine(path, (string.IsNullOrWhiteSpace(sfxName) ? Path.GetFileNameWithoutExtension(attachment.FileName) : sfxName) + ".pcm"), buffer);
                    File.Delete(tempPath);
                }
                catch (Exception ex)
                {
                    await message.RespondAsync($"Có lỗi xảy ra khi thêm SFX {Path.GetFileNameWithoutExtension(attachment.FileName)}!\r\n" + Formatter.BlockCode(ex.ToString()));
                    throw;
                }
            }
            await message.RespondAsync($"Đã thêm {message.Attachments.Count} SFX thành công!");
        }

        internal static async Task DeleteSFX(DiscordMessage message, string sfxName, string isSpecialStr)
        {
            if (!Utils.IsBotOwner(message.Author.Id))
            {
                await message.RespondAsync("Bạn không có quyền sử dụng lệnh này!");
                return;
            }
            string path = Config.gI().SFXFolder;
            if (isSpecialStr.Equals("special", StringComparison.InvariantCultureIgnoreCase))
                path = Config.gI().SFXFolderSpecial;
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
            if (!Utils.IsBotOwner(message.Author.Id))
            {
                await message.RespondAsync("Bạn không có quyền sử dụng lệnh này!");
                return;
            }
            if (message.Attachments.Count == 0)
            {
                await message.RespondAsync("Bạn chưa đính kèm file!");
                return;
            }
            using HttpClient client = new HttpClient();
            foreach (DiscordAttachment attachment in message.Attachments.Where(a => Path.GetExtension(a.FileName) == ".mp3"))
            {
                byte[] music = await client.GetByteArrayAsync(attachment.Url);
                await File.WriteAllBytesAsync(Path.Combine(Config.gI().MusicFolder, attachment.FileName), music);
            }
            await message.RespondAsync("Đã tải nhạc về bộ nhớ!");
        }

        internal static async Task ResetBotServerInstance(SlashCommandContext ctx, string serverIDstr)
        {
            if (serverIDstr != "this" && !Utils.IsBotOwner(ctx.Member.Id))
            {
                await ctx.RespondAsync(new DiscordInteractionResponseBuilder().WithContent("Bạn không có quyền sử dụng lệnh này!").AsEphemeral());
                return;
            }
            if (!ctx.Member.Permissions.HasFlag(DiscordPermissions.Administrator))
            {
                await ctx.RespondAsync(new DiscordInteractionResponseBuilder().WithContent("Bạn không có quyền sử dụng lệnh này!").AsEphemeral());
                return;
            }

            if (ulong.TryParse(serverIDstr, out ulong serverID))
                await BotServerInstance.RemoveBotServerInstance(serverID);
            else if (serverIDstr == "this")
                await BotServerInstance.RemoveBotServerInstance(ctx.Guild.Id);
            BotServerInstance.GetBotServerInstance(ctx.Guild);
            await ctx.RespondAsync(new DiscordInteractionResponseBuilder().WithContent("Đã đặt lại bot!").AsEphemeral());
        }

        internal static async Task SetBotStatus(SlashCommandContext ctx, DiscordActivityType activityType, string name, string state)
        {
            if (!Utils.IsBotOwner(ctx.Member.Id))
            {
                await ctx.RespondAsync(new DiscordInteractionResponseBuilder().WithContent("Bạn không có quyền sử dụng lệnh này!").AsEphemeral());
                return;
            }
            if (string.IsNullOrEmpty(name))
            {
                DiscordBotMain.activity = null;
                await ctx.RespondAsync(new DiscordInteractionResponseBuilder().WithContent("Đã đặt lại trạng thái bot!").AsEphemeral());
            }
            else
            {
                DiscordBotMain.activity = new CustomDiscordActivity(activityType, name, state);
                await ctx.RespondAsync(new DiscordInteractionResponseBuilder().WithContent("Đã đặt trạng thái bot!").AsEphemeral());
            }
        }
    }
}
