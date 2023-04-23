package plugin

import (
	"context"
	"encoding/json"
	"fmt"
	"sync"
	"time"

	"github.com/Decmoe47/async"
	"github.com/Decmoe47/rabbitool/dao"
	"github.com/Decmoe47/rabbitool/dto/qqbot/forum"
	dto "github.com/Decmoe47/rabbitool/dto/twitter"
	entity "github.com/Decmoe47/rabbitool/entity/subscribe"
	"github.com/Decmoe47/rabbitool/errx"
	"github.com/Decmoe47/rabbitool/service"
	"github.com/Decmoe47/rabbitool/util"
	"github.com/go-co-op/gocron"
	"github.com/rs/zerolog/log"
	"github.com/samber/lo"
)

type TwitterPlugin struct {
	*PluginBase

	svc          *service.TwitterService
	subscribeDao *dao.TwitterSubscribeDao
	configDao    *dao.TwitterSubscribeConfigDao

	storedTweets *sync.Map
}

func NewTwitterPlugin(base *PluginBase) *TwitterPlugin {
	return &TwitterPlugin{
		PluginBase:   base,
		svc:          service.NewTwitterService(),
		subscribeDao: dao.NewTwitterSubscribeDao(),
		configDao:    dao.NewTwitterSubscribeConfigDao(),
		storedTweets: &sync.Map{},
	}
}

func (t *TwitterPlugin) init(ctx context.Context, sch *gocron.Scheduler) error {
	_, err := sch.Every(5).Seconds().Do(func() {
		t.CheckAll(ctx)
	})
	return err
}

func (t *TwitterPlugin) CheckAll(ctx context.Context) {
	records, err := t.subscribeDao.GetAll(ctx)
	if err != nil {
		log.Error().Stack().Err(err).Msgf(err.Error())
	}
	if len(records) == 0 {
		log.Debug().Msgf("There isn't any twitter subscribe yet!")
	}

	var fns []func() error
	for _, record := range records {
		record := record
		fns = append(fns, func() error {
			return t.check(ctx, record)
		})
	}
	errs := async.ExecAllOne(ctx, fns).Await(ctx)
	for _, err := range errs {
		if err != nil {
			log.Error().Stack().Err(err).Msgf(err.Error())
		}
	}
}

func (t *TwitterPlugin) check(ctx context.Context, record *entity.TwitterSubscribe) (err error) {
	defer errx.Recover(&err)

	tweet, err := t.svc.GetLatestTweet(ctx, record.ScreenName)
	if err != nil {
		return err
	}

	if tweet.PubTime.Compare(*record.LastTweetTime) <= 0 {
		log.Debug().Msgf("No new tweet from the twitter user %s (screenName: %s).", tweet.Author,
			tweet.AuthorScreenName)
		return nil
	}

	// 宵禁时间发不出去，攒着
	now := time.Now().UTC().In(util.CST())
	if now.Hour() >= 0 && now.Hour() <= 5 {
		nested, _ := t.storedTweets.LoadOrStore(tweet.AuthorScreenName, &sync.Map{})
		nested.(*sync.Map).LoadOrStore(tweet.PubTime, tweet)

		log.Debug().Msgf("Tweet message of the user %s is skipped because it's curfew time now.",
			tweet.AuthorScreenName)
		return nil
	}

	// 过了宵禁把攒的先发了
	if nested, ok := t.storedTweets.Load(tweet.AuthorScreenName); ok {
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
			tw, ok := nestedMap.Load(uploadTime)
			if !ok {
				continue
			}
			err := t.pushTweetAndUpdateRecord(ctx, tw.(*dto.Tweet), record)
			if err != nil {
				errs = errx.Join(errs, err)
			}
			nestedMap.Delete(uploadTime)
		}

		return errs
	}

	return t.pushTweetAndUpdateRecord(ctx, tweet, record)
}

func (t *TwitterPlugin) pushTweetAndUpdateRecord(
	ctx context.Context,
	tweet *dto.Tweet,
	record *entity.TwitterSubscribe,
) error {
	err := t.pushTweetMsg(ctx, tweet, record)
	if err != nil {
		return err
	}

	record.LastTweetId = tweet.Id
	record.LastTweetTime = tweet.PubTime
	err = t.subscribeDao.Update(ctx, record)
	if err != nil {
		return err
	}

	log.Debug().Msgf("Succeeded to updated the twitter user(%s)'s record.", record.ScreenName)
	return nil
}

