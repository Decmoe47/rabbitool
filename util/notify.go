package util

import (
	"io"
	"time"

	"github.com/Decmoe47/rabbitool/conf"
	"github.com/Decmoe47/rabbitool/errx"
	"github.com/cockroachdb/errors"
	"github.com/rs/zerolog"
	"github.com/samber/lo"
	mail "github.com/xhit/go-simple-mail/v2"
)

type errorNotifier struct {
	client        *mail.SMTPClient
	errorCounters []*errorCounter

	io.Writer
	level zerolog.Level
}

type errorCounter struct {
	text               string
	amount             int
	timestampToRefresh int64
}

func newErrorNotifier() (*errorNotifier, error) {
	server := mail.NewSMTPClient()
	server.Host = conf.R.Notifier.Host
	server.Port = conf.R.Notifier.Port
	server.Username = conf.R.Notifier.UserName
	server.Password = conf.R.Notifier.Password
	server.Encryption = mail.EncryptionSTARTTLS
	server.KeepAlive = true
	server.SendTimeout = 10 * time.Second

	client, err := server.Connect()
	if err != nil {
		return nil, errors.WithStack(err)
	}

	errNotifier := &errorNotifier{client: client, level: zerolog.ErrorLevel}
	errNotifier.Writer = errNotifier

	return errNotifier, nil
}

func (e *errorNotifier) checkAndSend(text string) error {
	if !e.allow(text) {
		return nil
	}

	return e.send(text)
}

func (e *errorNotifier) send(text string) error {
	email := mail.NewMSG()
	email.SetFrom(conf.R.Notifier.From).
		AddTo(conf.R.Notifier.To...).
		SetSubject("Error occurred from rabbitool on "+time.Now().UTC().In(CST()).Format("2006-01-02 15:04:05 MST")).
		SetBody(mail.TextPlain, text)
	if email.Error != nil {
		return errors.WithStack(email.Error)
	}

	err := email.Send(e.client)
	if err != nil {
		return errors.WithStack(err)
	}
	return nil
}

func (e *errorNotifier) allow(text string) bool {
	now := time.Now().UTC().Unix()
	_, i, ok := lo.FindIndexOf(e.errorCounters, func(item *errorCounter) bool {
		return item.text == text
	})
	if !ok {
		i = len(e.errorCounters)
		e.errorCounters = append(e.errorCounters, &errorCounter{
			text:               text,
			timestampToRefresh: now + conf.R.Notifier.IntervalMinutes*60,
		})
	} else {
		e.errorCounters[i].amount++
	}

	if now < e.errorCounters[i].timestampToRefresh && e.errorCounters[i].amount >= conf.R.Notifier.AllowedAmount {
		e.errorCounters = lo.Reject(e.errorCounters, func(_ *errorCounter, index int) bool {
			return index == i
		})
		return true
	}

	if now >= e.errorCounters[i].timestampToRefresh {
		e.errorCounters[i].amount = 0
	}
	return false
}

func (e *errorNotifier) Write(p []byte) (n int, err error) {
	defer errx.Recover(&err)

	err = e.checkAndSend(string(p))
	if err != nil {
		return n, err
	}
	return len(p), nil
}

func (e *errorNotifier) writeLevel(l zerolog.Level, p []byte) (n int, err error) {
	if l >= e.level {
		return e.Writer.Write(p)
	}
	return len(p), nil
}
