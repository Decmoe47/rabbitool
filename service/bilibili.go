package service

import (
	"context"
	"strconv"
	"strings"
	"time"

	jv "github.com/Andrew-M-C/go.jsonvalue"
	dto "github.com/Decmoe47/rabbitool/dto/bilibili"
	"github.com/Decmoe47/rabbitool/errx"
	"github.com/Decmoe47/rabbitool/util"
	"github.com/Decmoe47/rabbitool/util/req"
	"github.com/cockroachdb/errors"
	ua "github.com/wux1an/fake-useragent"
	"golang.org/x/time/rate"

	"github.com/samber/lo"
)

type BilibiliService struct {
	limiter *rate.Limiter
}

func NewBilibiliService() *BilibiliService {
	return &BilibiliService{limiter: rate.NewLimiter(rate.Every(time.Second), 1)}
}

func (b *BilibiliService) RefreshCookies(ctx context.Context) error {
	resp, err := req.Client.R().
		SetContext(ctx).
		SetHeader("User-Agent", ua.Random()).
		Get("https://bilibili.com")
	if err != nil {
		return errors.WithStack(err)
	}

	req.Client.SetCommonCookies(resp.Cookies()...)
	return nil
}

func (b *BilibiliService) GetLive(ctx context.Context, uid uint) (*dto.Live, error) {
	err := b.limiter.Wait(ctx)
	if err != nil {
		return nil, errors.WithStack(err)
	}

	resp, err := req.Client.R().
		SetContext(ctx).
		SetHeader("User-Agent", ua.Random()).
		SetQueryParam("mid", strconv.FormatUint(uint64(uid), 10)).
		Get("https://api.bilibili.com/x/space/acc/info")
	if err != nil {
		return nil, errors.WithStack(err)
	}
	body, err := jv.UnmarshalString(resp.String())
	if err != nil {
		return nil, errors.WithStack(err)
	}

	if code, err := body.GetInt("code"); err != nil {
		return nil, errors.WithStack(err)
	} else if code != 0 {
		return nil, errors.Wrapf(
			errx.ErrBilibiliApi,
			"Failed to get the info from the bilibili user(uid: %d)!\nCode: %d\nBody: %s",
			code, code, body.String(),
		)
	}

	uname, err := body.GetString("data", "name")
	if err != nil {
		return nil, errors.WithStack(err)
	}
	roomId, err := body.GetUint("data", "live_room", "roomid")
	if err != nil {
		return nil, errors.WithStack(err)
	}

	resp2, err := req.Client.R().
		SetContext(ctx).
		SetHeader("User-Agent", ua.Random()).
		SetQueryParam("room_id", strconv.FormatUint(uint64(roomId), 10)).
		Get("https://api.live.bilibili.com/xlive/web-room/v1/index/getInfoByRoom")
	if err != nil {
		return nil, errors.WithStack(err)
	}
	body2, err := jv.UnmarshalString(resp2.String())
	if err != nil {
		return nil, errors.WithStack(err)
	}

	if code, err := body2.GetInt("code"); err != nil {
		return nil, errors.WithStack(err)
	} else if code != 0 {
		return nil, errors.Wrapf(
			errx.ErrBilibiliApi,
			"Failed to get the info from the bilibili user(uid: %d)!\nCode: %d\nBody: %s",
			code, code, body.String(),
		)
	}

	liveStatus, err := body2.GetInt("data", "room_info", "live_status")
	if err != nil {
		return nil, errors.WithStack(err)
	}

	switch liveStatus {
	case int(dto.EnumNoLiveStream):
		return &dto.Live{
			Uid:        uid,
			Uname:      uname,
			RoomId:     roomId,
			LiveStatus: dto.EnumNoLiveStream,
		}, nil
	case int(dto.EnumReplay):
		return &dto.Live{
			Uid:        uid,
			Uname:      uname,
			RoomId:     roomId,
			LiveStatus: dto.EnumReplay,
		}, nil
	case int(dto.EnumStreaming):
		title, err := body2.GetString("data", "room_info", "title")
		if err != nil {
			return nil, errors.WithStack(err)
		}
		coverUrl, err := body2.GetString("data", "room_info", "cover")
		if err != nil {
			return nil, errors.WithStack(err)
		}
		liveStartTime := time.Now().In(util.CST())

		return &dto.Live{
			Uid:           uid,
			Uname:         uname,
			RoomId:        roomId,
			LiveStatus:    dto.EnumStreaming,
			Title:         title,
			LiveStartTime: &liveStartTime,
			CoverUrl:      coverUrl,
		}, nil
	default:
		return nil, errors.Wrapf(
			errx.ErrNotSupported,
			"Unknown live status %s from the bilibili user %s(uid: %d)",
			liveStatus, uname, uid,
		)
	}
}

