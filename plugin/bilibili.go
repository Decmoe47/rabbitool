package plugin

import (
	"context"
	buildInErrors "errors"
	"fmt"
	"math/rand"
	"sync"
	"time"

	"github.com/Decmoe47/rabbitool/dao"
	dto "github.com/Decmoe47/rabbitool/dto/bilibili"
	entity "github.com/Decmoe47/rabbitool/entity/subscribe"
	"github.com/Decmoe47/rabbitool/errx"
	"github.com/Decmoe47/rabbitool/service"
	"github.com/Decmoe47/rabbitool/util"
	"github.com/go-co-op/gocron"
	"github.com/rs/zerolog/log"
	"github.com/samber/lo"
)

type BilibiliPlugin struct {
	*PluginBase

	svc          *service.BilibiliService
	subscribeDao *dao.BilibiliSubscribeDao
	configDao    *dao.BilibiliSubscribeConfigDao

	storedDynamics *sync.Map
	allow          bool
}

func NewBilibiliPlugin(base *PluginBase) *BilibiliPlugin {
	return &BilibiliPlugin{
		PluginBase:     base,
		svc:            service.NewBilibiliService(),
		subscribeDao:   dao.NewBilibiliSubscribeDao(),
		configDao:      dao.NewBilibiliSubscribeConfigDao(),
		storedDynamics: &sync.Map{},
		allow:          true,
	}
}

func (b *BilibiliPlugin) init(ctx context.Context, sch *gocron.Scheduler) error {
	err := b.refreshCookies(ctx) // 反爬应对
	if err != nil {
		log.Error().Stack().Err(err).Msgf(err.Error())
	}
	_, err = sch.Every(10).Seconds().Do(func() {
		if !b.allow {
			return
		}
		b.allow = false
		time.Sleep(time.Second * time.Duration(rand.Intn(50))) // 反爬应对
		b.checkAll(ctx)
		b.allow = true
	})
	return err
}

func (b *BilibiliPlugin) refreshCookies(ctx context.Context) error {
	return b.svc.RefreshCookies(ctx)
}

func (b *BilibiliPlugin) checkAll(ctx context.Context) {
	records, err := b.subscribeDao.GetAll(ctx)
	if err != nil {
		log.Error().Stack().Err(err).Msgf(err.Error())
	}
	if len(records) == 0 {
		log.Debug().Msgf("There isn't any bilibili subscribe yet!")
	}

	errsOfDynamic := make(chan error, len(records))
	errsOfLive := make(chan error, len(records))
	for _, record := range records {
		go func(record *entity.BilibiliSubscribe, errs chan error) {
			errs <- b.checkDynamic(ctx, record)
		}(record, errsOfDynamic)

		go func(record *entity.BilibiliSubscribe, errs chan error) {
			errs <- b.checkLive(ctx, record)
		}(record, errsOfLive)
	}
	for i := 0; i < len(records); i++ {
		if err := <-errsOfDynamic; err != nil {
			log.Error().
				Stack().Err(err).
				Str("uname", records[i].Uname).
				Uint("uid", records[i].Uid).
				Msgf("Failed to push dynamic message!")
		}
		if err := <-errsOfLive; err != nil {
			log.Error().
				Stack().Err(err).
				Str("uname", records[i].Uname).
				Uint("uid", records[i].Uid).
				Msgf("Failed to push live message!")
		}
	}
}

