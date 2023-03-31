package plugin

import "github.com/Decmoe47/rabbitool/service"

type PluginBase struct {
	qbSvc    *service.QQBotService
	uploader *service.UploaderService
}

func newPluginBase(qbSvc *service.QQBotService, uploader *service.UploaderService) *PluginBase {
	return &PluginBase{qbSvc: qbSvc, uploader: uploader}
}
