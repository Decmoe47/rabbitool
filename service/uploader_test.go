package service

import (
	"os"
	"testing"
	"time"

	"github.com/Decmoe47/rabbitool/conf"
	"github.com/Decmoe47/rabbitool/util"
	"github.com/Decmoe47/rabbitool/util/req"
	"github.com/stretchr/testify/require"
	"github.com/stretchr/testify/suite"
)

type CosServiceTestSuite struct {
	suite.Suite
	c *UploaderService
}

func (suite *CosServiceTestSuite) SetupTest() {
	req.InitClient(time.Second * 10)

	err := conf.Load("../configs.yml")
	require.NoError(suite.T(), err)

	suite.c, err = NewUploaderService()
	require.NoError(suite.T(), err)
}

func (suite *CosServiceTestSuite) TestCosService_UploadImage() {
	url, err := suite.c.UploadImage("https://i0.hdslb.com/bfs/new_dyn/5e162823c9c423cc5fea2c256368a8c31926687.jpg")
	require.NoError(suite.T(), err)

	resp, err := req.Client.R().Get(url)
	require.NoError(suite.T(), err)
	require.True(suite.T(), resp.IsSuccessState())
}

func (suite *CosServiceTestSuite) TestCosService_UploadVideo() {
	err := os.Setenv("http_proxy", "http://127.0.0.1:7890")
	require.NoError(suite.T(), err)
	err = os.Setenv("https_proxy", "http://127.0.0.1:7890")
	require.NoError(suite.T(), err)

	t := time.Date(2023, 2, 17, 20, 46, 00, 0, util.CST())
	url, err := suite.c.UploadVideo("https://twitter.com/Genshin_7/status/1626563613845778432", &t)
	require.NoError(suite.T(), err)

	resp, err := req.Client.R().Get(url)
	require.NoError(suite.T(), err)
	require.True(suite.T(), resp.IsSuccessState())
}

func TestCosService(t *testing.T) {
	suite.Run(t, new(CosServiceTestSuite))
}
