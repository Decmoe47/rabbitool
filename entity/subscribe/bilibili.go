package subscribe

import (
	"fmt"
	"strconv"
	"time"

	dto "github.com/Decmoe47/rabbitool/dto/bilibili"
	"github.com/Decmoe47/rabbitool/util"
	"gorm.io/gorm"

	"github.com/samber/lo"
)

type BilibiliSubscribe struct {
	gorm.Model

	Uid             uint
	Uname           string
	LastDynamicTime *time.Time
	LastDynamicType dto.DynamicTypeEnum
	LastLiveStatus  dto.LiveStatusEnum

	QQChannels []*QQChannelSubscribe `gorm:"many2many:qqChannel_with_bilibili_subscribe"`
}

func NewBilibiliSubscribe(uid uint, uname string) *BilibiliSubscribe {
	return &BilibiliSubscribe{
		Uid:             uid,
		Uname:           uname,
		LastDynamicTime: util.UnixDefaultTime(),
	}
}

func (b *BilibiliSubscribe) PropName() SubscribePropNameEnum { return EnumBilibiliSubscribes }
func (b *BilibiliSubscribe) GetId() string                   { return strconv.FormatUint(uint64(b.Uid), 10) }

func (b *BilibiliSubscribe) GetInfo(separator string) string {
	return fmt.Sprintf("uid=%d%suname=%s", b.Uid, separator, b.Uname)
}

func (b *BilibiliSubscribe) ContainsQQChannel(channelId string) bool {
	return lo.ContainsBy(b.QQChannels, func(item *QQChannelSubscribe) bool {
		return item.ChannelId == channelId
	})
}

func (b *BilibiliSubscribe) AddQQChannel(channel *QQChannelSubscribe) {
	b.QQChannels = append(b.QQChannels, channel)
}

func (b *BilibiliSubscribe) RemoveQQChannel(channelId string) {
	b.QQChannels = lo.Reject(b.QQChannels, func(item *QQChannelSubscribe, _ int) bool {
		return item.ChannelId == channelId
	})
}

type BilibiliSubscribeConfig struct {
	gorm.Model

	QQChannelID string
	QQChannel   *QQChannelSubscribe
	SubscribeID string
	Subscribe   *BilibiliSubscribe

	LivePush               bool
	DynamicPush            bool
	PureForwardDynamicPush bool
	LiveEndingPush         bool
}

func NewBilibiliSubscribeConfig(qqChannel *QQChannelSubscribe, subscribe *BilibiliSubscribe) *BilibiliSubscribeConfig {
	return &BilibiliSubscribeConfig{
		Subscribe:              subscribe,
		QQChannel:              qqChannel,
		QQChannelID:            qqChannel.ChannelId,
		LivePush:               true,
		DynamicPush:            true,
		PureForwardDynamicPush: false,
		LiveEndingPush:         false,
	}
}

func (b *BilibiliSubscribeConfig) GetConfigs(separator string) string {
	return fmt.Sprintf("livePush=%t%sdynamicPush=%t%spureForwardDynamicPush=%t%sLiveEndingPush=%t",
		b.LivePush,
		separator,
		b.DynamicPush,
		separator,
		b.PureForwardDynamicPush,
		separator,
		b.LiveEndingPush,
	)
}
