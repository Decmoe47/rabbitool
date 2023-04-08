package dao

import (
	"context"
	"strconv"

	entity "github.com/Decmoe47/rabbitool/entity/subscribe"
	"github.com/Decmoe47/rabbitool/errx"
	"github.com/Decmoe47/rabbitool/util"
	"github.com/cockroachdb/errors"
	"gorm.io/gorm"
)

type BilibiliSubscribeDao struct {
}

func NewBilibiliSubscribeDao() *BilibiliSubscribeDao {
	return &BilibiliSubscribeDao{}
}

func (b *BilibiliSubscribeDao) GetAll(ctx context.Context) ([]*entity.BilibiliSubscribe, error) {
	records := []*entity.BilibiliSubscribe{}
	if err := _db.WithContext(ctx).Preload("QQChannels").Find(&records).Error; err != nil {
		return nil, errx.WithStack(err, nil)
	}
	return records, nil
}

func (b *BilibiliSubscribeDao) Get(ctx context.Context, uid string) (*entity.BilibiliSubscribe, error) {
	uidInUint64, err := strconv.ParseUint(uid, 10, 32)
	if err != nil {
		return nil, errx.WithStack(err, map[string]any{"uid": uid})
	}
	return b.GetByUint(ctx, uint(uidInUint64))
}

func (b *BilibiliSubscribeDao) GetByUint(ctx context.Context, uid uint) (*entity.BilibiliSubscribe, error) {
	record := &entity.BilibiliSubscribe{}
	if err := _db.WithContext(ctx).Preload("QQChannels").
		Where(&entity.BilibiliSubscribe{Uid: uid}).
		First(record).Error; err != nil {
		return nil, errx.WithStack(err, map[string]any{"uid": uid})
	}
	return record, nil
}

func (b *BilibiliSubscribeDao) Add(ctx context.Context, record *entity.BilibiliSubscribe) error {
	if err := _db.WithContext(ctx).Create(record).Error; err != nil {
		return errx.WithStack(err, map[string]any{"uid": record.Uid})
	}
	return nil
}

func (b *BilibiliSubscribeDao) Delete(ctx context.Context, uid string) (*entity.BilibiliSubscribe, error) {
	uidInUint64, err := strconv.ParseUint(uid, 10, 32)
	if err != nil {
		return nil, errx.WithStack(err, map[string]any{"uid": uid})
	}
	return b.DeleteByUint(ctx, uint(uidInUint64))
}

func (b *BilibiliSubscribeDao) DeleteByUint(
	ctx context.Context,
	uid uint,
) (*entity.BilibiliSubscribe, error) {
	record, err := b.GetByUint(ctx, uid)
	if err != nil {
		return nil, err
	}

	if err = _db.WithContext(ctx).Delete(record).Error; err != nil {
		return nil, errx.WithStack(err, map[string]any{"uid": uid})
	}
	return record, nil
}

func (b *BilibiliSubscribeDao) Update(ctx context.Context, record *entity.BilibiliSubscribe) error {
	if err := _db.WithContext(ctx).Save(record).Error; err != nil {
		return errx.WithStack(err, map[string]any{"uid": record.Uid})
	}
	return nil
}

type BilibiliSubscribeConfigDao struct {
}

func NewBilibiliSubscribeConfigDao() *BilibiliSubscribeConfigDao {
	return &BilibiliSubscribeConfigDao{}
}

func (b *BilibiliSubscribeConfigDao) GetAll(
	ctx context.Context,
	uid string,
) ([]*entity.BilibiliSubscribeConfig, error) {
	uidInUint64, err := strconv.ParseUint(uid, 10, 32)
	if err != nil {
		return nil, errx.WithStack(err, map[string]any{"uid": uid})
	}
	return b.GetAllByUint(ctx, uint(uidInUint64))
}

func (b *BilibiliSubscribeConfigDao) GetAllByUint(
	ctx context.Context,
	uid uint,
) ([]*entity.BilibiliSubscribeConfig, error) {
	records := []*entity.BilibiliSubscribeConfig{}
	if err := _db.WithContext(ctx).
		Preload("QQChannel").
		Preload("Subscribe").
		Where(&entity.BilibiliSubscribeConfig{Subscribe: &entity.BilibiliSubscribe{Uid: uid}}).
		Find(&records).Error; err != nil {
		return nil, errx.WithStack(err, map[string]any{"uid": uid})
	}
	return records, nil
}

func (b *BilibiliSubscribeConfigDao) Get(
	ctx context.Context,
	channelId string,
	uid string,
) (*entity.BilibiliSubscribeConfig, error) {
	uidInUint64, err := strconv.ParseUint(uid, 10, 32)
	if err != nil {
		return nil, errx.WithStack(err, map[string]any{"uid": uid})
	}
	return b.GetByUint(ctx, channelId, uint(uidInUint64))
}

func (b *BilibiliSubscribeConfigDao) GetByUint(
	ctx context.Context,
	channelId string,
	uid uint,
) (*entity.BilibiliSubscribeConfig, error) {
	record := &entity.BilibiliSubscribeConfig{}
	if err := _db.WithContext(ctx).
		Preload("QQChannel").
		Preload("Subscribe").
		Where(&entity.BilibiliSubscribeConfig{
			Subscribe: &entity.BilibiliSubscribe{Uid: uid},
			QQChannel: &entity.QQChannelSubscribe{ChannelId: channelId},
		}).
		First(&record).Error; err != nil {
		return nil, errx.WithStack(err, map[string]any{"uid": uid})
	}
	return record, nil
}

func (b *BilibiliSubscribeConfigDao) CreateOrUpdate(
	ctx context.Context,
	channel *entity.QQChannelSubscribe,
	subscribe *entity.BilibiliSubscribe,
	configs map[string]any,
) (*entity.BilibiliSubscribeConfig, error) {
	record, err := b.GetByUint(ctx, channel.ChannelId, subscribe.Uid)
	if errors.Is(err, gorm.ErrRecordNotFound) {
		record = entity.NewBilibiliSubscribeConfig(channel, subscribe)
		if configs != nil {
			util.UpdateFields(record, configs)
		}
	} else if err != nil {
		return nil, err
	}

	if configs != nil {
		util.UpdateFields(record, configs)
	}

	if err := _db.WithContext(ctx).Save(&record).Error; err != nil {
		return nil, errx.WithStack(err, map[string]any{"uid": subscribe.Uid})
	}
	return record, nil
}

func (b *BilibiliSubscribeConfigDao) Delete(
	ctx context.Context,
	channelId string,
	uid string,
) (*entity.BilibiliSubscribeConfig, error) {
	uidInUint64, err := strconv.ParseUint(uid, 10, 32)
	if err != nil {
		return nil, errx.WithStack(err, map[string]any{"uid": uid})
	}
	return b.DeleteByUint(ctx, channelId, uint(uidInUint64))
}

func (b *BilibiliSubscribeConfigDao) DeleteByUint(
	ctx context.Context,
	channelId string,
	uid uint,
) (*entity.BilibiliSubscribeConfig, error) {
	record, err := b.GetByUint(ctx, channelId, uid)
	if err != nil {
		return nil, err
	}

	if err = _db.WithContext(ctx).Delete(&record).Error; err != nil {
		return nil, errx.WithStack(err, map[string]any{"uid": uid})
	}
	return record, nil
}
