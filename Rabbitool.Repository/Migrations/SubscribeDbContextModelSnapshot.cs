﻿// <auto-generated />
using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Rabbitool.Repository.Subscribe;

#nullable disable

namespace Rabbitool.Repository.Migrations
{
    [DbContext(typeof(SubscribeDbContext))]
    partial class SubscribeDbContextModelSnapshot : ModelSnapshot
    {
        protected override void BuildModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder.HasAnnotation("ProductVersion", "8.0.0");

            modelBuilder.Entity("BilibiliSubscribe_QQChannelSubscribe", b =>
                {
                    b.Property<Guid>("BilibiliSubscribesId")
                        .HasColumnType("TEXT");

                    b.Property<Guid>("QQChannelsId")
                        .HasColumnType("TEXT");

                    b.HasKey("BilibiliSubscribesId", "QQChannelsId");

                    b.HasIndex("QQChannelsId");

                    b.ToTable("BilibiliSubscribe_QQChannelSubscribe");
                });

            modelBuilder.Entity("MailSubscribe_QQChannelSubscribe", b =>
                {
                    b.Property<Guid>("MailSubscribesId")
                        .HasColumnType("TEXT");

                    b.Property<Guid>("QQChannelsId")
                        .HasColumnType("TEXT");

                    b.HasKey("MailSubscribesId", "QQChannelsId");

                    b.HasIndex("QQChannelsId");

                    b.ToTable("MailSubscribe_QQChannelSubscribe");
                });

            modelBuilder.Entity("Rabbitool.Model.Entity.Subscribe.BilibiliSubscribeConfigEntity", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("TEXT");

                    b.Property<bool>("DynamicPush")
                        .HasColumnType("INTEGER");

                    b.Property<bool>("LivePush")
                        .HasColumnType("INTEGER");

                    b.Property<bool>("PureForwardDynamicPush")
                        .HasColumnType("INTEGER");

                    b.Property<Guid>("QQChannelId")
                        .HasColumnType("TEXT");

                    b.Property<Guid>("SubscribeId")
                        .HasColumnType("TEXT");

                    b.HasKey("Id");

                    b.HasIndex("QQChannelId");

                    b.HasIndex("SubscribeId");

                    b.ToTable("BilibiliSubscribeConfig");
                });

