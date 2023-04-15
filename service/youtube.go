package service

import (
	"context"
	"time"

	"github.com/Decmoe47/rabbitool/conf"
	dto "github.com/Decmoe47/rabbitool/dto/youtube"
	"github.com/Decmoe47/rabbitool/errx"
	"github.com/rs/zerolog/log"
	"golang.org/x/time/rate"
	"google.golang.org/api/option"
	"google.golang.org/api/youtube/v3"
)

type YoutubeService struct {
	client *youtube.Service
	// See https://developers.google.com/youtube/v3/getting-started
	limiter *rate.Limiter
}

func NewYoutubeService(ctx context.Context) (*YoutubeService, error) {
	service, err := youtube.NewService(ctx, option.WithAPIKey(conf.R.Youtube.ApiKey))
	if err != nil {
		return nil, errx.WithStack(err, nil)
	}
	return &YoutubeService{
		client:  service,
		limiter: rate.NewLimiter(rate.Every(time.Minute), 6),
	}, nil
}

func (y *YoutubeService) GetLatestVideoOrLive(ctx context.Context, channelId string) (dto.IItem, error) {
	err := y.limiter.Wait(ctx)
	if err != nil {
		return nil, errx.WithStack(err, nil)
	}

	errFields := map[string]any{"channelId": channelId}

	// https://developers.google.com/youtube/v3/docs/channels
	listChannels := y.client.Channels.List([]string{"contentDetails"})
	resp, err := listChannels.Context(ctx).Id(channelId).Do()
	if err != nil {
		return nil, errx.WithStack(err, errFields)
	}
	channel := resp.Items[0]

	err = y.limiter.Wait(ctx)
	if err != nil {
		return nil, errx.WithStack(err, nil)
	}
	// https://developers.google.com/youtube/v3/docs/playlistItems
	listPlaylistItems := y.client.PlaylistItems.List([]string{"contentDetails"})
	resp2, err := listPlaylistItems.Context(ctx).PlaylistId(channel.ContentDetails.RelatedPlaylists.Uploads).Do()
	if err != nil {
		return nil, errx.WithStack(err, errFields)
	}
	item := resp2.Items[0]

	err = y.limiter.Wait(ctx)
	if err != nil {
		return nil, errx.WithStack(err, nil)
	}
	// https://developers.google.com/youtube/v3/docs/videos
	listVideos := y.client.Videos.List([]string{"snippet", "liveStreamingDetails"})
	resp3, err := listVideos.Context(ctx).Id(item.ContentDetails.VideoId).Do()
	if err != nil {
		return nil, errx.WithStack(err, errFields)
	}
	video := resp3.Items[0]

	return y.createDto(channelId, item.ContentDetails.VideoId, video)
}

func (y *YoutubeService) IsStreaming(ctx context.Context, liveRoomId string) (*dto.Live, bool) {
	errFields := map[string]any{"liveRoomId": liveRoomId}

	err := y.limiter.Wait(ctx)
	if err != nil {
		log.Error().Stack().Err(errx.WithStack(err, errFields)).Msg(err.Error())
		return nil, false
	}

	listVideos := y.client.Videos.List([]string{"snippet", "liveStreamingDetails"})
	resp, err := listVideos.Context(ctx).Id(liveRoomId).Do()
	if err != nil {
		log.Error().Stack().Err(errx.WithStack(err, errFields)).Msg(err.Error())
		return nil, false
	}
	video := resp.Items[0]

	switch video.Snippet.LiveBroadcastContent {
	case "live":
		item, err := y.createDto(video.Snippet.ChannelId, liveRoomId, video)
		if err != nil {
			log.Error().Stack().Err(errx.WithStack(err, errFields)).Msg(err.Error())
			return nil, false
		}
		return item.(*dto.Live), true
	default:
		return nil, false
	}
}

func (y *YoutubeService) createDto(channelId string, itemId string, video *youtube.Video) (dto.IItem, error) {
	errFields := map[string]any{"channelId": channelId, "itemId": itemId, "videoTitle": video.Snippet.Title}

	switch video.Snippet.LiveBroadcastContent {
	case "live":
		t, err := time.Parse(time.RFC3339, video.LiveStreamingDetails.ActualStartTime)
		if err != nil {
			return nil, errx.WithStack(err, errFields)
		}
		return &dto.Live{
			ItemBase: &dto.ItemBase{
				Type:         dto.EnumLive,
				ChannelId:    channelId,
				Author:       video.Snippet.ChannelTitle,
				Id:           itemId,
				Title:        video.Snippet.Title,
				ThumbnailUrl: y.getThumbnailUrl(video.Snippet.Thumbnails),
				Url:          "https://www.youtube.com/watch?v=" + itemId,
			},
			ActualStartTime: &t,
		}, nil
	case "upcoming":
		t, err := time.Parse(time.RFC3339, video.LiveStreamingDetails.ScheduledStartTime)
		if err != nil {
			return nil, errx.WithStack(err, errFields)
		}
		return &dto.Live{
			ItemBase: &dto.ItemBase{
				Type:         dto.EnumLive, // TODO
				ChannelId:    channelId,
				Author:       video.Snippet.ChannelTitle,
				Id:           itemId,
				Title:        video.Snippet.Title,
				ThumbnailUrl: y.getThumbnailUrl(video.Snippet.Thumbnails),
				Url:          "https://www.youtube.com/watch?v=" + itemId,
			},
			ScheduledStartTime: &t,
		}, nil
	default:
		t, err := time.Parse(time.RFC3339, video.Snippet.PublishedAt)
		if err != nil {
			return nil, errx.WithStack(err, errFields)
		}
		return &dto.Video{
			ItemBase: &dto.ItemBase{
				Type:         dto.EnumVideo,
				ChannelId:    channelId,
				Author:       video.Snippet.ChannelTitle,
				Id:           itemId,
				Title:        video.Snippet.Title,
				ThumbnailUrl: y.getThumbnailUrl(video.Snippet.Thumbnails),
				Url:          "https://www.youtube.com/watch?v=" + itemId,
			},
			PubTime: &t,
		}, nil
	}
}

func (y *YoutubeService) getThumbnailUrl(details *youtube.ThumbnailDetails) string {
	if details.Maxres != nil {
		return details.Maxres.Url
	} else if details.High != nil {
		return details.High.Url
	} else if details.Medium != nil {
		return details.Medium.Url
	}
	return details.Default.Url
}
