#pragma warning disable CS8604 // Possible null reference argument.
#pragma warning disable CS8605 // Unboxing a possibly null value.
#pragma warning disable CS8602 // Dereference of a possibly null reference.
#pragma warning disable CS8600 // Converting null literal or possible null value to non-nullable type.
#pragma warning disable CS8619 // Nullability of reference types in value doesn't match target type.

using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using SoundCloudExplode;
using SoundCloudExplode.Bridge;
using SoundCloudExplode.Common;
using SoundCloudExplode.Exceptions;
using SoundCloudExplode.Tracks;
using SoundCloudExplode.Users;

namespace CatBot.SoundCloudExplodeExtension
{
    internal static class Extension
    {
        static async IAsyncEnumerable<Batch<Track>> GetTrackBatchesAsync(UserClient client, string url, string trackQuery, int offset = Constants.DefaultOffset, int limit = Constants.DefaultLimit, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            if (limit < 0 || limit > 200)
                throw new SoundcloudExplodeException($"Limit must be between 0 and 200");
            var user = await client.GetAsync(url, cancellationToken);
            if (user is null)
                yield break;
            var endpoint = (SoundcloudEndpoint)typeof(UserClient).GetField("<endpoint>P", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(client);
            var http = (HttpClient)typeof(UserClient).GetField("<http>P", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(client);
            MethodInfo executeGetAsync = typeof(UserClient).Assembly.GetType("SoundCloudExplode.Utils.Extensions.HttpExtensions").GetMethod("ExecuteGetAsync");

            var nextUrl = "";
            while (true)
            {
                if (!string.IsNullOrEmpty(nextUrl))
                    url = nextUrl;
                else
                    url = $"https://api-v2.soundcloud.com/{(trackQuery == "reposts" ? "stream" : "")}/users/{user.Id}/{trackQuery}?offset={offset}&limit={limit}&client_id={endpoint.ClientId}";
                ValueTask<string> executeGetAsyncValueTask = (ValueTask<string>)executeGetAsync.Invoke(null, [http, url, cancellationToken]);
                var response = await executeGetAsyncValueTask;
                var doc = JsonDocument.Parse(response).RootElement;
                var collectionStr = doc.GetProperty("collection").ToString();
                if (string.IsNullOrEmpty(collectionStr))
                    break;

                Type sourceGenerationContext = typeof(SoundCloudClient).Assembly.GetType("SoundCloudExplode.SourceGenerationContext");
                JsonTypeInfo<Track> track = (JsonTypeInfo<Track>)sourceGenerationContext.GetProperty("Track", BindingFlags.Public | BindingFlags.Instance).GetValue(sourceGenerationContext.GetProperty("Default", BindingFlags.Public | BindingFlags.Static).GetValue(null));
                List<Track> list = doc.GetProperty("collection")
                    .EnumerateArray()
                    .Select(x =>
                    {
                        return x.GetProperty("track").Deserialize(track);
                    })
                    .Where(tr => tr is not null)
                    .ToList();
                yield return new Batch<Track>(list);
                nextUrl = doc.GetProperty("next_href").GetString();
                if (string.IsNullOrEmpty(nextUrl))
                    break;
                nextUrl += $"&client_id={endpoint.ClientId}";
            }
        }

        internal static IAsyncEnumerable<Track> GetRepostedTracksAsync(this UserClient client, string url, int offset = 0, int limit = 50, CancellationToken cancellationToken = default) => FlattenAsync(GetTrackBatchesAsync(client, url, "reposts", offset, limit, cancellationToken));

        static IAsyncEnumerable<T> FlattenAsync<T>(IAsyncEnumerable<Batch<T>> source) where T : IBatchItem => SelectManyAsync(source, b => b.Items);

        static async IAsyncEnumerable<T> SelectManyAsync<TSource, T>(IAsyncEnumerable<TSource> source, Func<TSource, IEnumerable<T>> transform)
        {
            await foreach (var i in source)
            {
                foreach (var j in transform(i))
                    yield return j;
            }
        }
    }
}

#pragma warning restore CS8600 // Converting null literal or possible null value to non-nullable type.
#pragma warning restore CS8602 // Dereference of a possibly null reference.
#pragma warning restore CS8605 // Unboxing a possibly null value.
#pragma warning restore CS8604 // Possible null reference argument.
#pragma warning restore CS8619 // Nullability of reference types in value doesn't match target type.