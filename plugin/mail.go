package plugin

import (
	"context"
	"encoding/json"
	"fmt"
	"strings"
	"sync"
	"time"

	"github.com/Decmoe47/async"
	"github.com/Decmoe47/rabbitool/dao"
	dto "github.com/Decmoe47/rabbitool/dto/mail"
	entity "github.com/Decmoe47/rabbitool/entity/subscribe"
	"github.com/Decmoe47/rabbitool/errx"
	"github.com/Decmoe47/rabbitool/event"
	"github.com/Decmoe47/rabbitool/service"
	"github.com/Decmoe47/rabbitool/util"
	"github.com/go-co-op/gocron"
	"github.com/rs/zerolog/log"
	"github.com/samber/lo"
)

type MailPlugin struct {
	*PluginBase

	services     []*service.MailService
	subscribeDao *dao.MailSubscribeDao
	configDao    *dao.MailSubscribeConfigDao

	storedMails *sync.Map
}

func NewMailPlugin(base *PluginBase) *MailPlugin {
	return &MailPlugin{
		PluginBase:   base,
		subscribeDao: dao.NewMailSubscribeDao(),
		configDao:    dao.NewMailSubscribeConfigDao(),
		storedMails:  &sync.Map{},
	}
}

func (m *MailPlugin) init(ctx context.Context, sch *gocron.Scheduler) error {
	event.OnCancelKeyPressed = m.logOutAll
	event.OnMailSubscribeAdded = m.handleSubscribeAddedEvent
	event.OnMailSubscribeDeleted = m.handleSubscribeDeletedEvent

	_, err := sch.Every(5).Seconds().Do(func() {
		m.CheckAll(ctx)
	})
	return err
}

func (m *MailPlugin) CheckAll(ctx context.Context) {
	records, err := m.subscribeDao.GetAll(ctx)
	if err != nil {
		log.Error().Stack().Err(err).Msgf(err.Error())
	}
	if len(records) == 0 {
		log.Debug().Msgf("There isn't any mail subscribe yet!")
	}

	var fns []func() error
	for _, record := range records {
		svc, ok := lo.Find(m.services, func(item *service.MailService) bool {
			return item.Address == record.Address
		})
		if !ok {
			svc, err = service.NewMailService(&service.NewMailServiceOptions{
				Address:  record.Address,
				UserName: record.UserName,
				Password: record.Password,
				Host:     record.Host,
				Port:     record.Port,
				Ssl:      record.Ssl,
				Mailbox:  record.Mailbox,
			})
			if err != nil {
				log.Error().
					Stack().Err(err).
					Str("address", record.Address).
					Msgf("Failed to create new mailService!\nerr: %s", err.Error())
			}
		}

		record := record
		fns = append(fns, func() error {
			return m.check(ctx, svc, record)
		})
	}

	errs := async.ExecAllOne(ctx, fns).Await(ctx)
	for _, err := range errs {
		if err != nil {
			log.Error().Stack().Err(err).Msg(err.Error())
		}
	}
}

func (m *MailPlugin) check(ctx context.Context, svc *service.MailService, record *entity.MailSubscribe) (err error) {
	defer errx.Recover(&err)

	select {
	case <-ctx.Done():
		return nil
	default:
	}

	mail, err := svc.GetLatestMail()
	if err != nil {
		return err
	}

	if mail.Time.Compare(*record.LastMailTime) <= 0 {
		log.Debug().Msgf("No new mail from the mail user %s.", record.Address)
		return nil
	}

	// 宵禁时间发不出去，攒着
	now := time.Now().UTC().In(util.CST())
	if now.Hour() >= 0 && now.Hour() <= 5 {
		nested, _ := m.storedMails.LoadOrStore(record.Address, &sync.Map{})
		nested.(*sync.Map).LoadOrStore(mail.Time, mail)

		log.Debug().Msgf("Mail message of the user %s is skipped because it's curfew time now.",
			record.Address)
		return nil
	}

	// 过了宵禁把攒的先发了
	if nested, ok := m.storedMails.Load(record.Address); ok {
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
			err := m.pushMailAndUpdateRecord(ctx, tw.(*dto.Mail), record)
			if err != nil {
				errs = errx.Join(errs, err)
			}
			nestedMap.Delete(uploadTime)
		}

		return errs
	}

	return m.pushMailAndUpdateRecord(ctx, mail, record)
}

