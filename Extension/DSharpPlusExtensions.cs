#pragma warning disable CS8600 // Converting null literal or possible null value to non-nullable type.
#pragma warning disable CS8602 // Dereference of a possibly null reference.
#pragma warning disable DSP0004 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
#pragma warning disable CS8605 // Unboxing a possibly null value.
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
#pragma warning disable CS8604 // Possible null reference argument.

using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.Net;
using DSharpPlus.Net.Abstractions;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using static CatBot.Extensions.CustomDiscordActivity;

namespace CatBot.Extensions
{
    internal static class DSharpPlusExtensions
    {
        internal static async Task UpdateStatusAsync(this DiscordClient discordClient, CustomDiscordActivity customActivity, DiscordUserStatus? userStatus = null, DateTimeOffset? idleSince = null)
        {
            long num = (idleSince is not null) ? Utilities.GetUnixTime(idleSince.Value) : 0;
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
                IsAFK = idleSince is not null,
                Status = userStatus ?? DiscordUserStatus.Online,
            };
            await discordClient.SendPayloadAsync(GatewayOpCode.StatusUpdate, statusUpdate, 0);
        }

        internal static async ValueTask ModifyVoiceStatusAsync(this DiscordChannel discordChannel, string status)
        {
            BaseDiscordClient client = (BaseDiscordClient)typeof(SnowflakeObject).GetProperty("Discord", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(discordChannel);
            DiscordRestApiClient apiClient = (DiscordRestApiClient)typeof(BaseDiscordClient).GetProperty("ApiClient", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(client);
            RestClient rest = (RestClient)typeof(DiscordRestApiClient).GetField("rest", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(apiClient);
            Type restRequest = typeof(DiscordClient).Assembly.GetType("DSharpPlus.Net.RestRequest");
            object restRequestInst = Activator.CreateInstance(restRequest, true);

            restRequest.GetProperty("Route").SetValue(restRequestInst, $"/channels/{discordChannel.Id}/voice-status");
            restRequest.GetProperty("Url").SetValue(restRequestInst, $"/channels/{discordChannel.Id}/voice-status");
            restRequest.GetProperty("Method").SetValue(restRequestInst, HttpMethod.Put);
            restRequest.GetProperty("Payload").SetValue(restRequestInst, $"{{\"status\":\"{status}\"}}");
            ValueTask<RestResponse> response = (ValueTask<RestResponse>)rest.GetType().GetMethod("ExecuteRequestAsync", BindingFlags.NonPublic | BindingFlags.Instance).MakeGenericMethod(restRequest).Invoke(rest, [restRequestInst]);
            await response.ConfigureAwait(false);
        }
    }

    internal class CustomDiscordActivity
    {
        [JsonProperty(nameof(ApplicationID))]
        internal ulong ApplicationID { get; set; }

        [JsonProperty(nameof(Name))]
        internal string Name { get; set; } = "";

        [JsonConverter(typeof(StringEnumConverter))]
        [JsonProperty(nameof(ActivityType))]
        internal DiscordActivityType ActivityType { get; set; }

        [JsonProperty(nameof(State))]
        internal string State { get; set; } = " ";

        [JsonProperty(nameof(Delay))]
        internal int Delay { get; set; }

        internal CustomDiscordActivity() { }
        internal CustomDiscordActivity(ulong applicationID = 0, DiscordActivityType type = DiscordActivityType.Playing, string name = "", string state = " ", int delay = 60000)
        {
            ApplicationID = applicationID;
            ActivityType = type;
            Name = name;
            State = state;
            Delay = delay;
        }

        internal class TransportActivity
        {
            [JsonIgnore]
            internal ulong? ApplicationId { get; set; }

            [JsonProperty("application_id", NullValueHandling = NullValueHandling.Ignore)]
            public string ApplicationIdStr
            {
                get => ApplicationId?.ToString() ?? "";
                set => ApplicationId = ulong.Parse(value);
            }

            [JsonProperty("type", NullValueHandling = NullValueHandling.Ignore)]
            public DiscordActivityType ActivityType { get; set; }

            [JsonProperty("name", NullValueHandling = NullValueHandling.Ignore)]
            public string Name { get; set; }

            [JsonProperty("state", NullValueHandling = NullValueHandling.Ignore)]
            public string State { get; set; }
        }

        internal class StatusUpdate
        {
            [JsonProperty("status")]
            public string StatusString
            {
                get
                {
                    switch (Status)
                    {
                        case DiscordUserStatus.Offline:
                        case DiscordUserStatus.Invisible:
                            return "invisible";
                        case DiscordUserStatus.Online:
                            return "online";
                        case DiscordUserStatus.Idle:
                            return "idle";
                        case DiscordUserStatus.DoNotDisturb:
                            return "dnd";
                    }
                    return "online";
                }
                set
                {
                    switch (value)
                    {
                        case "invisible":
                            Status = DiscordUserStatus.Invisible;
                            break;
                        case "online":
                            Status = DiscordUserStatus.Online;
                            break;
                        case "idle":
                            Status = DiscordUserStatus.Idle;
                            break;
                        case "dnd":
                            Status = DiscordUserStatus.DoNotDisturb;
                            break;
                        default:
                            Status = DiscordUserStatus.Offline;
                            break;
                    }
                }
            }

            [JsonIgnore]
            internal DiscordUserStatus Status { get; set; }

            [JsonProperty("since", NullValueHandling = NullValueHandling.Ignore)]
            public long? IdleSince { get; set; }

            [JsonProperty("activities", NullValueHandling = NullValueHandling.Ignore)]
            public List<TransportActivity> Activities { get; set; }

            [JsonProperty("afk")]
            public bool IsAFK { get; set; }
        }
    }
}

#pragma warning restore CS8604 // Possible null reference argument.
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
#pragma warning restore CS8605 // Unboxing a possibly null value.
#pragma warning restore DSP0004 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
#pragma warning restore CS8602 // Dereference of a possibly null reference.
#pragma warning restore CS8600 // Converting null literal or possible null value to non-nullable type.