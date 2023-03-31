package errx

import (
	"fmt"
	"io"

	"github.com/cockroachdb/errors"
)

var (
	ErrInvalidParam  = errors.New("[ErrInvalidParam]")
	ErrNotSupported  = errors.New("[ErrNotSupported]")
	ErrUnInitialized = errors.New("[ErrUnInitialized]")

	ErrBilibiliApi = errors.New("[ErrBilibiliApi]")
	ErrTwitterApi  = errors.New("[ErrTwitterApi]")
	ErrMailApi     = errors.New("[ErrMailApi]")
	ErrQQBotApi    = errors.New("[ErrQQBotApi]")
)

type ErrPanic struct {
	Msg   string
	Stack string
}

func NewErrPanic(msg string, stack string) *ErrPanic {
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
