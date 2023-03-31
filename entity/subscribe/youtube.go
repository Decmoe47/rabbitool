package subscribe

import (
	"fmt"
	"time"

	"github.com/Decmoe47/rabbitool/util"
	"github.com/samber/lo"
	"gorm.io/gorm"
)

type YoutubeSubscribe struct {
	gorm.Model

	Name             string
	ChannelId        string
	LastVideoId      string
	LastVideoPubTime *time.Time

	LastLiveRoomId         string
	LastLiveStartTime      *time.Time
	AllUpcomingLiveRoomIds []string `gorm:"serializer:StrSliceToJson;type:text"`
	AllArchiveVideoIds     []string `gorm:"serializer:StrSliceToJson;type:text"`

	QQChannels []*QQChannelSubscribe `gorm:"many2many:qqChannel_with_youtube_subscribe"`
}

func NewYoutubeSubscribe(name string, channelId string) *YoutubeSubscribe {
	return &YoutubeSubscribe{
		Name:              name,
		ChannelId:         channelId,
		LastVideoPubTime:  util.UnixDefaultTime(),
		LastLiveStartTime: util.UnixDefaultTime(),
	}
}

func (y *YoutubeSubscribe) PropName() SubscribePropNameEnum { return EnumYoutubeSubscribes }
func (y *YoutubeSubscribe) GetId() string                   { return y.ChannelId }

func (y *YoutubeSubscribe) GetInfo(separator string) string {
	return fmt.Sprintf("channelId=%s%sname=%s", y.ChannelId, separator, y.Name)
}

func (y *YoutubeSubscribe) ContainsQQChannel(qqChannelId string) bool {
	return lo.ContainsBy(y.QQChannels, func(item *QQChannelSubscribe) bool {
		return item.ChannelId == qqChannelId
	})
}

func (y *YoutubeSubscribe) RemoveQQChannel(qqChannelId string) {
	y.QQChannels = lo.Reject(y.QQChannels, func(item *QQChannelSubscribe, _ int) bool {
		return item.ChannelId == qqChannelId
	})
}

func (y *YoutubeSubscribe) AddQQChannel(qqChannel *QQChannelSubscribe) {
	y.QQChannels = append(y.QQChannels, qqChannel)
}

type YoutubeSubscribeConfig struct {
	gorm.Model

	QQChannelID string
	QQChannel   *QQChannelSubscribe
	SubscribeID string
	Subscribe   *YoutubeSubscribe

	VideoPush        bool
	LivePush         bool
	UpcomingLivePush bool
	ArchivePush      bool
}

func NewYoutubeSubscribeConfig(qqChannel *QQChannelSubscribe, subscribe *YoutubeSubscribe) *YoutubeSubscribeConfig {
	return &YoutubeSubscribeConfig{
		QQChannel:        qqChannel,
		QQChannelID:      qqChannel.ChannelId,
		Subscribe:        subscribe,
		VideoPush:        true,
		LivePush:         true,
		UpcomingLivePush: true,
		ArchivePush:      false,
	}
}

func (y *YoutubeSubscribeConfig) GetConfigs(separator string) string {
	return fmt.Sprintf(`videoPush=%t%slivePush=%t%supcomingLivePush=%t%sarchivePush=%t`,
		y.VideoPush,
		separator,
		y.LivePush,
		separator,
		y.UpcomingLivePush,
		separator,
		y.ArchivePush,
	)
}
