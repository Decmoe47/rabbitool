package forum

type ElemTypeEnum int

const (
	Text ElemTypeEnum = iota + 1
	Image
	Video
	Url
)

type Elem struct {
	Text  *TextElem    `json:"text,omitempty"`
	Image *ImageElem   `json:"image,omitempty"`
	Video *VideoElem   `json:"video,omitempty"`
	Url   *UrlElem     `json:"url,omitempty"`
	Type  ElemTypeEnum `json:"type"`
}
