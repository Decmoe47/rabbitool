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

func InitLog() error {
	zerolog.ErrorStackMarshaler = func(err error) interface{} {
		return fmt.Sprintf("%+v", err)
	}

	consoleWriter := zerolog.NewConsoleWriter(
		func(w *zerolog.ConsoleWriter) {
			w.Out = os.Stdout
			w.TimeFormat = time.RFC3339
			w.FormatErrFieldName = func(i any) string {
				if i.(string) == "error" {
					return fmt.Sprintf("\n\n- %s: ", i)
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
	consoleWriterLeveled := &levelWriter{
		Writer: consoleWriter,
		level:  convertLevel(conf.R.Log.ConsoleLevel),
	}

	fileWriter := consoleWriter
	fileWriter.Out = &lumberjack.Logger{
		Filename:   "log/rabbitool.log",
		MaxSize:    1,
		MaxAge:     30,
		MaxBackups: 5,
		LocalTime:  false,
		Compress:   false,
	}
	fileWriter.NoColor = true
	fileWriterLeveled := &levelWriter{Writer: fileWriter, level: convertLevel(conf.R.Log.FileLevel)}

	errNotifier, err := newErrorNotifier()
	if err != nil {
		return err
	}

	if conf.R.Notifier == nil {
		log.Logger = log.Output(zerolog.MultiLevelWriter(consoleWriterLeveled, fileWriterLeveled))
	} else {
		log.Logger = log.Output(zerolog.MultiLevelWriter(consoleWriterLeveled, fileWriterLeveled, errNotifier))
	}

	return nil
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
