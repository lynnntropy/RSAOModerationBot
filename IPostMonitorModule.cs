using System.Collections.Generic;
using System.Threading.Tasks;
using RedditSharp.Things;

namespace RSAOModerationBot
{
    public interface IPostMonitorModule : IModule
    {
        Task ProcessNewPosts(List<Post> posts);
    }
}