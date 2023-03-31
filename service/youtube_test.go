package service

import (
	"context"
	"os"
	"testing"

	"github.com/Decmoe47/rabbitool/conf"
	"github.com/stretchr/testify/assert"
	"github.com/stretchr/testify/require"
	"github.com/stretchr/testify/suite"
)

type YoutubeServiceTestSuite struct {
	suite.Suite
	y   *YoutubeService
	ctx context.Context

	channelIdList []string
}

func (suite *YoutubeServiceTestSuite) SetupTest() {
	err := os.Setenv("http_proxy", "http://127.0.0.1:7890")
	require.NoError(suite.T(), err)
	err = os.Setenv("https_proxy", "http://127.0.0.1:7890")
	require.NoError(suite.T(), err)

	err = conf.Load("../configs.yml")
	require.NoError(suite.T(), err)

	suite.ctx = context.Background()
	suite.y, err = NewYoutubeService(suite.ctx)
	require.NoError(suite.T(), err)

	suite.channelIdList = []string{
		"UCp6993wxpyDPHUpavwDFqgg",
		"UCt0clH12Xk1-Ej5PXKGfdPA",
		"UCIdEIHpS0TdkqRkHL5OkLtA",
		"UCvNn6mRroGDctnqU-FUsszg",
	}
}

func (suite *YoutubeServiceTestSuite) TestGetLatestVideoOrLive() {
	for _, channelId := range suite.channelIdList {
		suite.T().Run(channelId, func(t *testing.T) {
			_, err := suite.y.GetLatestVideoOrLive(suite.ctx, channelId)
			assert.NoError(t, err)
		})
	}
}

func (suite *YoutubeServiceTestSuite) TestIsStreaming() {
	_, ok := suite.y.IsStreaming(suite.ctx, "WosLaHVMLCI")
	assert.True(suite.T(), ok)
}

func TestYoutubeService(t *testing.T) {
	suite.Run(t, new(YoutubeServiceTestSuite))
}
