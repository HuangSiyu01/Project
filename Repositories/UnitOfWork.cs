using Project.Data;
using Project.Models;

namespace Project.Repositories
{
    public class UnitOfWork : IUnitOfWork
    {
        private readonly ApplicationDbContext _context;

        public UnitOfWork(ApplicationDbContext context)
        {
            _context = context;
            Posts = new Repository<Post>(_context);
            PostImages = new Repository<PostImage>(_context);
            Comments = new Repository<Comment>(_context);
            Likes = new Repository<Like>(_context);
            Favorites = new Repository<Favorite>(_context);
            Follows = new Repository<Follow>(_context);
        }

        public IRepository<Post> Posts { get; }
        public IRepository<PostImage> PostImages { get; }
        public IRepository<Comment> Comments { get; }
        public IRepository<Like> Likes { get; }
        public IRepository<Favorite> Favorites { get; }
        public IRepository<Follow> Follows { get; }

        public async Task<int> SaveChangesAsync() => await _context.SaveChangesAsync();
        public void Dispose() => _context.Dispose();
    }
}