func (b *BilibiliPlugin) checkDynamic(ctx context.Context, record *entity.BilibiliSubscribe) (err error) {
	defer errx.Recover(&err)

	dynamic, err := b.svc.GetLatestDynamic(ctx, record.Uid, 0, 0)
	if err != nil {
		return err
	}

	if dynamic.GetDynamicUploadTime().Compare(*record.LastDynamicTime) <= 0 {
		log.Debug().
			Str("uname", dynamic.GetUname()).
			Uint("uid", dynamic.GetUid()).
			Msgf("No new dynamic from the bilibili user %s.", dynamic.GetUname())
		return nil
	}

	// 宵禁时间发不出去，攒着
	now := time.Now().UTC().In(util.CST())
	if now.Hour() >= 0 && now.Hour() <= 5 {
		nested, _ := b.storedDynamics.LoadOrStore(dynamic.GetUid(), &sync.Map{})
		nested.(*sync.Map).LoadOrStore(dynamic.GetDynamicUploadTime(), dynamic)

		log.Debug().
			Str("uname", dynamic.GetUname()).
			Uint("uid", dynamic.GetUid()).
			Msgf("Dynamic message of the user %s is skipped because it's curfew time now.", dynamic.GetUname())
		return nil
	}

	// 过了宵禁把攒的先发了
	if nested, ok := b.storedDynamics.Load(dynamic.GetUid()); ok {
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
			dy, ok := nestedMap.Load(uploadTime)
			if !ok {
				continue
			}
			err := b.pushDynamicAndUpdateRecord(ctx, dy.(dto.IDynamic), record)
			if err != nil {
				errs = buildInErrors.Join(errs, err)
			}
			nestedMap.Delete(uploadTime)
		}

		if errs != nil {
			return errs
		}
		return nil
	}

	return b.pushDynamicAndUpdateRecord(ctx, dynamic, record)
}

func (b *BilibiliPlugin) pushDynamicAndUpdateRecord(
	ctx context.Context,
	dynamic dto.IDynamic,
	record *entity.BilibiliSubscribe,
) error {
	err := b.pushDynamicMsg(ctx, dynamic, record)
	if err != nil {
		return err
	}

	record.LastDynamicTime = dynamic.GetDynamicUploadTime()
	record.LastDynamicType = dynamic.GetDynamicType()
	err = b.subscribeDao.Update(ctx, record)
	if err != nil {
		return err
	}
	log.Debug().
		Str("uname", dynamic.GetUname()).
		Uint("uid", dynamic.GetUid()).
		Msgf("Succeeded to updated the bilibili user %s's record.", dynamic.GetUname())
	return nil
}

func (b *BilibiliPlugin) pushDynamicMsg(
	ctx context.Context,
	dynamic dto.IDynamic,
	record *entity.BilibiliSubscribe,
) error {
	title, text, imgUrls := b.dynamicToStr(dynamic)

	var redirectImgUrls []string
	if imgUrls != nil {
		for _, imgUrl := range imgUrls {
			url, err := b.uploader.UploadImage(imgUrl)
			if err != nil {
				return err
			}

			redirectImgUrls = append(redirectImgUrls, url)
		}
	}

	configs, err := b.configDao.GetAllByUint(ctx, dynamic.GetUid())
	if err != nil {
		return err
	}

	count := len(record.QQChannels)
	errs := make(chan error, count)
	for _, channel := range record.QQChannels {
		if _, err := b.qbSvc.GetChannel(ctx, channel.ChannelId); err != nil {
			log.Warn().
				Str("channelName", channel.ChannelName).
				Str("channelId", channel.ChannelId).
				Msgf("The channel %s doesn't exist!", channel.ChannelName)
			continue
		}

		config, ok := lo.Find(configs, func(item *entity.BilibiliSubscribeConfig) bool {
			return item.QQChannel.ChannelId == channel.ChannelId
		})
		if !ok {
			log.Error().
				Uint("uid", dynamic.GetUid()).
				Str("channelName", channel.ChannelName).
				Msg("Failed to get configs for the bilibili subscribe!")
			continue
		}

		if !config.DynamicPush {
			continue
		}
		if dynamic.GetDynamicType() == dto.EnumPureForward && !config.PureForwardDynamicPush {
			continue
		}

		go func(channel *entity.QQChannelSubscribe, errs chan error) {
			defer errx.RecoverAndLog()

			_, err := b.qbSvc.PushCommonMessage(ctx, channel.ChannelId, title+"\n\n"+text, redirectImgUrls)
			if err == nil {
				log.Info().Msgf("Succeeded to push the dynamic message to the channel %s", channel.ChannelName)
			}
			errs <- err
		}(channel, errs)
	}

	return util.ReceiveErrs(errs, count)
}

