package subscribe

import (
	"context"
	"strconv"
	"strings"

	"github.com/Decmoe47/rabbitool/dao"
	"github.com/Decmoe47/rabbitool/dto"
	"github.com/Decmoe47/rabbitool/errx"
	"github.com/Decmoe47/rabbitool/service"
	"github.com/cockroachdb/errors"
	"github.com/rs/zerolog/log"
	"github.com/samber/lo"
	qqBotDto "github.com/tencent-connect/botgo/dto"
)

var (
	AllSubscribeCommands = []*dto.CommandInfo{
		{
			Name:      "/订阅",
			Format:    []string{"/订阅", "[订阅平台]", "[订阅对象的id]", "[配置(可选)]"},
			Example:   "/订阅 b站 434565011 pureForwardDynamicPush=true livePush=false",
			Responder: respondToAddOrUpdateSubscribeCommand,
		}, {
			Name:      "/取消订阅",
			Format:    []string{"/取消订阅", "[订阅平台]", "[订阅对象的id]", "[请求参数(可选)]"},
			Example:   "/取消订阅 b站 434565011 bot通知",
			Responder: respondToDeleteSubscribeCommand,
		}, {
			Name:      "/列出订阅",
			Format:    []string{"/列出订阅", "[订阅平台]", "[订阅对象的id(可选，留空为所有)]", "[请求参数(可选)]"},
			Example:   "/列出订阅 b站 434565011",
			Responder: respondToListSubscribeCommand,
		},
	}
	supportedPlatforms = []string{"b站", "推特", "油管", "邮箱"}
	qbSvc              *service.QQBotService
)

func InitSubscribeCommandHandler(svc *service.QQBotService) {
	qbSvc = svc
}

type subscribeCommandTypeEnum int

const (
	addOrUpdate subscribeCommandTypeEnum = iota
	remove
	list
)

func respondToAddOrUpdateSubscribeCommand(ctx context.Context, cmd []string, msg *qqBotDto.WSATMessageData) string {
	result, err := respondToSubscribeCommand(ctx, cmd, msg, addOrUpdate)
	if err != nil {
		log.Error().Stack().Err(err).Msgf(err.Error())
		return "内部错误！"
	}
	return result
}

func respondToDeleteSubscribeCommand(ctx context.Context, cmd []string, msg *qqBotDto.WSATMessageData) string {
	result, err := respondToSubscribeCommand(ctx, cmd, msg, remove)
	if err != nil {
		log.Error().Stack().Err(err).Msgf(err.Error())
		return "内部错误！"
	}
	return result
}

func respondToListSubscribeCommand(ctx context.Context, cmd []string, msg *qqBotDto.WSATMessageData) string {
	result, err := respondToSubscribeCommand(ctx, cmd, msg, list)
	if err != nil {
		log.Error().Stack().Err(err).Msgf(err.Error())
		return "内部错误！"
	}
	return result
}

func respondToSubscribeCommand(
	ctx context.Context,
	cmd []string,
	msg *qqBotDto.WSATMessageData,
	cmdType subscribeCommandTypeEnum,
) (string, error) {
	if qbSvc == nil {
		return "", errors.Wrap(
			errx.ErrUnInitialized,
			"You must initialize SubscribeCommandResponder first by SubscribeCommandResponder.setting()!",
		)
	}

	if cmdType == list {
		if len(cmd) < 2 {
			return "错误：参数不足！", nil
		}
	} else if len(cmd) < 3 {
		return "错误：参数不足！", nil
	}

	if !lo.Contains(supportedPlatforms, cmd[1]) {
		return "错误：不支持的订阅平台！\n当前支持的订阅平台：" + strings.Join(supportedPlatforms, " "), nil
	}

	configs := []string{}
	configMap := map[string]any{}
	guild, err := qbSvc.GetGuild(ctx, msg.GuildID)
	if err != nil {
		return "", err
	}
	channelName := ""
	channelId := msg.ChannelID
	subscribeId := ""

	if cmdType == list {
		if len(cmd) == 3 && strings.Contains(cmd[2], "=") {
			configs = cmd[2:]
		}
	} else {
		subscribeId = cmd[2]
		if len(cmd) >= 4 && strings.Contains(cmd[3], "=") {
			configs = cmd[3:]
		}
	}

	for _, config := range configs {
		kv := strings.Split(config, "=")
		if kv[0] != "" && kv[1] != "" {
			configMap[kv[0]] = parseValue(kv[0], kv[1])
		}
	}

	if v, ok := configMap["channel"]; ok {
		v := v.(string)
		channel, err := qbSvc.GetChannelByName(ctx, v, msg.GuildID)
		if err != nil {
			return "错误：不存在名为 " + v + " 的子频道！", nil
		}
		channelId = channel.ID
		channelName = channel.Name
	} else {
		channel, err := qbSvc.GetChannel(ctx, channelId)
		if err != nil {
			return "", err
		}
		channelName = channel.Name
	}

	subscribeInfo := &dto.SubscribeCommand{
		Cmd:         cmd[0],
		Platform:    cmd[1],
		SubscribeId: subscribeId,
		QQChannel: &dto.SubscribeCommandQQChannel{
			GuildId:   msg.GuildID,
			GuildName: guild.Name,
			Id:        channelId,
			Name:      channelName,
		},
		Configs: lo.Ternary(len(configMap) != 0, configMap, nil),
	}

	handler := getSubscribeCommandHandler(subscribeInfo.Platform)
	switch cmdType {
	case addOrUpdate:
		return handler.add(ctx, subscribeInfo)
	case remove:
		return handler.delete(ctx, subscribeInfo)
	case list:
		return handler.list(ctx, subscribeInfo)
	default:
		panic(errx.ErrInvalidParam) // 断定上层不会传错
	}
}

func parseValue(key string, value string) any {
	if key == "channel" {
		return strings.ReplaceAll(value, `"`, "")
	} else if value == "true" {
		return true
	} else if value == "false" {
		return false
	} else if int64Result, err := strconv.ParseInt(value, 10, 64); err == nil {
		return int64Result
	} else if floatResult, err := strconv.ParseFloat(value, 32); err == nil {
		return floatResult
	}
	return nil
}

func getSubscribeCommandHandler(platform string) iSubscribeCommandHandler {
	qcsDao := dao.NewQQChannelSubscribeDao()
	switch platform { // TODO: switch
	case "b站":
		return newBilibiliSubscribeCommandHandler(
			qbSvc,
			qcsDao,
			dao.NewBilibiliSubscribeDao(),
			dao.NewBilibiliSubscribeConfigDao(),
		)
	case "推特":
		return newTwitterSubscribeCommandHandler(
			qbSvc,
			qcsDao,
			dao.NewTwitterSubscribeDao(),
			dao.NewTwitterSubscribeConfigDao(),
		)
	case "油管":
		return newYoutubeSubscribeCommandHandler(
			qbSvc,
			qcsDao,
			dao.NewYoutubeSubscribeDao(),
			dao.NewYoutubeSubscribeConfigDao(),
		)
	case "邮箱":
		return newMailSubscribeCommandHandler(
			qbSvc,
			qcsDao,
			dao.NewMailSubscribeDao(),
			dao.NewMailSubscribeConfigDao(),
		)
	default:
		panic(errors.Wrapf(errx.ErrNotSupported, "The platform %s is not supported!", platform))
	}
}
