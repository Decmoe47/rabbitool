package conf

type Gorm struct {
	DbPath string  `yaml:"dbPath"`
	Logger *Logger `yaml:"logger,omitempty"`
}
