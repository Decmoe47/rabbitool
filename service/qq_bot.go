package service

import (
	"context"
	"encoding/json"
	buildInErrors "errors"
	"fmt"
	"strings"
	"time"

	"github.com/Decmoe47/rabbitool/conf"
	forumDto "github.com/Decmoe47/rabbitool/dto/qqbot/forum"
	"github.com/Decmoe47/rabbitool/errx"
	"github.com/Decmoe47/rabbitool/util"
	"github.com/cockroachdb/errors"
	"github.com/rs/zerolog/log"
	"github.com/samber/lo"
	"github.com/tencent-connect/botgo"
	"github.com/tencent-connect/botgo/dto"
	qqBotErrs "github.com/tencent-connect/botgo/errs"
	"github.com/tencent-connect/botgo/event"
	"github.com/tencent-connect/botgo/openapi"
	"github.com/tencent-connect/botgo/token"
	"golang.org/x/time/rate"
)

type QQBotService struct {
	api      openapi.OpenAPI
	token    *token.Token
	ws       *dto.WebsocketAP
	handlers []any

	botId          string
	sandboxGuildId string

	limiter *rate.Limiter
}

func NewQQBotService(ctx context.Context) (*QQBotService, error) {
	botgo.SetLogger(&LoggerForQQBot{})

	botToken := token.BotToken(conf.R.QQBot.AppId, conf.R.QQBot.Token)
	api := botgo.NewOpenAPI(botToken).WithTimeout(3 * time.Second)
	ws, err := api.WS(ctx, nil, "")
	if err != nil {
		return nil, errors.WithStack(err)
	}

	return &QQBotService{
		api:   api,
		token: botToken,
		ws:    ws,
		limiter: rate.NewLimiter(rate.Every(time.Second*4),
			4), // See https://bot.q.qq.com/wiki/develop/api/openapi/message/post_messages.html
	}, nil
}

func (q *QQBotService) Run(ctx context.Context) (err error) {
	q.registerMessageAuditEvent()
	intent := event.RegisterHandlers(q.handlers...)
	go func() {
		err = botgo.NewSessionManager().Start(q.ws, q.token, &intent)
	}()
	if err != nil {
		return errors.WithStack(err)
	}

	time.Sleep(time.Second * 3)

	botId, err := q.getBotId(ctx)
	if err != nil {
		return errors.WithStack(err)
	}
	q.botId = botId

	sandboxGuild, err := q.GetGuildByName(ctx, conf.R.QQBot.SandboxGuildName)
	if err != nil {
		return errors.WithStack(err)
	}
	q.sandboxGuildId = sandboxGuild.ID

	return nil
}

func (q *QQBotService) RegisterAtMessageEvent(
	ctx context.Context,
	fn func(ctx context.Context, msg *dto.WSATMessageData) string,
) {
	var handler event.ATMessageEventHandler = func(event *dto.WSPayload, data *dto.WSATMessageData) error {
		if !(event.Type == dto.EventAtMessageCreate) {
			return nil
		}
		// if !strings.Contains(data.Content, "<@!"+q.botId+">") {
		// 	return nil
		// }
		// 在沙箱频道里发消息，正式环境里的bot不会响应
		if !conf.R.QQBot.IsSandbox && data.GuildID == q.sandboxGuildId {
			return nil
		}
		log.Info().
			Str("messageId", data.ID).
			Str("guidId", data.GuildID).
			Str("channelId", data.ChannelID).
			Str("content", data.Content).
			Msg("Received an @ message.")

		_, err := q.postMessage(ctx, data.ChannelID, &dto.MessageToCreate{
			Content: fn(ctx, data),
			MsgID:   data.ID,
			MessageReference: &dto.MessageReference{
				MessageID:             data.ID,
				IgnoreGetMessageError: false,
			},
		})
		if err != nil {
			return errors.WithStack(err)
		}

		return nil
	}
	q.handlers = append(q.handlers, handler)
}

func (q *QQBotService) registerMessageAuditEvent() {
	var handler event.MessageAuditEventHandler = func(event *dto.WSPayload, data *dto.WSMessageAuditData) error {
		if event.Type == dto.EventMessageAuditPass {
			log.Info().
				Str("messageId", data.MessageID).
				Str("channelId", data.ChannelID).
				Str("guildId", data.GuildID).
				Str("createTime", data.CreateTime).
				Msgf("Message audit passed.")
		} else if event.Type == dto.EventMessageAuditReject {
			log.Info().
				Str("messageId", data.MessageID).
				Str("channelId", data.ChannelID).
				Str("guildId", data.GuildID).
				Str("createTime", data.CreateTime).
				Msgf("Message audit rejected!")
		}

		return nil
	}
	q.handlers = append(q.handlers, handler)
}

