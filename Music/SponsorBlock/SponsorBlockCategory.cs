using System.Runtime.Serialization;
using DSharpPlus.Commands.Processors.SlashCommands.ArgumentModifiers;
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
        [ChoiceDisplayName("Intermission/Intro animation")]
        Intro = 2,
        [EnumMember(Value = "outro")]
        [ChoiceDisplayName("Endcards/Credits")]
        Outro = 4,
        [EnumMember(Value = "selfpromo")]
        [ChoiceDisplayName("Unpaid/Self promotion")]
        SelfPromo = 8,
        [EnumMember(Value = "preview")]
        [ChoiceDisplayName("Preview/Recap")]
        Preview = 16,
        [EnumMember(Value = "filler")]
        [ChoiceDisplayName("Filler tangent/Jokes")]
        Filler = 32,
        [EnumMember(Value = "interaction")]
        [ChoiceDisplayName("Interaction reminder (Subscribe)")]
        Interaction = 64,
        [EnumMember(Value = "music_offtopic")]
        [ChoiceDisplayName("Music: Non-music section")]
        MusicOffTopic = 128,
        [ChoiceDisplayName("Tất cả")]
        All = Sponsor | Intro | Outro | SelfPromo | Preview | Filler | Interaction | MusicOffTopic
    }
}