// @param offsetDynamic - 建议0
//
// @param needTop - 建议0
func (b *BilibiliService) GetLatestDynamic(ctx context.Context, uid uint, offsetDynamic int, needTop int) (
	dto.IDynamic,
	error,
) {
	err := b.limiter.Wait(ctx)
	if err != nil {
		return nil, errors.WithStack(err)
	}

	resp, err := req.Client.R().
		SetContext(ctx).
		SetHeader("User-Agent", ua.Random()).
		SetQueryParams(map[string]string{
			"offsetDynamic": strconv.Itoa(offsetDynamic),
			"needTop":       strconv.Itoa(needTop),
			"host_uid":      strconv.FormatUint(uint64(uid), 10),
		}).
		Get("https://api.vc.bilibili.com/dynamic_svr/v1/dynamic_svr/space_history")
	if err != nil {
		return nil, errors.WithStack(err)
	}
	body, err := jv.UnmarshalString(resp.String())
	if err != nil {
		return nil, errors.WithStack(err)
	}

	if code, err := body.GetInt("code"); err != nil {
		return nil, errors.WithStack(err)
	} else if code != 0 {
		return nil, errors.Wrapf(
			errx.ErrBilibiliApi,
			"Failed to get the info from the bilibili user(uid: %d)!\nCode: %d\nBody: %s",
			uid, code, body.String(),
		)
	}

	firstDynamic, err := body.Get("data", "cards", 0)
	if errors.Is(err, jv.ErrNotFound) {
		return nil, err
	} else if err != nil {
		return nil, errors.WithStack(err)
	}

	err = b.unmarshalCard(firstDynamic)
	if err != nil {
		return nil, errors.WithMessagef(err, "Failed to unmarshal the card for uid: %d", uid)
	}

	dynamicTypeInt, err := firstDynamic.GetInt("desc", "type")
	if err != nil {
		return nil, errors.WithStack(err)
	}
	dynamicType := dto.DynamicTypeEnum(dynamicTypeInt)

	switch dynamicType {
	case dto.EnumTextOnly, dto.EnumWithImage, dto.EnumWebActivity:
		return b.toCommonDynamic(ctx, firstDynamic)
	case dto.EnumVideo:
		return b.toVideoDynamic(ctx, firstDynamic)
	case dto.EnumArticle:
		return b.toArticleDynamic(ctx, firstDynamic)
	case dto.EnumForward:
		return b.toForwardDynamic(ctx, firstDynamic)
	default:
		return nil, errors.Wrapf(
			errx.ErrNotSupported,
			"Not supported dynamic type %d\\nDynamic: %s",
			dynamicType, firstDynamic.String(),
		)
	}
}

func (b *BilibiliService) unmarshalCard(dynamic *jv.V) error {
	cardStr, err := dynamic.GetString("card")
	if err != nil {
		return errors.WithStack(err)
	}

	card, err := jv.UnmarshalString(cardStr)
	if err != nil {
		return errors.WithStack(err)
	}

	if originStr, err := card.GetString("origin"); err == nil {
		origin, err := jv.UnmarshalString(originStr)
		if err != nil {
			return errors.WithStack(err)
		}
		_, err = card.Set(origin).At("origin")
		if err != nil {
			return errors.WithStack(err)
		}
	}

	if originExtendJsonStr, err := card.GetString("origin_extend_json"); err == nil {
		originExtendJson, err := jv.UnmarshalString(originExtendJsonStr)
		if err != nil {
			return errors.WithStack(err)
		}
		_, err = card.Set(originExtendJson).At("origin_extend_json")
		if err != nil {
			return errors.WithStack(err)
		}
	}

	if ctrlStr, err := card.GetString("ctrl"); err == nil {
		ctrl, err := jv.UnmarshalString(ctrlStr)
		if err != nil {
			return errors.WithStack(err)
		}
		_, err = card.Set(ctrl).At("ctrl")
		if err != nil {
			return errors.WithStack(err)
		}
	}

	_, err = dynamic.Set(card).At("card")
	if err != nil {
		return errors.WithStack(err)
	}

	return nil
}

