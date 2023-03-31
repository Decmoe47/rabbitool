package errx

import (
	"fmt"
	"runtime"
	"testing"
)

func TestErrPanic(t *testing.T) {
	var buf [4096]byte
	n := runtime.Stack(buf[:], false)
	stack := string(buf[:n])

	err := NewErrPanic("abc", stack)
	fmt.Printf("%+v", err)
}
