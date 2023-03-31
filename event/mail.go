package event

import (
	"github.com/Decmoe47/rabbitool/service"
)

var OnMailSubscribeAdded func(opts *service.NewMailServiceOptions) error
var OnMailSubscribeDeleted func(address string) error
