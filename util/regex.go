package util

import "regexp"

var (
	RegexUrl         = regexp.MustCompile(`(http|https)://[\w\-_]+(\.[\w\-_]+)+([\w\-.,@?^=%&:/~+#]*[\w\-@?^=%&/~+#])?`)
	RegexMailAddress = regexp.MustCompile(`[A-Za-z0-9-_]+@[a-zA-Z0-9_-]+(\.[a-zA-Z0-9_-]+)`)
)