func (t *TwitterPlugin) pushTweetMsg(ctx context.Context, tweet *dto.Tweet, record *entity.TwitterSubscribe) error {
	title, text, err := t.tweetToStr(tweet)
	if err != nil {
		return err
	}

	imgUrls, err := t.getTweetImgUrls(tweet)
	if err != nil {
		return err
	}

	configs, err := t.configDao.GetAll(ctx, record.ScreenName)
	if err != nil {
		return err
	}

	var fns []func() error
	for _, channel := range record.QQChannels {
		channel := channel
		if _, err := t.qbSvc.GetChannel(ctx, channel.ChannelId); err != nil {
			log.Warn().
				Str("channelName", channel.ChannelName).
				Str("channelId", channel.ChannelId).
				Msgf("The channel %s doesn't exist!", channel.ChannelName)
			continue
		}

		config, ok := lo.Find(configs, func(item *entity.TwitterSubscribeConfig) bool {
			return item.QQChannel.ChannelId == channel.ChannelId
		})
		if !ok {
			log.Error().
				Str("screenName", tweet.AuthorScreenName).
				Str("channelName", channel.ChannelName).
				Msg("Failed to get configs for the twitter subscribe!")
			continue
		}

		if tweet.Origin != nil {
			if !config.QuotePush {
				continue
			}
		}
		if config.PushToThread {
			richText, err := t.tweetToRichText(tweet, text)
			if err != nil {
				return err
			}
			j, err := json.Marshal(richText)
			if err != nil {
				return errx.WithStack(err, map[string]any{"screenName": record.ScreenName})
			}

			fns = append(fns, func() (err error) {
				defer errx.Recover(&err)

				err = t.qbSvc.PostThread(ctx, channel.ChannelId, title, string(j))
				if err == nil {
					log.Info().Msgf("Succeeded to push the tweet message to the channel %s", channel.ChannelName)
				}
				return err
			})
			continue
		}

		fns = append(fns, func() (err error) {
			defer errx.Recover(&err)

			_, err = t.qbSvc.PushCommonMessage(ctx, channel.ChannelId, title+"\n\n"+text, imgUrls)
			if err == nil {
				log.Info().Msgf("Succeeded to push the tweet message to the channel %s", channel.ChannelName)
			}
			return err
		})
	}

	return errx.Blend(async.ExecAllOne(ctx, fns).Await(ctx))
}

func (t *TwitterPlugin) tweetToStr(tweet *dto.Tweet) (title string, text string, err error) {
	pubTimeStr := tweet.PubTime.In(util.CST()).Format("2006-01-02 15:04:05 MST")

	if tweet.Origin == nil {
		title = "【新推文】来自 " + tweet.Author
		text = fmt.Sprintf(
			`%s
——————————
推文发布时间：%s
推文链接：%s`,
			addRedirectToUrls(tweet.Text),
			pubTimeStr,
			addRedirectToUrls(tweet.Url),
		)
	} else if tweet.Type == dto.EnumQuote {
		title = "【新带评论转发推文】来自 " + tweet.Author
		text = fmt.Sprintf(
			`%s
——————————
推文发布时间：%s
推文链接：%s

====================
【原推文】来自 %s

%s
——————————
原推文发布时间：%s
原推文链接：%s`,
			addRedirectToUrls(tweet.Text),
			pubTimeStr,
			addRedirectToUrls(tweet.Url),
			tweet.Origin.Author,
			addRedirectToUrls(tweet.Origin.Text),
			tweet.Origin.PubTime.In(util.CST()).Format("2006-01-02 15:04:05 MST"),
			addRedirectToUrls(tweet.Origin.Url),
		)
	} else {
		return "", "", errx.New(errx.ErrNotSupported, "Not Supported tweet type %d", tweet.Type)
	}

	if tweet.HasVideo || (tweet.Origin != nil && tweet.Origin.HasVideo) {
		videoUrl, err := t.uploader.UploadVideo(tweet.Url, tweet.PubTime)
		if err != nil {
			return "", "", err
		}

		text += "\n\n视频下载直链：" + videoUrl
	}

	if tweet.ImageUrls != nil && len(tweet.ImageUrls) != 0 {
		text += "\n图片："
	}

	return
}

func (t *TwitterPlugin) getTweetImgUrls(tweet *dto.Tweet) (result []string, err error) {
	var imgUrls []string

	if tweet.ImageUrls != nil {
		imgUrls = tweet.ImageUrls
	} else if tweet.Origin != nil && tweet.Origin.ImageUrls != nil {
		imgUrls = tweet.Origin.ImageUrls
	}

	for _, url := range imgUrls {
		imgUrl, err := t.uploader.UploadImage(url)
		if err != nil {
			log.Error().Stack().Err(err).Msgf(err.Error())
			continue
		}
		result = append(result, imgUrl)
	}
	return
}

func (t *TwitterPlugin) tweetToRichText(tweet *dto.Tweet, text string) (*forum.RichText, error) {
	result := service.TextToRichText(text)

	if tweet.ImageUrls != nil {
		paras, err := service.ImagesToParagraphs(tweet.ImageUrls, t.uploader)
		if err != nil {
			return nil, err
		}
		result.Paragraphs = append(result.Paragraphs, paras...)
	} else if tweet.Origin != nil && tweet.Origin.ImageUrls != nil {
		paras, err := service.ImagesToParagraphs(tweet.Origin.ImageUrls, t.uploader)
		if err != nil {
			return nil, err
		}
		result.Paragraphs = append(result.Paragraphs, paras...)
	}

	if tweet.HasVideo {
		paras, err := service.VideoToParagraphs(tweet.Url, tweet.PubTime, t.uploader)
		if err != nil {
			return nil, err
		}
		result.Paragraphs = append(result.Paragraphs, paras...)
	} else if tweet.Origin != nil && tweet.Origin.HasVideo {
		paras, err := service.VideoToParagraphs(tweet.Origin.Url, tweet.Origin.PubTime, t.uploader)
		if err != nil {
			return nil, err
		}
		result.Paragraphs = append(result.Paragraphs, paras...)
	}
	return result, nil
}
