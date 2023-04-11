package errx

import (
	"fmt"
	"io"
	"runtime"

	"github.com/rs/zerolog/log"
)

type ErrPanic struct {
	Msg   string
	Stack string
}

func NewErrPanic(msg string) *ErrPanic {
	var buf [4096]byte
	n := runtime.Stack(buf[:], false)
	stack := string(buf[:n])

	return &ErrPanic{Msg: msg, Stack: stack}
}

func (e *ErrPanic) Error() string { return e.Msg }

func (e *ErrPanic) Format(s fmt.State, verb rune) {
	switch verb {
	case 'v':
		if s.Flag('+') {
			_, _ = io.WriteString(s, fmt.Sprintf("%s\n%s", e.Msg, e.Stack))
			return
		}
		fallthrough
	case 's':
		_, _ = io.WriteString(s, e.Msg)
	case 'q':
		_, _ = fmt.Fprintf(s, "%q", e.Msg)
	}
}

func Recover(returnErr *error) {
	err := recover()
	if err == nil {
		return
	}

	switch e := err.(type) {
	case string:
		*returnErr = NewErrPanic(e)
	case error:
		if _, ok := e.(fmt.Formatter); ok {
			*returnErr = WithStack(e, nil)
		} else {
			*returnErr = NewErrPanic(e.Error())
		}
	default:
		*returnErr = NewErrPanic(fmt.Sprintf("%v", e))
	}
}

func RecoverAndSendErr(returnErr chan error) {
	err := recover()
	if err == nil {
		returnErr <- nil
		return
	}

	switch e := err.(type) {
	case string:
		returnErr <- NewErrPanic(e)
	case error:
		if _, ok := e.(fmt.Formatter); ok {
			returnErr <- WithStack(e, nil)
		} else {
			returnErr <- NewErrPanic(e.Error())
		}
	default:
		returnErr <- NewErrPanic(fmt.Sprintf("%v", e))
	}
}

func RecoverAndLog() {
	err := recover()
	if err == nil {
		return
	}

	switch e := err.(type) {
	case string:
		log.Error().Stack().Err(NewErrPanic(e)).Msgf(e)
	case error:
		if _, ok := e.(fmt.Formatter); ok {
			log.Error().Stack().Err(WithStack(e, nil)).Msgf(e.Error())
		} else {
			e = NewErrPanic(e.Error())
			log.Error().Stack().Err(e).Msgf(e.Error())
		}
	default:
		ee := NewErrPanic(fmt.Sprintf("%v", e))
		log.Error().Stack().Err(ee).Msgf(ee.Error())
	}
}
