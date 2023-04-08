package dao

import (
	"context"

	entity "github.com/Decmoe47/rabbitool/entity/subscribe"
	"github.com/Decmoe47/rabbitool/errx"
	"github.com/cockroachdb/errors"
	"gorm.io/gorm"
	"gorm.io/gorm/clause"
)

type QQChannelSubscribeDao struct {
}

func NewQQChannelSubscribeDao() *QQChannelSubscribeDao {
	return &QQChannelSubscribeDao{}
}

func (q *QQChannelSubscribeDao) GetAll(
	ctx context.Context,
	guildId string,
) ([]*entity.QQChannelSubscribe, error) {
	records := []*entity.QQChannelSubscribe{}
	if err := _db.WithContext(ctx).
		Preload(clause.Associations).
		Where(&entity.QQChannelSubscribe{GuildId: guildId}).
		Find(&records).Error; err != nil {
		return nil, errx.WithStack(err, map[string]any{"guildId": guildId})
	}
	return records, nil
}

func (q *QQChannelSubscribeDao) Get(
	ctx context.Context,
	guildId string,
	channelId string,
	example entity.ISubscribe,
) (*entity.QQChannelSubscribe, error) {
	record := &entity.QQChannelSubscribe{}
	if err := _db.WithContext(ctx).
		Preload(string(example.PropName())).
		Where(&entity.QQChannelSubscribe{GuildId: guildId, ChannelId: channelId}).
		First(record).Error; err != nil {
		return nil, errx.WithStack(err, map[string]any{"guildId": guildId, "channelId": channelId})
	}
	return record, nil
}

func (q *QQChannelSubscribeDao) Create(
	ctx context.Context,
	guildId, guildName, channelId, channelName string,
) (*entity.QQChannelSubscribe, error) {
	record := entity.NewQQChannelSubscribe(guildId, guildName, channelId, channelName)
	if err := _db.WithContext(ctx).Create(record).Error; err != nil {
		return nil, errx.WithStack(err, map[string]any{
			"guildId":     guildId,
			"guildName":   guildName,
			"channelId":   channelId,
			"channelName": channelName,
		})
	}
	return record, nil
}

func (q *QQChannelSubscribeDao) GetOrCreate(
	ctx context.Context,
	guildId, guildName, channelId, channelName string,
	example entity.ISubscribe,
) (*entity.QQChannelSubscribe, error) {
	record, err := q.Get(ctx, guildId, channelId, example)
	if errors.Is(err, gorm.ErrRecordNotFound) {
		record, err = q.Create(ctx, guildId, guildName, channelId, channelName)
		if err != nil {
			return nil, err
		}
	}
	return record, nil
}

// AddSubscribe 将subscribe追加到指定的QQChannelSubscribe中，
//
// 然后同时保存subscribe和QQChannelSubscribe。
//
// （如果QQChannelSubscribe不存在则自动创建）
func (q *QQChannelSubscribeDao) AddSubscribe(
	ctx context.Context,
	guildId string,
	guildName string,
	channelId string,
	channelName string,
	subscribe entity.ISubscribe,
) (*entity.QQChannelSubscribe, error) {
	record, err := q.GetOrCreate(ctx, guildId, guildName, channelId, channelName, subscribe)
	if err != nil {
		return nil, err
	}
	if !record.ContainsSubscribe(subscribe) {
		record.AddSubscribe(subscribe)
		subscribe.AddQQChannel(record)
	}

	if err = _db.WithContext(ctx).Save(record).Error; err != nil {
		return nil, errx.WithStack(err, map[string]any{
			"guildId":       guildId,
			"guildName":     guildName,
			"channelId":     channelId,
			"channelName":   channelName,
			"subscribeId":   subscribe.GetId(),
			"subscribeType": subscribe.PropName(),
		})
	}
	if err = _db.WithContext(ctx).Save(subscribe).Error; err != nil {
		return nil, errx.WithStack(err, map[string]any{
			"guildId":       guildId,
			"guildName":     guildName,
			"channelId":     channelId,
			"channelName":   channelName,
			"subscribeId":   subscribe.GetId(),
			"subscribeType": subscribe.PropName(),
		})
	}

	return record, nil
}

func (q *QQChannelSubscribeDao) RemoveSubscribe(
	ctx context.Context,
	guildId string,
	channelId string,
	subscribeId string,
	subscribe entity.ISubscribe,
) (*entity.QQChannelSubscribe, error) {
	record, err := q.Get(ctx, guildId, channelId, subscribe)
	if err != nil {
		return nil, err
	}

	record.RemoveSubscribeById(subscribeId, subscribe)
	subscribe.RemoveQQChannel(channelId)

	if err := _db.WithContext(ctx).Save(record).Error; err != nil {
		return nil, errx.WithStack(err, map[string]any{
			"guildId":       guildId,
			"channelId":     channelId,
			"subscribeId":   subscribe.GetId(),
			"subscribeType": subscribe.PropName(),
		})
	}
	if err = _db.WithContext(ctx).Save(subscribe).Error; err != nil {
		return nil, errx.WithStack(err, map[string]any{
			"guildId":       guildId,
			"channelId":     channelId,
			"subscribeId":   subscribe.GetId(),
			"subscribeType": subscribe.PropName(),
		})
	}
	return record, nil
}

func (q *QQChannelSubscribeDao) Delete(ctx context.Context, record *entity.QQChannelSubscribe) error {
	if err := _db.WithContext(ctx).Delete(record).Error; err != nil {
		return errx.WithStack(err, map[string]any{"channelId": record.ChannelId, "channelName": record.ChannelName})
	}
	return nil
}
