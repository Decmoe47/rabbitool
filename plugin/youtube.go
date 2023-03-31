package plugin

import (
	"context"
	buildInErrors "errors"
	"fmt"
	"sync"
	"time"

	"github.com/Decmoe47/rabbitool/dao"
	dto "github.com/Decmoe47/rabbitool/dto/youtube"
	entity "github.com/Decmoe47/rabbitool/entity/subscribe"
	"github.com/Decmoe47/rabbitool/errx"
	"github.com/Decmoe47/rabbitool/service"
	"github.com/Decmoe47/rabbitool/util"
	"github.com/go-co-op/gocron"
	"github.com/rs/zerolog/log"
	"github.com/samber/lo"
)

type YoutubePlugin struct {
	*PluginBase

	svc          *service.YoutubeService
	subscribeDao *dao.YoutubeSubscribeDao
	configDao    *dao.YoutubeSubscribeConfigDao

	storedVideos *sync.Map
}

func NewYoutubePlugin(ctx context.Context, basePlugin *PluginBase) (*YoutubePlugin, error) {
	svc, err := service.NewYoutubeService(ctx)
	if err != nil {
		return nil, err
	}
	return &YoutubePlugin{
		PluginBase:   basePlugin,
		svc:          svc,
		subscribeDao: dao.NewYoutubeSubscribeDao(),
		configDao:    dao.NewYoutubeSubscribeConfigDao(),
		storedVideos: &sync.Map{},
	}, nil
}

func (y *YoutubePlugin) init(ctx context.Context, sch *gocron.Scheduler) error {
	_, err := sch.Every(5).Seconds().Do(func() {
		y.CheckAll(ctx)
	})
	return err
}

func (y *YoutubePlugin) CheckAll(ctx context.Context) {
	records, err := y.subscribeDao.GetAll(ctx)
	if err != nil {
		log.Error().Stack().Err(err).Msgf(err.Error())
	}
	if len(records) == 0 {
		log.Debug().Msgf("There isn't any youtube subscribe yet!")
	}

	errs := make(chan error, len(records))
	for _, record := range records {
		go func(record *entity.YoutubeSubscribe, errs chan error) {
			errs <- y.check(ctx, record)
		}(record, errs)
	}
	for i := 0; i < len(records); i++ {
		if err := <-errs; err != nil {
			log.Error().
				Stack().Err(err).
				Str("channelId", records[i].ChannelId).
				Str("name", records[i].Name).
				Msgf("Failed to push youtube item message!")
		}
	}
}

func (y *YoutubePlugin) check(ctx context.Context, record *entity.YoutubeSubscribe) (err error) {
	defer errx.Recover(&err)

	item, err := y.svc.GetLatestVideoOrLive(ctx, record.ChannelId)
	if err != nil {
		return err
	}

	switch im := item.(type) {
	case *dto.Live:
		if im.Type == dto.EnumUpcomingLive && !lo.Contains(record.AllUpcomingLiveRoomIds, im.Id) {
			record.AllUpcomingLiveRoomIds = append(record.AllUpcomingLiveRoomIds, im.Id)
			err := y.subscribeDao.Update(ctx, record)
			if err != nil {
				return err
			}
			log.Debug().
				Str("name", im.Author).
				Str("channelId", im.ChannelId).
				Msgf("Succeeded to updated the youtube user's record.")

			return y.pushUpcomingLive(ctx, im, record)
		} else if im.Type == dto.EnumLive &&
			im.Id != record.LastLiveRoomId &&
			!lo.Contains(record.AllUpcomingLiveRoomIds, im.Id) {
			return y.pushLiveAndUpdateRecord(ctx, im, record)
		}
	case *dto.Video:
		if im.PubTime.Compare(*record.LastVideoPubTime) <= 0 {
			log.Debug().Msgf("No new video from the youtube user %s.", im.ChannelId)
			return nil
		}

		now := time.Now().In(util.CST())

		if now.Hour() >= 0 && now.Hour() <= 5 {
			nested, _ := y.storedVideos.LoadOrStore(im.ChannelId, &sync.Map{})
			nested.(*sync.Map).LoadOrStore(im.PubTime, im)

			log.Debug().
				Str("name", im.Author).
				Str("channelId", im.ChannelId).
				Msgf("Youtube video message of the user %s is skipped because it's curfew time now.", im.Author)
			return nil
		}

		if nested, ok := y.storedVideos.Load(im.ChannelId); ok {
			var (
				errs      error
				keys      []*time.Time
				nestedMap = nested.(*sync.Map)
			)

			nestedMap.Range(func(k, _ any) bool {
				keys = append(keys, k.(*time.Time))
				return true
			})
			for _, uploadTime := range keys {
				m, ok := nestedMap.Load(uploadTime)
				if !ok {
					continue
				}
				err := y.pushVideoAndUpdateRecord(ctx, m.(*dto.Video), record)
				if err != nil {
					errs = buildInErrors.Join(errs, err)
				}
				nestedMap.Delete(uploadTime)
			}

			return errs
		}

		return y.pushVideoAndUpdateRecord(ctx, im, record)
	}
	return nil
}

