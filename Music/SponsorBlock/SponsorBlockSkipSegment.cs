using Newtonsoft.Json;

namespace CatBot.Music.SponsorBlock
{
    internal class SponsorBlockSkipSegment
    {
        [JsonProperty("segment")]
        internal SponsorBlockSegment Segment { get; set; }
        [JsonProperty("UUID")]
        internal string UUID { get; set; }
        [JsonProperty("category")]
        internal SponsorBlockCategory Category { get; set; }
        [JsonProperty("videoDuration")]
        internal float VideoDuration { get; set; }
        [JsonProperty("actionType")]
        internal SponsorBlockActionType ActionType { get; set; }
        [JsonProperty("locked")]
        internal bool Locked { get; set; }
        [JsonProperty("votes")]
        internal int Votes { get; set; }
        [JsonProperty("description")]
        internal string Description { get; set; }
    }
}
