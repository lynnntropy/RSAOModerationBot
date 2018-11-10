using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
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

        /// <summary>
        /// The entry point of the program.
        /// </summary>
        public static async Task Main()
        {
            await InitializeContainerAsync();
            
            using (var scope = Container.BeginLifetimeScope())
            {
                var logger = scope.Resolve<ILogger>();
                
                var modules = scope.Resolve<IEnumerable<IModule>>();
                foreach (var module in modules)
                {
                    logger.Information($"Found module: {module.GetType().Name} {(module is IPostMonitorModule ? "(post monitor)": "")}");
                }
            }
            
            _loopTimer = new Timer(30000);

            _loopTimer.Elapsed += async (sender, args) =>
            {
                await CheckForNewPostsAsync();
            };
        
            _loopTimer.AutoReset = true;
            _loopTimer.Enabled = true;

            await Task.Delay(-1);
        }

        /// <summary>
        /// Checks for new posts. This method is used to provide new
        /// posts to modules implementing <c>IPostMonitorModule</c>.
        /// </summary>
        private static async Task CheckForNewPostsAsync()
        {
            using (var scope = Container.BeginLifetimeScope())
            {
                var time = _lastLoopTimeUtc;
                _lastLoopTimeUtc = DateTimeOffset.UtcNow;

                var subreddit = scope.Resolve<Subreddit>();
                var logger = scope.Resolve<ILogger>();

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
                    await module.ProcessNewPostsAsync(newPosts);
                }
            }
        }

        /// <summary>
        /// Initializes the DI container.
        /// </summary>
        private static async Task InitializeContainerAsync()
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
            
            var httpClient = new HttpClient();
            containerBuilder.RegisterInstance(httpClient).As<HttpClient>();
            
            logger.Information($"Loaded subreddit /r/{subreddit.Name}.");

            containerBuilder.RegisterType<ImagePostTrackerModule>()
                .As<IModule>()
                .As<IPostMonitorModule>();
            
            containerBuilder.RegisterType<DiscordWebhookModule>()
                .As<IModule>()
                .As<IPostMonitorModule>();
            
            logger.Information("Container initialization complete.");

            Container = containerBuilder.Build();
        }
    }
}