func (q *QQBotService) getBotId(ctx context.Context) (string, error) {
	bot, err := q.api.Me(ctx)
	if err != nil {
		return "", errors.WithStack(err)
	}
	return bot.ID, nil
}

func (q *QQBotService) GetAllGuilds(ctx context.Context) ([]*dto.Guild, error) {
	err := q.limiter.Wait(ctx)
	if err != nil {
		return nil, errors.WithStack(err)
	}
	guilds, err := q.api.MeGuilds(ctx, &dto.GuildPager{})
	if err != nil {
		return nil, errors.WithStack(err)
	}
	return guilds, nil
}

func (q *QQBotService) GetGuild(ctx context.Context, guildId string) (*dto.Guild, error) {
	guilds, err := q.GetAllGuilds(ctx)
	if err != nil {
		return nil, err
	}
	guild, ok := lo.Find(guilds, func(item *dto.Guild) bool {
		return item.ID == guildId
	})
	if !ok {
		return nil, errors.Wrapf(errx.ErrQQBotApi, `Failed to get the guild by the id "%s"`, guildId)
	}
	return guild, nil
}

func (q *QQBotService) GetGuildByName(ctx context.Context, name string) (*dto.Guild, error) {
	guilds, err := q.GetAllGuilds(ctx)
	if err != nil {
		return nil, err
	}
	guild, ok := lo.Find(guilds, func(item *dto.Guild) bool {
		return item.Name == name
	})
	if !ok {
		return nil, errors.Wrapf(errx.ErrQQBotApi, `Failed to get the guild by the name "%s"`, name)
	}
	return guild, nil
}

func (q *QQBotService) GetAllChannels(ctx context.Context, guildId string) ([]*dto.Channel, error) {
	err := q.limiter.Wait(ctx)
	if err != nil {
		return nil, errors.WithStack(err)
	}
	channels, err := q.api.Channels(ctx, guildId)
	if err != nil {
		return nil, errors.WithStack(err)
	}
	return channels, nil
}

func (q *QQBotService) GetChannel(ctx context.Context, channelId string) (*dto.Channel, error) {
	err := q.limiter.Wait(ctx)
	if err != nil {
		return nil, errors.WithStack(err)
	}
	channel, err := q.api.Channel(ctx, channelId)
	if err != nil {
		return nil, errors.WithStack(err)
	}
	return channel, nil
}

func (q *QQBotService) GetChannelByName(ctx context.Context, name string, guildId string) (*dto.Channel, error) {
	channels, err := q.GetAllChannels(ctx, guildId)
	if err != nil {
		return nil, err
	}
	channel, ok := lo.Find(channels, func(item *dto.Channel) bool {
		return item.Name == name
	})
	if !ok {
		return nil, errors.Wrapf(errx.ErrQQBotApi, `Failed to get the channel by the name "%s"`, name)
	}
	return channel, nil
}

func (q *QQBotService) postMessage(
	ctx context.Context,
	channelId string,
	content *dto.MessageToCreate,
) (*dto.Message, error) {
	if !conf.R.QQBot.IsSandbox && channelId == q.sandboxGuildId {
		return nil, nil
	}

	err := q.limiter.Wait(ctx)
	if err != nil {
		return nil, errors.WithStack(err)
	}

	contentJson, err := json.Marshal(content)
	if err != nil {
		return nil, errors.WithStack(err)
	}
	log.Info().
		Str("channelId", channelId).
		RawJSON("content", contentJson).
		Msgf("Posting public message to the QQ channel %s...", channelId)

	resp, err := q.api.PostMessage(ctx, channelId, content)
	if e, ok := err.(*qqBotErrs.Err); ok && e.Code() == 202 {
		log.Warn().Msgf(e.Error())
	} else if err != nil {
		return nil, errors.WithStack(err)
	}

	log.Info().
		Str("channelId", channelId).
		RawJSON("content", contentJson).
		Msgf("Successfully posted public message to the QQ channel %s.", channelId)
	return resp, nil
}

func (q *QQBotService) PushCommonMessage(
	ctx context.Context,
	channelId string,
	text string,
	imgUrls []string,
) (*dto.Message, error) {
	if imgUrls == nil {
		return q.postMessage(ctx, channelId, &dto.MessageToCreate{
			Content: text,
		})
	}

	switch len(imgUrls) {
	case 0:
		return q.postMessage(ctx, channelId, &dto.MessageToCreate{
			Content: text,
		})
	case 1:
		return q.postMessage(ctx, channelId, &dto.MessageToCreate{
			Content: text,
			Image:   imgUrls[0],
		})
	default:
		msg, err := q.postMessage(ctx, channelId, &dto.MessageToCreate{
			Content: text,
			Image:   imgUrls[0],
		})
		if err != nil {
			return nil, err
		}
		var errs error
		for _, img := range imgUrls[1:] {
			_, err = q.postMessage(ctx, channelId, &dto.MessageToCreate{
				Image: img,
			})
			if err != nil {
				errs = buildInErrors.Join(errs, err)
			}
		}
		if errs != nil {
			return nil, errs
		}

		return msg, nil
	}
}

