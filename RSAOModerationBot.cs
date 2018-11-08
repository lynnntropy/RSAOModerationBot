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
using Serilog;

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
            
            var logger = new LoggerConfiguration()
                .WriteTo.Console()
                .WriteTo.File("log.txt", rollingInterval: RollingInterval.Day)
                .CreateLogger();

            containerBuilder.RegisterInstance(logger).As<ILogger>();
            
            var webAgent = new BotWebAgent(
                configuration["reddit:username"],
                configuration["reddit:password"],
                configuration["reddit:clientId"],
                configuration["reddit:clientSecret"],
                configuration["reddit:redirectUri"]
            );

            var reddit = new Reddit(webAgent, true);
            containerBuilder.RegisterInstance(reddit).As<Reddit>();
            
            logger.Information($"Logged into Reddit as /u/{reddit.User}.");
            
            var subreddit = await reddit.GetSubredditAsync(configuration["reddit:subreddit"]);
            containerBuilder.RegisterInstance(subreddit).As<Subreddit>();
            
            logger.Information($"Loaded subreddit /r/{subreddit.Name}.");

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

                    List<Post> newPosts;
                    
                    try
                    {
                        newPosts = await subreddit.GetPosts(Subreddit.Sort.New)
                            .TakeWhile(p => p.CreatedUTC >= time)
                            .ToList();
                    }
                    catch (OperationCanceledException e)
                    {
                        logger.Error(e, "Encountered OperationCanceledException when trying to fetch new posts.");
                        return;
                    }
                    
                    if (newPosts.Count == 0) return;

                    foreach (var post in newPosts)
                    {
                        logger.Information($"New post: {post.Title} by {post.AuthorName} ({post.Shortlink})");
                    }

                    var postMonitorModules = scope.Resolve<IEnumerable<IPostMonitorModule>>();
                    foreach (var module in postMonitorModules)
                    {
                        await module.ProcessNewPosts(newPosts);
                    }
                }
            };
        
            _loopTimer.AutoReset = true;
            _loopTimer.Enabled = true;
            
            logger.Information("Initialization complete.");

            await Task.Delay(-1);
        }
    }
}