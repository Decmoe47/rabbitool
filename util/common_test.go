package util

import (
	"testing"

	"github.com/stretchr/testify/assert"
)

func TestUpdateFields(t *testing.T) {
	target := struct{ Name string }{Name: "bbbb"}
	fields := map[string]any{"name": "aaa", "age": 123}

	t.Run("testShouldSuccess", func(t *testing.T) {
		UpdateFields(target, fields)
		assert.NotEqual(t, "aaa", target.Name)
	})

	t.Run("testShouldFail", func(t *testing.T) {
		defer func() {
			err := recover()
			assert.Contains(t, err.(error).Error(), "The target is not a pointer!")
		}()
		UpdateFields(target, fields)
	})
}

func TestTryGetMapValue(t *testing.T) {
	m := map[string]any{"name": "aaa"}

	t.Run("testShouldSuccess", func(t *testing.T) {
		result, ok := TryGetMapValue[string](m, "name")
		if !ok || result != "aaa" {
			t.Errorf("result got = %v, want = %v", result, "aaa")
		}
	})

	t.Run("testShouldFail1", func(t *testing.T) {
		_, ok := TryGetMapValue[string](m, "age")
		assert.False(t, ok)
	})

	t.Run("testShouldFail2", func(t *testing.T) {
		_, ok := TryGetMapValue[int](m, "name")
		assert.False(t, ok)
	})
}
