package service

import (
	"fmt"
	"strings"

	"github.com/Decmoe47/rabbitool/conf"
	"github.com/Decmoe47/rabbitool/errx"
	"github.com/Decmoe47/rabbitool/util"
	"github.com/rs/zerolog"
	"gopkg.in/natefinch/lumberjack.v2"
)

type LoggerForQQBot struct {
	logger *zerolog.Logger
}

func NewLoggerForQQBot() (*LoggerForQQBot, error) {
	var (
		logger *zerolog.Logger
		err    error
	)

	if conf.R.QQBot.Logger == nil {
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
			ConsoleLevel: conf.R.QQBot.Logger.ConsoleLevel,
			FileLevel:    conf.R.QQBot.Logger.FileLevel,
			FileOpts: &lumberjack.Logger{
				Filename:   "log/qq_bot.log",
				MaxSize:    1,
				MaxAge:     15,
				MaxBackups: 3,
				LocalTime:  false,
				Compress:   false,
			},
		})
	}
	if err != nil {
		return nil, err
	}
	return &LoggerForQQBot{logger: logger}, nil
}

func (l *LoggerForQQBot) Debug(v ...any) {
	l.logger.Debug().Msgf("[QQBot] %v", v...)
}

func (l *LoggerForQQBot) Info(v ...any) {
	l.logger.Info().Msgf("[QQBot] %v", v...)
}

func (l *LoggerForQQBot) Warn(v ...any) {
	l.logger.Warn().Msgf("[QQBot] %v", v...)
}

func (l *LoggerForQQBot) Error(v ...any) {
	l.logger.Error().Msgf("[QQBot] %v", v...)
}

func (l *LoggerForQQBot) Debugf(format string, v ...any) {
	l.logger.Debug().Msgf("[QQBot] "+format, v...)
}

func (l *LoggerForQQBot) Infof(format string, v ...any) {
	for _, value := range v {
		if str, ok := value.(string); ok && strings.Contains(str, "Heartbeat") {
			l.logger.Debug().Msgf("[QQBot] "+format, v...)
			return
		}
	}
	l.logger.Info().Msgf("[QQBot] "+format, v...)
}

func (l *LoggerForQQBot) Warnf(format string, v ...any) {
	l.logger.Warn().Msgf("[QQBot] "+format, v...)
}

func (l *LoggerForQQBot) Errorf(format string, v ...any) {
	if strings.Contains(format, "PANIC") {
		var err error
		for _, value := range v {
			if e, ok := value.(error); ok {
				if ee, ok := e.(fmt.Formatter); ok {
					err = ee.(error)
				} else {
					err = errx.WithStack(err, nil)
				}
				break
			}
		}
		if err != nil {
			l.logger.Error().Stack().Err(err).Msgf("[QQBot] "+format, v...)
		}
	}
	l.logger.Error().Msgf("[QQBot] "+format, v...)
}

func (l *LoggerForQQBot) Sync() error {
	return nil
}
