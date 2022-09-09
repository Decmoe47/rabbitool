﻿using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
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
        modelBuilder.Entity<BilibiliSubscribeEntity>()
            .HasMany(e => e.QQChannels)
            .WithMany(e => e.BilibiliSubscribes)
            .UsingEntity("BilibiliSubscribe_QQChannels");
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

        modelBuilder.Entity<YoutubeSubscribeEntity>()
            .Property(e => e.AllArchiveVideoIds)
            .HasConversion(
                v => JsonConvert.SerializeObject(v),
                v => DeserializeListString(v));
        modelBuilder.Entity<YoutubeSubscribeEntity>()
            .Property(e => e.AllUpcomingLiveRoomIds)
            .HasConversion(
                v => JsonConvert.SerializeObject(v),
                v => DeserializeListString(v));

        modelBuilder.Entity<BilibiliSubscribeEntity>()
            .Property(e => e.LastDynamicTime)
            .HasConversion(
                v => v.ToString("yyyy-MM-ddTHH:mm:sszzz"),
                v => DateTime.ParseExact(v, "yyyy-MM-ddTHH:mm:sszzz", null).ToUniversalTime());
        modelBuilder.Entity<YoutubeSubscribeEntity>()
            .Property(e => e.LastVideoPubTime)
            .HasConversion(
                v => v.ToString("yyyy-MM-ddTHH:mm:sszzz"),
                v => DateTime.ParseExact(v, "yyyy-MM-ddTHH:mm:sszzz", null).ToUniversalTime());
        modelBuilder.Entity<YoutubeSubscribeEntity>()
            .Property(e => e.LastLiveStartTime)
            .HasConversion(
                v => v.ToString("yyyy-MM-ddTHH:mm:sszzz"),
                v => DateTime.ParseExact(v, "yyyy-MM-ddTHH:mm:sszzz", null).ToUniversalTime());
        modelBuilder.Entity<TwitterSubscribeEntity>()
            .Property(e => e.LastTweetTime)
            .HasConversion(
                v => v.ToString("yyyy-MM-ddTHH:mm:sszzz"),
                v => DateTime.ParseExact(v, "yyyy-MM-ddTHH:mm:sszzz", null).ToUniversalTime());
        modelBuilder.Entity<MailSubscribeEntity>()
            .Property(e => e.LastMailTime)
            .HasConversion(
                v => v.ToString("yyyy-MM-ddTHH:mm:sszzz"),
                v => DateTime.ParseExact(v, "yyyy-MM-ddTHH:mm:sszzz", null).ToUniversalTime());
    }

    private static List<string> DeserializeListString(string v)
    {
        return JsonConvert.DeserializeObject<List<string>>(v) ?? new List<string>();
    }
}
