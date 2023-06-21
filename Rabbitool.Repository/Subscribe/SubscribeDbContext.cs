using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Rabbitool.Model.Entity.Subscribe;

namespace Rabbitool.Repository.Subscribe;

public class SubscribeDbContext : DbContext
{
    public DbSet<QQChannelSubscribeEntity> QQChannelSubscribeEntity => Set<QQChannelSubscribeEntity>();
    public DbSet<BilibiliSubscribeEntity> BibiliSubscribeEntity => Set<BilibiliSubscribeEntity>();
    public DbSet<BilibiliSubscribeConfigEntity> BibiliSubscribeConfigEntity => Set<BilibiliSubscribeConfigEntity>();
    public DbSet<TwitterSubscribeEntity> TwitterSubscribeEntity => Set<TwitterSubscribeEntity>();
    public DbSet<TwitterSubscribeConfigEntity> TwitterSubscribeConfigEntity => Set<TwitterSubscribeConfigEntity>();
    public DbSet<YoutubeSubscribeEntity> YoutubeSubscribeEntity => Set<YoutubeSubscribeEntity>();
    public DbSet<YoutubeSubscribeConfigEntity> YoutubeSubscribeConfigEntity => Set<YoutubeSubscribeConfigEntity>();
    public DbSet<MailSubscribeEntity> MailSubscribeEntity => Set<MailSubscribeEntity>();
    public DbSet<MailSubscribeConfigEntity> MailSubscribeConfigEntity => Set<MailSubscribeConfigEntity>();

    private readonly string _dbPath;

    public SubscribeDbContext()
    {
        _dbPath = "rabbitool.db";
    }

    public SubscribeDbContext(string dbPath)
    {
        _dbPath = dbPath;
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
            optionsBuilder.UseSqlite($"Data Source={_dbPath}");
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // relations
        modelBuilder.Entity<BilibiliSubscribeEntity>()
            .HasMany(e => e.QQChannels)
            .WithMany(e => e.BilibiliSubscribes)
            .UsingEntity("BilibiliSubscribe_QQChannelSubscribes");
        modelBuilder.Entity<YoutubeSubscribeEntity>()
            .HasMany(e => e.QQChannels)
            .WithMany(e => e.YoutubeSubscribes)
            .UsingEntity("YoutubeSubscribe_QQChannelSubscribes");
        modelBuilder.Entity<TwitterSubscribeEntity>()
            .HasMany(e => e.QQChannels)
            .WithMany(e => e.TwitterSubscribes)
            .UsingEntity("TwitterSubscribe_QQChannelSubscribes");
        modelBuilder.Entity<MailSubscribeEntity>()
            .HasMany(e => e.QQChannels)
            .WithMany(e => e.MailSubscribes)
            .UsingEntity("MailSubscribe_QQChannelSubscribes");

        // comparars
        ValueComparer<List<string>> comparerForList = new(
            (c1, c2) => CompareList(c1, c2),
            c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
            c => c.ToList());
        ValueComparer<DateTime> comparerForDateTime = new(
            (c1, c2) => CompareDateTime(c1, c2),
            c => c.GetHashCode(),
            c => DateTime
                .ParseExact(c.ToString("yyyy-MM-ddTHH:mm:sszzz"), "yyyy-MM-ddTHH:mm:sszzz", null)
                .ToUniversalTime());

        // conversions
        modelBuilder.Entity<YoutubeSubscribeEntity>()
            .Property(e => e.AllArchiveVideoIds)
            .HasConversion(
                v => string.Join(',', v),
                v => v.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList(),
                comparerForList);
        modelBuilder.Entity<YoutubeSubscribeEntity>()
            .Property(e => e.AllUpcomingLiveRoomIds)
            .HasConversion(
                v => string.Join(',', v),
                v => v.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList(),
                comparerForList);
        modelBuilder.Entity<BilibiliSubscribeEntity>()
            .Property(e => e.LastDynamicTime)
            .HasConversion(
                v => v.ToString("yyyy-MM-ddTHH:mm:sszzz"),
                v => DateTime.ParseExact(v, "yyyy-MM-ddTHH:mm:sszzz", null).ToUniversalTime(),
                comparerForDateTime);
        modelBuilder.Entity<YoutubeSubscribeEntity>()
            .Property(e => e.LastVideoPubTime)
            .HasConversion(
                v => v.ToString("yyyy-MM-ddTHH:mm:sszzz"),
                v => DateTime.ParseExact(v, "yyyy-MM-ddTHH:mm:sszzz", null).ToUniversalTime(),
                comparerForDateTime);
        modelBuilder.Entity<YoutubeSubscribeEntity>()
            .Property(e => e.LastLiveStartTime)
            .HasConversion(
                v => v.ToString("yyyy-MM-ddTHH:mm:sszzz"),
                v => DateTime.ParseExact(v, "yyyy-MM-ddTHH:mm:sszzz", null).ToUniversalTime(),
                comparerForDateTime);
        modelBuilder.Entity<TwitterSubscribeEntity>()
            .Property(e => e.LastTweetTime)
            .HasConversion(
                v => v.ToString("yyyy-MM-ddTHH:mm:sszzz"),
                v => DateTime.ParseExact(v, "yyyy-MM-ddTHH:mm:sszzz", null).ToUniversalTime(),
                comparerForDateTime);
        modelBuilder.Entity<MailSubscribeEntity>()
            .Property(e => e.LastMailTime)
            .HasConversion(
                v => v.ToString("yyyy-MM-ddTHH:mm:sszzz"),
                v => DateTime.ParseExact(v, "yyyy-MM-ddTHH:mm:sszzz", null).ToUniversalTime(),
                comparerForDateTime);
    }

    private static bool CompareList<T>(List<T>? list1, List<T>? list2)
    {
        list1 ??= new List<T>();
        list2 ??= new List<T>();
        return list1.SequenceEqual(list2);
    }

    private static bool CompareDateTime(DateTime? time1, DateTime? time2)
    {
        time1 ??= DateTime.MinValue;
        time2 ??= DateTime.MinValue;
        return time1 == time2;
    }
}
