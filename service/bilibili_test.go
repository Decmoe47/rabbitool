package service

import (
	"context"
	"strconv"
	"testing"
	"time"

	"github.com/Decmoe47/rabbitool/conf"
	"github.com/Decmoe47/rabbitool/util/req"
	"github.com/stretchr/testify/assert"
	"github.com/stretchr/testify/require"
	"github.com/stretchr/testify/suite"
)

type BilibiliServiceTestSuite struct {
	suite.Suite
	ctx context.Context
	b   *BilibiliService

	uidList []uint
}

func (suite *BilibiliServiceTestSuite) SetupTest() {
	suite.ctx = context.Background()
	req.InitClient(time.Second * 10)

	err := conf.Load("../configs.yml")
	require.NoError(suite.T(), err)

	suite.b = NewBilibiliService()
	err = suite.b.RefreshCookies(suite.ctx)
	require.NoError(suite.T(), err)

	suite.uidList = []uint{509069409, 434565011}
}

func (suite *BilibiliServiceTestSuite) TestGetLive() {
	for _, uid := range suite.uidList {
		suite.T().Run(strconv.FormatUint(uint64(uid), 10), func(t *testing.T) {
			_, err := suite.b.GetLive(suite.ctx, uid)
			assert.NoError(suite.T(), err)
		})
	}
}

func (suite *BilibiliServiceTestSuite) TestGetLatestDynamic() {
	for _, uid := range suite.uidList {
		suite.T().Run(strconv.FormatUint(uint64(uid), 10), func(t *testing.T) {
			_, err := suite.b.GetLatestDynamic(suite.ctx, uid, 0, 0)
			assert.NoError(suite.T(), err)
		})
	}
}

func TestBilibiliService(t *testing.T) {
	suite.Run(t, new(BilibiliServiceTestSuite))
}
