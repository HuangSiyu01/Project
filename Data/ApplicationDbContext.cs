using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Project.Models;

namespace Project.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(
            DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Post> Posts { get; set; }
        public DbSet<PostImage> PostImages { get; set; }
        public DbSet<Comment> Comments { get; set; }
        public DbSet<Like> Likes { get; set; }
        public DbSet<Favorite> Favorites { get; set; }
        public DbSet<Follow> Follows { get; set; }
        public DbSet<Attraction> Attractions { get; set; }
        public DbSet<AttractionImage> AttractionImages { get; set; }
        public DbSet<ShoppingCartItem> ShoppingCartItems { get; set; }
        public DbSet<Order> Orders { get; set; }
        public DbSet<OrderItem> OrderItems { get; set; }
        public DbSet<RefundRequest> RefundRequests { get; set; }
        public DbSet<Ticket> Tickets { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // Configure the relationship between Post and PostImage
            builder.Entity<PostImage>()
                .HasOne(pi => pi.Post)
                .WithMany(p => p.Images)
                .HasForeignKey(pi => pi.PostId)
                .OnDelete(DeleteBehavior.Cascade);

            // Configure the self-referencing relationship for Comment replies
            builder.Entity<Comment>()
                .HasOne(c => c.ParentComment)
                .WithMany(c => c.Replies)
                .HasForeignKey(c => c.ParentCommentId)
                .OnDelete(DeleteBehavior.Restrict);

            // Configure the relationship between Post and User
            builder.Entity<Post>()
                .HasOne(p => p.User)
                .WithMany(u => u.Posts)
                .HasForeignKey(p => p.UserId)
                .OnDelete(DeleteBehavior.NoAction);

            // Configure the relationship between Comment and User
            builder.Entity<Comment>()
                .HasOne(c => c.User)
                .WithMany()
                .HasForeignKey(c => c.UserId)
                .OnDelete(DeleteBehavior.NoAction);

            // Configure the relationship between Like and Post
            builder.Entity<Like>()
                .HasOne(l => l.Post)
                .WithMany(p => p.Likes)
                .HasForeignKey(l => l.PostId)
                .OnDelete(DeleteBehavior.Cascade);

            // Configure the relationship between Like and User
            builder.Entity<Like>()
                .HasOne(l => l.User)
                .WithMany(u => u.Likes)
                .HasForeignKey(l => l.UserId)
                .OnDelete(DeleteBehavior.NoAction);

            // Configure the relationship between Favorite and Post
            builder.Entity<Favorite>()
                .HasOne(f => f.Post)
                .WithMany(p => p.Favorites)
                .HasForeignKey(f => f.PostId)
                .OnDelete(DeleteBehavior.Cascade);

            // Configure the relationship between Favorite and User
            builder.Entity<Favorite>()
                .HasOne(f => f.User)
                .WithMany(u => u.Favorites)
                .HasForeignKey(f => f.UserId)
                .OnDelete(DeleteBehavior.NoAction);

            // Configure the relationship between Follow and Follower
            builder.Entity<Follow>()
                .HasOne(f => f.Follower)
                .WithMany(u => u.Following)
                .HasForeignKey(f => f.FollowerId)
                .OnDelete(DeleteBehavior.NoAction);

            // Configure the relationship between Follow and Followee
            builder.Entity<Follow>()
                .HasOne(f => f.Followee)
                .WithMany(u => u.Followers)
                .HasForeignKey(f => f.FolloweeId)
                .OnDelete(DeleteBehavior.NoAction);

            // Create a unique index to prevent duplicate likes
            builder.Entity<Like>()
                .HasIndex(l => new { l.PostId, l.UserId })
                .IsUnique();

            // Create a unique index to prevent duplicate favorites
            builder.Entity<Favorite>()
                .HasIndex(f => new { f.PostId, f.UserId })
                .IsUnique();

            // Create a unique index to prevent duplicate follow relationships
            builder.Entity<Follow>()
                .HasIndex(f => new { f.FollowerId, f.FolloweeId })
                .IsUnique();

            // Configure the precision of attraction ticket prices
            builder.Entity<Attraction>()
                .Property(a => a.TicketPrice)
                .HasPrecision(10, 2);

            // Configure the relationship between Attraction and AttractionImage
            builder.Entity<AttractionImage>()
                .HasOne(ai => ai.Attraction)
                .WithMany(a => a.Images)
                .HasForeignKey(ai => ai.AttractionId)
                .OnDelete(DeleteBehavior.Cascade);

            // Configure ShoppingCartItem relationships
            builder.Entity<ShoppingCartItem>(entity =>
            {
                entity.HasKey(item => item.Id);

                entity.Property(item => item.UserId)
                    .IsRequired();

                entity.HasIndex(item => new
                {
                    item.UserId,
                    item.AttractionId
                }).IsUnique();

                entity.HasOne<ApplicationUser>()
                    .WithMany()
                    .HasForeignKey(item => item.UserId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(item => item.Attraction)
                    .WithMany()
                    .HasForeignKey(item => item.AttractionId)
                    .OnDelete(DeleteBehavior.NoAction);
            });

            // Configure Order relationships
            builder.Entity<Order>(entity =>
            {
                entity.HasKey(order => order.Id);

                entity.Property(order => order.UserId)
                    .IsRequired();

                entity.Property(order => order.OrderStatus)
                    .HasMaxLength(30)
                    .IsRequired();

                entity.Property(order => order.PaymentStatus)
                    .HasMaxLength(30)
                    .IsRequired();

                entity.Property(order => order.TotalAmount)
                    .HasPrecision(10, 2);

                entity.Property(order => order.StripeSessionId)
                    .HasMaxLength(255);

                entity.Property(order => order.StripePaymentIntentId)
                    .HasMaxLength(255);

                entity.HasOne(order => order.User)
                    .WithMany()
                    .HasForeignKey(order => order.UserId)
                    .OnDelete(DeleteBehavior.NoAction);
            });

            // Configure OrderItem relationships
            builder.Entity<OrderItem>(entity =>
            {
                entity.HasKey(item => item.Id);

                entity.Property(item => item.UnitPrice)
                    .HasPrecision(10, 2);

                entity.HasOne(item => item.Order)
                    .WithMany(order => order.Items)
                    .HasForeignKey(item => item.OrderId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(item => item.Attraction)
                    .WithMany()
                    .HasForeignKey(item => item.AttractionId)
                    .OnDelete(DeleteBehavior.NoAction);
            });

            // Configure RefundRequest relationships
            builder.Entity<RefundRequest>(entity =>
            {
                entity.HasKey(request => request.Id);

                entity.Property(request => request.UserId)
                    .IsRequired();

                entity.Property(request => request.Reason)
                    .HasMaxLength(1000)
                    .IsRequired();

                entity.Property(request => request.RefundStatus)
                    .HasMaxLength(30)
                    .IsRequired();

                entity.Property(request => request.AdminRemarks)
                    .HasMaxLength(1000);

                entity.Property(request => request.ReviewedByUserId)
                    .HasMaxLength(255);

                entity.Property(request => request.RefundAmount)
                    .HasPrecision(10, 2);

                entity.Property(request => request.StripeRefundId)
                    .HasMaxLength(255);

                entity.HasOne(request => request.Order)
                    .WithMany(order => order.RefundRequests)
                    .HasForeignKey(request => request.OrderId)
                    .OnDelete(DeleteBehavior.NoAction);

                // Optional to preserve legacy refund rows created before item-level refunds.
                // New refund requests always set OrderItemId in OrdersController.
                entity.HasOne(request => request.OrderItem)
                    .WithMany()
                    .HasForeignKey(request => request.OrderItemId)
                    .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(request => request.User)
                    .WithMany()
                    .HasForeignKey(request => request.UserId)
                    .OnDelete(DeleteBehavior.NoAction);
            });

            // Configure individually issued e-tickets.
            builder.Entity<Ticket>(entity =>
            {
                entity.HasKey(ticket => ticket.Id);

                entity.Property(ticket => ticket.TicketNumber)
                    .HasMaxLength(64)
                    .IsRequired();

                entity.HasIndex(ticket => ticket.TicketNumber)
                    .IsUnique();

                entity.Property(ticket => ticket.Status)
                    .HasMaxLength(30)
                    .IsRequired();

                entity.HasOne(ticket => ticket.Order)
                    .WithMany()
                    .HasForeignKey(ticket => ticket.OrderId)
                    .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(ticket => ticket.OrderItem)
                    .WithMany()
                    .HasForeignKey(ticket => ticket.OrderItemId)
                    .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(ticket => ticket.RefundRequest)
                    .WithMany()
                    .HasForeignKey(ticket => ticket.RefundRequestId)
                    .OnDelete(DeleteBehavior.NoAction);
            });

            // Seed initial attraction data
            // Only three attractions are included. Mount Huangshan is not included.
            builder.Entity<Attraction>().HasData(
                new Attraction
                {
                    Id = 1,
                    Name = "The Forbidden City",
                    City = "Beijing, China",
                    Description = "Explore the former imperial palace of China, with historic halls, courtyards and royal collections.",
                    ImageUrl = "/images/attractions/forbidden-city.jpg.jpg",
                    TicketPrice = 60.00m,
                    IsAvailable = true
                },
                new Attraction
                {
                    Id = 2,
                    Name = "Badaling Great Wall",
                    City = "Beijing, China",
                    Description = "Visit one of the best-known sections of the Great Wall of China and enjoy its mountain views.",
                    ImageUrl = "/images/attractions/great-wall.jpg",
                    TicketPrice = 40.00m,
                    IsAvailable = true
                },
                new Attraction
                {
                    Id = 3,
                    Name = "Emperor Qinshihuang's Mausoleum Site Museum",
                    City = "Xi'an, China",
                    Description = "Discover the world-famous Terracotta Army and the archaeological heritage of China's first emperor.",
                    ImageUrl = "/images/attractions/terracotta-army.jpg",
                    TicketPrice = 120.00m,
                    IsAvailable = true
                }
            );

            // Seed additional attraction images
            // Main images are stored in Attraction.ImageUrl,
            // so they should not be duplicated here.
            builder.Entity<AttractionImage>().HasData(
                new AttractionImage
                {
                    Id = 1,
                    AttractionId = 1,
                    ImageUrl = "/images/attractions/forbidden-city-1.jpg.jpg"
                },
                new AttractionImage
                {
                    Id = 2,
                    AttractionId = 2,
                    ImageUrl = "/images/attractions/great-wall-1.jpg"
                }
            );
        }
    }
}