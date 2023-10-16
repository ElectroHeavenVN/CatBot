using System.Runtime.Serialization;
using DSharpPlus.SlashCommands;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace CatBot.Music.SponsorBlock
{
    [JsonConverter(typeof(StringEnumConverter))]
    public enum SponsorBlockCategory
    {
        [EnumMember(Value = "sponsor")]
        Sponsor = 1,
        [EnumMember(Value = "intro")]
        [ChoiceName("Intermission/Intro animation")]
        Intro = 2,
        [EnumMember(Value = "outro")]
        [ChoiceName("Endcards/Credits")]
        Outro = 4,
        [EnumMember(Value = "selfpromo")]
        [ChoiceName("Unpaid/Self promotion")]
        SelfPromo = 8,
        [EnumMember(Value = "preview")]
        [ChoiceName("Preview/Recap")]
        Preview = 16,
        [EnumMember(Value = "filler")]
        [ChoiceName("Filler tangent/Jokes")]
        Filler = 32,
        [EnumMember(Value = "interaction")]
        [ChoiceName("Interaction reminder (Subscribe)")]
        Interaction = 64,
        [EnumMember(Value = "music_offtopic")]
        [ChoiceName("Music: Non-music section")]
        MusicOffTopic = 128,
        [ChoiceName("Tất cả")]
        All = Sponsor | Intro | Outro | SelfPromo | Preview | Filler | Interaction | MusicOffTopic
    }
}
