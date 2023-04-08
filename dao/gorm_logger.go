package dao

import (
	"context"
	"fmt"
	"strings"
	"time"

	"github.com/Decmoe47/rabbitool/errx"
	"github.com/rs/zerolog/log"
	"gorm.io/gorm/logger"
)

type LoggerForGorm struct {
	LogLevel logger.LogLevel
}

func (l *LoggerForGorm) LogMode(level logger.LogLevel) logger.Interface {
	l.LogLevel = level
	return l
}

func (l *LoggerForGorm) Info(ctx context.Context, s string, v ...any) {
	if l.LogLevel >= logger.Info {
		log.Info().Msgf("[Gorm] "+s, v...)
	}
}

func (l *LoggerForGorm) Warn(ctx context.Context, s string, v ...any) {
	if l.LogLevel >= logger.Warn {
		log.Warn().Msgf("[Gorm] "+s, v...)
	}
}

func (l *LoggerForGorm) Error(ctx context.Context, s string, v ...any) {
	if l.LogLevel < logger.Error {
		return
	}
	var err error
	for _, value := range v {
		if e, ok := value.(error); ok {
			err = errx.WithStack(e, nil)
			break
		}
	}

	if err != nil {
		if strings.Contains(err.Error(), "record not found") {
			log.Warn().Msgf("[Gorm] "+s, v...)
		} else {
			log.Error().Stack().Err(err).Msgf("[Gorm] "+s, v...)
		}
	}
	log.Error().Msgf("[Gorm] "+s, v...)
}

func (l *LoggerForGorm) Trace(
	ctx context.Context,
	begin time.Time,
	fc func() (sql string, rowsAffected int64),
	err error,
) {
	if l.LogLevel <= logger.Silent {
		return
	}

	elapsed := time.Since(begin)
	sql, rows := fc()
	switch {
	case err != nil && l.LogLevel >= logger.Error:
		if strings.Contains(err.Error(), "record not found") {
			if rows == -1 {
				log.Error().Msgf("[Gorm] %f-%s", float64(elapsed.Nanoseconds())/1e6, sql)
			} else {
				log.Error().Msgf("[Gorm] %f-%d %s", float64(elapsed.Nanoseconds())/1e6, rows, sql)
			}
		} else {
			if rows == -1 {
				log.Error().Stack().Err(errx.WithStack(err, nil)).Msgf(
					"[Gorm] %f-%s",
					float64(elapsed.Nanoseconds())/1e6, sql)
			} else {
				log.Error().Stack().Err(errx.WithStack(err, nil)).Msgf(
					"[Gorm] %f-%d %s",
					float64(elapsed.Nanoseconds())/1e6, rows, sql)
			}
		}
	case elapsed > 200*time.Millisecond && l.LogLevel >= logger.Warn:
		slowLog := fmt.Sprintf("SLOW SQL >= %v", 200*time.Millisecond)
		if rows == -1 {
			log.Warn().Msgf("[Gorm] %s. %f-%s", slowLog, float64(elapsed.Nanoseconds())/1e6, sql)
		} else {
			log.Warn().Msgf("[Gorm] %s. %f-%d %s", slowLog, float64(elapsed.Nanoseconds())/1e6, rows, sql)
		}
	case l.LogLevel >= logger.Info:
		if rows == -1 {
			log.Trace().Msgf("[Gorm] %f-%s", float64(elapsed.Nanoseconds())/1e6, sql)
		} else {
			log.Trace().Msgf("[Gorm] %f-%d %s", float64(elapsed.Nanoseconds())/1e6, rows, sql)
		}
	}
}
