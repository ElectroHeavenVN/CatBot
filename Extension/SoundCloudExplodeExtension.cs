using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using System.Threading;
using System.Threading.Tasks;
using DSharpPlus.Net;
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
        static async Task<IEnumerable<Batch<Track>>> GetTrackBatchesAsync(UserClient client, string url, string trackQuery, int offset = Constants.DefaultOffset, int limit = Constants.DefaultLimit, CancellationToken cancellationToken = default)
        {
            if (limit < 0 || limit > 200)
                throw new SoundcloudExplodeException($"Limit must be between 0 and 200");
            var user = await client.GetAsync(url, cancellationToken);
            if (user is null)
                return Enumerable.Empty<Batch<Track>>();
            var endpoint = (SoundcloudEndpoint)typeof(UserClient).GetField("<endpoint>P", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(client);
            var http = (HttpClient)typeof(UserClient).GetField("<http>P", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(client);
            MethodInfo executeGetAsync = typeof(UserClient).Assembly.GetType("SoundCloudExplode.Utils.Extensions.HttpExtensions").GetMethod("ExecuteGetAsync");

            var nextUrl = "";
            var batches = new List<Batch<Track>>();
            while (true)
            {
                if (!string.IsNullOrEmpty(nextUrl))
                    url = nextUrl;
                else
                    url = $"https://api-v2.soundcloud.com/{(trackQuery == "reposts" ? "stream" : "")}/users/{user.Id}/{trackQuery}?offset={offset}&limit={limit}&client_id={endpoint.ClientId}";
                ValueTask<string> executeGetAsyncValueTask = (ValueTask<string>)executeGetAsync.Invoke(null, new object[] { http, url, cancellationToken });
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
                    .Where(tr => tr != null)
                    .ToList();
                batches.Add(new Batch<Track>(list));
                nextUrl = doc.GetProperty("next_href").GetString();
                if (string.IsNullOrEmpty(nextUrl))
                    break;
                nextUrl += $"&client_id={endpoint.ClientId}";
                if (string.IsNullOrEmpty(collectionStr))
                    break;
            }
            return batches;
        }

        public static async Task<IEnumerable<Track>> GetRepostedTracksAsync(this UserClient client, string url, int offset = 0, int limit = 50, CancellationToken cancellationToken = default)
        {
            return await FlattenAsync(GetTrackBatchesAsync(client, url, "reposts", offset, limit, cancellationToken));
        }

        static async Task<IEnumerable<T>> FlattenAsync<T>(Task<IEnumerable<Batch<T>>> sourceTask) where T : IBatchItem => await SelectManyAsync(sourceTask, b => b.Items);

        static async Task<IEnumerable<T>> SelectManyAsync<TSource, T>(Task<IEnumerable<TSource>> sourceTask, Func<TSource, IEnumerable<T>> transform)
        {
            var source = await sourceTask;
            var result = new List<T>();
            foreach (var item in source)
                result.AddRange(transform(item));
            return result;
        }
    }
}
