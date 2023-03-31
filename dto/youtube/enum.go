package youtube

type YoutubeTypeEnum int

const (
	EnumVideo YoutubeTypeEnum = iota
	EnumLive
	EnumUpcomingLive
)
