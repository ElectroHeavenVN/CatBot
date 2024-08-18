using Newtonsoft.Json;

namespace CatBot.Music.SponsorBlock
{
    [JsonConverter(typeof(SponsorBlockSegmentJsonConverter))]
    internal class SponsorBlockSegment
    {
        public SponsorBlockSegment() { }
        public SponsorBlockSegment(double start) 
        {
            Start = End = start;
        }
        public SponsorBlockSegment(double start, double end) 
        {
            Start = start;
            End = end;
        }
        public SponsorBlockSegment(double[] startEnd) 
        {
            Start = startEnd[0];
            End = startEnd[1];
        }
        internal double[] GetArray() => [Start, End];
        internal bool IsLengthZero() => End - Start == 0;
        internal double Start { get; set; }
        internal double End { get; set; }
    }
}
