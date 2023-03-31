package dao

import (
	"context"

	entity "github.com/Decmoe47/rabbitool/entity/subscribe"
)

type ISubscribeDao[T entity.ISubscribe] interface {
	Get(ctx context.Context, id string) (T, error)
	GetAll(ctx context.Context) ([]T, error)
	Add(ctx context.Context, entity T) error
	Delete(ctx context.Context, id string) (T, error)
	Update(ctx context.Context, record T) error
}

type ISubscribeConfigDao[T1 entity.ISubscribe, T2 entity.ISubscribeConfig] interface {
	GetAll(ctx context.Context, id string) ([]T2, error)
	Get(ctx context.Context, qqChannelId string, id string) (T2, error)
	Delete(ctx context.Context, qqChannelId string, id string) (T2, error)
	CreateOrUpdate(
		ctx context.Context,
		qqChannel *entity.QQChannelSubscribe,
		subscribe T1,
		configs map[string]any,
	) (T2, error)
}