func (b *BilibiliPlugin) checkLive(ctx context.Context, record *entity.BilibiliSubscribe) error {
	live, err := b.svc.GetLive(ctx, record.Uid)
	if err != nil {
		return err
	}

	if record.LastLiveStatus != dto.EnumStreaming {
		if live.LiveStatus != dto.EnumStreaming {
			// 未开播
			log.Debug().
				Str("uname", live.Uname).
				Uint("uid", live.Uid).
				Msgf("No live now from the bilibili user %s.", live.Uname)
		} else {
			// 开播
			err := b.pushLiveAndUpdateRecord(ctx, record, live, dto.EnumStreaming)
			if err != nil {
				return err
			}
		}
	} else {
		if live.LiveStatus == dto.EnumStreaming {
			// 直播中
			log.Debug().
				Str("uname", live.Uname).
				Uint("uid", live.Uid).
				Msgf("The bilibili user %s is living.", live.Uname)
		} else {
			// 下播
			err := b.pushLiveAndUpdateRecord(ctx, record, live, dto.EnumStreaming)
			if err != nil {
				return err
			}
		}
	}
	return nil
}

func (b *BilibiliPlugin) pushLiveAndUpdateRecord(
	ctx context.Context,
	record *entity.BilibiliSubscribe,
	live *dto.Live,
	liveStatus dto.LiveStatusEnum,
) error {
	now := time.Now().UTC().In(util.CST())
	if now.Hour() >= 0 && now.Hour() <= 5 {
		log.Debug().
			Str("uname", live.Uname).
			Uint("uid", live.Uid).
			Msgf("Live message of the user %s is skipped because it's curfew time now.", live.Uname)
	} else {
		err := b.pushLiveMsg(ctx, live, record, liveStatus)
		if err != nil {
			return err
		}

		record.LastLiveStatus = live.LiveStatus
		err = b.subscribeDao.Update(ctx, record)
		if err != nil {
			return err
		}
		log.Debug().
			Str("uname", live.Uname).
			Uint("uid", live.Uid).
			Msgf("Succeeded to updated the bilibili user %s's record.", live.Uname)
	}
	return nil
}

func (b *BilibiliPlugin) pushLiveMsg(
	ctx context.Context,
	live *dto.Live,
	record *entity.BilibiliSubscribe,
	liveStatus dto.LiveStatusEnum,
) error {
	title, text := b.liveToStr(live)
	redirectCoverUrl := lo.Ternary(live.CoverUrl != "", []string{live.CoverUrl}, nil)

	configs, err := b.configDao.GetAllByUint(ctx, record.Uid)
	if err != nil {
		return err
	}

	count := len(record.QQChannels)
	errs := make(chan error, count)
	for _, channel := range record.QQChannels {
		if _, err := b.qbSvc.GetChannel(ctx, channel.ChannelId); err != nil {
			log.Warn().
				Str("channelName", channel.ChannelName).
				Str("channelId", channel.ChannelId).
				Msgf("The channel %s doesn't exist!", channel.ChannelName)
			continue
		}

		config, ok := lo.Find(configs, func(item *entity.BilibiliSubscribeConfig) bool {
			return item.QQChannel.ChannelId == channel.ChannelId
		})
		if !ok {
			log.Error().
				Uint("uid", live.Uid).
				Str("channelName", channel.ChannelName).
				Msg("Failed to get configs for the bilibili subscribe!")
			continue
		}

		if !config.LivePush {
			continue
		}
		if liveStatus == dto.EnumNoLiveStream && !config.LiveEndingPush {
			continue
		}

		go func(channel *entity.QQChannelSubscribe, errCh chan error) {
			defer errx.RecoverAndLog()

			_, err := b.qbSvc.PushCommonMessage(ctx, channel.ChannelId, title+"\n\n"+text, redirectCoverUrl)
			if err == nil {
				log.Info().Msgf("Succeeded to push the dynamic message to the channel %s", channel.ChannelName)
			}
			errCh <- err
		}(channel, errs)
	}

	return util.ReceiveErrs(errs, count)
}

