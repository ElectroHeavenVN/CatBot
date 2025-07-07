using CatBot.Extensions;
using CatBot.Instance;
using DSharpPlus;
using DSharpPlus.Commands;
using DSharpPlus.Commands.Processors.SlashCommands;
using DSharpPlus.Commands.Processors.TextCommands;
using DSharpPlus.Entities;
using DSharpPlus.VoiceNext;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace CatBot.Admin
{
    internal class AdminCommandsCore
    {
        internal static async Task JoinVoiceChannel(CommandContext ctx, string channelIDstr)
        {
            await ctx.DeferResponseAsync();
            if (ctx.Member is null)
            {
                await ctx.DeleteResponseAsync();
                return;
            }
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
            KeyValuePair<DiscordChannel?, VoiceNextConnection?> keyValuePair = await BotServerInstance.JoinVoiceChannel(channelID);
            if (keyValuePair.Key is null)
                await ctx.FollowupAsync(new DiscordFollowupMessageBuilder().WithContent($"Không tìm thấy kênh thoại với ID {channelID}!").AsEphemeral());
            else if (keyValuePair.Value is null)
                await ctx.FollowupAsync(new DiscordFollowupMessageBuilder().WithContent($"Không thể kết nối với kênh thoại <#{keyValuePair.Key.Id}>!").AsEphemeral());
            else
                await ctx.FollowupAsync(new DiscordFollowupMessageBuilder().WithContent($"Đã kết nối với kênh thoại <#{keyValuePair.Key.Id}>!").AsEphemeral());
        }

        internal static async Task LeaveVoiceChannel(CommandContext ctx, string serverIDstr)
        {
            await ctx.DeferResponseAsync();
            if (ctx.Member is null || ctx.Guild is null)
            {
                await ctx.DeleteResponseAsync();
                return;
            }
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
            BotServerInstance serverInstance = BotServerInstance.GetBotServerInstance(ctx.Guild.Id);
            serverInstance.suppressOnVoiceStateUpdatedEvent = true;
            KeyValuePair<DiscordChannel?, bool> result = BotServerInstance.LeaveVoiceChannel(serverID).Result;
            if (!result.Value)
                await ctx.FollowupAsync(new DiscordFollowupMessageBuilder().WithContent($"Không thể ngắt kết nối kênh thoại <#{result.Key?.Id}>!").AsEphemeral());
            else
                await ctx.FollowupAsync(new DiscordFollowupMessageBuilder().WithContent($"Đã ngắt kết nối kênh thoại <#{result.Key?.Id}>!").AsEphemeral());
            serverInstance.suppressOnVoiceStateUpdatedEvent = false;
        }

        internal static async Task AddSFX(SlashCommandContext ctx, DiscordAttachment sfx, string sfxName, bool isSpecial)
        {
            if (!Utils.IsBotOwner(ctx.User.Id))
            {
                await ctx.RespondAsync("Bạn không có quyền sử dụng lệnh này!");
                return;
            }
            using HttpClient client = new HttpClient();
            string? sfxUrl = sfx.Url;
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
                File.WriteAllBytes(Path.Combine(path, (string.IsNullOrWhiteSpace(sfxName) ? Path.GetFileNameWithoutExtension(sfx.FileName) : sfxName) + ".pcm"), buffer);
                File.Delete(tempPath);
            }
            catch (Exception ex)
            {
                await ctx.RespondAsync($"Có lỗi xảy ra khi thêm SFX {Path.GetFileNameWithoutExtension(sfx.FileName)}!\r\n" + Formatter.BlockCode(ex.ToString()));
                throw;
            }
            await ctx.RespondAsync($"Đã thêm SFX thành công!");
        }

        internal static async Task AddSFX(TextCommandContext ctx, string sfxName, bool isSpecial)
        {
            if (!Utils.IsBotOwner(ctx.User.Id))
            {
                await ctx.RespondAsync("Bạn không có quyền sử dụng lệnh này!");
                return;
            }
            if (ctx.Message.Attachments.Count == 0)
            {
                await ctx.RespondAsync("Bạn chưa đính kèm file!");
                return;
            }
            if (ctx.Message.Attachments.Count > 1 && !string.IsNullOrWhiteSpace(sfxName))
            {
                await ctx.RespondAsync("Chỉ được đính kèm 1 file!");
                return;
            }
            using HttpClient client = new HttpClient();
            foreach (DiscordAttachment attachment in ctx.Message.Attachments)
            {
                string? sfxUrl = attachment.Url;
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
                    await ctx.RespondAsync($"Có lỗi xảy ra khi thêm SFX {Path.GetFileNameWithoutExtension(attachment.FileName)}!\r\n" + Formatter.BlockCode(ex.ToString()));
                    throw;
                }
            }
            await ctx.RespondAsync($"Đã thêm {ctx.Message.Attachments.Count} SFX thành công!");
        }

        internal static async Task DeleteSFX(CommandContext ctx, string sfxName)
        {
            if (ctx.User is null)
                return;
            if (!Utils.IsBotOwner(ctx.User.Id))
            {
                await ctx.RespondAsync("Bạn không có quyền sử dụng lệnh này!");
                return;
            }
            if (File.Exists(Path.Combine(Config.gI().SFXFolderSpecial, sfxName + ".pcm")))
            {
                File.Delete(Path.Combine(Config.gI().SFXFolderSpecial, sfxName + ".pcm"));
                await ctx.RespondAsync("Đã xóa SFX thành công!");
            }
            else if (File.Exists(Path.Combine(Config.gI().SFXFolder, sfxName + ".pcm")))
            {
                File.Delete(Path.Combine(Config.gI().SFXFolder, sfxName + ".pcm"));
                await ctx.RespondAsync("Đã xóa SFX thành công!");
            }
            else
                await ctx.RespondAsync("SFX không tồn tại!");
        }

        internal static async Task DownloadMusic(SlashCommandContext ctx, DiscordAttachment attachment)
        {
            if (!Utils.IsBotOwner(ctx.User.Id))
            {
                await ctx.RespondAsync("Bạn không có quyền sử dụng lệnh này!");
                return;
            }
            if (Path.GetExtension(attachment.FileName) != ".mp3")
            {
                await ctx.RespondAsync("File đính kèm không phải là file nhạc MP3!");
                return;
            }
            using HttpClient client = new HttpClient();
            byte[] music = await client.GetByteArrayAsync(attachment.Url);
            await File.WriteAllBytesAsync(Path.Combine(Config.gI().MusicFolder, attachment.FileName ?? "file.mp3"), music);
            await ctx.RespondAsync("Đã tải nhạc về bộ nhớ!");
        }

        internal static async Task DownloadMusic(TextCommandContext ctx)
        {
            if (!Utils.IsBotOwner(ctx.User.Id))
            {
                await ctx.RespondAsync("Bạn không có quyền sử dụng lệnh này!");
                return;
            }
            if (ctx.Message.Attachments.Count == 0)
            {
                await ctx.RespondAsync("Bạn chưa đính kèm file!");
                return;
            }
            using HttpClient client = new HttpClient();
            foreach (DiscordAttachment attachment in ctx.Message.Attachments.Where(a => Path.GetExtension(a.FileName) == ".mp3"))
            {
                byte[] music = await client.GetByteArrayAsync(attachment.Url);
                await File.WriteAllBytesAsync(Path.Combine(Config.gI().MusicFolder, attachment.FileName ?? "file.mp3"), music);
            }
            await ctx.RespondAsync("Đã tải nhạc về bộ nhớ!");
        }

        internal static async Task ResetBotServerInstance(CommandContext ctx, string serverIDstr)
        {
            if (ctx.Member is null)
            {
                await ctx.DeleteResponseAsync();
                return;
            }
            if (serverIDstr != "this" && !Utils.IsBotOwner(ctx.Member.Id))
            {
                await ctx.RespondAsync(new DiscordInteractionResponseBuilder().WithContent("Bạn không có quyền sử dụng lệnh này!").AsEphemeral());
                return;
            }
            if (!ctx.Member.Permissions.HasFlag(DiscordPermission.Administrator))
            {
                await ctx.RespondAsync(new DiscordInteractionResponseBuilder().WithContent("Bạn không có quyền sử dụng lệnh này!").AsEphemeral());
                return;
            }

            if (ulong.TryParse(serverIDstr, out ulong serverID))
                await BotServerInstance.RemoveBotServerInstance(serverID);
            else if (serverIDstr == "this" && ctx.Guild is not null)
                await BotServerInstance.RemoveBotServerInstance(ctx.Guild.Id);
            if (ctx.Guild is not null)
                BotServerInstance.GetBotServerInstance(ctx.Guild.Id);
            await ctx.RespondAsync(new DiscordInteractionResponseBuilder().WithContent("Đã đặt lại bot!").AsEphemeral());
        }

        internal static async Task SetBotStatus(CommandContext ctx, DiscordActivityType activityType, string name, string state)
        {
            if (ctx.Member is null)
            {
                await ctx.DeleteResponseAsync();
                return;
            }
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
                DiscordBotMain.activity = new CustomDiscordActivity(DiscordBotMain.botClient.CurrentApplication.Id, activityType, name, state);
                await ctx.RespondAsync(new DiscordInteractionResponseBuilder().WithContent("Đã đặt trạng thái bot!").AsEphemeral());
            }
        }

        internal static async Task RestartBot(CommandContext ctx)
        {
            if (ctx.Member is null)
            {
                await ctx.DeleteResponseAsync();
                return;
            }
            if (!Utils.IsBotOwner(ctx.Member.Id))
            {
                await ctx.RespondAsync(new DiscordInteractionResponseBuilder().WithContent("Bạn không có quyền sử dụng lệnh này!").AsEphemeral());
                return;
            }
            await ctx.RespondAsync(new DiscordInteractionResponseBuilder().WithContent("Đang khởi động lại bot...").AsEphemeral());
            await DiscordBotMain.botClient.DisconnectAsync();
            Process.Start(new ProcessStartInfo
            {
                FileName = Environment.ProcessPath,
                UseShellExecute = true,
            });
            Environment.Exit(0);
        }
    }
}
