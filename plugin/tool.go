package plugin

import (
	"github.com/Decmoe47/rabbitool/conf"
	"github.com/Decmoe47/rabbitool/util"
)

func addRedirectToUrls(text string) string {
	if text == "" {
		return "（无文本）"
	} else {
		return util.RegexUrl.ReplaceAllStringFunc(text, func(s string) string {
			return conf.R.RedirectUrl + s
		})
	}
}
