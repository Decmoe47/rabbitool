package conf

import (
	"os"

	"github.com/cockroachdb/errors"
	"gopkg.in/yaml.v3"
)

type Configs struct {
	RedirectUrl string `yaml:"redirectUrl"`
	DbPath      string `yaml:"dbPath"`

	Log      *Log      `yaml:"log"`
	Oss      *Oss      `yaml:"oss"`
	Notifier *Notifier `yaml:"notifier,omitempty"` // nullable

	QQBot   *QQBot   `yaml:"qqBot"`
	Twitter *Twitter `yaml:"twitter,omitempty"` // nullable
	Youtube *Youtube `yaml:"youtube,omitempty"` // nullable
}

var R *Configs

func Load(path string) error {
	file, err := os.ReadFile(path)
	if err != nil {
		return errors.WithStack(err)
	}

	err = yaml.Unmarshal(file, &R)
	if err != nil {
		return errors.WithStack(err)
	}

	return nil
}
