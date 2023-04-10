package conf

type QQBot struct {
	AppId            uint64 `yaml:"appId"`
	Token            string `yaml:"token"`
	IsSandbox        bool   `yaml:"isSandbox"`
	SandboxGuildName string `yaml:"sandboxGuildName"`

	Logger *Logger `yaml:"logger,omitempty"`
}
