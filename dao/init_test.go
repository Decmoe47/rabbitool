package dao

import (
	"testing"

	"github.com/spf13/viper"
	"github.com/stretchr/testify/require"
)

func TestInitDb(t *testing.T) {
	viper.SetConfigName("test_configs")
	viper.SetConfigType("yaml")
	viper.SetConfigFile("../test_configs.yml")
	err := viper.ReadInConfig()
	require.NoError(t, err)

	err = InitDb(viper.GetString("dbPath"))
	require.NoError(t, err)
}
