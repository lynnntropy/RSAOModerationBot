using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using RedditSharp.Things;

namespace RSAOModerationBot.Module
{
    public class DiscordWebhookModule : IPostMonitorModule
    {
        private readonly HttpClient _httpClient;
        private readonly Subreddit _subreddit;
        private readonly string[] _webhookUrls;

        public DiscordWebhookModule(
            IConfiguration configuration,
            HttpClient httpClient,
            Subreddit subreddit
        )
        {
            _httpClient = httpClient;
            _subreddit = subreddit;
            _webhookUrls = configuration.GetSection("discord:webhookUrls").Get<string[]>();
        }

        public async Task ProcessNewPostsAsync(List<Post> posts)
        {
            if (_webhookUrls == null) return;
            
            foreach (var url in _webhookUrls)
            {
                foreach (var post in posts)
                {
                    await SendMessageToWebhookAsync(post, url);
                }
            }
        }

        private async Task SendMessageToWebhookAsync(Post post, String webhookUrl)
        {
            var descriptionBuilder = new StringBuilder();
            descriptionBuilder.Append($"**{post.Title}**");

            if (post.SelfText.Length > 0)
            {
                descriptionBuilder.Append("\n");

                if (post.SelfText.Length <= 250)
                {
                    descriptionBuilder.Append(post.SelfText);
                }
                else
                {
                    descriptionBuilder.Append(post.SelfText.Substring(0, 247));                    
                    descriptionBuilder.Append("...");                    
                }
            }
            
            var messageObject = new
            {
                username = "/r/SAO Moderation Bot",
                avatar_url = "https://i.imgur.com/JgH9D87.png",
                embeds = new[]
                {
                    new
                    {
                        title = $"New post in /r/{_subreddit.Name} by /u/{post.AuthorName}",
                        description = descriptionBuilder.ToString(),
                        url = post.Shortlink,
                        
                        image = !IsImagePost(post) ? null : new
                        {
                            url = post.Url.ToString()
                        },
                        
                        color = 0x9CCC65,
                        timestamp = post.CreatedUTC.ToString("o"),
                        footer = new
                        {
                            text = "/r/SAO Moderation Bot"
                        },
                        author = new
                        {
                            name = $"/r/{_subreddit.Name}",
                            url = $"https://reddit.com/r/{_subreddit.Name}",
                            icon_url = "http://www.redditstatic.com/desktop2x/img/favicon/android-icon-192x192.png"
                        }
                    }
                }
            };

            var json = JsonConvert.SerializeObject(messageObject);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(webhookUrl, content);
        }
        
        private static bool IsImagePost(Post post)
        {
            return Regex
                .Match(post.Url.ToString(), @"\.(jpg|jpeg|png|gif|bmp)$")
                .Success;
        }
    }
}