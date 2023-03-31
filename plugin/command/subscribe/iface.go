package subscribe

import (
	"context"

	"github.com/Decmoe47/rabbitool/dto"
)

type iSubscribeCommandHandler interface {
	checkId(ctx context.Context, id string) (name, errMsg string)
	add(ctx context.Context, cmd *dto.SubscribeCommand) (string, error)
	delete(ctx context.Context, cmd *dto.SubscribeCommand) (string, error)
	list(ctx context.Context, cmd *dto.SubscribeCommand) (string, error)
}
