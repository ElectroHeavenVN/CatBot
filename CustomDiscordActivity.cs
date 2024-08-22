using DSharpPlus.Entities;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace CatBot
{
    internal class CustomDiscordActivity 
    {
        [JsonProperty("type", NullValueHandling = NullValueHandling.Ignore)]
        [JsonConverter(typeof(StringEnumConverter))]
        internal DiscordActivityType ActivityType { get; set; }

        [JsonProperty("name", NullValueHandling = NullValueHandling.Ignore)]
        internal string Name { get; set; } = "";

        [JsonProperty("state", NullValueHandling = NullValueHandling.Ignore)]
        internal string State { get; set; } = "";

        CustomDiscordActivity() { }

        internal CustomDiscordActivity(DiscordActivityType type = DiscordActivityType.Playing, string name = "", string state = " ")
        {
            ActivityType = type;
            Name = name;
            State = state;
        }
        
        internal class TransportActivity
        {
            [JsonIgnore]
            internal ulong? ApplicationId { get; set; }

            [JsonProperty("application_id", NullValueHandling = NullValueHandling.Ignore)]
            public string ApplicationIdStr
            {
                get => ApplicationId.HasValue ? ApplicationId.Value.ToString() : "0";

                set => ApplicationId = value != null ? ulong.Parse(value) : null;
            }

            [JsonProperty("type", NullValueHandling = NullValueHandling.Ignore)]
            public DiscordActivityType DiscordActivityType { get; set; }

            [JsonProperty("name", NullValueHandling = NullValueHandling.Ignore)]
            public required string Name { get; set; }

            [JsonProperty("state", NullValueHandling = NullValueHandling.Ignore)]
            public required string State { get; set; }
        }

        internal class StatusUpdate
        {
            [JsonProperty("status")]
            public string StatusString
            {
                get
                {
                    return Status switch
                    {
                        DiscordUserStatus.Offline or DiscordUserStatus.Invisible => "invisible",
                        DiscordUserStatus.Online => "online",
                        DiscordUserStatus.Idle => "idle",
                        DiscordUserStatus.DoNotDisturb => "dnd",
                        _ => "online",
                    };
                }
                set
                {
                    Status = value switch
                    {
                        "invisible" => DiscordUserStatus.Invisible,
                        "online" => DiscordUserStatus.Online,
                        "idle" => DiscordUserStatus.Idle,
                        "dnd" => DiscordUserStatus.DoNotDisturb,
                        _ => DiscordUserStatus.Offline,
                    };
                }
            }

            [JsonIgnore]
            internal DiscordUserStatus Status { get; set; }

            [JsonProperty("since", NullValueHandling = NullValueHandling.Ignore)]
            public long? IdleSince { get; set; }

            [JsonProperty("activities", NullValueHandling = NullValueHandling.Ignore)]
            public List<TransportActivity>? Activities { get; set; }

            [JsonProperty("afk")]
            public bool IsAFK { get; set; }
        }
    }
}