            modelBuilder.Entity("Rabbitool.Model.Entity.Subscribe.BilibiliSubscribeEntity", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("TEXT");

                    b.Property<string>("LastDynamicTime")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.Property<int>("LastDynamicType")
                        .HasColumnType("INTEGER");

                    b.Property<int>("LastLiveStatus")
                        .HasColumnType("INTEGER");

                    b.Property<uint>("Uid")
                        .HasColumnType("INTEGER");

                    b.Property<string>("Uname")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.HasKey("Id");

                    b.ToTable("BilibiliSubscribe");
                });

            modelBuilder.Entity("Rabbitool.Model.Entity.Subscribe.MailSubscribeConfigEntity", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("TEXT");

                    b.Property<bool>("Detail")
                        .HasColumnType("INTEGER");

                    b.Property<bool>("PushToThread")
                        .HasColumnType("INTEGER");

                    b.Property<Guid>("QQChannelId")
                        .HasColumnType("TEXT");

                    b.Property<Guid>("SubscribeId")
                        .HasColumnType("TEXT");

                    b.HasKey("Id");

                    b.HasIndex("QQChannelId");

                    b.HasIndex("SubscribeId");

                    b.ToTable("MailSubscribeConfig");
                });

            modelBuilder.Entity("Rabbitool.Model.Entity.Subscribe.MailSubscribeEntity", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("TEXT");

                    b.Property<string>("Address")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.Property<string>("Host")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.Property<string>("LastMailTime")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.Property<string>("Mailbox")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.Property<string>("Password")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.Property<int>("Port")
                        .HasColumnType("INTEGER");

                    b.Property<bool>("Ssl")
                        .HasColumnType("INTEGER");

                    b.Property<string>("Username")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.HasKey("Id");

                    b.ToTable("MailSubscribe");
                });

            modelBuilder.Entity("Rabbitool.Model.Entity.Subscribe.QQChannelSubscribeEntity", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("TEXT");

                    b.Property<string>("ChannelId")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.Property<string>("ChannelName")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.Property<string>("GuildId")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.Property<string>("GuildName")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.HasKey("Id");

                    b.ToTable("QQChannelSubscribe");
                });

            modelBuilder.Entity("Rabbitool.Model.Entity.Subscribe.TwitterSubscribeConfigEntity", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("TEXT");

                    b.Property<bool>("PushToThread")
                        .HasColumnType("INTEGER");

                    b.Property<Guid>("QQChannelId")
                        .HasColumnType("TEXT");

                    b.Property<bool>("RtPush")
                        .HasColumnType("INTEGER");

                    b.Property<Guid>("SubscribeId")
                        .HasColumnType("TEXT");

                    b.HasKey("Id");

                    b.HasIndex("QQChannelId");

                    b.HasIndex("SubscribeId");

                    b.ToTable("TwitterSubscribeConfig");
                });

            modelBuilder.Entity("Rabbitool.Model.Entity.Subscribe.TwitterSubscribeEntity", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("TEXT");

                    b.Property<string>("LastTweetId")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.Property<string>("LastTweetTime")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.Property<string>("ScreenName")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.HasKey("Id");

                    b.ToTable("TwitterSubscribe");
                });

            modelBuilder.Entity("Rabbitool.Model.Entity.Subscribe.YoutubeSubscribeConfigEntity", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("TEXT");

                    b.Property<bool>("ArchivePush")
                        .HasColumnType("INTEGER");

                    b.Property<bool>("LivePush")
                        .HasColumnType("INTEGER");

                    b.Property<Guid>("QQChannelId")
                        .HasColumnType("TEXT");

                    b.Property<Guid>("SubscribeId")
                        .HasColumnType("TEXT");

                    b.Property<bool>("UpcomingLivePush")
                        .HasColumnType("INTEGER");

                    b.Property<bool>("VideoPush")
                        .HasColumnType("INTEGER");

                    b.HasKey("Id");

                    b.HasIndex("QQChannelId");

                    b.HasIndex("SubscribeId");

                    b.ToTable("YoutubeSubscribeConfig");
                });

            modelBuilder.Entity("Rabbitool.Model.Entity.Subscribe.YoutubeSubscribeEntity", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("TEXT");

                    b.Property<string>("AllArchiveVideoIds")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.Property<string>("AllUpcomingLiveRoomIds")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.Property<string>("ChannelId")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.Property<string>("LastLiveRoomId")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.Property<string>("LastLiveStartTime")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.Property<string>("LastVideoId")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.Property<string>("LastVideoPubTime")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.HasKey("Id");

                    b.ToTable("YoutubeSubscribe");
                });

            modelBuilder.Entity("TwitterSubscribe_QQChannelSubscribe", b =>
                {
                    b.Property<Guid>("QQChannelsId")
                        .HasColumnType("TEXT");

                    b.Property<Guid>("TwitterSubscribesId")
                        .HasColumnType("TEXT");

                    b.HasKey("QQChannelsId", "TwitterSubscribesId");

                    b.HasIndex("TwitterSubscribesId");

                    b.ToTable("TwitterSubscribe_QQChannelSubscribe");
                });

            modelBuilder.Entity("YoutubeSubscribe_QQChannelSubscribe", b =>
                {
                    b.Property<Guid>("QQChannelsId")
                        .HasColumnType("TEXT");

                    b.Property<Guid>("YoutubeSubscribesId")
                        .HasColumnType("TEXT");

                    b.HasKey("QQChannelsId", "YoutubeSubscribesId");

                    b.HasIndex("YoutubeSubscribesId");

                    b.ToTable("YoutubeSubscribe_QQChannelSubscribe");
                });

            modelBuilder.Entity("BilibiliSubscribe_QQChannelSubscribe", b =>
                {
                    b.HasOne("Rabbitool.Model.Entity.Subscribe.BilibiliSubscribeEntity", null)
                        .WithMany()
                        .HasForeignKey("BilibiliSubscribesId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("Rabbitool.Model.Entity.Subscribe.QQChannelSubscribeEntity", null)
                        .WithMany()
                        .HasForeignKey("QQChannelsId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });

            modelBuilder.Entity("MailSubscribe_QQChannelSubscribe", b =>
                {
                    b.HasOne("Rabbitool.Model.Entity.Subscribe.MailSubscribeEntity", null)
                        .WithMany()
                        .HasForeignKey("MailSubscribesId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("Rabbitool.Model.Entity.Subscribe.QQChannelSubscribeEntity", null)
                        .WithMany()
                        .HasForeignKey("QQChannelsId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });

            modelBuilder.Entity("Rabbitool.Model.Entity.Subscribe.BilibiliSubscribeConfigEntity", b =>
                {
                    b.HasOne("Rabbitool.Model.Entity.Subscribe.QQChannelSubscribeEntity", "QQChannel")
                        .WithMany()
                        .HasForeignKey("QQChannelId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("Rabbitool.Model.Entity.Subscribe.BilibiliSubscribeEntity", "Subscribe")
                        .WithMany()
                        .HasForeignKey("SubscribeId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("QQChannel");

                    b.Navigation("Subscribe");
                });

            modelBuilder.Entity("Rabbitool.Model.Entity.Subscribe.MailSubscribeConfigEntity", b =>
                {
                    b.HasOne("Rabbitool.Model.Entity.Subscribe.QQChannelSubscribeEntity", "QQChannel")
                        .WithMany()
                        .HasForeignKey("QQChannelId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("Rabbitool.Model.Entity.Subscribe.MailSubscribeEntity", "Subscribe")
                        .WithMany()
                        .HasForeignKey("SubscribeId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("QQChannel");

                    b.Navigation("Subscribe");
                });

            modelBuilder.Entity("Rabbitool.Model.Entity.Subscribe.TwitterSubscribeConfigEntity", b =>
                {
                    b.HasOne("Rabbitool.Model.Entity.Subscribe.QQChannelSubscribeEntity", "QQChannel")
                        .WithMany()
                        .HasForeignKey("QQChannelId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("Rabbitool.Model.Entity.Subscribe.TwitterSubscribeEntity", "Subscribe")
                        .WithMany()
                        .HasForeignKey("SubscribeId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("QQChannel");

                    b.Navigation("Subscribe");
                });

            modelBuilder.Entity("Rabbitool.Model.Entity.Subscribe.YoutubeSubscribeConfigEntity", b =>
                {
                    b.HasOne("Rabbitool.Model.Entity.Subscribe.QQChannelSubscribeEntity", "QQChannel")
                        .WithMany()
                        .HasForeignKey("QQChannelId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("Rabbitool.Model.Entity.Subscribe.YoutubeSubscribeEntity", "Subscribe")
                        .WithMany()
                        .HasForeignKey("SubscribeId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("QQChannel");

                    b.Navigation("Subscribe");
                });

            modelBuilder.Entity("TwitterSubscribe_QQChannelSubscribe", b =>
                {
                    b.HasOne("Rabbitool.Model.Entity.Subscribe.QQChannelSubscribeEntity", null)
                        .WithMany()
                        .HasForeignKey("QQChannelsId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("Rabbitool.Model.Entity.Subscribe.TwitterSubscribeEntity", null)
                        .WithMany()
                        .HasForeignKey("TwitterSubscribesId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });

            modelBuilder.Entity("YoutubeSubscribe_QQChannelSubscribe", b =>
                {
                    b.HasOne("Rabbitool.Model.Entity.Subscribe.QQChannelSubscribeEntity", null)
                        .WithMany()
                        .HasForeignKey("QQChannelsId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("Rabbitool.Model.Entity.Subscribe.YoutubeSubscribeEntity", null)
                        .WithMany()
                        .HasForeignKey("YoutubeSubscribesId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });
#pragma warning restore 612, 618
        }
    }
}
