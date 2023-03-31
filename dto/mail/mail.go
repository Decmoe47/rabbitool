package mail

import "time"

type Mail struct {
	From    []*AddressInfo
	To      []*AddressInfo
	Subject string
	Time    *time.Time
	Text    string
}

type AddressInfo struct {
	Address string
	Name    string
}