// PostThead 发布帖子（仅支持json格式）
func (q *QQBotService) PostThread(ctx context.Context, channelId, title, json string) error {
	err := q.limiter.Wait(ctx)
	if err != nil {
		return errors.WithStack(err)
	}

	log.Info().
		Str("channelId", channelId).
		Str("title", title).
		Str("content", json).
		Msg("Posing QQ channel thead...")
	_, err = q.api.Transport(ctx, "POST", fmt.Sprintf("/channels/%s/threads", channelId), map[string]any{
		"title":   title,
		"content": json,
		"format":  4,
	})
	if err != nil {
		return errors.WithStack(err)
	}

	log.Info().
		Str("channelId", channelId).
		Str("title", title).
		Str("content", json).
		Msgf("Successfully posted QQ channel thread to the channel %s.", channelId)
	return nil
}

func TextToRichText(text string) *forumDto.RichText {
	var (
		paras       []*forumDto.Paragraph
		newTextList []string
	)

	text = strings.ReplaceAll(text, "\r", "")
	textList := strings.Split(text, "\n")

	for _, line := range textList {
		preceding, rest, url := extractUrl(line)
		newTextList = append(newTextList, preceding)
		if url != "" {
			newTextList = append(newTextList, "@isURL#"+url)
		}
		if rest != "" {
			newTextList = append(newTextList, rest)
		}
	}

	for _, line := range newTextList {
		if existUrl(line) {
			if strings.HasPrefix(line, "@isURL#") {
				url := strings.ReplaceAll(line, "@isURL#", "")
				paras[len(paras)-1].Elems = append(paras[len(paras)-1].Elems, &forumDto.Elem{
					Url:  &forumDto.UrlElem{Url: url, Desc: url},
					Type: forumDto.Url,
				})
			} else {
				paras = append(paras, &forumDto.Paragraph{
					Elems: []*forumDto.Elem{
						{
							Url:  &forumDto.UrlElem{Url: line, Desc: line},
							Type: forumDto.Url,
						},
					},
					Props: &forumDto.ParagraphProps{Alignment: forumDto.Left},
				})
			}
		} else {
			paras = append(paras, &forumDto.Paragraph{
				Elems: []*forumDto.Elem{
					{
						Text: &forumDto.TextElem{Text: line},
						Type: forumDto.Text,
					},
				},
				Props: &forumDto.ParagraphProps{Alignment: forumDto.Left},
			})
		}
	}

	// 空白行
	for i := 0; i < len(paras); i++ {
		if paras[i].Elems != nil {
			for _, elem := range paras[i].Elems {
				if elem.Text != nil && elem.Text.Text == "" {
					paras[i] = &forumDto.Paragraph{Props: &forumDto.ParagraphProps{Alignment: forumDto.Left}}
				}
			}
		}
	}

	return &forumDto.RichText{Paragraphs: paras}
}

func ImagesToParagraphs(urls []string, uploader *UploaderService) ([]*forumDto.Paragraph, error) {
	imgElems := make([]*forumDto.Elem, len(urls))

	for _, url := range urls {
		uploadedUrl, err := uploader.UploadImage(url)
		if err != nil {
			return nil, errors.WithStack(err)
		}

		imgElems = append(imgElems, &forumDto.Elem{
			Image: &forumDto.ImageElem{ThirdUrl: uploadedUrl},
			Type:  forumDto.Image,
		})
	}

	return []*forumDto.Paragraph{
		{
			Elems: imgElems,
			Props: &forumDto.ParagraphProps{Alignment: forumDto.Middle},
		},
	}, nil
}

func VideoToParagraphs(url string, pubTime *time.Time, uploader *UploaderService) (
	[]*forumDto.Paragraph,
	error,
) {
	url, err := uploader.UploadVideo(url, pubTime)
	if err != nil {
		return nil, errors.WithStack(err)
	}

	return []*forumDto.Paragraph{
		{
			Elems: []*forumDto.Elem{
				{
					Text: &forumDto.TextElem{Text: "视频：" + url},
					Type: forumDto.Text,
				}, {
					Video: &forumDto.VideoElem{ThirdUrl: url},
					Type:  forumDto.Video,
				},
			},
		},
	}, nil
}

func extractUrl(text string) (preceding, rest, url string) {
	loc := util.RegexUrl.FindStringIndex(text)
	if loc == nil {
		return text, "", ""
	}

	return text[0:loc[0]], text[loc[1] : len(text)-1], text[loc[0]:loc[1]]
}

func existUrl(text string) bool {
	return util.RegexUrl.MatchString(text)
}
