package service

import (
	"context"
	"testing"

	"github.com/Decmoe47/rabbitool/conf"
	"github.com/samber/lo"
	"github.com/stretchr/testify/require"
	"github.com/stretchr/testify/suite"
	"github.com/tencent-connect/botgo/dto"
)

type QQBotServiceTestSuite struct {
	suite.Suite
	ctx context.Context
	q   *QQBotService
}

func (suite *QQBotServiceTestSuite) SetupTest() {
	suite.ctx = context.Background()

	err := conf.Load("../configs.yml")
	require.NoError(suite.T(), err)

	suite.q, err = NewQQBotService(suite.ctx)
	require.NoError(suite.T(), err)

	err = suite.q.Run(suite.ctx)
	require.NoError(suite.T(), err)
}

func (suite *QQBotServiceTestSuite) TestPostMessage() {
	channels, err := suite.q.GetAllChannels(suite.ctx, suite.q.sandboxGuildId)
	require.NoError(suite.T(), err)

	channel, _ := lo.Find(channels, func(item *dto.Channel) bool {
		return item.Name == "默认"
	})

	_, err = suite.q.postMessage(suite.ctx, channel.ID, &dto.MessageToCreate{
		Content: "【新动态】来自 物述有栖Official\n\n4/6\r\n看来今天参加开学典礼的人很多\r\n春假真是一晃而过呢\r\n\r\n祝不管是过着真正新生活的小兔子们，还是生活没有发生变化的小兔子们，都有美好的一年₍ᐢ｡•༝•｡ᐢ₎❣\r\n\r\n而且今天好像是健身日，或许缓解运动不足是个不错的选择(-°ᵕ°-)\n——————————\n动态发布时间：2023-04-06 10:56:07 CST\n动态链接：https://redirect-2g1tb8d680f7fddc-1302910426.ap-shanghai.app.tcloudbase.com/to/?url=https://t.bilibili.com/781303372240125955\n图片：",
		Image:   "https://rabbitool.oss-cn-shanghai.aliyuncs.com/data/images/Fs4_nBVaAAAuNmd.jpg",
	})
	require.NoError(suite.T(), err)
}

func TestQQBotService(t *testing.T) {
	suite.Run(t, new(QQBotServiceTestSuite))
}
