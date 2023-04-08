package dao

import (
	entity "github.com/Decmoe47/rabbitool/entity/subscribe"
	"github.com/Decmoe47/rabbitool/errx"
	"github.com/glebarez/sqlite"
	"gorm.io/gorm"
	"gorm.io/gorm/logger"
	"gorm.io/gorm/schema"
)

var _db *gorm.DB

func InitDb(dbPath string) error {
	db, err := gorm.Open(sqlite.Open(dbPath+"?_pragma=foreign_keys(1)"), &gorm.Config{
		Logger: &LoggerForGorm{LogLevel: logger.Warn},
	})
	if err != nil {
		return errx.WithStack(err, nil)
	}

	schema.RegisterSerializer("StrSliceToJson", &StrSliceToJson{})

	err = db.AutoMigrate(
		&entity.QQChannelSubscribe{},
		&entity.BilibiliSubscribe{},
		&entity.BilibiliSubscribeConfig{},
		&entity.TwitterSubscribe{},
		&entity.TwitterSubscribeConfig{},
		&entity.YoutubeSubscribe{},
		&entity.YoutubeSubscribeConfig{},
		&entity.MailSubscribe{},
		&entity.MailSubscribeConfig{},
	)
	if err != nil {
		return errx.WithStack(err, nil)
	}

	_db = db
	return nil
}
