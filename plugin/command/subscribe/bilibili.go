package subscribe

import (
	"context"
	"strconv"

	jv "github.com/Andrew-M-C/go.jsonvalue"
	"github.com/Decmoe47/rabbitool/dao"
	entity "github.com/Decmoe47/rabbitool/entity/subscribe"
	"github.com/Decmoe47/rabbitool/errx"
	"github.com/Decmoe47/rabbitool/service"
	"github.com/Decmoe47/rabbitool/util/req"
	"github.com/cockroachdb/errors"
	"github.com/rs/zerolog/log"
	ua "github.com/wux1an/fake-useragent"
)

type bilibiliSubscribeCommandHandler struct {
	*baseSubscribeCommandHandler[
		*entity.BilibiliSubscribe,
		*entity.BilibiliSubscribeConfig,
		*dao.BilibiliSubscribeDao,
		*dao.BilibiliSubscribeConfigDao,
	]
}

func newBilibiliSubscribeCommandHandler(
	qbSvc *service.QQBotService,
	qcsDao *dao.QQChannelSubscribeDao,
	subscribeDao *dao.BilibiliSubscribeDao,
	configDao *dao.BilibiliSubscribeConfigDao,
) *bilibiliSubscribeCommandHandler {
	b := &bilibiliSubscribeCommandHandler{
		baseSubscribeCommandHandler: &baseSubscribeCommandHandler[
			*entity.BilibiliSubscribe,
			*entity.BilibiliSubscribeConfig,
			*dao.BilibiliSubscribeDao,
			*dao.BilibiliSubscribeConfigDao,
		]{
			qbSvc:        qbSvc,
			qcsDao:       qcsDao,
			subscribeDao: subscribeDao,
			configDao:    configDao,
		},
	}
	b.iSubscribeCommandHandler = b
	return b
}

func (b *bilibiliSubscribeCommandHandler) checkId(ctx context.Context, uid string) (name, errMsg string) {
	_, err := strconv.ParseUint(uid, 10, 32)
	if err != nil {
		return "", "错误：uid不正确！"
	}

	resp, err := req.Client.R().
		SetQueryParam("mid", uid).
		SetHeader("User-Agent", ua.Random()).
		Get("https://api.bilibili.com/x/space/acc/info")
	if err != nil {
		log.Error().Stack().Err(errx.WithStack(err, map[string]any{"uid": uid})).Msg(err.Error())
		return "", "内部错误！"
	}

	body, err := jv.UnmarshalString(service.Replace509(resp.String()))
	if err != nil {
		log.Error().Stack().Err(errx.WithStack(err, map[string]any{"uid": uid})).Msg(err.Error())
		return "", "内部错误！"
	}

	if code, err := body.GetInt("code"); err != nil {
		log.Error().Stack().Err(errx.WithStack(err, map[string]any{"uid": uid})).Msg(err.Error())
		return "", "内部错误！"
	} else if code != 0 {
		err = errx.New(errx.ErrBilibiliApi, "Failed to get bilibili user(uid: %s)'s info!", uid)
		log.Error().Stack().Err(err).Msg(err.Error())
		return "", "内部错误！"
	}

	name, err = body.GetString("data", "name")
	if errors.Is(err, jv.ErrNotFound) {
		return "", "错误：uid为 " + uid + " 的用户在b站上不存在!"
	} else if err != nil {
		log.Error().Stack().Err(errx.WithStack(err, map[string]any{"uid": uid})).Msg(err.Error())
		return "", "内部错误！"
	} else if name == "" {
		return "", "错误：uid为 " + uid + " 的用户在b站上不存在!"
	}

	return name, ""
}
