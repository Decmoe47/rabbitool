package command

import (
	"context"
	"fmt"
	"strings"

	"github.com/Decmoe47/rabbitool/dto"
	"github.com/Decmoe47/rabbitool/plugin/command/subscribe"
	"github.com/samber/lo"
	qqBotDto "github.com/tencent-connect/botgo/dto"
)

var (
	allCommands = subscribe.AllSubscribeCommands
)

func GenerateReplyMsg(ctx context.Context, msg *qqBotDto.WSATMessageData) string {
	msgList := strings.Split(strings.ReplaceAll(msg.Content, "\xa0", " "), " ")
	cmd := lo.Reject(msgList, func(s string, _ int) bool {
		return s == ""
	})
	cmd = lo.Drop(cmd, 1)

	if len(cmd) == 0 {
		return "错误：指令错误！\n输入 /帮助 获取指令列表"
	}

	if cmd[0] == "/帮助" {
		cmdStr := ""
		for _, v := range allCommands {
			cmdStr += strings.Join(v.Format, " ") + "\n"
		}
		return fmt.Sprintf("支持的命令：\n%s\n详细设置请前往项目主页。", cmdStr)
	}

	cmdInfo, ok := lo.Find(allCommands, func(c *dto.CommandInfo) bool {
		return c.Name == cmd[0]
	})
	if !ok {
		return "错误：指令错误！\n输入 /帮助 获取指令列表"
	}

	return cmdInfo.Responder(ctx, cmd, msg)
}