func (b *BilibiliPlugin) dynamicToStr(dynamic dto.IDynamic) (title string, text string, imgUrls []string) {
	switch d := dynamic.(type) {
	case *dto.CommonDynamic:
		title, text = b.commonDynamicToStr(d)
		imgUrls = d.ImageUrls
	case *dto.VideoDynamic:
		title, text = b.videoDynamicToStr(d)
		imgUrls = []string{d.VideoThumbnailUrl}
	case *dto.ArticleDynamic:
		title, text = b.articleDynamicToStr(d)
		imgUrls = []string{d.ArticleThumbnailUrl}
	case *dto.ForwardDynamic:
		return b.forwardDynamicToStr(d)
	default:
		panic(errx.ErrInvalidParam)
	}

	return
}

func (b *BilibiliPlugin) commonDynamicToStr(dynamic *dto.CommonDynamic) (title, text string) {
	title = "【新动态】来自 " + dynamic.Uname
	uploadTime := dynamic.DynamicUploadTime.In(util.CST()).Format("2006-01-02 15:04:05 MST")

	if dynamic.Reserve == nil {
		text = fmt.Sprintf(
			`%s
——————————
动态发布时间：%s
动态链接：%s`,
			addRedirectToUrls(dynamic.Text),
			uploadTime,
			addRedirectToUrls(dynamic.DynamicUrl),
		)
	} else {
		text = fmt.Sprintf(
			`%s

%s
预约时间：%s
——————————
动态发布时间：%s
动态链接：%s`,
			addRedirectToUrls(dynamic.Text),
			dynamic.Reserve.Title,
			dynamic.Reserve.StartTime.In(util.CST()).Format("2006-01-02 15:04:05 MST"),
			uploadTime,
			addRedirectToUrls(dynamic.DynamicUrl),
		)
	}

	if dynamic.ImageUrls != nil && len(dynamic.ImageUrls) != 0 {
		text += "\n图片："
	}
	return
}

func (b *BilibiliPlugin) videoDynamicToStr(dynamic *dto.VideoDynamic) (title, text string) {
	title = "【新b站视频】来自 " + dynamic.Uname
	text = fmt.Sprintf(
		`【视频标题】
%s

【动态内容】
%s
——————————
视频发布时间：%s
视频链接：%s
视频封面：`,
		dynamic.VideoTitle,
		lo.Ternary(dynamic.DynamicText == "", "（无动态文本）", dynamic.DynamicText),
		dynamic.DynamicUploadTime.In(util.CST()).Format("2006-01-02 15:04:05 MST"),
		addRedirectToUrls(dynamic.VideoUrl),
	)
	return
}

func (b *BilibiliPlugin) articleDynamicToStr(dynamic *dto.ArticleDynamic) (title, text string) {
	title = "【新专栏】来自 " + dynamic.Uname
	text = fmt.Sprintf(
		`【专栏标题】
%s
——————————
专栏发布时间：%s
专栏链接：%s
专栏封面：`,
		dynamic.ArticleTitle,
		dynamic.DynamicUploadTime.In(util.CST()).Format("2006-01-02 15:04:05 MST"),
		addRedirectToUrls(dynamic.ArticleUrl),
	)
	return
}

