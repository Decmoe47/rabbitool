package errx

import (
	"fmt"
	"runtime"

	"github.com/cockroachdb/errors"
	"github.com/rs/zerolog/log"
)

func Recover(returnErr *error) {
	err := recover()
	if err == nil {
		return
	}

	var buf [4096]byte
	n := runtime.Stack(buf[:], false)
	stack := string(buf[:n])

	switch e := err.(type) {
	case string:
		*returnErr = NewErrPanic(e, stack)
	case error:
		if _, ok := e.(fmt.Formatter); ok {
			*returnErr = errors.WithStack(e)
		} else {
			*returnErr = NewErrPanic(e.Error(), stack)
		}
	default:
		*returnErr = NewErrPanic(fmt.Sprintf("%v", e), stack)
	}
}

func RecoverAndLog() {
	err := recover()
	if err == nil {
		return
	}

	var buf [4096]byte
	n := runtime.Stack(buf[:], false)
	stack := string(buf[:n])

	switch e := err.(type) {
	case string:
		log.Error().Stack().Err(NewErrPanic(e, stack)).Msgf(e)
	case error:
		if _, ok := e.(fmt.Formatter); ok {
			log.Error().Stack().Err(errors.WithStack(e)).Msgf(e.Error())
		} else {
			e = NewErrPanic(e.Error(), stack)
			log.Error().Stack().Err(e).Msgf(e.Error())
		}
	default:
		ee := NewErrPanic(fmt.Sprintf("%v", e), stack)
		log.Error().Stack().Err(ee).Msgf(ee.Error())
	}
}
