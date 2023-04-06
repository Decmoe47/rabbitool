package service

import (
	"fmt"
	"strings"

	"github.com/cockroachdb/errors"
	"github.com/rs/zerolog/log"
)

type LoggerForQQBot struct {
}

func (l *LoggerForQQBot) Debug(v ...any) {
	log.Debug().Msgf("[QQBot] %v", v...)
}

func (l *LoggerForQQBot) Info(v ...any) {
	log.Info().Msgf("[QQBot] %v", v...)
}

func (l *LoggerForQQBot) Warn(v ...any) {
	log.Warn().Msgf("[QQBot] %v", v...)
}

func (l *LoggerForQQBot) Error(v ...any) {
	log.Error().Msgf("[QQBot] %v", v...)
}

func (l *LoggerForQQBot) Debugf(format string, v ...any) {
	log.Debug().Msgf("[QQBot] "+format, v...)
}

func (l *LoggerForQQBot) Infof(format string, v ...any) {
	for _, value := range v {
		if str, ok := value.(string); ok && strings.Contains(str, "Heartbeat") {
			log.Debug().Msgf("[QQBot] "+format, v...)
			return
		}
	}
	log.Info().Msgf("[QQBot] "+format, v...)
}

func (l *LoggerForQQBot) Warnf(format string, v ...any) {
	log.Warn().Msgf("[QQBot] "+format, v...)
}

func (l *LoggerForQQBot) Errorf(format string, v ...any) {
	if strings.Contains(format, "PANIC") {
		var err error
		for _, value := range v {
			if e, ok := value.(error); ok {
				if ee, ok := e.(fmt.Formatter); ok {
					err = ee.(error)
				} else {
					err = errors.WithStack(e)
				}
				break
			}
		}
		if err != nil {
			log.Error().Stack().Err(err).Msgf("[QQBot] "+format, v...)
		}
	}
	log.Error().Msgf("[QQBot] "+format, v...)
}

func (l *LoggerForQQBot) Sync() error {
	return nil
}
