package dao

import (
	"context"

	entity "github.com/Decmoe47/rabbitool/entity/subscribe"
	"github.com/Decmoe47/rabbitool/util"
	"github.com/cockroachdb/errors"
	"gorm.io/gorm"
)

type YoutubeSubscribeDao struct {
}

func NewYoutubeSubscribeDao() *YoutubeSubscribeDao {
	return &YoutubeSubscribeDao{}
}

func (y *YoutubeSubscribeDao) GetAll(ctx context.Context) ([]*entity.YoutubeSubscribe, error) {
	records := []*entity.YoutubeSubscribe{}
	if err := _db.WithContext(ctx).Preload("QQChannels").Find(&records).Error; err != nil {
		return nil, errors.WithStack(err)
	}
	return records, nil
}

func (y *YoutubeSubscribeDao) Get(ctx context.Context, channelId string) (*entity.YoutubeSubscribe, error) {
	record := &entity.YoutubeSubscribe{}
	if err := _db.WithContext(ctx).Preload("QQChannels").
		Where(&entity.YoutubeSubscribe{ChannelId: channelId}).
		First(record).Error; err != nil {
		return nil, errors.WithStack(err)
	}
	return record, nil
}

func (y *YoutubeSubscribeDao) Add(ctx context.Context, subscribe *entity.YoutubeSubscribe) error {
	if err := _db.WithContext(ctx).Create(subscribe).Error; err != nil {
		return errors.WithStack(err)
	}
	return nil
}

func (y *YoutubeSubscribeDao) Delete(
	ctx context.Context,
	channelId string,
) (*entity.YoutubeSubscribe, error) {
	record, err := y.Get(ctx, channelId)
	if err != nil {
		return nil, err
	}

	if err = _db.WithContext(ctx).Delete(record).Error; err != nil {
		return nil, err
	}
	return record, nil
}

func (y *YoutubeSubscribeDao) Update(ctx context.Context, record *entity.YoutubeSubscribe) error {
	if err := _db.WithContext(ctx).Save(record).Error; err != nil {
		return errors.WithStack(err)
	}
	return nil
}

type YoutubeSubscribeConfigDao struct {
}

func NewYoutubeSubscribeConfigDao() *YoutubeSubscribeConfigDao {
	return &YoutubeSubscribeConfigDao{}
}

func (y *YoutubeSubscribeConfigDao) GetAll(
	ctx context.Context,
	channelId string,
) ([]*entity.YoutubeSubscribeConfig, error) {
	records := []*entity.YoutubeSubscribeConfig{}
	if err := _db.WithContext(ctx).
		Preload("QQChannel").
		Preload("Subscribe").
		Where(&entity.YoutubeSubscribeConfig{Subscribe: &entity.YoutubeSubscribe{ChannelId: channelId}}).
		Find(&records).Error; err != nil {
		return nil, errors.WithStack(err)
	}
	return records, nil
}

func (y *YoutubeSubscribeConfigDao) Get(
	ctx context.Context,
	qqChannelId string,
	channelId string,
) (*entity.YoutubeSubscribeConfig, error) {
	record := &entity.YoutubeSubscribeConfig{}
	if err := _db.WithContext(ctx).
		Preload("QQChannel").
		Preload("Subscribe").
		Where(&entity.YoutubeSubscribeConfig{
			Subscribe: &entity.YoutubeSubscribe{ChannelId: channelId},
			QQChannel: &entity.QQChannelSubscribe{ChannelId: qqChannelId},
		}).
		First(record).Error; err != nil {
		return nil, errors.WithStack(err)
	}
	return record, nil
}

func (y *YoutubeSubscribeConfigDao) CreateOrUpdate(
	ctx context.Context,
	qqChannel *entity.QQChannelSubscribe,
	subscribe *entity.YoutubeSubscribe,
	configs map[string]any,
) (*entity.YoutubeSubscribeConfig, error) {
	record, err := y.Get(ctx, qqChannel.ChannelId, subscribe.ChannelId)
	if errors.Is(err, gorm.ErrRecordNotFound) {
		record = entity.NewYoutubeSubscribeConfig(qqChannel, subscribe)
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

func (y *YoutubeSubscribeConfigDao) Delete(
	ctx context.Context,
	qqChannelId string,
	channelId string,
) (*entity.YoutubeSubscribeConfig, error) {
	record, err := y.Get(ctx, qqChannelId, channelId)
	if err != nil {
		return nil, err
	}

	if err = _db.WithContext(ctx).Delete(record).Error; err != nil {
		return nil, errors.WithStack(err)
	}
	return record, nil
}
