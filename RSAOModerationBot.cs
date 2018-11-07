using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Timers;
using Autofac;
using Microsoft.Extensions.Configuration;
using RedditSharp;
using RedditSharp.Things;
using RSAOModerationBot.Module;

namespace RSAOModerationBot
{
    class RSAOModerationBot
    {
        private static Timer _loopTimer;
        private static DateTimeOffset _lastLoopTimeUtc = DateTimeOffset.UtcNow;

        private static IContainer Container { get; set; }

        public static async Task Main()
        {
            var containerBuilder = new ContainerBuilder();
            
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("config.json", true, true)
                .Build();

            containerBuilder.RegisterInstance(configuration)
                .As<IConfigurationRoot>()
                .As<IConfiguration>();
            
            var webAgent = new BotWebAgent(
                configuration["reddit:username"],
                configuration["reddit:password"],
                configuration["reddit:clientId"],
                configuration["reddit:clientSecret"],
                configuration["reddit:redirectUri"]
            );

            var reddit = new Reddit(webAgent, true);
            containerBuilder.RegisterInstance(reddit).As<Reddit>();
            
            var subreddit = await reddit.GetSubredditAsync("/r/omegavesko");
            containerBuilder.RegisterInstance(subreddit).As<Subreddit>();

            containerBuilder.RegisterType<ImagePostTrackerModule>()
                .As<IModule>()
                .As<IPostMonitorModule>();

            Container = containerBuilder.Build();
            
            _loopTimer = new Timer(30000);

            _loopTimer.Elapsed += async (sender, args) =>
            {
                using (var scope = Container.BeginLifetimeScope())
                {
                    var time = _lastLoopTimeUtc;
                    _lastLoopTimeUtc = DateTimeOffset.UtcNow;

                    var newPosts = await subreddit.GetPosts()
                        .TakeWhile(p => p.CreatedUTC >= time)
                        .ToList();

                    if (newPosts.Count == 0) return;

                    var postMonitorModules = scope.Resolve<IEnumerable<IPostMonitorModule>>();
                    foreach (var module in postMonitorModules)
                    {
                        await module.ProcessNewPosts(newPosts);
                    }
                }
            };
        
            _loopTimer.AutoReset = true;
            _loopTimer.Enabled = true;

            await Task.Delay(-1);
        }
    }
}