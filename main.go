package main

import (
	"context"
	"fmt"
	"net/http"
	_ "net/http/pprof"
	"time"

	"github.com/Decmoe47/rabbitool/conf"
	"github.com/Decmoe47/rabbitool/dao"
	"github.com/Decmoe47/rabbitool/plugin"
	"github.com/Decmoe47/rabbitool/util"
	"github.com/Decmoe47/rabbitool/util/req"
	"github.com/rs/zerolog/log"
	"gopkg.in/natefinch/lumberjack.v2"
)

func main() {
	ctx, cancel := context.WithCancel(context.Background())

	err := conf.Load("./configs.yml")
	if err != nil {
		panic(err)
	}

	if conf.R.PprofListenerPort != 0 {
		go func() {
			err := http.ListenAndServe(fmt.Sprintf(":%d", conf.R.PprofListenerPort), nil)
			if err != nil {
				panic(err)
			}
		}()
	}

	logger, err := util.InitLogger(&util.InitLoggerOptions{
		Global:       true,
		ConsoleLevel: conf.R.DefaultLogger.ConsoleLevel,
		FileLevel:    conf.R.DefaultLogger.FileLevel,
		FileOpts: &lumberjack.Logger{
			Filename:   "log/rabbitool.log",
			MaxSize:    1,
			MaxAge:     30,
			MaxBackups: 5,
			LocalTime:  false,
			Compress:   false,
		},
	})
	if err != nil {
		panic(err)
	}
	log.Logger = *logger

	err = dao.InitDb(conf.R.Gorm.DbPath)
	if err != nil {
		panic(err)
	}
	req.InitClient(time.Second * 10)

	loader, base, err := plugin.NewPluginLoader(ctx)
	if err != nil {
		panic(err)
	}
	loader.Load(
		plugin.NewQQBotPlugin(base),
		plugin.NewBilibiliPlugin(base),
		plugin.NewMailPlugin(base),
	)

	if conf.R.Twitter != nil {
		loader.Load(plugin.NewTwitterPlugin(base))
	}
	if conf.R.Youtube != nil {
		ytbPlugin, err := plugin.NewYoutubePlugin(ctx, base)
		if err != nil {
			panic(err)
		}
		loader.Load(ytbPlugin)
	}

	err = loader.Run(ctx, cancel)
	if err != nil {
		panic(err)
	}
}
