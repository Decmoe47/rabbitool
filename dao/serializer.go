package dao

import (
	"context"
	"encoding/json"
	"reflect"

	"gorm.io/gorm/schema"
)

type StrSliceToJson struct {
}

// 实现 Scan 方法
func (*StrSliceToJson) Scan(
	ctx context.Context,
	field *schema.Field,
	dst reflect.Value,
	dbValue any,
) (err error) {
	fieldValue := reflect.New(field.FieldType)
	if dbValue != nil {
		err = json.Unmarshal([]byte(dbValue.(string)), fieldValue.Interface())
	}
	field.ReflectValueOf(ctx, dst).Set(fieldValue.Elem())
	return
}

// 实现 Value 方法
func (*StrSliceToJson) Value(ctx context.Context, field *schema.Field, dst reflect.Value, fieldValue any) (any, error) {
	b, err := json.Marshal(fieldValue)
	return string(b), err
}
