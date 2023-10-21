using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using SoundCloudExplode.Bridge;
using SoundCloudExplode.Common;
using SoundCloudExplode.Exceptions;
using SoundCloudExplode.Tracks;
using SoundCloudExplode.Users;

namespace CatBot.SoundCloudExplodeExtension
{
    public class SoundCloudCollectionItem : IBatchItem
    {
        [JsonPropertyName("created_at")]
        public DateTime CreatedAt { get; set; }
            
        [JsonPropertyName("type")]
        public string Type { get; set; }

        [JsonPropertyName("kind")]
        public string Kind { get; set; }

        [JsonPropertyName("user")]
        public User User { get; set; }

        [JsonPropertyName("uuid")]
        public string UUID { get; set; }

        [JsonPropertyName("caption")]
        public string Caption { get; set; }

        [JsonPropertyName("track")]
        public Track Track { get; set; }
    }

    internal static class Extension
    {
        public static async Task<IEnumerable<Batch<SoundCloudCollectionItem>>> GetItemBatchesAsync(UserClient client, string url, string queryPart, int offset = 0, int limit = 50, CancellationToken cancellationToken = default)
        {
            SoundcloudEndpoint _endpoint = (SoundcloudEndpoint)typeof(UserClient).GetField("_endpoint", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(client);
            HttpClient _http = (HttpClient)typeof(UserClient).GetField("_http", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(client);
            if (limit < 0 || limit > 200)
                throw new SoundcloudExplodeException($"Limit must be between 0 and 200");
            User user = await client.GetAsync(url, cancellationToken);
            if (user == null)
                return Enumerable.Empty<Batch<SoundCloudCollectionItem>>();
            string nextUrl = null;
            var batches = new List<Batch<SoundCloudCollectionItem>>();
            while (true)
            {
                url = nextUrl ?? $"https://api-v2.soundcloud.com{(queryPart == "reposts" ? "/stream" : "")}/users/{user.Id}/{queryPart}?offset={offset}&limit={limit}&client_id={_endpoint.ClientId}";
                ValueTask<string> executeGetAsyncValueTask = (ValueTask<string>)typeof(UserClient).Assembly.GetType("SoundCloudExplode.Utils.Extensions.HttpExtensions").GetMethod("ExecuteGetAsync").Invoke(null, new object[] { _http, url, cancellationToken });
                await executeGetAsyncValueTask.ConfigureAwait(false);
                JsonElement doc = JsonDocument.Parse(executeGetAsyncValueTask.Result).RootElement;
                string collectionStr = doc.GetProperty("collection").ToString();
                if (!string.IsNullOrEmpty(collectionStr))
                {
                    List<SoundCloudCollectionItem> list = JsonSerializer.Deserialize<List<SoundCloudCollectionItem>>(collectionStr);
                    if (list == null || !list.Any())
                        break;
                    batches.Add(new Batch<SoundCloudCollectionItem>(list));
                    nextUrl = doc.GetProperty("next_href").GetString();
                    if (!string.IsNullOrEmpty(nextUrl))
                        nextUrl = nextUrl + "&client_id=" + _endpoint.ClientId;
                    else
                        break;
                }
                else
                    break;
            }
            return batches;
        }

        public static async Task<IEnumerable<SoundCloudCollectionItem>> GetLikedItemsAsync(this UserClient client, string url, int offset = 0, int limit = 50, CancellationToken cancellationToken = default)
        {
            return await FlattenAsync(GetItemBatchesAsync(client, url, "likes", offset, limit, cancellationToken));
        }

        public static async Task<IEnumerable<SoundCloudCollectionItem>> GetRepostItemsAsync(this UserClient client, string url, int offset = 0, int limit = 50, CancellationToken cancellationToken = default)
        {
            return await FlattenAsync(GetItemBatchesAsync(client, url, "reposts", offset, limit, cancellationToken));
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
