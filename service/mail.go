package service

import (
	"fmt"
	"io"
	"net/mail"

	dto "github.com/Decmoe47/rabbitool/dto/mail"
	"github.com/Decmoe47/rabbitool/errx"
	"github.com/emersion/go-imap"
	id "github.com/emersion/go-imap-id"
	imapClient "github.com/emersion/go-imap/client"
)

type MailService struct {
	Address string
	mailbox string

	client *imapClient.Client
}

type NewMailServiceOptions struct {
	Address  string `yaml:"address"`
	UserName string `yaml:"userName"`
	Password string `yaml:"password"`
	Host     string `yaml:"host"`
	Port     int    `yaml:"port"`
	Ssl      bool   `yaml:"ssl"`
	Mailbox  string `yaml:"mailbox"`
}

func NewMailService(opts *NewMailServiceOptions) (*MailService, error) {
	c, err := imapClient.Dial(fmt.Sprintf("%s:%d", opts.Host, opts.Port))
	if err != nil {
		return nil, errx.WithStack(err, nil)
	}

	idClient := id.NewClient(c)
	_, err = idClient.ID(
		id.ID{
			id.FieldName:    "IMAPClient",
			id.FieldVersion: "3.1.0",
		},
	)
	if err != nil {
		return nil, errx.WithStack(err, nil)
	}

	err = c.Login(opts.UserName, opts.Password)
	if err != nil {
		return nil, errx.WithStack(err, nil)
	}

	return &MailService{
		Address: opts.Address,
		mailbox: opts.Mailbox,
		client:  c,
	}, nil
}

func (m *MailService) Logout() error {
	return m.client.Logout()
}

func (m *MailService) GetLatestMail() (*dto.Mail, error) {
	inbox, err := m.client.Select(m.mailbox, false)
	if err != nil {
		return nil, errx.WithStack(err, nil)
	}
	if inbox.Messages == 0 {
		return nil, errx.New(errx.ErrMailApi, "No message in inbox which mail is %s", m.Address)
	}

	seqset := &imap.SeqSet{}
	seqset.AddRange(inbox.Messages, inbox.Messages)

	messages := make(chan *imap.Message, 10)
	done := make(chan error, 1)
	section := &imap.BodySectionName{}
	go func() {
		done <- m.client.Fetch(seqset, []imap.FetchItem{imap.FetchEnvelope, section.FetchItem()}, messages)
	}()

	message := <-messages
	r := message.GetBody(section)
	if r == nil {
		return nil, errx.New(errx.ErrMailApi, "Server didn't returned message body!")
	}

	if err := <-done; err != nil {
		return nil, errx.WithStack(err, nil)
	}

	msg, err := mail.ReadMessage(r)
	if err != nil {
		return nil, errx.WithStack(err, nil)
	}

	text, err := io.ReadAll(msg.Body)
	if err != nil {
		return nil, errx.WithStack(err, nil)
	}

	return &dto.Mail{
		From:    m.toAddressInfo(message.Envelope.From),
		To:      m.toAddressInfo(message.Envelope.To),
		Subject: message.Envelope.Subject,
		Time:    &message.Envelope.Date,
		Text:    string(text),
	}, nil
}

func (m *MailService) toAddressInfo(addresses []*imap.Address) (result []*dto.AddressInfo) {
	for _, address := range addresses {
		result = append(result, &dto.AddressInfo{
			Address: address.Address(),
			Name:    address.PersonalName,
		})
	}
	return
}