func (m *MailPlugin) pushMailAndUpdateRecord(
	ctx context.Context,
	mail *dto.Mail,
	record *entity.MailSubscribe,
) error {
	err := m.pushMailMsg(ctx, mail, record)
	if err != nil {
		return err
	}

	record.LastMailTime = mail.Time
	err = m.subscribeDao.Update(ctx, record)
	if err != nil {
		return err
	}

	log.Debug().Msgf("Succeeded to updated the mail user(%s)'s record.", record.Address)
	return nil
}

func (m *MailPlugin) pushMailMsg(ctx context.Context, mail *dto.Mail, record *entity.MailSubscribe) error {
	title, text, header := m.mailToStr(mail)

	configs, err := m.configDao.GetAll(ctx, record.Address)
	if err != nil {
		return err
	}

	var fns []func() error
	for _, channel := range record.QQChannels {
		channel := channel
		if _, err := m.qbSvc.GetChannel(ctx, channel.ChannelId); err != nil {
			log.Warn().
				Str("channelName", channel.ChannelName).
				Str("channelId", channel.ChannelId).
				Msgf("The channel %s doesn't exist!", channel.ChannelName)
			continue
		}

		config, ok := lo.Find(configs, func(item *entity.MailSubscribeConfig) bool {
			return item.QQChannel.ChannelId == channel.ChannelId
		})
		if !ok {
			log.Error().
				Str("address", record.Address).
				Msg("Failed to get configs for the mail subscribe!")
			continue
		}

		if config.ContainsHeader {
			text = header + "\n" + text
		}

		if config.PushToThread {
			richText := service.TextToRichText(text)
			richTextJson, err := json.Marshal(richText)
			if err != nil {
				return errx.WithStack(err, map[string]any{"address": record.Address})
			}

			fns = append(fns, func() (err error) {
				defer errx.Recover(&err)

				err = m.qbSvc.PostThread(ctx, channel.ChannelId, title, string(richTextJson))
				if err == nil {
					log.Info().Msgf("Succeeded to push the mail message to the channel %s", channel.ChannelName)
				}
				return err
			})
			continue
		}

		fns = append(fns, func() (err error) {
			defer errx.Recover(&err)

			_, err = m.qbSvc.PushCommonMessage(ctx, channel.ChannelId, title+"\n\n"+text, nil)
			if err == nil {
				log.Info().Msgf("Succeeded to push the mail message to the channel %s", channel.ChannelName)
			}
			return err
		})
	}

	return errx.Blend(async.ExecAllOne(ctx, fns).Await(ctx))
}

func (m *MailPlugin) mailToStr(mail *dto.Mail) (title string, text string, header string) {
	from := ""
	to := ""
	for _, v := range mail.From {
		from += v.Address + " "
	}
	for _, v := range mail.To {
		to += v.Address + " "
	}

	title = "【新邮件】"
	text = addRedirectToUrls(mail.Text)

	text = util.RegexMailAddress.ReplaceAllStringFunc(text, func(s string) string {
		return strings.ReplaceAll(s, ".", "*")
	})

	header = fmt.Sprintf(
		`From: %s
To: %s
Time: %s
Subject: %s
——————————
`,
		from,
		to,
		mail.Time.In(util.CST()).Format("2006-01-02 15:04:05 MST"),
		mail.Subject,
	)

	header = util.RegexMailAddress.ReplaceAllStringFunc(header, func(s string) string {
		return strings.ReplaceAll(s, ".", "*")
	})

	return
}

func (m *MailPlugin) handleSubscribeAddedEvent(opts *service.NewMailServiceOptions) error {
	if !lo.ContainsBy(m.services, func(item *service.MailService) bool {
		return item.Address == opts.Address
	}) {
		svc, err := service.NewMailService(opts)
		if err != nil {
			return err
		}
		m.services = append(m.services, svc)
	}
	return nil
}

func (m *MailPlugin) handleSubscribeDeletedEvent(address string) error {
	svc, ok := lo.Find(m.services, func(item *service.MailService) bool {
		return item.Address == address
	})
	if !ok {
		log.Warn().Str("address", address).Msgf("The mail service doesn't exist!")
		return nil
	}

	err := svc.Logout()
	if err != nil {
		return err
	}

	m.services = lo.Reject(m.services, func(item *service.MailService, _ int) bool {
		return item.Address == address
	})
	return nil
}

func (m *MailPlugin) logOutAll() (errs error) {
	for _, svc := range m.services {
		err := svc.Logout()
		errs = errx.Join(errs, err)
	}
	return
}
