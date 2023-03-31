package plugin

import (
	"context"

	"github.com/go-co-op/gocron"
)

type IPlugin interface {
	init(ctx context.Context, sch *gocron.Scheduler) error
}

type IRunnablePlugin interface {
	IPlugin

	run(ctx context.Context) error
}
