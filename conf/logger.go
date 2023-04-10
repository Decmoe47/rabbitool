package conf

type Logger struct {
	ConsoleLevel string `yaml:"consoleLevel"`
	FileLevel    string `yaml:"fileLevel"`
}
