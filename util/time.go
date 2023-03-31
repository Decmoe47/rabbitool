package util

import "time"

func CST() *time.Location {
	tz, err := time.LoadLocation("Asia/Shanghai")
	if err != nil {
		panic(err)
	}
	return tz
}

func UnixDefaultTime() *time.Time {
	t := time.Date(1970, 1, 1, 0, 0, 0, 0, time.UTC)
	return &t
}
