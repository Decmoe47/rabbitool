package twitter

import "time"

type Tweet struct {
	Type TweetTypeEnum
	Id   string
	Url  string

	PubTime          *time.Time
	Author           string
	AuthorScreenName string

	Text      string
	ImageUrls []string // nullable
	HasVideo  bool

	Origin *Tweet // nullable
}