func (b *BilibiliService) getBaseDynamic(
	ctx context.Context,
	dynamic *jv.V,
	dynamicType dto.DynamicTypeEnum,
	origin bool,
) (
	*dto.BaseDynamic,
	error,
) {
	var (
		uid               uint
		uname             string
		dynamicId         string
		dynamicUploadTime time.Time
		err               error
	)

	if origin {
		uid, err = dynamic.GetUint("desc", "origin", "uid")
		if err != nil {
			return nil, errors.WithStack(err)
		}
		uname, err = b.getUname(ctx, uid)
		if err != nil {
			return nil, err
		}
		dynamicId, err = dynamic.GetString("desc", "origin", "dynamic_id_str")
		if err != nil {
			return nil, errors.WithStack(err)
		}
		dynamicUploadTimeStamp, err := dynamic.GetInt64("desc", "origin", "timestamp")
		if err != nil {
			return nil, errors.WithStack(err)
		}
		dynamicUploadTime = time.Unix(dynamicUploadTimeStamp, 0).UTC()
	} else {
		uid, err = dynamic.GetUint("desc", "uid")
		if err != nil {
			return nil, errors.WithStack(err)
		}
		uname, err = dynamic.GetString("desc", "user_profile", "info", "uname")
		if err != nil {
			return nil, errors.WithStack(err)
		}
		dynamicId, err = dynamic.GetString("desc", "dynamic_id_str")
		if err != nil {
			return nil, errors.WithStack(err)
		}
		dynamicUploadTimeStamp, err := dynamic.GetInt64("desc", "timestamp")
		if err != nil {
			return nil, errors.WithStack(err)
		}
		dynamicUploadTime = time.Unix(dynamicUploadTimeStamp, 0).UTC()
	}

	return &dto.BaseDynamic{
		Uid:               uid,
		Uname:             uname,
		DynamicType:       dynamicType,
		DynamicId:         dynamicId,
		DynamicUrl:        "https://t.bilibili.com/" + dynamicId,
		DynamicUploadTime: &dynamicUploadTime,
	}, nil
}

func (b *BilibiliService) getUname(ctx context.Context, uid uint) (string, error) {
	err := b.limiter.Wait(ctx)
	if err != nil {
		return "", errors.WithStack(err)
	}

	resp, err := req.Client.R().
		SetContext(ctx).
		SetHeader("User-Agent", ua.Random()).
		SetQueryParam("mid", strconv.FormatUint(uint64(uid), 10)).
		Get("https://api.bilibili.com/x/space/acc/info")
	if err != nil {
		return "", errors.WithStack(err)
	}
	body, err := jv.UnmarshalString(resp.String())
	if err != nil {
		return "", errors.WithStack(err)
	}

	if code, err := body.GetInt("code"); err != nil {
		return "", errors.WithStack(err)
	} else if code != 0 {
		return "", errors.Wrapf(
			errx.ErrBilibiliApi,
			"Failed to get the uname of uid %d\nCode: %d\nBody: %s",
			uid, code, body.String(),
		)
	}

	uname, err := body.GetString("data", "name")
	if err != nil {
		return "", errors.WithStack(err)
	}
	return uname, nil
}

func (b *BilibiliService) toCommonDynamic(ctx context.Context, dynamic *jv.V) (*dto.CommonDynamic, error) {
	var imgUrls []string
	if pics, err := dynamic.Get("card", "item", "pictures"); err == nil {
		for _, v := range pics.ForRangeArr() {
			if src, err := v.GetString("img_src"); err == nil {
				imgUrls = append(imgUrls, src)
			}
		}
	}

	base, err := b.getBaseDynamic(ctx, dynamic, lo.Ternary(imgUrls == nil, dto.EnumTextOnly, dto.EnumWithImage), false)
	if err != nil {
		return nil, err
	}
	text, err := dynamic.GetString("card", "item", "description")
	if errors.Is(err, jv.ErrNotFound) {
		text, err = dynamic.GetString("card", "item", "content")
		if err != nil {
			return nil, errors.WithStack(err)
		}
	} else if err != nil {
		return nil, errors.WithStack(err)
	}
	reserve, err := b.getReserve(dynamic)
	if err != nil {
		return nil, err
	}

	return &dto.CommonDynamic{
		BaseDynamic: base,
		Text:        text,
		ImageUrls:   imgUrls,
		Reserve:     reserve,
	}, nil
}

