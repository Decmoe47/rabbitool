package youtube

import "time"

type IItem interface {
	GetType() YoutubeTypeEnum
	GetChannelId() string
}

type ItemBase struct {
	Type      YoutubeTypeEnum
	ChannelId string
	Author    string

	Id           string
	Title        string
	ThumbnailUrl string
	Url          string
}

func (i *ItemBase) GetType() YoutubeTypeEnum { return i.Type }
func (i *ItemBase) GetChannelId() string     { return i.ChannelId }

type Video struct {
	*ItemBase
	PubTime *time.Time
}

type Live struct {
	*ItemBase
	ScheduledStartTime *time.Time // nullable
	ActualStartTime    *time.Time // nullable
}
