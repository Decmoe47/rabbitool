package errx

import (
	buildInErrors "errors"
	"fmt"
	"testing"
	"time"
)

func TestErrPanic(t *testing.T) {
	err := NewErrPanic("abc")
	fmt.Printf("%+v", err)
}

func TestNewWithFields(t *testing.T) {
	err := NewWithFields(ErrInvalidParam, "abc", map[string]any{
		"name":   "test",
		"time":   time.Now(),
		"count":  15,
		"isTest": true,
	})

	fmt.Printf("%+v\n", err)
}

func TestWithStack(t *testing.T) {
	err := buildInErrors.New("test")

	fmt.Printf("%+v\n", WithStack(err, map[string]any{
		"name":   "test",
		"time":   time.Now(),
		"count":  15,
		"isTest": true,
	}))
}
