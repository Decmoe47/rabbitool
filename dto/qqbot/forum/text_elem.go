package forum

type TextElem struct {
	Text  string     `json:"text"`
	Props *TextProps `json:",omitempty"`
}

type TextProps struct {
	FontBold  bool `json:"font_bold,omitempty"`
	Italic    bool `json:"italic,omitempty"`
	Underline bool `json:"underline,omitempty"`
}
