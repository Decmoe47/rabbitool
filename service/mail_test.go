package service

import (
	"testing"

	"github.com/spf13/viper"
	"github.com/stretchr/testify/require"
)

func TestMailService_GetLatestMail(t *testing.T) {
	viper.SetConfigName("test_configs")
	viper.SetConfigType("yaml")
	viper.AddConfigPath("../")
	err := viper.ReadInConfig()
	require.NoError(t, err)

	m, err := NewMailService(&NewMailServiceOptions{
		Address:  viper.GetString("mail.address"),
		UserName: viper.GetString("mail.userName"),
		Password: viper.GetString("mail.password"),
		Host:     viper.GetString("mail.host"),
		Port:     viper.GetInt("mail.port"),
		Ssl:      viper.GetBool("mail.ssl"),
		Mailbox:  viper.GetString("mail.mailbox"),
	})
	require.NoError(t, err)

	_, err = m.GetLatestMail()
	require.NoError(t, err)

	err = m.Logout()
	require.NoError(t, err)
}