func (y *YoutubePlugin) checkUpcomingLive(ctx context.Context, record *entity.YoutubeSubscribe) error {
	all := record.AllUpcomingLiveRoomIds
	var errs error
	for _, roomId := range all {
		if live, ok := y.svc.IsStreaming(ctx, roomId); ok {
			log.Debug().Msgf("Youtube upcoming live (roomId: %s) starts streaming.", roomId)
			err := y.pushLiveAndUpdateRecord(ctx, live, record)
			if err != nil {
				errs = buildInErrors.Join(errs, err)
				continue
			}

			record.AllUpcomingLiveRoomIds = lo.Reject(record.AllUpcomingLiveRoomIds, func(item string, _ int) bool {
				return item == roomId
			})
			err = y.subscribeDao.Update(ctx, record)
			if err != nil {
				errs = buildInErrors.Join(errs, err)
				continue
			}
		}
	}
	return errs
}

func (y *YoutubePlugin) pushLiveAndUpdateRecord(
	ctx context.Context,
	live *dto.Live,
	record *entity.YoutubeSubscribe,
) error {
	now := time.Now().UTC().In(util.CST())
	if now.Hour() >= 0 && now.Hour() <= 5 {
		log.Debug().
			Msgf("Youtube upcoming live message of the user %s is skipped because it's curfew time now.", live.Author)
	} else {
		err := y.pushItemMsg(ctx, live, record)
		if err != nil {
			return err
		}
	}

	record.LastLiveRoomId = live.Id
	record.LastLiveStartTime = live.ActualStartTime
	record.AllArchiveVideoIds = append(record.AllArchiveVideoIds, live.Id)

	if len(record.AllArchiveVideoIds) > 5 {
		record.AllArchiveVideoIds = lo.Reject(record.AllArchiveVideoIds, func(_ string, index int) bool {
			return index == 0
		})
	}
	err := y.subscribeDao.Update(ctx, record)
	if err != nil {
		return err
	}
	log.Debug().
		Str("channelId", live.ChannelId).
		Msgf("Succeeded to updated the youtube user(%s)'s record.", live.Author)
	return nil
}

func (y *YoutubePlugin) pushUpcomingLive(ctx context.Context, live *dto.Live, record *entity.YoutubeSubscribe) error {
	now := time.Now().UTC().In(util.CST())
	if now.Hour() >= 0 && now.Hour() <= 5 {
		log.Debug().
			Msgf("Youtube upcoming live message of the user %s is skipped because it's curfew time now.", live.Author)
	} else {
		err := y.pushItemMsg(ctx, live, record)
		if err != nil {
			return err
		}
	}
	return nil
}

func (y *YoutubePlugin) pushVideoAndUpdateRecord(
	ctx context.Context,
	video *dto.Video,
	record *entity.YoutubeSubscribe,
) error {
	err := y.pushItemMsg(ctx, video, record)
	if err != nil {
		return err
	}

	record.LastVideoId = video.Id
	record.LastVideoPubTime = video.PubTime
	err = y.subscribeDao.Update(ctx, record)
	if err != nil {
		return err
	}
	log.Debug().
		Str("channelId", video.ChannelId).
		Msgf("Succeeded to updated the youtube user(%s)'s record.", video.Author)
	return nil
}

