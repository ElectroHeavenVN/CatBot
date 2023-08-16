using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using SpotifyExplode.Artists;
using SpotifyExplode.Playlists;
using SpotifyExplode.Tracks;

namespace DiscordBot.Extension
{
    internal static class SpotifyExplodeExtension
    {
        internal static async ValueTask<List<Track>> GetTopTracks(this ArtistClient client, ArtistId artistId, string market = "US", CancellationToken cancellationToken = default)
        {
            object _spotifyHttp = typeof(ArtistClient).GetField(nameof(_spotifyHttp), BindingFlags.NonPublic | BindingFlags.Instance).GetValue(client);
            JsonSerializerOptions options = (JsonSerializerOptions)typeof(Track).Assembly.GetType("SpotifyExplode.Utils.JsonDefaults").GetProperty("Options", BindingFlags.Public | BindingFlags.Static).GetValue(null);
            MethodInfo getAsync = typeof(Track).Assembly.GetType("SpotifyExplode.SpotifyHttp").GetMethod("GetAsync", BindingFlags.Public | BindingFlags.Instance);
            ValueTask<string> vstr = (ValueTask<string>)getAsync.Invoke(_spotifyHttp, new object[] { $"https://api.spotify.com/v1/artists/{artistId}/top-tracks?market={market}", cancellationToken });
            await vstr.ConfigureAwait(false);
            JsonNode jsonNode = JsonNode.Parse(vstr.Result);
            return JsonSerializer.Deserialize<List<Track>>(jsonNode["tracks"].ToJsonString(), options);
        }

        internal static async ValueTask<string[]> GetImagesAsync(this PlaylistClient client, PlaylistId playlistId, CancellationToken cancellationToken = default)
        {
            object _spotifyHttp = typeof(PlaylistClient).GetField(nameof(_spotifyHttp), BindingFlags.NonPublic | BindingFlags.Instance).GetValue(client);
            JsonSerializerOptions options = (JsonSerializerOptions)typeof(Track).Assembly.GetType("SpotifyExplode.Utils.JsonDefaults").GetProperty("Options", BindingFlags.Public | BindingFlags.Static).GetValue(null);
            MethodInfo getAsync = typeof(Track).Assembly.GetType("SpotifyExplode.SpotifyHttp").GetMethod("GetAsync", BindingFlags.Public | BindingFlags.Instance);
            ValueTask<string> vstr = (ValueTask<string>)getAsync.Invoke(_spotifyHttp, new object[] { $"https://api.spotify.com/v1/playlists/{playlistId}", cancellationToken });
            await vstr.ConfigureAwait(false);
            JsonNode jsonNode = JsonNode.Parse(vstr.Result);
            return jsonNode["images"].AsArray().Where(n => n["url"] != null).Select(n => n["url"].ToString()).ToArray();
        }
    }
}
