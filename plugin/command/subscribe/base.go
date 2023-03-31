package subscribe

import (
	"context"
	buildInErrors "errors"
	"strings"

	"github.com/Decmoe47/rabbitool/dao"
	"github.com/Decmoe47/rabbitool/dto"
	entity "github.com/Decmoe47/rabbitool/entity/subscribe"
	"github.com/Decmoe47/rabbitool/service"
	"github.com/Decmoe47/rabbitool/util"
	"github.com/cockroachdb/errors"
	"github.com/rs/zerolog/log"
	"github.com/samber/lo"
	"gorm.io/gorm"
)

type baseSubscribeCommandHandler[
	TSubscribe entity.ISubscribe,
	TConfig entity.ISubscribeConfig,
	TSubscribeDao dao.ISubscribeDao[TSubscribe],
	TConfigDao dao.ISubscribeConfigDao[TSubscribe, TConfig],
] struct {
	qbSvc        *service.QQBotService
	qcsDao       *dao.QQChannelSubscribeDao
	subscribeDao TSubscribeDao
	configDao    TConfigDao

	iSubscribeCommandHandler
}

func (b *baseSubscribeCommandHandler[TSubscribe, TConfig, TSubscribeDao, TConfigDao]) add(
	ctx context.Context,
	cmd *dto.SubscribeCommand,
) (string, error) {
	if cmd.SubscribeId == "" {
		return "请输入 " + cmd.Platform + " 对应的id！", nil
	}

	name, errMsg := b.checkId(ctx, cmd.SubscribeId)
	if errMsg != "" {
		return errMsg, nil
	}

	var (
		subscribeAreCreated bool
		record              TSubscribe
		channel             *entity.QQChannelSubscribe
		err                 error
	)

	record, err = b.subscribeDao.Get(ctx, cmd.SubscribeId)
	if errors.Is(err, gorm.ErrRecordNotFound) {
		record = entity.NewSubscribe[TSubscribe](cmd.SubscribeId, name)
		err = b.subscribeDao.Add(ctx, record)
		if err != nil {
			return "", err
		}

		subscribeAreCreated = true
	} else if err != nil {
		return "", err
	}

	channel, err = b.qcsDao.AddSubscribe(ctx, cmd.QQChannel.GuildId, cmd.QQChannel.GuildName, cmd.QQChannel.Id,
		cmd.QQChannel.Name, record)
	if err != nil {
		return "", err
	}

	_, err = b.configDao.CreateOrUpdate(ctx, channel, record, cmd.Configs)
	if err != nil {
		return "", err
	}

	if subscribeAreCreated {
		return "成功：已添加订阅到 " + cmd.QQChannel.Name + " 子频道！", nil
	}
	return "成功：已更新在 " + cmd.QQChannel.Name + " 子频道中的此订阅的配置！", nil
}

func (b *baseSubscribeCommandHandler[TSubscribe, TConfig, TSubscribeDao, TConfigDao]) delete(
	ctx context.Context,
	cmd *dto.SubscribeCommand,
) (string, error) {
	if cmd.SubscribeId == "" {
		return "请输入 " + cmd.Platform + " 对应的id！", nil
	}

	_, errMsg := b.checkId(ctx, cmd.SubscribeId)
	if errMsg != "" {
		return errMsg, nil
	}

	subscribe, err := b.subscribeDao.Get(ctx, cmd.SubscribeId)
	if errors.Is(err, gorm.ErrRecordNotFound) {
		return "错误：不存在该订阅！", nil
	} else if err != nil {
		return "", err
	}

	record, err := b.qcsDao.RemoveSubscribe(ctx, cmd.QQChannel.GuildId, cmd.QQChannel.Id, cmd.SubscribeId, subscribe)
	if err != nil {
		return "", err
	}

	if record.SubscribeAreAllEmpty() {
		err := b.qcsDao.Delete(ctx, record)
		if err != nil {
			return "", err
		}
	}

	_, err = b.subscribeDao.Delete(ctx, cmd.SubscribeId)
	if errors.Is(err, gorm.ErrRecordNotFound) {
		return "错误：不存在该订阅！", nil
	} else if err != nil {
		return "", err
	}
	_, err = b.configDao.Delete(ctx, cmd.QQChannel.Id, cmd.SubscribeId)
	if errors.Is(err, gorm.ErrRecordNotFound) {
		return "错误：不存在该订阅！", nil
	} else if err != nil {
		return "", err
	}

	return "成功：已删除在 " + cmd.QQChannel.Name + " 子频道中的此订阅！", nil
}

