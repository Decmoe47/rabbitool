package service

import (
	"context"
	"os"
	"testing"
	"time"

	"github.com/Decmoe47/rabbitool/conf"
	"github.com/Decmoe47/rabbitool/util/req"
	"github.com/stretchr/testify/require"
)

func TestTwitterService_GetLatestTweet(tt *testing.T) {
	err := os.Setenv("http_proxy", "http://127.0.0.1:7890")
	require.NoError(tt, err)
	err = os.Setenv("https_proxy", "http://127.0.0.1:7890")
	require.NoError(tt, err)

	ctx := context.Background()
	req.InitClient(time.Second * 10)

	err = conf.Load("../configs.yml")
	require.NoError(tt, err)

	t := NewTwitterService()

	_, err = t.GetLatestTweet(ctx, "AliceMononobe")
	require.NoError(tt, err)
}
