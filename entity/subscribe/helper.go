package subscribe

import (
	"strconv"

	"github.com/Decmoe47/rabbitool/errx"
	"github.com/cockroachdb/errors"
)

// NewSubscribe 根据T自动构造对应struct（但除 MailSubscribe 外）
func NewSubscribe[T ISubscribe](id, name string) T {
	var example T

	switch any(example).(type) {	// TODO: switch
	case *BilibiliSubscribe:
		// 由上层判断id是否为数字，因此这里断定不会有错
		uidUint64, err := strconv.ParseUint(id, 10, 32)
		if err != nil {
			panic(err)
		}
		return any(NewBilibiliSubscribe(uint(uidUint64), name)).(T)
	case *TwitterSubscribe:
		return any(NewTwitterSubscribe(id, name)).(T)
	case *YoutubeSubscribe:
		return any(NewYoutubeSubscribe(id, name)).(T)
	default:
		panic(errors.Wrapf(errx.ErrNotSupported, "The type of the example %T is not supported!", example))
	}
}
