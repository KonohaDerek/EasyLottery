using System.Net.Http.Json;
using System.Text.RegularExpressions;
using EasyLotteryDomain.Models.Config;
using EasyLotteryDomain.Models.Youtube;
using Google.Apis.Services;
using Google.Apis.YouTube.v3;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Youtube.Api.V3;
using Grpc.Core;
using Grpc.Net.Client;
using System.Threading.Channels;

namespace EasyLotteryDomain.Services
{
    public class YouTubeServiceHelper
    {
        private readonly IConfiguration configuration;
        private readonly SystemSettingsService? systemSettingsService;
        private readonly ILogger<YouTubeServiceHelper> logger;

        public YouTubeServiceHelper(SystemSettingsService systemSettingsService, IConfiguration configuration, ILogger<YouTubeServiceHelper> logger)
        {
            this.logger = logger;
            this.systemSettingsService = systemSettingsService;
            this.configuration = configuration;
        }

        public YouTubeServiceHelper(IConfiguration configuration, ILogger<YouTubeServiceHelper> logger)
        {
            this.logger = logger;
            this.configuration = configuration;
        }

        public async Task<bool> HasConfiguredCredentialsAsync()
        {
            var settings = await GetEffectiveYouTubeSettingsAsync();
            return !string.IsNullOrWhiteSpace(settings.ApiKey);
        }


        private async Task<YouTubeApiSettings> GetEffectiveYouTubeSettingsAsync()
        {
            var bundledSettings = GetBundledYouTubeSettings();
            if (systemSettingsService == null)
            {
                return bundledSettings;
            }

            var persistedSettings = await systemSettingsService.GetYouTubeSettingsAsync();
            return MergeYouTubeSettings(bundledSettings, persistedSettings);
        }

        private YouTubeApiSettings GetBundledYouTubeSettings()
        {
            return new YouTubeApiSettings
            {
                ApiKey = configuration["YouTubeApi:ApiKey"] ?? ""
            };
        }

        private static YouTubeApiSettings MergeYouTubeSettings(YouTubeApiSettings bundledSettings, YouTubeApiSettings? persistedSettings)
        {
            persistedSettings ??= new YouTubeApiSettings();

            return new YouTubeApiSettings
            {
                ApiKey = !string.IsNullOrWhiteSpace(bundledSettings.ApiKey) ? bundledSettings.ApiKey : persistedSettings.ApiKey ?? ""
            };
        }


        public async Task<string?> GetLiveChatIDFromLiveUrlAsync(string youtubeUrl)
        {
            string liveID = GetYouTubeLiveID(youtubeUrl);
            
            if (string.IsNullOrWhiteSpace(liveID))
            {
                logger.LogInformation("無法解析 YouTube URL。直播地址尚未開始直播或已結束。");
                throw new Exception("直播地址尚未開始直播或已結束");
            }

            var liveInfo =await GetYoutubeLiveInfoAsync(liveID);
            if (liveInfo == null)
            {
                logger.LogInformation("直播地址尚未開始直播或已結束");
                throw new Exception("直播地址尚未開始直播或已結束");
            }

            return liveInfo.ActiveLiveChatId;
        }


        public async Task<IEnumerable<Google.Apis.YouTube.v3.Data.LiveChatMessage>> ListLiveChatMessageAsync(string chatID)
        {
            var apiKey = await GetConfiguredApiKeyAsync();
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                throw new InvalidOperationException("YouTube API key is required. Please configure the API key first.");
            }

            using var httpClient = new HttpClient();

            var response = await httpClient.GetAsync($"https://www.googleapis.com/youtube/v3/liveChat/messages?liveChatId={Uri.EscapeDataString(chatID)}&part=snippet,authorDetails&key={Uri.EscapeDataString(apiKey)}");

            if (response.IsSuccessStatusCode)
            {
                var info = await response.Content.ReadFromJsonAsync<Google.Apis.YouTube.v3.Data.LiveChatMessageListResponse>();
                if (info == null)
                {
                    return [];
                }

                return info.Items;
            }

