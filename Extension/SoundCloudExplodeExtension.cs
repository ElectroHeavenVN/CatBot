using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using SoundCloudExplode.Bridge;
using SoundCloudExplode.Exceptions;
using SoundCloudExplode.Playlists;
using SoundCloudExplode.Tracks;
using SoundCloudExplode.Users;

namespace DiscordBot.SoundCloudExplodeExtension
{
    internal static class Extension
    {
        public static async ValueTask<List<Track>> GetLikedTracksAsync(this UserClient userClient, string url, int offset = 0, int limit = 50, CancellationToken cancellationToken = default)
        {
            if (limit < 0 || limit > 200)
                throw new SoundcloudExplodeException($"Limit must be between {0} and {200}");

            User user = await userClient.GetAsync(url, cancellationToken);
            HttpClient _http = (HttpClient)typeof(UserClient).GetField("_http", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(userClient);
            SoundcloudEndpoint _endpoint = (SoundcloudEndpoint)typeof(UserClient).GetField("_endpoint", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(userClient);

            ValueTask<string> task = (ValueTask<string>)typeof(UserClient).Assembly.GetType("SoundCloudExplode.Utils.Extensions.HttpExtensions").GetMethod("ExecuteGetAsync", new Type[] { typeof(HttpClient), typeof(string), typeof(CancellationToken) }).Invoke(null, new object[] { _http, $"https://api-v2.soundcloud.com/users/{user.Id}/likes?offset={offset}&limit={limit}&client_id={_endpoint.ClientId}", cancellationToken });
            await task.ConfigureAwait(false);
            JsonNode node = JsonNode.Parse(task.Result);
            List<Track> result = new List<Track>();
            return node["collection"].AsArray().Where(n => n["track"] != null).Select(n => JsonSerializer.Deserialize<Track>(n["track"].ToString())).ToList();
        }

        public static async ValueTask<List<Track>> GetRepostTracksAsync(this UserClient userClient, string url, int offset = 0, int limit = 50, CancellationToken cancellationToken = default)
        {
            if (limit < 0 || limit > 200)
                throw new SoundcloudExplodeException($"Limit must be between {0} and {200}");

            User user = await userClient.GetAsync(url, cancellationToken);
            HttpClient _http = (HttpClient)typeof(UserClient).GetField("_http", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(userClient);
            SoundcloudEndpoint _endpoint = (SoundcloudEndpoint)typeof(UserClient).GetField("_endpoint", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(userClient);

            ValueTask<string> task = (ValueTask<string>)typeof(UserClient).Assembly.GetType("SoundCloudExplode.Utils.Extensions.HttpExtensions").GetMethod("ExecuteGetAsync", new Type[] { typeof(HttpClient), typeof(string), typeof(CancellationToken) }).Invoke(null, new object[] { _http, $"https://api-v2.soundcloud.com/stream/users/{user.Id}/reposts?offset={offset}&limit={limit}&client_id={_endpoint.ClientId}", cancellationToken });
            await task.ConfigureAwait(false);
            JsonNode node = JsonNode.Parse(task.Result);
            return node["collection"].AsArray().Where(n => n["track"] != null).Select(n => JsonSerializer.Deserialize<Track>(n["track"].ToString())).ToList();
        }
    }
}
