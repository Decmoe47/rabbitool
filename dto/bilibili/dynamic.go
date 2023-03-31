package bilibili

import "time"

type IDynamic interface {
	GetUid() uint
	GetUname() string
	GetDynamicType() DynamicTypeEnum
	GetDynamicUploadTime() *time.Time
}

type BaseDynamic struct {
	Uid               uint
	Uname             string
	DynamicType       DynamicTypeEnum
	DynamicId         string
	DynamicUrl        string
	DynamicUploadTime *time.Time
}

func (b *BaseDynamic) GetUid() uint                     { return b.Uid }
func (b *BaseDynamic) GetUname() string                 { return b.Uname }
func (b *BaseDynamic) GetDynamicType() DynamicTypeEnum  { return b.DynamicType }
func (b *BaseDynamic) GetDynamicUploadTime() *time.Time { return b.DynamicUploadTime }

type CommonDynamic struct {
	*BaseDynamic

	Text      string
	ImageUrls []string
	Reserve   *Reserve
}

type Reserve struct {
	Title     string
	StartTime *time.Time
}

type VideoDynamic struct {
	*BaseDynamic

	DynamicText       string
	VideoTitle        string
	VideoThumbnailUrl string
	VideoUrl          string
}

type ArticleDynamic struct {
	*BaseDynamic

	ArticleTitle        string
	ArticleThumbnailUrl string
	ArticleUrl          string
}

type ForwardDynamic struct {
	*BaseDynamic

	DynamicText string
	Origin      any // 可以是 CommonDynamic | VideoDynamic | ArticleDynamic | string
}

type LiveCardDynamic struct {
	*BaseDynamic

	RoomId        uint
	LiveStatus    LiveStatusEnum
	LiveStartTime *time.Time
	Title         string
	CoverUrl      string
}
