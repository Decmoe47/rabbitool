package dto

import (
	"context"

	"github.com/tencent-connect/botgo/dto"
)

type SubscribeCommand struct {
	Cmd         string
	Platform    string
	SubscribeId string // nullable
	QQChannel   *SubscribeCommandQQChannel
	Configs     map[string]any // nullable
}

type SubscribeCommandQQChannel struct {
	GuildId   string
	GuildName string
	Id        string
	Name      string
}

type CommandInfo struct {
	Name      string
	Format    []string
	Example   string
	Responder func(context.Context, []string, *dto.WSATMessageData) string
}
