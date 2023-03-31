package subscribe

import (
	"fmt"
	"time"

	"github.com/Decmoe47/rabbitool/util"
	"github.com/samber/lo"
	"gorm.io/gorm"
)

type TwitterSubscribe struct {
	gorm.Model

	Name          string
	ScreenName    string
	LastTweetId   string
	LastTweetTime *time.Time

	QQChannels []*QQChannelSubscribe `gorm:"many2many:qqChannel_with_twitter_subscribe"`
}

func NewTwitterSubscribe(screenName, name string) *TwitterSubscribe {
	return &TwitterSubscribe{
		Name:          name,
		ScreenName:    screenName,
		LastTweetTime: util.UnixDefaultTime(),
	}
}

func (t *TwitterSubscribe) PropName() SubscribePropNameEnum { return EnumTwitterSubscribes }
func (t *TwitterSubscribe) GetId() string                   { return t.ScreenName }

func (t *TwitterSubscribe) GetInfo(separator string) string {
	return fmt.Sprintf("screenName=%s%sname=%s", t.ScreenName, separator, t.Name)
}

func (t *TwitterSubscribe) ContainsQQChannel(channelId string) bool {
	return lo.ContainsBy(t.QQChannels, func(item *QQChannelSubscribe) bool {
		return item.ChannelId == channelId
	})
}

func (t *TwitterSubscribe) RemoveQQChannel(channelId string) {
	t.QQChannels = lo.Reject(t.QQChannels, func(item *QQChannelSubscribe, _ int) bool {
		return item.ChannelId == channelId
	})
}

func (t *TwitterSubscribe) AddQQChannel(channel *QQChannelSubscribe) {
	t.QQChannels = append(t.QQChannels, channel)
}

type TwitterSubscribeConfig struct {
	gorm.Model

	QQChannelID string
	QQChannel   *QQChannelSubscribe
	SubscribeID string
	Subscribe   *TwitterSubscribe

	QuotePush    bool
	PushToThread bool
}

func NewTwitterSubscribeConfig(qqChannel *QQChannelSubscribe, subscribe *TwitterSubscribe) *TwitterSubscribeConfig {
	return &TwitterSubscribeConfig{
		QQChannel:    qqChannel,
		QQChannelID:  qqChannel.ChannelId,
		Subscribe:    subscribe,
		QuotePush:    true,
		PushToThread: false,
	}
}

func (t *TwitterSubscribeConfig) GetConfigs(separator string) string {
	return fmt.Sprintf("quotePush=%t%spushToThread=%t",
		t.QuotePush,
		separator,
		t.PushToThread,
	)
}
