package subscribe

import (
	"fmt"
	"time"

	"github.com/Decmoe47/rabbitool/util"
	"github.com/samber/lo"
	"gorm.io/gorm"
)

type MailSubscribe struct {
	gorm.Model

	UserName string
	Address  string
	Password string
	Mailbox  string
	Host     string
	Port     int
	Ssl      bool

	LastMailTime *time.Time

	QQChannels []*QQChannelSubscribe `gorm:"many2many:qqChannel_with_mail_subscribe"`
}

func NewMailSubscribe(
	address string,
	userName string,
	password string,
	host string,
	port int,
	ssl bool,
	mailbox string,
) *MailSubscribe {
	return &MailSubscribe{
		UserName:     userName,
		Address:      address,
		Password:     password,
		Mailbox:      mailbox,
		Host:         host,
		Port:         port,
		Ssl:          ssl,
		LastMailTime: util.UnixDefaultTime(),
	}
}

func (m *MailSubscribe) PropName() SubscribePropNameEnum { return EnumMailSubscribe }
func (m *MailSubscribe) GetId() string                   { return m.UserName }

func (m *MailSubscribe) GetInfo(separator string) string {
	return fmt.Sprintf("userName=%s%spassword=%s%saddress=%s%smailbox=%s%shost=%s%sport=%d%sssl=%t%s",
		m.UserName,
		separator,
		m.Password,
		separator,
		m.Address,
		separator,
		m.Mailbox,
		separator,
		m.Host,
		separator,
		m.Port,
		separator,
		m.Ssl,
		separator,
	)
}

func (m *MailSubscribe) ContainsQQChannel(channelId string) bool {
	return lo.ContainsBy(m.QQChannels, func(item *QQChannelSubscribe) bool {
		return item.ChannelId == channelId
	})
}

func (m *MailSubscribe) RemoveQQChannel(channelId string) {
	m.QQChannels = lo.Reject(m.QQChannels, func(item *QQChannelSubscribe, _ int) bool {
		return item.ChannelId == channelId
	})
}

func (m *MailSubscribe) AddQQChannel(channel *QQChannelSubscribe) {
	m.QQChannels = append(m.QQChannels, channel)
}

type MailSubscribeConfig struct {
	gorm.Model

	QQChannelID string
	QQChannel   *QQChannelSubscribe
	SubscribeID string
	Subscribe   *MailSubscribe

	ContainsHeader bool
	PushToThread   bool
}

func NewMailSubscribeConfig(qqChannel *QQChannelSubscribe, subscribe *MailSubscribe) *MailSubscribeConfig {
	return &MailSubscribeConfig{
		QQChannel:      qqChannel,
		QQChannelID:    qqChannel.ChannelId,
		Subscribe:      subscribe,
		ContainsHeader: false,
		PushToThread:   false,
	}
}

func (m *MailSubscribeConfig) GetConfigs(separator string) string {
	return fmt.Sprintf("containsHeader=%t%spushToThread=%t",
		m.ContainsHeader,
		separator,
		m.PushToThread,
	)
}
