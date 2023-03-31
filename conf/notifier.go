package conf

type Notifier struct {
	Host     string   `yaml:"host"`
	Port     int      `yaml:"port"`
	Ssl      bool     `yaml:"ssl"`
	UserName string   `yaml:"userName"`
	Password string   `yaml:"password"`
	From     string   `yaml:"from"`
	To       []string `yaml:"to"`

	IntervalMinutes int64 `yaml:"intervalMinutes"`
	AllowedAmount   int   `yaml:"allowedAmount"`
}