func (y *YoutubePlugin) pushItemMsg(ctx context.Context, item dto.IItem, record *entity.YoutubeSubscribe) error {
	title, text, imgUrl := y.itemToStr(item)
	uploadedImgUrl, err := y.uploader.UploadImage(imgUrl)
	if err != nil {
		return err
	}

	configs, err := y.configDao.GetAll(ctx, record.ChannelId)
	if err != nil {
		return err
	}

	count := len(record.QQChannels)
	errs := make(chan error, count)
	for _, channel := range record.QQChannels {
		if _, err := y.qbSvc.GetChannel(ctx, channel.ChannelId); err != nil {
			log.Warn().
				Str("channelName", channel.ChannelName).
				Str("channelId", channel.ChannelId).
				Msgf("The channel %s doesn't exist!", channel.ChannelName)
			continue
		}

		config, ok := lo.Find(configs, func(config *entity.YoutubeSubscribeConfig) bool {
			return config.QQChannel.ChannelId == channel.ChannelId
		})
		if !ok {
			log.Error().
				Str("channelId", item.GetChannelId()).
				Msg("Failed to get configs for the youtube subscribe!")
			continue
		}

		if item.GetType() == dto.EnumVideo && !config.VideoPush {
			continue
		}
		if item.GetType() == dto.EnumLive && !config.LivePush {
			continue
		}
		if item.GetType() == dto.EnumUpcomingLive && !config.UpcomingLivePush {
			continue
		}
		if config.ArchivePush && lo.Contains(record.AllArchiveVideoIds, item.GetChannelId()) {
			continue
		}

		go func(channel *entity.QQChannelSubscribe, errs chan error) {
			defer errx.RecoverAndLog()

			_, err := y.qbSvc.PushCommonMessage(ctx, channel.ChannelId, title+"\n\n"+text, []string{uploadedImgUrl})
			if err == nil {
				log.Info().Msgf("Succeeded to push the youtube message to the channel %s", channel.ChannelName)
			}
			errs <- err
		}(channel, errs)
	}

	return util.ReceiveErrs(errs, count)
}

func (y *YoutubePlugin) itemToStr(item dto.IItem) (title, text, imgUrl string) {
	switch i := item.(type) {
	case *dto.Live:
		if i.Type == dto.EnumLive {
			title = "【油管开播】来自 " + i.Author
		} else {
			title = "【油管预定开播】来自 " + i.Author
		}
		text = y.liveToStr(i)
		imgUrl = i.ThumbnailUrl
	case *dto.Video:
		title = "【新油管视频】来自 " + i.Author
		text = y.videoToStr(i)
		imgUrl = i.ThumbnailUrl
	}
	return
}

func (y *YoutubePlugin) liveToStr(live *dto.Live) string {
	if live.Type == dto.EnumLive {
		return fmt.Sprintf(
			`直播标题：%s
开播时间：%s
直播间链接：%s
直播间封面：`,
			live.Title,
			live.ActualStartTime.In(util.CST()).Format("2006-01-02 15:04:05 MST"),
			addRedirectToUrls(live.Url),
		)
	} else {
		return fmt.Sprintf(
			`直播标题：%s
开播时间：%s
直播间链接：%s
直播间封面：`,
			live.Title,
			live.ScheduledStartTime.In(util.CST()).Format("2006-01-02 15:04:05 MST"),
			addRedirectToUrls(live.Url),
		)
	}
}

func (y *YoutubePlugin) videoToStr(video *dto.Video) string {
	return fmt.Sprintf(
		`视频标题：%s
视频发布时间：%s
视频链接：%s
视频封面：`,
		video.Title,
		video.PubTime.In(util.CST()).Format("2006-01-02 15:04:05 MST"),
		addRedirectToUrls(video.Url),
	)
}