func (b *BilibiliService) getReserve(dynamic *jv.V) (*dto.Reserve, error) {
	if addOnCardInfo, err := dynamic.GetInt("display", "add_on_card_info", 0,
		"add_on_card_show_type"); err == nil && addOnCardInfo == 6 {
		title, err := dynamic.GetString("display", "origin", "add_on_card_info", 0, "reserve_attach_card", "title")
		if err != nil {
			return nil, errors.WithStack(err)
		}

		liveStartTimeStamp, err := dynamic.GetInt64("display", "origin", "add_on_card_info", 0, "reserve_attach_card",
			"livePlanStartTime")
		if err != nil {
			return nil, errors.WithStack(err)
		}
		liveStartTime := time.Unix(liveStartTimeStamp, 0).UTC()

		return &dto.Reserve{
			Title:     title,
			StartTime: &liveStartTime,
		}, nil
	} else if addOnCardShowType, err := dynamic.GetInt("display", "add_on_card_info", 0,
		"add_on_card_show_type"); err == nil && addOnCardShowType == 6 {
		title, err := dynamic.GetString("display", "add_on_card_info", 0, "reserve_attach_card", "title")
		if err != nil {
			return nil, errors.WithStack(err)
		}

		liveStartTimeStamp, err := dynamic.GetInt64("display", "add_on_card_info", 0, "reserve_attach_card",
			"livePlanStartTime")
		if err != nil {
			return nil, errors.WithStack(err)
		}
		liveStartTime := time.Unix(liveStartTimeStamp, 0).UTC()

		return &dto.Reserve{
			Title:     title,
			StartTime: &liveStartTime,
		}, nil
	} else {
		return nil, nil
	}
}

func (b *BilibiliService) toVideoDynamic(ctx context.Context, dynamic *jv.V) (*dto.VideoDynamic, error) {
	base, err := b.getBaseDynamic(ctx, dynamic, dto.EnumVideo, false)
	if err != nil {
		return nil, err
	}
	title, err := dynamic.GetString("card", "title")
	if err != nil {
		return nil, errors.WithStack(err)
	}
	thumbnailUrl, err := dynamic.GetString("card", "pic")
	if err != nil {
		return nil, errors.WithStack(err)
	}
	url, err := dynamic.GetString("card", "short_link")
	if err != nil {
		return nil, errors.WithStack(err)
	}

	return &dto.VideoDynamic{
		BaseDynamic:       base,
		VideoTitle:        title,
		VideoThumbnailUrl: thumbnailUrl,
		VideoUrl:          url,
	}, nil
}

func (b *BilibiliService) toArticleDynamic(ctx context.Context, dynamic *jv.V) (*dto.ArticleDynamic, error) {
	base, err := b.getBaseDynamic(ctx, dynamic, dto.EnumArticle, false)
	if err != nil {
		return nil, err
	}
	title, err := dynamic.GetString("card", "title")
	if err != nil {
		return nil, errors.WithStack(err)
	}
	thumbnailUrl, err := dynamic.GetString("card", "image_urls", 0)
	if err != nil {
		return nil, errors.WithStack(err)
	}
	articleId, err := dynamic.GetString("card", "id")
	if err != nil {
		return nil, errors.WithStack(err)
	}

	return &dto.ArticleDynamic{
		BaseDynamic:         base,
		ArticleTitle:        title,
		ArticleThumbnailUrl: thumbnailUrl,
		ArticleUrl:          "https://www.bilibili.com/read/cv" + articleId,
	}, nil
}

