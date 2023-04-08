package subscribe

import (
	"context"

	"github.com/Decmoe47/rabbitool/dao"
	entity "github.com/Decmoe47/rabbitool/entity/subscribe"
	"github.com/Decmoe47/rabbitool/errx"
	"github.com/Decmoe47/rabbitool/service"
	"github.com/mmcdole/gofeed"
	"github.com/rs/zerolog/log"
)

type youtubeSubscribeCommandHandler struct {
	*baseSubscribeCommandHandler[
		*entity.YoutubeSubscribe,
		*entity.YoutubeSubscribeConfig,
		*dao.YoutubeSubscribeDao,
		*dao.YoutubeSubscribeConfigDao,
	]
}

func newYoutubeSubscribeCommandHandler(
	qbSvc *service.QQBotService,
	qcsDao *dao.QQChannelSubscribeDao,
	subscribeDao *dao.YoutubeSubscribeDao,
	configDao *dao.YoutubeSubscribeConfigDao,
) *youtubeSubscribeCommandHandler {
	y := &youtubeSubscribeCommandHandler{
		baseSubscribeCommandHandler: &baseSubscribeCommandHandler[*entity.YoutubeSubscribe,
			*entity.YoutubeSubscribeConfig,
			*dao.YoutubeSubscribeDao,
			*dao.YoutubeSubscribeConfigDao,
		]{
			qbSvc:        qbSvc,
			qcsDao:       qcsDao,
			subscribeDao: subscribeDao,
			configDao:    configDao,
		},
	}
	y.iSubscribeCommandHandler = y
	return y
}

func (y *youtubeSubscribeCommandHandler) checkId(ctx context.Context, channelId string) (channelName, errMsg string) {
	fp := gofeed.NewParser()
	feed, err := fp.ParseURL("https://www.youtube.com/feeds/videos.xml?channel_id=" + channelId)
	if err != nil {
		log.Warn().Stack().Err(errx.WithStack(err, map[string]any{"channelId": channelId})).Msg(err.Error())
		return "", "错误：channelId为 " + channelId + " 的用户在油管上不存在！"
	}

	return feed.Items[0].Authors[0].Name, ""
}
