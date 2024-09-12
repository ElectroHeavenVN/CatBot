using System.Reflection;
using DSharpPlus;
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
            if (string.IsNullOrEmpty(customActivity.State))
            {
                await discordClient.UpdateStatusAsync(new DiscordActivity(customActivity.Name, customActivity.ActivityType), userStatus, idleSince);
                return;
            }
            long num = (idleSince != null) ? Utilities.GetUnixTime(idleSince.Value) : 0;
            StatusUpdate statusUpdate = new StatusUpdate
            {
                Activities = new List<TransportActivity>()
                {
                    new TransportActivity()
                    {
                        ApplicationId = discordClient.CurrentApplication.Id,
                        DiscordActivityType = customActivity.ActivityType,
                        Name = customActivity.Name,
                        State = customActivity.State
                    }
                },
                IdleSince = num,
                IsAFK = idleSince != null,
                Status = userStatus ?? DiscordUserStatus.Online,
            };
#pragma warning disable DSP0004 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
            await discordClient.SendPayloadAsync(GatewayOpCode.StatusUpdate, statusUpdate, 0);
#pragma warning restore DSP0004 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
        }
        
        internal static async Task UpdateStatusAsync(this DiscordClient discordClient, List<CustomDiscordActivity> customActivities, DiscordUserStatus? userStatus = null, DateTimeOffset? idleSince = null)
        {
            if (customActivities.Any(customActivity => string.IsNullOrEmpty(customActivity.State)))
                return;
            long num = (idleSince != null) ? Utilities.GetUnixTime(idleSince.Value) : 0;
            StatusUpdate statusUpdate = new StatusUpdate
            {
                Activities = new List<TransportActivity>(),
                IdleSince = num,
                IsAFK = idleSince != null,
                Status = userStatus ?? DiscordUserStatus.Online,
            };
            foreach (CustomDiscordActivity customActivity in customActivities)
            {
                statusUpdate.Activities.Add(new TransportActivity()
                {
                    ApplicationId = discordClient.CurrentApplication.Id,
                    DiscordActivityType = customActivity.ActivityType,
                    Name = customActivity.Name,
                    State = customActivity.State
                });
            }
#pragma warning disable DSP0004 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
            await discordClient.SendPayloadAsync(GatewayOpCode.StatusUpdate, statusUpdate, 0);
#pragma warning restore DSP0004 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
        }

        internal static ValueTask<RestResponse> ModifyVoiceStatusAsync(this DiscordChannel discordChannel, string status)
        {
            /*
            RestRequest request = new()
            {
                Route = $"{Endpoints.CHANNELS}/{channelId}",
                Url = new($"{Endpoints.CHANNELS}/{channelId}"),
                Method = HttpMethod.Put
            };

            await this.rest.ExecuteRequestAsync(request);
            */

            string route = $"channels/{discordChannel.Id}/voice-status";
            DiscordApiClient apiClient = (DiscordApiClient)typeof(BaseDiscordClient).GetProperty("ApiClient", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(DiscordBotMain.botClient);
            RestClient rest = (RestClient)typeof(DiscordApiClient).GetField("rest", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(apiClient);
            Type? restRequest = typeof(DiscordApiClient).Assembly.GetType("DSharpPlus.Net.RestRequest");
            object restRequestObj = Activator.CreateInstance(restRequest, true);
            restRequest.GetProperty("Route", BindingFlags.Public | BindingFlags.Instance).SetValue(restRequestObj, route);
            restRequest.GetProperty("Url", BindingFlags.Public | BindingFlags.Instance).SetValue(restRequestObj, route);
            restRequest.GetProperty("Method", BindingFlags.Public | BindingFlags.Instance).SetValue(restRequestObj, HttpMethod.Put);
            restRequest.GetProperty("Payload", BindingFlags.Public | BindingFlags.Instance).SetValue(restRequestObj, $"{{\"status\":\"{status}\"}}");
            return (ValueTask<RestResponse>)typeof(RestClient).GetMethod("ExecuteRequestAsync", BindingFlags.NonPublic | BindingFlags.Instance).MakeGenericMethod([restRequest]).Invoke(rest, [restRequestObj]);
        }
    }
}
