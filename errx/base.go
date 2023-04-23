package errx

import (
	buildInErrors "errors"
	"fmt"

	"github.com/cockroachdb/errors"
)

type ErrorName string

type Error struct {
	Name   ErrorName
	Msg    string
	Fields map[string]any
	Cause  error
}

func New(name ErrorName, msg string, args ...any) error {
	if args != nil {
		return errors.WithStack(&Error{
			Name: name,
			Msg:  fmt.Sprintf(msg, args...),
		})
	}

	return errors.WithStack(&Error{
		Name: name,
		Msg:  msg,
	})
}

func NewWithFields(name ErrorName, msg string, fields map[string]any, args ...any) error {
	if args != nil {
		return errors.WithStack(&Error{
			Name:   name,
			Msg:    fmt.Sprintf(msg, args...),
			Fields: fields,
		})
	}

	return errors.WithStack(&Error{
		Name:   name,
		Msg:    msg,
		Fields: fields,
	})
}

func NewWithCause(name ErrorName, msg string, fields map[string]any, cause error, args ...any) error {
	if args != nil {
		return errors.WithStack(&Error{
			Name:   name,
			Msg:    fmt.Sprintf(msg, args...),
			Fields: fields,
			Cause:  cause,
		})
	}

	return errors.WithStack(&Error{
		Name:   name,
		Msg:    msg,
		Fields: fields,
		Cause:  cause,
	})
}

func WithStack(err error, fields map[string]any) error {
	return errors.WithStack(&Error{
		Name:   "ErrWithStack",
		Msg:    err.Error(),
		Fields: fields,
		Cause:  err,
	})
}

func (e *Error) Error() string {
	if e.Fields != nil {
		fieldsStr := ""
		for k, v := range e.Fields {
			if vv, ok := v.(interface{ String() string }); ok {
				fieldsStr += fmt.Sprintf("%s: %s\n", k, vv.String())
			} else {
				fieldsStr += fmt.Sprintf("%s: %+v\n", k, v)
			}
		}

		if e.Cause != nil {
			return fmt.Sprintf("[%s] %s\n%s: %s", e.Name, e.Msg, fieldsStr, e.Cause)
		}
		return fmt.Sprintf("[%s] %s\n%s", e.Name, e.Msg, fieldsStr)
	}

	if e.Cause != nil {
		return fmt.Sprintf("[%s] %s: %s", e.Name, e.Msg, e.Cause)
	}
	return fmt.Sprintf("[%s] %s", e.Name, e.Msg)
}

func (e *Error) Format(s fmt.State, verb rune) {
	errors.FormatError(e, s, verb)
}

func (e *Error) Unwrap() error { return e.Cause }

func Join(errs ...error) error {
	return buildInErrors.Join(errs...)
}

func Blend(errs []error) (res error) {
	for _, err := range errs {
		res = buildInErrors.Join(res, err)
	}
	return
}