func (b *baseSubscribeCommandHandler[TSubscribe, TConfig, TSubscribeDao, TConfigDao]) list(
	ctx context.Context,
	cmd *dto.SubscribeCommand,
) (string, error) {
	var exampleSubscribe TSubscribe

	if cmd.Configs != nil {
		if allChannels, ok := cmd.Configs["allChannels"]; ok && allChannels == true {
			return b.listAllSubscribesInGuild(ctx, cmd)
		}
	}

	subscribeName := strings.ReplaceAll(string(exampleSubscribe.PropName()), "Subscribes", "")
	record, err := b.qcsDao.Get(ctx, cmd.QQChannel.GuildId, cmd.QQChannel.Id, exampleSubscribe)
	if errors.Is(err, gorm.ErrRecordNotFound) {
		return "错误：" + cmd.QQChannel.Name + " 子频道未有 " + subscribeName + " 的任何订阅！", nil
	} else if err != nil {
		return "", err
	}

	subscribes := entity.GetSubscribes[TSubscribe](record)
	if subscribes == nil || len(subscribes) == 0 {
		return "错误：" + cmd.QQChannel.Name + " 子频道未有 " + subscribeName + " 的任何订阅！", nil
	}

	if cmd.SubscribeId == "" {
		return b.listAllSubscribesInChannel(ctx, cmd.QQChannel.Id, cmd.QQChannel.Name, subscribes)
	}
	return b.listSubscribesInChannel(ctx, cmd, subscribes)
}

// listAllSubscribesInGuild 列出当前频道里所有子频道的所有类型订阅
func (b *baseSubscribeCommandHandler[TSubscribe, TConfig, TSubscribeDao, TConfigDao]) listAllSubscribesInGuild(
	ctx context.Context,
	cmd *dto.SubscribeCommand,
) (string, error) {
	allChannels, err := b.qcsDao.GetAll(ctx, cmd.QQChannel.GuildId)
	if err != nil {
		return "", err
	}
	if allChannels == nil || len(allChannels) == 0 {
		return "错误：当前频道的任何子频道都没有订阅！", nil
	}

	var (
		result string
		errs   error
	)
	for _, channel := range allChannels {
		subscribes := entity.GetSubscribes[TSubscribe](channel)
		if subscribes == nil || len(subscribes) == 0 {
			continue
		}

		msg, err := b.listAllSubscribesInChannel(ctx, channel.ChannelId, channel.ChannelName, subscribes)
		if err != nil {
			errs = buildInErrors.Join(errs, err)
		}
		result = msg + "\n\n"
	}

	if errs != nil {
		return "", errs
	}
	return result, nil
}

// listAllSubscribesInChannel 列出指定子频道里的所有类型订阅
func (b *baseSubscribeCommandHandler[TSubscribe, TConfig, TSubscribeDao, TConfigDao]) listAllSubscribesInChannel(
	ctx context.Context,
	channelId string,
	channelName string,
	subscribes []TSubscribe,
) (string, error) {
	result := ""

	for _, subscribe := range subscribes {
		result += "- " + subscribe.GetInfo("，")
		config, err := b.configDao.Get(ctx, channelId, subscribe.GetId())
		if err != nil {
			return "", err
		}
		result += "；配置：" + config.GetConfigs("，") + "\n"
	}

	result = util.RegexMailAddress.ReplaceAllStringFunc(result, func(s string) string {
		return strings.ReplaceAll(s, ".", "*")
	}) // 邮箱地址会被识别为链接导致无法过审
	return "【子频道：" + channelName + "】\n" + result, nil
}

// listSubscribesInChannel 列出指定子频道里的指定类型订阅
func (b *baseSubscribeCommandHandler[TSubscribe, TConfig, TSubscribeDao, TConfigDao]) listSubscribesInChannel(
	ctx context.Context,
	cmd *dto.SubscribeCommand,
	subscribes []TSubscribe,
) (string, error) {
	result := ""

	_, errMsg := b.checkId(ctx, cmd.SubscribeId)
	if errMsg != "" {
		return errMsg, nil
	}

	subscribe, ok := lo.Find(subscribes, func(item TSubscribe) bool {
		return item.GetId() == cmd.SubscribeId
	})
	if !ok {
		log.Warn().
			Str("subscribeId", cmd.SubscribeId).
			Str("channelId", cmd.QQChannel.Id).
			Msgf("The subscribe doesn't exist in the channel!")
		return "错误：id为 " + cmd.SubscribeId + " 的用户未在 " + cmd.QQChannel.Name + " 子频道订阅过！", nil
	}

	config, err := b.configDao.Get(ctx, cmd.QQChannel.Id, cmd.SubscribeId)
	if errors.Is(err, gorm.ErrRecordNotFound) {
		result = subscribe.GetInfo("，")
	} else if err != nil {
		return "", err
	}
	result = subscribe.GetInfo("，") + "；配置：" + config.GetConfigs("，") + "\n"

	result = util.RegexMailAddress.ReplaceAllStringFunc(result, func(s string) string {
		return strings.ReplaceAll(s, ".", "*")
	})
	return result, nil
}
