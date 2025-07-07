using Newtonsoft.Json;
using System.Collections.Generic;

namespace CatBot
{
    internal class Data
    {
        static Data singletonInstance = new Data();
        internal static Data gI() => singletonInstance;

        [JsonProperty(nameof(CachedLocalSongAlbumArtworks))]
        internal Dictionary<string, string> CachedLocalSongAlbumArtworks { get; set; } = new Dictionary<string, string>();
    }
}
