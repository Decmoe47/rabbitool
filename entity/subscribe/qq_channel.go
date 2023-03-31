package subscribe

import (
	"github.com/Decmoe47/rabbitool/errx"
	"github.com/samber/lo"

	"github.com/cockroachdb/errors"
	"gorm.io/gorm"
)

type QQChannelSubscribe struct {
	gorm.Model

	GuildId     string
	GuildName   string
	ChannelId   string
	ChannelName string

	BilibiliSubscribes []*BilibiliSubscribe `gorm:"many2many:qqChannel_with_bilibili_subscribe"`
	TwitterSubscribes  []*TwitterSubscribe  `gorm:"many2many:qqChannel_with_twitter_subscribe"`
	YoutubeSubscribes  []*YoutubeSubscribe  `gorm:"many2many:qqChannel_with_youtube_subscribe"`
	MailSubscribes     []*MailSubscribe     `gorm:"many2many:qqChannel_with_mail_subscribe"`
}

type SubscribePropNameEnum string

const (
	EnumBilibiliSubscribes SubscribePropNameEnum = "BilibiliSubscribes"
	EnumTwitterSubscribes  SubscribePropNameEnum = "TwitterSubscribes"
	EnumYoutubeSubscribes  SubscribePropNameEnum = "YoutubeSubscribes"
	EnumMailSubscribe      SubscribePropNameEnum = "MailSubscribes"
)

func NewQQChannelSubscribe(guildId, guildName, channelId, channelName string) *QQChannelSubscribe {
	return &QQChannelSubscribe{
		GuildId:     guildId,
		GuildName:   guildName,
		ChannelId:   channelId,
		ChannelName: channelName,
	}
}

func GetSubscribes[T ISubscribe](channel *QQChannelSubscribe) []T {
	var example T
	switch any(example).(type) { // TODO: switch
	case *BilibiliSubscribe:
		return any(channel.BilibiliSubscribes).([]T)
	case *TwitterSubscribe:
		return any(channel.TwitterSubscribes).([]T)
	case *YoutubeSubscribe:
		return any(channel.YoutubeSubscribes).([]T)
	case *MailSubscribe:
		return any(channel.MailSubscribes).([]T)
	default:
		panic(errors.Wrapf(errx.ErrNotSupported, "Type of %T is not supported!", example))
	}
}

func (q *QQChannelSubscribe) ContainsSubscribe(subscribe ISubscribe) bool {
	switch subscribe.(type) { // TODO: switch
	case *BilibiliSubscribe:
		return lo.ContainsBy(q.BilibiliSubscribes, func(item *BilibiliSubscribe) bool {
			return item.GetId() == subscribe.GetId()
		})
	case *TwitterSubscribe:
		return lo.ContainsBy(q.TwitterSubscribes, func(item *TwitterSubscribe) bool {
			return item.GetId() == subscribe.GetId()
		})
	case *YoutubeSubscribe:
		return lo.ContainsBy(q.YoutubeSubscribes, func(item *YoutubeSubscribe) bool {
			return item.GetId() == subscribe.GetId()
		})
	case *MailSubscribe:
		return lo.ContainsBy(q.MailSubscribes, func(item *MailSubscribe) bool {
			return item.GetId() == subscribe.GetId()
		})
	default:
		return false
	}
}

func (q *QQChannelSubscribe) AddSubscribe(subscribe ISubscribe) (added bool) {
	switch s := subscribe.(type) { // TODO: switch
	case *BilibiliSubscribe:
		q.BilibiliSubscribes = append(q.BilibiliSubscribes, s)
	case *TwitterSubscribe:
		q.TwitterSubscribes = append(q.TwitterSubscribes, s)
	case *YoutubeSubscribe:
		q.YoutubeSubscribes = append(q.YoutubeSubscribes, s)
	case *MailSubscribe:
		q.MailSubscribes = append(q.MailSubscribes, s)
	default:
		return false
	}

	return true
}

func (q *QQChannelSubscribe) RemoveSubscribeById(subscribeId string, example ISubscribe) (removed bool) {
	switch example.(type) { // TODO: switch
	case *BilibiliSubscribe:
		q.BilibiliSubscribes = lo.Reject(q.BilibiliSubscribes, func(item *BilibiliSubscribe, _ int) bool {
			return item.GetId() == subscribeId
		})
	case *TwitterSubscribe:
		q.TwitterSubscribes = lo.Reject(q.TwitterSubscribes, func(item *TwitterSubscribe, _ int) bool {
			return item.GetId() == subscribeId
		})
	case *YoutubeSubscribe:
		q.YoutubeSubscribes = lo.Reject(q.YoutubeSubscribes, func(item *YoutubeSubscribe, _ int) bool {
			return item.GetId() == subscribeId
		})
	case *MailSubscribe:
		q.MailSubscribes = lo.Reject(q.MailSubscribes, func(item *MailSubscribe, _ int) bool {
			return item.GetId() == subscribeId
		})
	default:
		return false
	}

	return true
}

func (q *QQChannelSubscribe) SubscribeAreAllEmpty() bool {
	return (q.BilibiliSubscribes == nil || len(q.BilibiliSubscribes) == 0) &&
		(q.TwitterSubscribes == nil || len(q.TwitterSubscribes) == 0) &&
		(q.YoutubeSubscribes == nil || len(q.YoutubeSubscribes) == 0) &&
		(q.MailSubscribes == nil || len(q.MailSubscribes) == 0)
}
