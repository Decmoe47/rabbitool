package util

import (
	buildInErrors "errors"
	"reflect"

	"golang.org/x/text/cases"
	"golang.org/x/text/language"
)

// @param target - 如果传的不是struct且不是指针的话会直接panic
//
// @param fields - 键名会自动转为开头大写
func UpdateFields(target any, fields map[string]any) {
	targetValue := reflect.ValueOf(target)
	if targetValue.Kind() != reflect.Pointer {
		panic("The target is not a pointer!")
	}
	if targetValue.Elem().Kind() != reflect.Struct {
		panic("The target is not a struct!")
	}

	for k, v := range fields {
		field := targetValue.Elem().FieldByName(cases.Title(language.English, cases.NoLower).String(k))
		if field.CanSet() {
			field.Set(reflect.ValueOf(v))
		}
	}
}

func TryGetMapValue[T any](m map[string]any, key string) (T, bool) {
	var example T

	value, ok := m[key]
	if !ok {
		return any(example).(T), false
	}

	result, ok2 := value.(T)
	if !ok2 {
		return any(example).(T), false
	}

	return result, true
}

func ReceiveErrs(errCh chan error, count int) (errs error) {
	for i := 0; i < count; i++ {
		errs = buildInErrors.Join(errs, <-errCh)
	}
	return
}
