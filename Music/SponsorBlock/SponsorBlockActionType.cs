using System.Runtime.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace DiscordBot.Music.SponsorBlock
{
    [JsonConverter(typeof(StringEnumConverter))]
    internal enum SponsorBlockActionType
    {
        [EnumMember(Value = "skip")]
        Skip,
        [EnumMember(Value = "mute")]
        Mute,
        [EnumMember(Value = "full")]
        Full,
        [EnumMember(Value = "poi")]
        PointOfInterest,
        [EnumMember(Value = "chapter")]
        Chapter
    }
}
