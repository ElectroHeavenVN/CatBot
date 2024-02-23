using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.Net.Abstractions;
using Newtonsoft.Json;
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
            string customStatusRawJSONData = JsonConvert.SerializeObject(new GatewayPayload
            {
                OpCode = GatewayOpCode.StatusUpdate,
                Data = statusUpdate
            });
            //await discordClient.SendRawPayloadAsync(customStatusRawJSONData).ConfigureAwait(false);
            Task sendCustomStatusTask = (Task)typeof(DiscordClient).GetMethod("SendRawPayloadAsync", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(discordClient, new object[] { customStatusRawJSONData });
            await sendCustomStatusTask.ConfigureAwait(false);
        }
    }
}