func (b *BilibiliService) toForwardDynamic(ctx context.Context, dynamic *jv.V) (*dto.ForwardDynamic, error) {
	dynamicText, err := dynamic.GetString("card", "item", "content")
	if err != nil {
		return nil, errors.WithStack(err)
	}
	dynamicType := lo.Ternary(b.isPureForwardDynamic(dynamicText), dto.EnumPureForward, dto.EnumForward)
	base, err := b.getBaseDynamic(ctx, dynamic, dynamicType, false)
	if err != nil {
		return nil, err
	}

	if tips, err := dynamic.GetString("card", "item", "tips"); err == nil && tips == "源动态已被作者删除" {
		return &dto.ForwardDynamic{
			BaseDynamic: base,
			DynamicText: dynamicText,
			Origin:      "源动态已被作者删除",
		}, nil
	}

	var origin any

	originDynamicTypeInt, err := dynamic.GetInt("desc", "origin", "type")
	if err != nil {
		return nil, errors.WithStack(err)
	}
	originDynamicType := dto.DynamicTypeEnum(originDynamicTypeInt)
	originBase, err := b.getBaseDynamic(ctx, dynamic, originDynamicType, true)
	if err != nil {
		return nil, err
	}

	switch originDynamicType {
	case dto.EnumTextOnly, dto.EnumWithImage, dto.EnumWebActivity:
		var imgUrls []string
		if pics, err := dynamic.Get("card", "origin", "item", "pictures"); err == nil {
			for _, v := range pics.ForRangeArr() {
				if src, err := v.GetString("img_src"); err == nil {
					imgUrls = append(imgUrls, src)
				}
			}
		}

		text, err := dynamic.GetString("card", "origin", "item", "description")
		if errors.Is(err, jv.ErrNotFound) {
			text, err = dynamic.GetString("card", "origin", "item", "content")
			if err != nil {
				return nil, errors.WithStack(err)
			}
		} else if err != nil {
			return nil, errors.WithStack(err)
		}
		reserve, err := b.getReserve(dynamic)
		if err != nil {
			return nil, err
		}

		origin = &dto.CommonDynamic{
			BaseDynamic: originBase,
			Text:        text,
			ImageUrls:   imgUrls,
			Reserve:     reserve,
		}
	case dto.EnumVideo:
		title, err := dynamic.GetString("card", "origin", "title")
		if err != nil {
			return nil, errors.WithStack(err)
		}
		thumbnailUrl, err := dynamic.GetString("card", "origin", "pic")
		if err != nil {
			return nil, errors.WithStack(err)
		}
		url, err := dynamic.GetString("card", "origin", "short_link_v2")
		if err != nil {
			return nil, errors.WithStack(err)
		}

		origin = &dto.VideoDynamic{
			BaseDynamic:       originBase,
			VideoTitle:        title,
			VideoThumbnailUrl: thumbnailUrl,
			VideoUrl:          url,
		}
	case dto.EnumArticle:
		title, err := dynamic.GetString("card", "origin", "title")
		if err != nil {
			return nil, errors.WithStack(err)
		}
		thumbnailUrl, err := dynamic.GetString("card", "origin", "origin_image_urls", 0)
		if err != nil {
			return nil, errors.WithStack(err)
		}
		articleId, err := dynamic.GetString("card", "origin", "id")
		if err != nil {
			return nil, errors.WithStack(err)
		}

		origin = &dto.ArticleDynamic{
			BaseDynamic:         originBase,
			ArticleTitle:        title,
			ArticleThumbnailUrl: thumbnailUrl,
			ArticleUrl:          "https://www.bilibili.com/read/cv" + articleId,
		}
	case dto.EnumLiveCard:
		roomId, err := dynamic.GetUint("card", "origin", "roomid")
		if err != nil {
			return nil, errors.WithStack(err)
		}
		liveStatusInt, err := dynamic.GetInt("card", "origin", "live_status")
		if err != nil {
			return nil, errors.WithStack(err)
		}
		liveStatus := dto.LiveStatusEnum(liveStatusInt)
		liveStartTimestamp, err := dynamic.GetInt64("card", "origin", "first_live_time")
		liveStartTime := time.Unix(liveStartTimestamp, 0).UTC()
		title, err := dynamic.GetString("card", "origin", "title")
		if err != nil {
			return nil, errors.WithStack(err)
		}
		coverUrl, err := dynamic.GetString("card", "origin", "cover")
		if err != nil {
			return nil, errors.WithStack(err)
		}

		origin = &dto.LiveCardDynamic{
			BaseDynamic:   originBase,
			RoomId:        roomId,
			LiveStatus:    liveStatus,
			LiveStartTime: &liveStartTime,
			Title:         title,
			CoverUrl:      coverUrl,
		}
	default:
		return nil, errors.Wrapf(
			errx.ErrNotSupported,
			"Not supported origin dynamic type %d for origin dynamic from the bilibili user (uid: %d)!",
			dynamicType, originBase.Uid,
		)
	}

	return &dto.ForwardDynamic{
		BaseDynamic: base,
		DynamicText: dynamicText,
		Origin:      origin,
	}, nil
}

func (b *BilibiliService) isPureForwardDynamic(dynamicText string) bool {
	return dynamicText == "转发动态" || strings.HasPrefix(strings.Split(dynamicText, "//")[0], "@")
}
