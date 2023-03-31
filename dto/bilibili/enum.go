package bilibili

type DynamicTypeEnum int

const (
	EnumTextOnly    DynamicTypeEnum = 4
	EnumWithImage   DynamicTypeEnum = 2
	EnumVideo       DynamicTypeEnum = 8
	EnumArticle     DynamicTypeEnum = 64
	EnumForward     DynamicTypeEnum = 1
	EnumPureForward DynamicTypeEnum = 3 // 自己定义的，不是api里有的
	EnumWebActivity DynamicTypeEnum = 2042
	EnumLiveCard    DynamicTypeEnum = 4200 // 目前来看，只会出现在origin里
)

type LiveStatusEnum int

const (
	EnumNoLiveStream LiveStatusEnum = iota
	EnumStreaming
	EnumReplay
)
