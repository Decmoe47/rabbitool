package dao

import (
	"context"

	entity "github.com/Decmoe47/rabbitool/entity/subscribe"
	"github.com/Decmoe47/rabbitool/errx"
	"github.com/Decmoe47/rabbitool/util"
	"github.com/cockroachdb/errors"
	"gorm.io/gorm"
)

type TwitterSubscribeDao struct {
}

func NewTwitterSubscribeDao() *TwitterSubscribeDao {
	return &TwitterSubscribeDao{}
}

func (t *TwitterSubscribeDao) GetAll(ctx context.Context) ([]*entity.TwitterSubscribe, error) {
	records := []*entity.TwitterSubscribe{}
	if err := _db.WithContext(ctx).Preload("QQChannels").Find(&records).Error; err != nil {
		return nil, errx.WithStack(err, nil)
	}
	return records, nil
}

func (t *TwitterSubscribeDao) Get(ctx context.Context, screenName string) (*entity.TwitterSubscribe, error) {
	record := &entity.TwitterSubscribe{}
	if err := _db.WithContext(ctx).Preload("QQChannels").
		Where(&entity.TwitterSubscribe{ScreenName: screenName}).
		First(record).Error; err != nil {
		return nil, errx.WithStack(err, map[string]any{"screenName": screenName})
	}
	return record, nil
}

func (t *TwitterSubscribeDao) Add(ctx context.Context, record *entity.TwitterSubscribe) error {
	if err := _db.WithContext(ctx).Create(record).Error; err != nil {
		return errx.WithStack(err, map[string]any{"screenName": record.ScreenName})
	}
	return nil
}

func (t *TwitterSubscribeDao) Delete(
	ctx context.Context,
	screenName string,
) (*entity.TwitterSubscribe, error) {
	record, err := t.Get(ctx, screenName)
	if err != nil {
		return nil, err
	}

	if err = _db.WithContext(ctx).Delete(record).Error; err != nil {
		return nil, errx.WithStack(err, map[string]any{"screenName": screenName})
	}
	return record, nil
}

func (t *TwitterSubscribeDao) Update(ctx context.Context, record *entity.TwitterSubscribe) error {
	if err := _db.WithContext(ctx).Save(record).Error; err != nil {
		return errx.WithStack(err, map[string]any{"screenName": record.ScreenName})
	}
	return nil
}

type TwitterSubscribeConfigDao struct {
}

func NewTwitterSubscribeConfigDao() *TwitterSubscribeConfigDao {
	return &TwitterSubscribeConfigDao{}
}

func (t *TwitterSubscribeConfigDao) GetAll(
	ctx context.Context,
	screenName string,
) ([]*entity.TwitterSubscribeConfig, error) {
	records := []*entity.TwitterSubscribeConfig{}
	if err := _db.WithContext(ctx).
		Preload("QQChannel").
		Preload("Subscribe").
		Where(&entity.TwitterSubscribeConfig{Subscribe: &entity.TwitterSubscribe{ScreenName: screenName}}).
		Find(&records).Error; err != nil {
		return nil, errx.WithStack(err, map[string]any{"screenName": screenName})
	}
	return records, nil
}

func (t *TwitterSubscribeConfigDao) Get(
	ctx context.Context,
	channelId string,
	screenName string,
) (*entity.TwitterSubscribeConfig, error) {
	record := &entity.TwitterSubscribeConfig{}
	if err := _db.WithContext(ctx).
		Preload("QQChannel").
		Preload("Subscribe").
		Where(&entity.TwitterSubscribeConfig{
			Subscribe: &entity.TwitterSubscribe{ScreenName: screenName},
			QQChannel: &entity.QQChannelSubscribe{ChannelId: channelId},
		}).
		First(record).Error; err != nil {
		return nil, errx.WithStack(err, map[string]any{"screenName": screenName})
	}
	return record, nil
}

func (t *TwitterSubscribeConfigDao) CreateOrUpdate(
	ctx context.Context,
	channel *entity.QQChannelSubscribe,
	subscribe *entity.TwitterSubscribe,
	configs map[string]any,
) (*entity.TwitterSubscribeConfig, error) {
	record, err := t.Get(ctx, channel.ChannelId, subscribe.ScreenName)
	if errors.Is(err, gorm.ErrRecordNotFound) {
		record = entity.NewTwitterSubscribeConfig(channel, subscribe)
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
		return nil, errx.WithStack(err, map[string]any{"screenName": subscribe.ScreenName})
	}
	return record, nil
}

func (t *TwitterSubscribeConfigDao) Delete(
	ctx context.Context,
	channelId string,
	screenName string,
) (*entity.TwitterSubscribeConfig, error) {
	record, err := t.Get(ctx, channelId, screenName)
	if err != nil {
		return nil, err
	}

	if err = _db.WithContext(ctx).Delete(record).Error; err != nil {
		return nil, errx.WithStack(err, map[string]any{"screenName": screenName})
	}
	return record, nil
}
