package req

import (
	"time"

	"github.com/imroc/req/v3"
)

var Client *req.Client

func InitClient(timeout time.Duration) {
	Client = req.NewClient().SetTimeout(timeout)
}
