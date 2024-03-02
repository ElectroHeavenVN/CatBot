using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DSharpPlus.Entities;
using DSharpPlus.Net.Abstractions;
using Newtonsoft.Json;

namespace CatBot
{
    internal class CustomDiscordActivity 
    {
        internal ulong? ApplicationID { get; set; }

        internal ActivityType ActivityType { get; set; }

        internal string Name { get; set; }
    
        internal string State { get; set; }

        internal CustomDiscordActivity(ulong? applicationID = 0, ActivityType type = ActivityType.Playing, string name = "", string state = " ")
        {
            ApplicationID = applicationID;
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
                get => ApplicationId?.ToString();
                set => ApplicationId = ulong.Parse(value);
            }

            [JsonProperty("type", NullValueHandling = NullValueHandling.Ignore)]
            public ActivityType ActivityType { get; set; }

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
                        case UserStatus.Offline:
                        case UserStatus.Invisible:
                            return "invisible";
                        case UserStatus.Online:
                            return "online";
                        case UserStatus.Idle:
                            return "idle";
                        case UserStatus.DoNotDisturb:
                            return "dnd";
                    }
                    return "online";
                }
                set
                {
                    switch (value)
                    {
                        case "invisible":
                            Status = UserStatus.Invisible;
                            break;
                        case "online":
                            Status = UserStatus.Online;
                            break;
                        case "idle":
                            Status = UserStatus.Idle;
                            break;
                        case "dnd":
                            Status = UserStatus.DoNotDisturb;
                            break;
                        default:
                            Status = UserStatus.Offline;
                            break;
                    }
                }
            }

            [JsonIgnore]
            internal UserStatus Status { get; set; }

            [JsonProperty("since", NullValueHandling = NullValueHandling.Ignore)]
            public long? IdleSince { get; set; }

            [JsonProperty("activities", NullValueHandling = NullValueHandling.Ignore)]
            public List<TransportActivity> Activities { get; set; }

            [JsonProperty("afk")]
            public bool IsAFK { get; set; }
        }

        //internal class GatewayPayload
        //{
        //    [JsonProperty("op")]
        //    public GatewayOpCode OpCode { get; set; }

        //    [JsonProperty("d")]
        //    public object Data { get; set; }
        //}
    }
}
