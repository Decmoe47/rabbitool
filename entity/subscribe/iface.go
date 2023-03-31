package subscribe

type ISubscribe interface {
	PropName() SubscribePropNameEnum
	// 此id是指用户的唯一id，不是指数据库主键
	GetId() string
	GetInfo(separator string) string

	ContainsQQChannel(channelId string) bool
	AddQQChannel(qqChannel *QQChannelSubscribe)
	RemoveQQChannel(channelId string)
}

type ISubscribeConfig interface {
	GetConfigs(separator string) string
}
