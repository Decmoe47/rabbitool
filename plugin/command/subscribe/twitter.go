package subscribe

import (
	"context"

	jv "github.com/Andrew-M-C/go.jsonvalue"
	"github.com/Decmoe47/rabbitool/conf"
	"github.com/Decmoe47/rabbitool/dao"
	entity "github.com/Decmoe47/rabbitool/entity/subscribe"
	"github.com/Decmoe47/rabbitool/service"
	"github.com/Decmoe47/rabbitool/util/req"
	"github.com/cockroachdb/errors"
	"github.com/rs/zerolog/log"
)

type twitterSubscribeCommandHandler struct {
	*baseSubscribeCommandHandler[
		*entity.TwitterSubscribe,
		*entity.TwitterSubscribeConfig,
		*dao.TwitterSubscribeDao,
		*dao.TwitterSubscribeConfigDao,
	]
}

func newTwitterSubscribeCommandHandler(
	qbSvc *service.QQBotService,
	qcsDao *dao.QQChannelSubscribeDao,
	subscribeDao *dao.TwitterSubscribeDao,
	configDao *dao.TwitterSubscribeConfigDao,
) *twitterSubscribeCommandHandler {
	t := &twitterSubscribeCommandHandler{
		baseSubscribeCommandHandler: &baseSubscribeCommandHandler[
			*entity.TwitterSubscribe,
			*entity.TwitterSubscribeConfig,
			*dao.TwitterSubscribeDao,
			*dao.TwitterSubscribeConfigDao,
		]{
			qbSvc:        qbSvc,
			qcsDao:       qcsDao,
			subscribeDao: subscribeDao,
			configDao:    configDao,
		},
	}
	t.iSubscribeCommandHandler = t
	return t
}

func (t *twitterSubscribeCommandHandler) checkId(ctx context.Context, screenName string) (name, errMsg string) {
	resp, err := req.Client.R().
		SetBearerAuthToken(conf.R.Twitter.Token).
		Get("https://api.twitter.com/2/users/by/username/" + screenName)
	if resp.StatusCode == 404 {
		return "", "错误：screenName为 " + screenName + " 的用户在b站上不存在!"
	} else if err != nil {
		log.Error().Stack().Err(errors.WithStack(err)).Msg(err.Error())
		return "", "内部错误！"
	}

	body, err := jv.UnmarshalString(resp.String())
	if err != nil {
		log.Error().Stack().Err(errors.WithStack(err)).Msg(err.Error())
		return "", "内部错误！"
	}

	name, err = body.GetString("data", "name")
	if errors.Is(err, jv.ErrNotFound) {
		return "", "错误：screenName为 " + screenName + " 的用户在b站上不存在!"
	} else if err != nil {
		log.Error().Stack().Err(errors.WithStack(err)).Msg(err.Error())
		return "", "内部错误！"
	}
	return name, ""
}