func (b *BilibiliPlugin) forwardDynamicToStr(dynamic *dto.ForwardDynamic) (title string, text string, imgUrls []string) {
	title = "【新转发动态】来自 " + dynamic.Uname
	uploadTime := dynamic.DynamicUploadTime.In(util.CST()).Format("2006-01-02 15:04:05 MST")

	switch origin := dynamic.Origin.(type) {
	case string:
		text = fmt.Sprintf(
			`%s
——————————
动态发布时间：%s
动态链接：%s

====================
（原动态已被删除）`,
			addRedirectToUrls(dynamic.DynamicText),
			uploadTime,
			addRedirectToUrls(dynamic.DynamicUrl),
		)
	case *dto.CommonDynamic:
		originUploadTime := origin.DynamicUploadTime.In(util.CST()).Format("2006-01-02 15:04:05 MST")
		if origin.Reserve == nil {
			text = fmt.Sprintf(
				`%s
	——————————
	动态发布时间：%s
	动态链接：%s

	====================
	【原动态】来自 %s

	%s
	——————————
	原动态发布时间：%s
	原动态链接：%s`,
				addRedirectToUrls(dynamic.DynamicText),
				uploadTime,
				addRedirectToUrls(dynamic.DynamicUrl),
				origin.Uname,
				addRedirectToUrls(origin.Text),
				originUploadTime,
				addRedirectToUrls(origin.DynamicUrl),
			)
		} else {
			text = fmt.Sprintf(
				`%s
——————————
动态发布时间：%s
动态链接：%s

====================
【原动态】来自 %s

%s

%s
预约时间：%s
——————————
原动态发布时间：%s
原动态链接：%s`,
				addRedirectToUrls(dynamic.DynamicText),
				uploadTime,
				addRedirectToUrls(dynamic.DynamicUrl),
				origin.Uname,
				addRedirectToUrls(origin.Text),
				origin.Reserve.Title,
				origin.Reserve.StartTime.In(util.CST()).Format("2006-01-02 15:04:05 MST"),
				originUploadTime,
				addRedirectToUrls(origin.DynamicUrl),
			)
		}

		if origin.ImageUrls != nil && len(origin.ImageUrls) != 0 {
			text += "\n图片："
			imgUrls = origin.ImageUrls
		}
	case *dto.VideoDynamic:
		text = fmt.Sprintf(
			`%s

——————————
动态发布时间：%s
动态链接：%s

====================
【视频】来自 %s

【视频标题】
%s

【原动态内容】
%s
——————————
视频发布时间：%s
视频链接：%s
封面：`,
			addRedirectToUrls(dynamic.DynamicText),
			uploadTime,
			addRedirectToUrls(dynamic.DynamicUrl),
			origin.Uname,
			origin.VideoTitle,
			addRedirectToUrls(origin.DynamicText),
			origin.DynamicUploadTime.In(util.CST()).Format("2006-01-02 15:04:05 MST"),
			addRedirectToUrls(origin.VideoUrl),
		)
	case *dto.ArticleDynamic:
		text = fmt.Sprintf(
			`动态发布时间：%s
动态链接：%s

====================
【专栏】来自 %s

【专栏标题】
%s
——————————
专栏发布时间：%s
专栏链接：%s
封面：`,
			uploadTime,
			addRedirectToUrls(dynamic.DynamicUrl),
			origin.Uname,
			origin.ArticleTitle,
			origin.DynamicUploadTime.In(util.CST()).Format("2006-01-02 15:04:05 MST"),
			addRedirectToUrls(origin.ArticleUrl),
		)
	case *dto.LiveCardDynamic:
		text = fmt.Sprintf(
			`动态发布时间：%s
动态链接：%s

====================
【直播】来自 %s

直播标题：%s
直播开始时间：%s
直播间链接：%s
直播间封面：`,
			uploadTime,
			addRedirectToUrls(dynamic.DynamicUrl),
			origin.Uname,
			origin.Title,
			origin.LiveStartTime.In(util.CST()).Format("2006-01-02 15:04:05 MST"),
			addRedirectToUrls(fmt.Sprintf("https://live.bilibili.com/%d", origin.RoomId)),
		)
	default:
		panic(errx.ErrInvalidParam)
	}

	return
}

func (b *BilibiliPlugin) liveToStr(live *dto.Live) (title, text string) {
	if live.LiveStatus == dto.EnumStreaming && live.LiveStartTime != nil {
		title = "【b站开播】来自 " + live.Uname
		text = fmt.Sprintf(
			`直播标题：%s
开播时间：%s
直播间链接：%s`,
			live.Title,
			live.LiveStartTime.In(util.CST()).Format("2006-01-02 15:04:05 MST"),
			addRedirectToUrls(fmt.Sprintf("https://live.bilibili.com/%d", live.RoomId)),
		)
	} else {
		title = "【b站下播】来自 " + live.Uname
		text = fmt.Sprintf(
			`下播时间：%s
直播间链接：%s`,
			time.Now().UTC().In(util.CST()).Format("2006-01-02 15:04:05 MST"),
			addRedirectToUrls(fmt.Sprintf("https://live.bilibili.com/%d", live.RoomId)),
		)
	}
	return
}