using Newtonsoft.Json;

namespace CatBot.Music.SponsorBlock
{
    internal class SponsorBlockSegmentJsonConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType) => objectType == typeof(SponsorBlockSegment);

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.StartArray)
                return new SponsorBlockSegment((double[])serializer.Deserialize(reader, typeof(double[]))); 
            throw new Exception();
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer) => writer.WriteValue(((SponsorBlockSegment)value).GetArray());
    }
}
