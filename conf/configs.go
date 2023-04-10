package conf

import (
	"os"

	"github.com/Decmoe47/rabbitool/errx"
	"gopkg.in/yaml.v3"
)

type Configs struct {
	RedirectUrl       string    `yaml:"redirectUrl"`
	PprofListenerPort int       `yaml:"pprofListenerPort,omitempty"`
	DefaultLogger     *Logger   `yaml:"defaultLogger"`
	Gorm              *Gorm     `yaml:"gorm"`
	Oss               *Oss      `yaml:"oss"`
	Notifier          *Notifier `yaml:"notifier,omitempty"` // nullable

	QQBot   *QQBot   `yaml:"qqBot"`
	Twitter *Twitter `yaml:"twitter,omitempty"` // nullable
	Youtube *Youtube `yaml:"youtube,omitempty"` // nullable
}

var R *Configs

func Load(path string) error {
	file, err := os.ReadFile(path)
	if err != nil {
		return errx.WithStack(err, nil)
	}

	err = yaml.Unmarshal(file, &R)
	if err != nil {
		return errx.WithStack(err, nil)
	}

	return nil
}
