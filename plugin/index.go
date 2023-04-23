package plugin

import (
	"context"
	"os"
	"os/signal"
	"syscall"
	"time"

	"github.com/Decmoe47/rabbitool/errx"
	"github.com/Decmoe47/rabbitool/event"
	command "github.com/Decmoe47/rabbitool/plugin/command/subscribe"
	"github.com/Decmoe47/rabbitool/service"
	"github.com/go-co-op/gocron"
	"github.com/rs/zerolog/log"
)

type PluginLoader struct {
	Sch     *gocron.Scheduler
	Plugins []IPlugin
}

func NewPluginLoader(ctx context.Context) (*PluginLoader, *PluginBase, error) {
	qbSvc, err := service.NewQQBotService(ctx)
	if err != nil {
		return nil, nil, err
	}
	uploader, err := service.NewUploaderService()
	if err != nil {
		return nil, nil, err
	}
	base := newPluginBase(qbSvc, uploader)

	command.InitSubscribeCommandHandler(qbSvc)

	sch := gocron.NewScheduler(time.UTC)

	return &PluginLoader{
		Sch: sch,
	}, base, nil
}

func (p *PluginLoader) Load(plg ...IPlugin) {
	p.Plugins = append(p.Plugins, plg...)
}

func (p *PluginLoader) Run(ctx context.Context, cancel context.CancelFunc) (errs error) {
	exitChan := make(chan os.Signal)
	signal.Notify(exitChan, os.Interrupt, os.Kill, syscall.SIGTERM)
	go p.shutdownListener(exitChan, cancel)

	for _, plugin := range p.Plugins {
		err := plugin.init(ctx, p.Sch)
		errs = errx.Join(errs, err)

		if plg, ok := plugin.(IRunnablePlugin); ok {
			err := plg.run(ctx)
			errs = errx.Join(errs, err)
		}
	}
	if errs != nil {
		return errs
	}

	p.Sch.SingletonModeAll()
	p.Sch.StartBlocking()
	return nil
}

func (p *PluginLoader) shutdownListener(exitChan chan os.Signal, cancel context.CancelFunc) {
	for range time.Tick(time.Millisecond * 100) {
		select {
		case <-exitChan:
			log.Warn().Msg("Shutting down...")

			cancel()

			err := event.OnCancelKeyPressed()
			if err != nil {
				log.Error().Stack().Err(err).Msg(err.Error())
			}

			os.Exit(1)
		}
	}
}
