package util

import (
	"io"
	"sync"
	"time"

	"github.com/Decmoe47/rabbitool/conf"
	"github.com/Decmoe47/rabbitool/errx"
	"github.com/rs/zerolog"
	mail "github.com/xhit/go-simple-mail/v2"
)

type errorNotifier struct {
	client             *mail.SMTPClient
	errCount           int
	timeStampToRefresh int64

	io.Writer
	level zerolog.Level

	mu sync.Locker
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
		return nil, errx.WithStack(err, nil)
	}

	errNotifier := &errorNotifier{client: client, level: zerolog.ErrorLevel, mu: &sync.Mutex{}}
	errNotifier.Writer = errNotifier

	return errNotifier, nil
}

func (e *errorNotifier) checkAndSend(text string) error {
	if !e.allow() {
		return nil
	}
	return e.send(text)
}

func (e *errorNotifier) send(text string) error {
	errFields := map[string]any{"text": text}

	email := mail.NewMSG()
	email.SetFrom(conf.R.Notifier.From).
		AddTo(conf.R.Notifier.To...).
		SetSubject("Error occurred from rabbitool on "+time.Now().UTC().In(CST()).Format("2006-01-02 15:04:05 MST")).
		SetBody(mail.TextPlain, text)
	if email.Error != nil {
		return errx.WithStack(email.Error, errFields)
	}

	err := email.Send(e.client)
	if err != nil {
		return errx.WithStack(err, errFields)
	}
	return nil
}

func (e *errorNotifier) allow() bool {
	e.mu.Lock()
	defer e.mu.Unlock()

	now := time.Now().UTC().Unix()
	if now > e.timeStampToRefresh {
		e.timeStampToRefresh = now + conf.R.Notifier.IntervalMinutes*60
		e.errCount = 1
		return false
	}

	e.errCount++
	if e.errCount != conf.R.Notifier.AllowedAmount {
		return false
	}
	return true
}

func (e *errorNotifier) Write(p []byte) (n int, err error) {
	defer errx.Recover(&err)

	err = e.checkAndSend(string(p))
	if err != nil {
		return n, err
	}
	return len(p), nil
}

func (e *errorNotifier) WriteLevel(l zerolog.Level, p []byte) (n int, err error) {
	if l >= e.level {
		return e.Writer.Write(p)
	}
	return len(p), nil
}
