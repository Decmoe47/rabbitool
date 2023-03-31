package twitter

type TweetTypeEnum int

const (
	EnumCommon TweetTypeEnum = iota
	EnumRT
	EnumQuote
)
