using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.AsyncEvents;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Exceptions;
using DSharpPlus.CommandsNext.Executors;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using DSharpPlus.Net;
using DSharpPlus.Net.Abstractions;
using static CatBot.CustomDiscordActivity;

namespace CatBot.Extension
{
    internal static class DSharpPlusExtensions
    {
        internal static async Task UpdateStatusAsync(this DiscordClient discordClient, CustomDiscordActivity customActivity, UserStatus? userStatus = null, DateTimeOffset? idleSince = null)
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
                Status = userStatus ?? UserStatus.Online,
                
            };
            await discordClient.SendPayloadAsync(GatewayOpCode.StatusUpdate, statusUpdate);

            //string customStatusRawJSONData = JsonConvert.SerializeObject(new GatewayPayload
            //{
            //    OpCode = GatewayOpCode.StatusUpdate,
            //    Data = statusUpdate
            //});
            //Task sendCustomStatusTask = (Task)typeof(DiscordClient).GetMethod("SendRawPayloadAsync", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(discordClient, new object[] { customStatusRawJSONData });
            //await sendCustomStatusTask.ConfigureAwait(false);
        }

        internal static async Task HandleCommandsAsync(this CommandsNextExtension cne, DiscordClient sender, MessageCreateEventArgs e)
        {
            if (e.Author.IsBot && !e.Author.IsBotExcluded()) // bad bot
                return;

            CommandsNextConfiguration config = (CommandsNextConfiguration)typeof(CommandsNextExtension).GetProperty("Config", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(cne);

            object GetConfigProperty(string name) => typeof(CommandsNextConfiguration).GetProperty(name, BindingFlags.Public | BindingFlags.Instance).GetValue(config);

            if (!(bool)GetConfigProperty("EnableDms") && e.Channel.IsPrivate)
                return;

            var mpos = -1;
            if ((bool)GetConfigProperty("EnableMentionPrefix"))
                mpos = e.Message.GetMentionPrefixLength(cne.Client.CurrentUser);

            if (((IEnumerable<string>)GetConfigProperty("StringPrefixes")).Any())
                foreach (var pfix in (IEnumerable<string>)GetConfigProperty("StringPrefixes"))
                    if (mpos == -1 && !string.IsNullOrWhiteSpace(pfix))
                        mpos = e.Message.GetStringPrefixLength(pfix, (bool)GetConfigProperty("CaseSensitive") ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase);

            if (mpos == -1 && GetConfigProperty("PrefixResolver") != null)
                mpos = await ((PrefixResolverDelegate)GetConfigProperty("PrefixResolver"))(e.Message).ConfigureAwait(false);

            if (mpos == -1)
                return;

            var pfx = e.Message.Content.Substring(0, mpos);
            var cnt = e.Message.Content.Substring(mpos);

            int startPos = 0;

            object[] parameters = new object[] { cnt, startPos, GetConfigProperty("QuotationMarks") };

            string fname = (string)typeof(CommandsNextUtilities).GetMethod("ExtractNextArgument", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, parameters);

            Command cmd = cne.FindCommand(cnt, out var args);
            CommandContext ctx = cne.CreateContext(e.Message, pfx, cmd, args);

            if (cmd is null)
            {
                AsyncEvent<CommandsNextExtension, CommandErrorEventArgs> _error = (AsyncEvent<CommandsNextExtension, CommandErrorEventArgs>)typeof(CommandsNextExtension).GetField("_error", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(cne);
                CommandErrorEventArgs commandErrorEventArgs = new CommandErrorEventArgs();
                typeof(CommandErrorEventArgs).GetProperty("Context", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(commandErrorEventArgs, ctx);
                typeof(CommandErrorEventArgs).GetProperty("Exception", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(commandErrorEventArgs, new CommandNotFoundException(fname ?? "UnknownCmd"));

                await _error.InvokeAsync(cne, commandErrorEventArgs).ConfigureAwait(false);
                return;
            }

            await ((ICommandExecutor)GetConfigProperty("CommandExecutor")).ExecuteAsync(ctx).ConfigureAwait(false);
        }

        internal static Task<RestResponse> ModifyVoiceStatusAsync(this DiscordChannel discordChannel, string status)
        {
            string text = "/channels/:channel_id/voice-status";
            string text2;
            DiscordApiClient apiClient = (DiscordApiClient)typeof(BaseDiscordClient).GetProperty("ApiClient", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(DiscordBotMain.botClient);
            object _rest = typeof(DiscordApiClient).GetProperty("_rest", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(apiClient);
            BaseDiscordClient _discord = (BaseDiscordClient)typeof(DiscordApiClient).GetProperty("_discord", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(apiClient);

            object[] parameters = new object[] { RestRequestMethod.PUT, text, new { channel_id = discordChannel.Id }, null };
            object bucket = _rest.GetType().GetMethod("GetBucket", BindingFlags.Public | BindingFlags.Instance).Invoke(_rest, parameters);
            text2 = (string)parameters[parameters.Length - 1];

            Uri apiUriFor = (Uri)typeof(Utilities).GetMethods(BindingFlags.NonPublic | BindingFlags.Static).First(m => m.Name == "GetApiUriFor" && m.GetParameters().Length == 1).Invoke(null, new object[] { text2 });
            //Uri apiUriFor = new Uri("https://discord.com/api/v9" + text2);
            return (Task<RestResponse>)typeof(DiscordApiClient).GetMethod("DoRequestAsync", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(apiClient, new object[] { _discord, bucket, apiUriFor, RestRequestMethod.PUT, text, new Dictionary<string, string>(), $"{{\"status\":\"{status}\"}}", null });
        }
    }
}
