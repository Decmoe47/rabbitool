package bilibili

import "time"

type Live struct {
	Uid    uint
	Uname  string
	RoomId uint

	LiveStatus    LiveStatusEnum
	LiveStartTime *time.Time
	Title         string // nullable
	CoverUrl      string // nullable
}
