package forum

type Paragraph struct {
	Elems []*Elem         `json:"elems,omitempty"`
	Props *ParagraphProps `json:"props"`
}
