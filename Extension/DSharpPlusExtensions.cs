#pragma warning disable CS8604 // Possible null reference argument.
#pragma warning disable CS8605 // Unboxing a possibly null value.
#pragma warning disable CS8602 // Dereference of a possibly null reference.
#pragma warning disable CS8600 // Converting null literal or possible null value to non-nullable type.

using System.Reflection;
using DSharpPlus;
using DSharpPlus.Commands;
using DSharpPlus.Commands.Processors.SlashCommands;
using DSharpPlus.Commands.Processors.TextCommands;
using DSharpPlus.Entities;
using DSharpPlus.Net;
using DSharpPlus.Net.Abstractions;
using static CatBot.CustomDiscordActivity;

namespace CatBot.Extension
{
    internal static class DSharpPlusExtensions
    {
        internal static async Task UpdateStatusAsync(this DiscordClient discordClient, CustomDiscordActivity customActivity, DiscordUserStatus? userStatus = null, DateTimeOffset? idleSince = null)
        {
            long num = (idleSince != null) ? Utilities.GetUnixTime(idleSince.Value) : 0;
            StatusUpdate statusUpdate = new StatusUpdate
            {
                Activities = new List<TransportActivity>()
                {
                    new TransportActivity()
                    {
                        ApplicationId = customActivity.ApplicationID,
                        ActivityType = customActivity.ActivityType,
                        Name = customActivity.Name,
                        State = customActivity.State
                    }
                },
                IdleSince = num,
                IsAFK = idleSince != null,
                Status = userStatus ?? DiscordUserStatus.Online,
            };
            if (customActivity.ApplicationID == 0)
                statusUpdate.Activities[0].ApplicationId = discordClient.CurrentApplication.Id;
#pragma warning disable DSP0004 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
            await discordClient.SendPayloadAsync(GatewayOpCode.StatusUpdate, statusUpdate, 0);
#pragma warning restore DSP0004 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
        }

        internal static async ValueTask ModifyVoiceStatusAsync(this DiscordChannel discordChannel, string status)
        {
            BaseDiscordClient client = (BaseDiscordClient)typeof(SnowflakeObject).GetProperty("Discord", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(discordChannel);
            DiscordApiClient apiClient = (DiscordApiClient)typeof(BaseDiscordClient).GetProperty("ApiClient", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(client);
            object rest = typeof(DiscordApiClient).GetField("rest", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(apiClient);
            Type restRequest = typeof(DiscordClient).Assembly.GetType("DSharpPlus.Net.RestRequest");
            object restRequestInst = Activator.CreateInstance(restRequest, true);
            restRequest.GetProperty("Route").SetValue(restRequestInst, "/channels/:channel_id/voice-status");
            restRequest.GetProperty("Url").SetValue(restRequestInst, $"/channels/{discordChannel.Id}/voice-status");
            restRequest.GetProperty("Method").SetValue(restRequestInst, HttpMethod.Put);
            restRequest.GetProperty("Payload").SetValue(restRequestInst, $"{{\"status\":\"{status}\"}}");
            ValueTask<RestResponse> response = (ValueTask<RestResponse>)rest.GetType().GetMethod("ExecuteRequestAsync", BindingFlags.NonPublic | BindingFlags.Instance).MakeGenericMethod(restRequest).Invoke(rest, [restRequestInst]);
            await response.ConfigureAwait(false);
        }

        internal static async Task DeferAsync(this CommandContext ctx, bool ephemeral = false)
        {
            if (ctx is SlashCommandContext sctx)
                await sctx.DeferResponseAsync(ephemeral);
            else
                await ctx.DeferResponseAsync();
        }

        internal static async Task ReplyAsync(this CommandContext ctx, string content, bool ephemeral = false)
        {
            if (ctx is SlashCommandContext sctx)
                await sctx.RespondAsync(content, ephemeral);
            else
                await ctx.RespondAsync(content);
        }

        internal static async Task ReplyAsync(this CommandContext ctx, DiscordEmbed embed, bool ephemeral = false)
        {
            if (ctx is SlashCommandContext sctx)
                await sctx.RespondAsync(embed, ephemeral);
            else
                await ctx.RespondAsync(embed);
        }

        internal static async Task ReplyAsync(this CommandContext ctx, string content, DiscordEmbed embed, bool ephemeral = false)
        {
            if (ctx is SlashCommandContext sctx)
                await sctx.RespondAsync(content, embed, ephemeral);
            else
                await ctx.RespondAsync(content, embed);
        }

        internal static async Task DeleteReplyAsync(this CommandContext ctx)
        {
            if (ctx is SlashCommandContext sctx)
                await sctx.DeleteResponseAsync();
            else if (ctx is TextCommandContext tctx && tctx.Response is not null)
                await tctx.DeleteResponseAsync();
        }

        internal static async ValueTask<DiscordMessage?> FollowUpAsync(this CommandContext ctx, string content, bool ephemeral = false)
        {
            if (ctx is SlashCommandContext sctx)
                return await sctx.FollowupAsync(content, ephemeral);
            else if (ctx is TextCommandContext tctx)
            {
                if (tctx.Response is not null)
                    return await tctx.FollowupAsync(content);
                else
                {
                    await tctx.RespondAsync(content);
                    return tctx.Response;
                }
            }
            return null;
        }

        internal static async ValueTask<DiscordMessage?> FollowUpAsync(this CommandContext ctx, DiscordEmbed embed, bool ephemeral = false)
        {
            if (ctx is SlashCommandContext sctx)
                return await sctx.FollowupAsync(embed, ephemeral);
            else if (ctx is TextCommandContext tctx)
            {
                if (tctx.Response is not null)
                    return await tctx.FollowupAsync(embed);
                else
                {
                    await tctx.RespondAsync(embed);
                    return tctx.Response;
                }
            }
            return null;
        }

        internal static async ValueTask<DiscordMessage?> FollowUpAsync(this CommandContext ctx, string content, DiscordEmbed embed, bool ephemeral = false)
        {
            if (ctx is SlashCommandContext sctx)
                return await sctx.FollowupAsync(content, embed, ephemeral);
            else if (ctx is TextCommandContext tctx)
            {
                if (tctx.Response is not null)
                    return await tctx.FollowupAsync(content, embed);
                else
                {
                    await tctx.RespondAsync(content, embed);
                    return tctx.Response;
                }
            }
            return null;
        }

        internal static async ValueTask<DiscordMessage?> FollowUpAsync(this CommandContext ctx, IDiscordMessageBuilder messageBuilder)
        {
            if (ctx is SlashCommandContext sctx)
                return await sctx.FollowupAsync(messageBuilder);
            else if (ctx is TextCommandContext tctx)
            {
                if (tctx.Response is not null)
                    return await tctx.FollowupAsync(messageBuilder);
                else
                {
                    await tctx.RespondAsync(messageBuilder);
                    return tctx.Response;
                }
            }
            return null;
        }
    }
}

#pragma warning restore CS8600 // Converting null literal or possible null value to non-nullable type.
#pragma warning restore CS8602 // Dereference of a possibly null reference.
#pragma warning restore CS8605 // Unboxing a possibly null value.
#pragma warning restore CS8604 // Possible null reference argument.