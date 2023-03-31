package subscribe

import (
	"context"

	"github.com/Decmoe47/rabbitool/dao"
	"github.com/Decmoe47/rabbitool/dto"
	entity "github.com/Decmoe47/rabbitool/entity/subscribe"
	"github.com/Decmoe47/rabbitool/event"
	"github.com/Decmoe47/rabbitool/service"
	"github.com/Decmoe47/rabbitool/util"
	"github.com/cockroachdb/errors"
	"github.com/samber/lo"
	"gorm.io/gorm"
)

type mailSubscribeCommandHandler struct {
	*baseSubscribeCommandHandler[
		*entity.MailSubscribe,
		*entity.MailSubscribeConfig,
		*dao.MailSubscribeDao,
		*dao.MailSubscribeConfigDao,
	]
}

func newMailSubscribeCommandHandler(
	qbSvc *service.QQBotService,
	qcsDao *dao.QQChannelSubscribeDao,
	subscribeDao *dao.MailSubscribeDao,
	configDao *dao.MailSubscribeConfigDao,
) *mailSubscribeCommandHandler {
	m := &mailSubscribeCommandHandler{
		baseSubscribeCommandHandler: &baseSubscribeCommandHandler[
			*entity.MailSubscribe,
			*entity.MailSubscribeConfig,
			*dao.MailSubscribeDao,
			*dao.MailSubscribeConfigDao,
		]{
			qbSvc:        qbSvc,
			qcsDao:       qcsDao,
			subscribeDao: subscribeDao,
			configDao:    configDao,
		},
	}
	m.iSubscribeCommandHandler = m
	return m
}

func (m *mailSubscribeCommandHandler) checkId(_ context.Context, address string) (name, errMsg string) {
	matched := util.RegexMailAddress.MatchString(address)
	if !matched {
		return "", "错误：不合法的邮箱地址！"
	}
	return address, ""
}

func (m *mailSubscribeCommandHandler) add(ctx context.Context, cmd *dto.SubscribeCommand) (string, error) {
	if cmd.SubscribeId == "" {
		return "请输入 " + cmd.Platform + " 对应的id！", nil
	}
	address, errMsg := m.checkId(ctx, cmd.SubscribeId)
	if errMsg != "" {
		return errMsg, nil
	}

	if cmd.Configs == nil {
		return "错误：未正确指定邮箱地址！", nil
	}
	username, ok := util.TryGetMapValue[string](cmd.Configs, "username")
	if !ok || username == "" {
		return "错误：未正确指定邮箱地址！", nil
	}
	password, ok := util.TryGetMapValue[string](cmd.Configs, "password")
	if !ok || password == "" {
		return "错误：未正确指定邮箱密码！", nil
	}
	host, ok := util.TryGetMapValue[string](cmd.Configs, "host")
	if !ok || host == "" {
		return "错误：未正确指定host！", nil
	}
	port, ok := util.TryGetMapValue[int](cmd.Configs, "port")
	if !ok || port == 0 {
		return "错误：未正确指定port！", nil
	}
	mailbox, _ := util.TryGetMapValue[string](cmd.Configs, "mailbox")
	ssl, _ := util.TryGetMapValue[bool](cmd.Configs, "ssl")

	var subscribeAreCreated bool

	record, err := m.subscribeDao.Get(ctx, address)
	if errors.Is(err, gorm.ErrRecordNotFound) {
		record = entity.NewMailSubscribe(address, username, password, host, port, ssl, mailbox)
		err := m.subscribeDao.Update(ctx, record)
		if err != nil {
			return "", err
		}

		subscribeAreCreated = true
	} else if err != nil {
		return "", err
	}

	channel, err := m.qcsDao.AddSubscribe(ctx, cmd.QQChannel.GuildId, cmd.QQChannel.GuildName, cmd.QQChannel.Id,
		cmd.QQChannel.Name, record)
	if err != nil {
		return "", err
	}

	_, err = m.configDao.CreateOrUpdate(ctx, channel, record, cmd.Configs)
	if err != nil {
		return "", err
	}

	if subscribeAreCreated {
		err = event.OnMailSubscribeAdded(&service.NewMailServiceOptions{
			Address:  address,
			UserName: username,
			Password: password,
			Host:     host,
			Port:     port,
			Ssl:      ssl,
			Mailbox:  lo.Ternary(mailbox != "", mailbox, "INBOX"),
		})
		if err != nil {
			return "", err
		}
		return "成功：已添加订阅到 " + cmd.QQChannel.Name + " 子频道！", nil
	}
	return "成功：已更新在 " + cmd.QQChannel.Name + " 子频道中的此订阅的配置！", nil
}

func (m *mailSubscribeCommandHandler) delete(ctx context.Context, cmd *dto.SubscribeCommand) (string, error) {
	result, err := m.baseSubscribeCommandHandler.delete(ctx, cmd)
	if err != nil {
		return "", err
	}

	err = event.OnMailSubscribeDeleted(cmd.SubscribeId)
	if err != nil {
		return "", err
	}
	return result, nil
}
