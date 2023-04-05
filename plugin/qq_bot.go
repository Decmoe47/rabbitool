package plugin

import (
	"context"

	"github.com/Decmoe47/rabbitool/plugin/command"
	"github.com/Decmoe47/rabbitool/service"
	"github.com/go-co-op/gocron"
)

type QQBotPlugin struct {
	svc *service.QQBotService
}

func NewQQBotPlugin(base *PluginBase) *QQBotPlugin {
	return &QQBotPlugin{svc: base.qbSvc}
}

func (q *QQBotPlugin) init(ctx context.Context, _ *gocron.Scheduler) error {
	q.svc.RegisterAtMessageEvent(ctx, command.GenerateReplyMsg)
	return nil
}

func (q *QQBotPlugin) run(ctx context.Context) (err error) {
	return q.svc.Run(ctx)
}
