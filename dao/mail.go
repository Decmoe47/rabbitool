package dao

import (
	"context"

	entity "github.com/Decmoe47/rabbitool/entity/subscribe"
	"github.com/Decmoe47/rabbitool/util"
	"github.com/cockroachdb/errors"
	"gorm.io/gorm"
)

type MailSubscribeDao struct {
}

func NewMailSubscribeDao() *MailSubscribeDao {
	return &MailSubscribeDao{}
}

func (m *MailSubscribeDao) GetAll(ctx context.Context) ([]*entity.MailSubscribe, error) {
	records := []*entity.MailSubscribe{}
	if err := _db.WithContext(ctx).Preload("QQChannels").Find(&records).Error; err != nil {
		return nil, errors.WithStack(err)
	}
	return records, nil
}

func (m *MailSubscribeDao) Get(ctx context.Context, address string) (*entity.MailSubscribe, error) {
	record := &entity.MailSubscribe{}
	if err := _db.WithContext(ctx).Preload("QQChannels").
		Where(&entity.MailSubscribe{Address: address}).
		First(record).Error; err != nil {
		return nil, errors.WithStack(err)
	}
	return record, nil
}

func (m *MailSubscribeDao) Add(ctx context.Context, subscribe *entity.MailSubscribe) error {
	if err := _db.WithContext(ctx).Create(subscribe).Error; err != nil {
		return errors.WithStack(err)
	}
	return nil
}

func (m *MailSubscribeDao) Delete(ctx context.Context, address string) (*entity.MailSubscribe, error) {
	record, err := m.Get(ctx, address)
	if err != nil {
		return nil, err
	}

	if err = _db.WithContext(ctx).Delete(record).Error; err != nil {
		return nil, err
	}
	return record, nil
}

func (m *MailSubscribeDao) Update(ctx context.Context, record *entity.MailSubscribe) error {
	if err := _db.WithContext(ctx).Save(record).Error; err != nil {
		return errors.WithStack(err)
	}
	return nil
}

type MailSubscribeConfigDao struct {
}

func NewMailSubscribeConfigDao() *MailSubscribeConfigDao {
	return &MailSubscribeConfigDao{}
}

func (m *MailSubscribeConfigDao) GetAll(
	ctx context.Context,
	address string,
) ([]*entity.MailSubscribeConfig, error) {
	records := []*entity.MailSubscribeConfig{}
	if err := _db.WithContext(ctx).
		Preload("QQChannel").
		Preload("Subscribe").
		Where(&entity.MailSubscribeConfig{Subscribe: &entity.MailSubscribe{Address: address}}).
		Find(&records).Error; err != nil {
		return nil, errors.WithStack(err)
	}
	return records, nil
}

func (m *MailSubscribeConfigDao) Get(
	ctx context.Context,
	channelId string,
	address string,
) (*entity.MailSubscribeConfig, error) {
	record := &entity.MailSubscribeConfig{}
	if err := _db.WithContext(ctx).
		Preload("QQChannel").
		Preload("Subscribe").
		Where(&entity.MailSubscribeConfig{
			Subscribe: &entity.MailSubscribe{Address: address},
			QQChannel: &entity.QQChannelSubscribe{ChannelId: channelId},
		}).
		First(record).Error; err != nil {
		return nil, errors.WithStack(err)
	}
	return record, nil
}

func (m *MailSubscribeConfigDao) CreateOrUpdate(
	ctx context.Context,
	channel *entity.QQChannelSubscribe,
	subscribe *entity.MailSubscribe,
	configs map[string]any,
) (*entity.MailSubscribeConfig, error) {
	record, err := m.Get(ctx, channel.ChannelId, subscribe.Address)
	if errors.Is(err, gorm.ErrRecordNotFound) {
		record = entity.NewMailSubscribeConfig(channel, subscribe)
		if configs != nil {
			util.UpdateFields(record, configs)
		}
	} else if err != nil {
		return nil, err
	}

	if configs != nil {
		util.UpdateFields(record, configs)
	}

	if err := _db.WithContext(ctx).Save(record).Error; err != nil {
		return nil, errors.WithStack(err)
	}
	return record, nil
}

func (m *MailSubscribeConfigDao) Delete(
	ctx context.Context,
	channelId string,
	address string,
) (*entity.MailSubscribeConfig, error) {
	record, err := m.Get(ctx, channelId, address)
	if err != nil {
		return nil, err
	}

	if err = _db.WithContext(ctx).Delete(record).Error; err != nil {
		return nil, errors.WithStack(err)
	}
	return record, nil
}
