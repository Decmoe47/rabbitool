using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Rabbitool.Common.Configs;
using Rabbitool.Model.Entity.Subscribe;

namespace Rabbitool.Repository.Subscribe;

public class SubscribeDbContext(CommonConfig commonConfig) : DbContext
{
    public DbSet<QQChannelSubscribeEntity> QQChannelSubscribes => Set<QQChannelSubscribeEntity>();
    public DbSet<BilibiliSubscribeEntity> BilibiliSubscribes => Set<BilibiliSubscribeEntity>();
    public DbSet<BilibiliSubscribeConfigEntity> BilibiliSubscribeConfigs => Set<BilibiliSubscribeConfigEntity>();
    public DbSet<TwitterSubscribeEntity> TwitterSubscribes => Set<TwitterSubscribeEntity>();
    public DbSet<TwitterSubscribeConfigEntity> TwitterSubscribeConfigs => Set<TwitterSubscribeConfigEntity>();
    public DbSet<YoutubeSubscribeEntity> YoutubeSubscribes => Set<YoutubeSubscribeEntity>();
    public DbSet<YoutubeSubscribeConfigEntity> YoutubeSubscribeConfigs => Set<YoutubeSubscribeConfigEntity>();
    public DbSet<MailSubscribeEntity> MailSubscribes => Set<MailSubscribeEntity>();
    public DbSet<MailSubscribeConfigEntity> MailSubscribeConfigs => Set<MailSubscribeConfigEntity>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured) optionsBuilder.UseSqlite($"Data Source={commonConfig.DbPath}");
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // relations
        modelBuilder.Entity<BilibiliSubscribeEntity>()
            .HasMany(e => e.QQChannels)
            .WithMany(e => e.BilibiliSubscribes)
            .UsingEntity("BilibiliSubscribe_QQChannelSubscribe");
        modelBuilder.Entity<YoutubeSubscribeEntity>()
            .HasMany(e => e.QQChannels)
            .WithMany(e => e.YoutubeSubscribes)
            .UsingEntity("YoutubeSubscribe_QQChannelSubscribe");
        modelBuilder.Entity<TwitterSubscribeEntity>()
            .HasMany(e => e.QQChannels)
            .WithMany(e => e.TwitterSubscribes)
            .UsingEntity("TwitterSubscribe_QQChannelSubscribe");
        modelBuilder.Entity<MailSubscribeEntity>()
            .HasMany(e => e.QQChannels)
            .WithMany(e => e.MailSubscribes)
            .UsingEntity("MailSubscribe_QQChannelSubscribe");

        // compares
        ValueComparer<List<string>> comparerForList = new(
            (c1, c2) => CompareList(c1, c2),
            c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
            c => c.ToList());
        ValueComparer<DateTime> comparerForDateTime = new(
            (c1, c2) => CompareDateTime(c1, c2),
            c => c.GetHashCode(),
            c => DateTime
                .ParseExact(c.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:sszzz"), "yyyy-MM-ddTHH:mm:sszzz", null)
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
                v => v.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:sszzz"),
                v => DateTime.ParseExact(v, "yyyy-MM-ddTHH:mm:sszzz", null).ToUniversalTime(),
                comparerForDateTime);
        modelBuilder.Entity<YoutubeSubscribeEntity>()
            .Property(e => e.LastVideoPubTime)
            .HasConversion(
                v => v.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:sszzz"),
                v => DateTime.ParseExact(v, "yyyy-MM-ddTHH:mm:sszzz", null).ToUniversalTime(),
                comparerForDateTime);
        modelBuilder.Entity<YoutubeSubscribeEntity>()
            .Property(e => e.LastLiveStartTime)
            .HasConversion(
                v => v.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:sszzz"),
                v => DateTime.ParseExact(v, "yyyy-MM-ddTHH:mm:sszzz", null).ToUniversalTime(),
                comparerForDateTime);
        modelBuilder.Entity<TwitterSubscribeEntity>()
            .Property(e => e.LastTweetTime)
            .HasConversion(
                v => v.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:sszzz"),
                v => DateTime.ParseExact(v, "yyyy-MM-ddTHH:mm:sszzz", null).ToUniversalTime(),
                comparerForDateTime);
        modelBuilder.Entity<MailSubscribeEntity>()
            .Property(e => e.LastMailTime)
            .HasConversion(
                v => v.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:sszzz"),
                v => DateTime.ParseExact(v, "yyyy-MM-ddTHH:mm:sszzz", null).ToUniversalTime(),
                comparerForDateTime);
    }

    private static bool CompareList<T>(List<T>? list1, List<T>? list2)
    {
        list1 ??= [];
        list2 ??= [];
        return list1.SequenceEqual(list2);
    }

    private static bool CompareDateTime(DateTime? time1, DateTime? time2)
    {
        time1 ??= DateTime.MinValue;
        time2 ??= DateTime.MinValue;
        return time1 == time2;
    }
}