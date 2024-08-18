using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

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
