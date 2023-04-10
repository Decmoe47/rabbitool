package dao

import (
	"context"
	"fmt"
	"strings"
	"time"

	"github.com/Decmoe47/rabbitool/conf"
	"github.com/Decmoe47/rabbitool/errx"
	"github.com/Decmoe47/rabbitool/util"
	"github.com/rs/zerolog"
	"gopkg.in/natefinch/lumberjack.v2"
	gormLogger "gorm.io/gorm/logger"
)

type LoggerForGorm struct {
	logger   *zerolog.Logger
	LogLevel gormLogger.LogLevel
}

func NewLoggerForGorm(logLevel gormLogger.LogLevel) (*LoggerForGorm, error) {
	var (
		logger *zerolog.Logger
		err    error
	)

	if conf.R.Gorm.Logger == nil {
		logger, err = util.InitLogger(&util.InitLoggerOptions{
			Global:       true,
			ConsoleLevel: conf.R.DefaultLogger.ConsoleLevel,
			FileLevel:    conf.R.DefaultLogger.FileLevel,
			FileOpts: &lumberjack.Logger{
				Filename:   "log/rabbitool.log",
				MaxSize:    1,
				MaxAge:     30,
				MaxBackups: 5,
				LocalTime:  false,
				Compress:   false,
			},
		})
	} else {
		logger, err = util.InitLogger(&util.InitLoggerOptions{
			Global:       false,
			ConsoleLevel: conf.R.Gorm.Logger.ConsoleLevel,
			FileLevel:    conf.R.Gorm.Logger.FileLevel,
			FileOpts: &lumberjack.Logger{
				Filename:   "log/gorm.log",
				MaxSize:    1,
				MaxAge:     30,
				MaxBackups: 5,
				LocalTime:  false,
				Compress:   false,
			},
		})
	}
	if err != nil {
		return nil, err
	}
	return &LoggerForGorm{logger: logger, LogLevel: logLevel}, nil
}

func (l *LoggerForGorm) LogMode(level gormLogger.LogLevel) gormLogger.Interface {
	l.LogLevel = level
	return l
}

func (l *LoggerForGorm) Info(ctx context.Context, s string, v ...any) {
	if l.LogLevel >= gormLogger.Info {
		l.logger.Info().Msgf("[Gorm] "+s, v...)
	}
}

func (l *LoggerForGorm) Warn(ctx context.Context, s string, v ...any) {
	if l.LogLevel >= gormLogger.Warn {
		l.logger.Warn().Msgf("[Gorm] "+s, v...)
	}
}

func (l *LoggerForGorm) Error(ctx context.Context, s string, v ...any) {
	if l.LogLevel < gormLogger.Error {
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
			l.logger.Warn().Msgf("[Gorm] "+s, v...)
		} else {
			l.logger.Error().Stack().Err(err).Msgf("[Gorm] "+s, v...)
		}
	}
	l.logger.Error().Msgf("[Gorm] "+s, v...)
}

func (l *LoggerForGorm) Trace(
	ctx context.Context,
	begin time.Time,
	fc func() (sql string, rowsAffected int64),
	err error,
) {
	if l.LogLevel <= gormLogger.Silent {
		return
	}

	elapsed := time.Since(begin)
	sql, rows := fc()
	switch {
	case err != nil && l.LogLevel >= gormLogger.Error:
		if strings.Contains(err.Error(), "record not found") {
			if rows == -1 {
				l.logger.Error().Msgf("[Gorm] %f-%s", float64(elapsed.Nanoseconds())/1e6, sql)
			} else {
				l.logger.Error().Msgf("[Gorm] %f-%d %s", float64(elapsed.Nanoseconds())/1e6, rows, sql)
			}
		} else {
			if rows == -1 {
				l.logger.Error().Stack().Err(errx.WithStack(err, nil)).Msgf(
					"[Gorm] %f-%s",
					float64(elapsed.Nanoseconds())/1e6, sql)
			} else {
				l.logger.Error().Stack().Err(errx.WithStack(err, nil)).Msgf(
					"[Gorm] %f-%d %s",
					float64(elapsed.Nanoseconds())/1e6, rows, sql)
			}
		}
	case elapsed > 200*time.Millisecond && l.LogLevel >= gormLogger.Warn:
		slowLog := fmt.Sprintf("SLOW SQL >= %v", 200*time.Millisecond)
		if rows == -1 {
			l.logger.Warn().Msgf("[Gorm] %s. %f-%s", slowLog, float64(elapsed.Nanoseconds())/1e6, sql)
		} else {
			l.logger.Warn().Msgf("[Gorm] %s. %f-%d %s", slowLog, float64(elapsed.Nanoseconds())/1e6, rows, sql)
		}
	case l.LogLevel >= gormLogger.Info:
		if rows == -1 {
			l.logger.Trace().Msgf("[Gorm] %f-%s", float64(elapsed.Nanoseconds())/1e6, sql)
		} else {
			l.logger.Trace().Msgf("[Gorm] %f-%d %s", float64(elapsed.Nanoseconds())/1e6, rows, sql)
		}
	}
}
