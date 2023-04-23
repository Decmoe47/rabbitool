package util

import (
	"fmt"
	"io"
	"os"
	"strconv"
	"time"

	"github.com/Decmoe47/rabbitool/conf"
	"github.com/rs/zerolog"
	"github.com/rs/zerolog/log"
	"gopkg.in/natefinch/lumberjack.v2"
)

type InitLoggerOptions struct {
	Global       bool
	ConsoleLevel string             // 可选，为空则不输出到console
	FileLevel    string             // 可选，为空则不输出到文件
	FileOpts     *lumberjack.Logger // 必须FileLevel也要填
}

func InitLogger(opts *InitLoggerOptions) (*zerolog.Logger, error) {
	zerolog.ErrorStackMarshaler = func(err error) interface{} {
		return fmt.Sprintf("%+v", err)
	}

	consoleWriter := zerolog.NewConsoleWriter(
		func(w *zerolog.ConsoleWriter) {
			w.Out = os.Stdout
			w.TimeFormat = time.RFC3339
			w.FormatErrFieldName = func(i any) string {
				if i.(string) == "error" {
					return ""
				}
				return fmt.Sprintf("\n- %s: ", i)
			}
			w.FormatFieldName = func(i any) string {
				return fmt.Sprintf("\n- %s: ", i)
			}
			w.FormatFieldValue = func(i any) string {
				value := fmt.Sprintf("%s", i)
				result, err := strconv.Unquote(value)
				if err != nil {
					result = value
				}
				return result
			}
		},
	)

	var writers []io.Writer

	if opts.ConsoleLevel != "" {
		consoleWriterLeveled := &levelWriter{
			Writer: consoleWriter,
			level:  convertLevel(opts.ConsoleLevel),
		}
		writers = append(writers, consoleWriterLeveled)
	}
	if opts.FileLevel != "" {
		fileWriter := consoleWriter
		fileWriter.Out = opts.FileOpts
		fileWriter.NoColor = true
		fileWriterLeveled := &levelWriter{Writer: fileWriter, level: convertLevel(opts.FileLevel)}
		writers = append(writers, fileWriterLeveled)
	}
	if conf.R.Notifier != nil {
		errNotifier, err := newErrorNotifier()
		if err != nil {
			return nil, err
		}
		writers = append(writers, errNotifier)
	}

	var result zerolog.Logger
	if opts.Global {
		result = log.Output(zerolog.MultiLevelWriter(writers...))
	} else {
		result = zerolog.New(os.Stderr).With().Timestamp().Logger().Output(zerolog.MultiLevelWriter(writers...))
	}

	return &result, nil
}

func convertLevel(level string) zerolog.Level {
	switch level {
	case "disabled":
		return zerolog.Disabled
	case "trace":
		return zerolog.TraceLevel
	case "debug":
		return zerolog.DebugLevel
	case "info":
		return zerolog.InfoLevel
	case "warn", "warning":
		return zerolog.WarnLevel
	case "error":
		return zerolog.ErrorLevel
	case "fatal":
		return zerolog.FatalLevel
	default:
		return zerolog.InfoLevel
	}
}

type levelWriter struct {
	io.Writer
	level zerolog.Level
}

func (lw *levelWriter) WriteLevel(l zerolog.Level, p []byte) (n int, err error) {
	if l >= lw.level {
		return lw.Writer.Write(p)
	}
	return len(p), nil
}
