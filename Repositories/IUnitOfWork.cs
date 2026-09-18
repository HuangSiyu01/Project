using Project.Models;

namespace Project.Repositories
{
    public interface IUnitOfWork : IDisposable
    {
        IRepository<Post> Posts { get; }
        IRepository<PostImage> PostImages { get; }
        IRepository<Comment> Comments { get; }
        IRepository<Like> Likes { get; }
        IRepository<Favorite> Favorites { get; }
        IRepository<Follow> Follows { get; }

        Task<int> SaveChangesAsync();
    }
}