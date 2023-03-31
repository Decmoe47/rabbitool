package forum

type AlignmentEnum int

const (
	Left AlignmentEnum = iota
	Middle
	Right
)

type ParagraphProps struct {
	Alignment AlignmentEnum `json:"alignment"`
}