            logger.LogInformation($"Error: {response.StatusCode}");
            return [];
        }


        public async Task<Google.Apis.YouTube.v3.Data.VideoLiveStreamingDetails?> GetYoutubeLiveInfoAsync(string liveID)
        {
            var apiKey = await GetConfiguredApiKeyAsync();
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                throw new InvalidOperationException("YouTube API key is required. Please configure the API key first.");
            }

            using var httpClient = new HttpClient();
            var response = await httpClient.GetAsync($"https://www.googleapis.com/youtube/v3/videos?part=liveStreamingDetails&id={Uri.EscapeDataString(liveID)}&key={Uri.EscapeDataString(apiKey)}");

            if (response.IsSuccessStatusCode)
            {
                var info = await response.Content.ReadFromJsonAsync<Google.Apis.YouTube.v3.Data.VideoListResponse>();
                return info?.Items.FirstOrDefault(o => o.LiveStreamingDetails != null)?.LiveStreamingDetails;
            }

            logger.LogInformation($"Error: {response.StatusCode}");
            return null;
        }

        private async Task<string> GetConfiguredApiKeyAsync()
        {
            var settings = await GetEffectiveYouTubeSettingsAsync();
            return settings.ApiKey ?? string.Empty;
        }

        public static string GetYouTubeLiveID(string url)
        {
            // 正則表達式來匹配 YouTube Video ID
            var regex = new Regex(@"(?:https?:\/\/)?(?:www\.)?(?:youtube\.com\/(?:watch\?v=|live\/|embed\/|v\/|.+\?v=)|youtu\.be\/)([\w_-]+)");
            var match = regex.Match(url);

            if (match.Success)
            {
                return match.Groups[1].Value;  // 取得 Video ID
            }

            return "";  // 若無法匹配則返回 null
        }

        public ChannelReader<LiveChatMessageInfo> ListenLiveChatMessagesByGrpcAsync(string liveChatId, CancellationToken cancellationToken)
        {
            var channel = Channel.CreateUnbounded<LiveChatMessageInfo>();

            _ = Task.Run(async () =>
            {
                var grpcUrl = configuration["Grpc:LiveChatServiceUrl"] ?? "https://youtube.googleapis.com";

                using var grpcChannel = GrpcChannel.ForAddress(grpcUrl);
                var client = new V3DataLiveChatMessageService.V3DataLiveChatMessageServiceClient(grpcChannel);

                string? nextPageToken = null;
                var metadata = new Metadata();

                try
                {
                    while (!cancellationToken.IsCancellationRequested)
                    {
                        var request = new LiveChatMessageListRequest
                        {
                            LiveChatId = liveChatId,
                            MaxResults = 20,
                        };
                        if (!string.IsNullOrEmpty(nextPageToken))
                            request.PageToken = nextPageToken;
                        request.Part.Add("snippet");
                        request.Part.Add("authorDetails");

                        using var call = client.StreamList(request, metadata, cancellationToken: cancellationToken);

                        await foreach (var response in call.ResponseStream.ReadAllAsync(cancellationToken))
                        {
                            foreach (var msg in response.Items)
                            {
                                var info = new LiveChatMessageInfo(
                                    msg.AuthorDetails?.ChannelId ?? "",
                                    msg.AuthorDetails?.DisplayName ?? "",
                                    msg.Snippet?.DisplayMessage ?? "",
                                    msg
                                );
                                await channel.Writer.WriteAsync(info, cancellationToken);
                            }
                            nextPageToken = response.NextPageToken;
                        }

                        if (string.IsNullOrEmpty(nextPageToken))
                            break;
                    }
                }
                catch (RpcException ex) when (ex.StatusCode == StatusCode.Cancelled)
                {
                    logger.LogInformation("gRPC 監聽已取消");
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "gRPC 監聽發生錯誤");
                }
                finally
                {
                    channel.Writer.Complete();
                }
            }, cancellationToken);

            return channel.Reader;
        }
    }
}
