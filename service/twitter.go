package service

import (
	"context"
	"strings"
	"time"

	jv "github.com/Andrew-M-C/go.jsonvalue"
	"github.com/Decmoe47/rabbitool/conf"
	dto "github.com/Decmoe47/rabbitool/dto/twitter"
	"github.com/Decmoe47/rabbitool/errx"
	"github.com/Decmoe47/rabbitool/util/req"
	"github.com/cockroachdb/errors"
	"github.com/rs/zerolog/log"
	"github.com/samber/lo"
	"golang.org/x/time/rate"
)

type TwitterService struct {
	userApiLimiter *rate.Limiter
	// See https://developer.twitter.com/en/docs/twitter-api/tweets/timelines/migrate
	tweetApiLimiter *rate.Limiter
}

func NewTwitterService() *TwitterService {
	return &TwitterService{
		userApiLimiter:  rate.NewLimiter(rate.Every(time.Second), 1),
		tweetApiLimiter: rate.NewLimiter(rate.Every(time.Minute), 11),
	}
}

func (t *TwitterService) GetLatestTweet(ctx context.Context, screenName string) (*dto.Tweet, error) {
	err := t.tweetApiLimiter.Wait(ctx)
	if err != nil {
		return nil, errx.WithStack(err, nil)
	}

	userId, _, err := t.getUserId(ctx, screenName)
	if err != nil {
		return nil, err
	}

	resp, err := req.Client.R().
		SetBearerAuthToken(conf.R.Twitter.Token).
		SetQueryParams(map[string]string{
			"exclude":      "retweets,replies",
			"tweet.fields": "author_id,created_at,entities,in_reply_to_user_id,referenced_tweets,text",
			"expansions":   "author_id,in_reply_to_user_id,referenced_tweets.id,referenced_tweets.id.author_id,attachments.media_keys",
			"user.fields":  "username,name",
			"media.fields": "preview_image_url,type,url",
			"max_results":  "5",
		}).
		SetPathParam("userId", userId).
		Get("https://api.twitter.com/2/users/{userId}/tweets")
	if err != nil {
		return nil, errx.WithStack(err, nil)
	}
	body, err := jv.UnmarshalString(resp.String())
	if err != nil {
		return nil, errx.WithStack(err, map[string]any{"body": resp.String()})
	}

	errFields := map[string]any{"body": body}

	tweet, err := body.Get("data", 0)
	if err != nil {
		return nil, errx.WithStack(err, errFields)
	}
	id, err := tweet.GetString("id")
	if err != nil {
		return nil, errx.WithStack(err, errFields)
	}
	text, err := tweet.GetString("text")
	if err != nil {
		return nil, errx.WithStack(err, errFields)
	}

	var (
		hasVideo  bool
		imgUrls   []string
		origin    *dto.Tweet
		tweetType = dto.EnumCommon
	)
	if media, err := tweet.GetArray("entities", "urls"); err == nil {
		text, imgUrls, hasVideo = t.getMedia(ctx, body, media, text, id)
	}

	if originId, err := tweet.GetString("referenced_tweets", 0, "id"); err == nil {
		originType, err := tweet.GetString("referenced_tweets", 0, "type")
		if err != nil {
			return nil, errx.WithStack(err, errFields)
		}

		switch originType {
		case "quoted":
			tweetType = dto.EnumQuote
		default:
			return nil, errx.New(
				errx.ErrTwitterApi,
				"Unknown origin type %s!\nTweet: %s",
				originType,
				tweet.String(),
			)
		}

		origin, err := t.getOriginTweet(ctx, originId, body)
		if err != nil {
			return nil, err
		}
		text = strings.ReplaceAll(text, origin.Url, "")
	}

	pubTimeStr, err := tweet.GetString("created_at")
	if err != nil {
		return nil, errx.WithStack(err, errFields)
	}
	pubTime, err := time.Parse("2006-01-02T15:04:05.000Z", pubTimeStr)
	if err != nil {
		return nil, errx.WithStack(err, errFields)
	}
	pubTime = pubTime.UTC()

	author, err := body.GetString("includes", "users", 0, "name")
	if err != nil {
		return nil, errx.WithStack(err, errFields)
	}

	return &dto.Tweet{
		Type:             tweetType,
		Id:               id,
		Url:              "https://twitter.com/" + screenName + "/status/" + id,
		PubTime:          &pubTime,
		Author:           author,
		AuthorScreenName: screenName,
		Text:             text,
		ImageUrls:        imgUrls,
		HasVideo:         hasVideo,
		Origin:           origin,
	}, nil
}

func (t *TwitterService) getOriginTweet(ctx context.Context, originId string, body *jv.V) (*dto.Tweet, error) {
	errFields := map[string]any{"body": body}

	origins, err := body.GetArray("includes", "tweets")
	if err != nil {
		return nil, errx.WithStack(err, errFields)
	}

	for _, origin := range origins.ForRangeArr() {
		if origin.MustGet("id").String() == originId {
			author, screenName, err := t.getUserName(ctx, origin.MustGet("author_id").String())
			if err != nil {
				log.Error().Stack().Err(err).Msgf(err.Error())
				continue
			}
			text, err := origin.GetString("text")
			if err != nil {
				log.Error().Stack().Err(err).Msgf(err.Error())
				continue
			}

			var (
				imgUrls  []string
				hasVideo bool
			)

			if media, err := origin.GetArray("entities", "urls"); err == nil {
				text, imgUrls, hasVideo = t.getMedia(ctx, body, media, text, originId)
			}

			pubTimeStr, err := origin.GetString("created_at")
			if err != nil {
				return nil, errx.WithStack(err, errFields)
			}
			pubTime, err := time.Parse("2006-01-02T15:04:05.000Z", pubTimeStr)
			if err != nil {
				return nil, errx.WithStack(err, errFields)
			}
			pubTime = pubTime.UTC()

			return &dto.Tweet{
				Type:             dto.EnumCommon,
				Id:               originId,
				Url:              "https://twitter.com/" + screenName + "/status/" + originId,
				PubTime:          &pubTime,
				Author:           author,
				AuthorScreenName: screenName,
				Text:             text,
				ImageUrls:        imgUrls,
				HasVideo:         hasVideo,
			}, nil
		}
	}
	return nil, errx.New(errx.ErrTwitterApi, "Couldn't find the origin tweet!(originId: %s)", originId)
}

func (t *TwitterService) getUserId(ctx context.Context, screenName string) (userId string, name string, err error) {
	err = t.userApiLimiter.Wait(ctx)
	if err != nil {
		return "", "", errx.WithStack(err, nil)
	}

	resp, err := req.Client.R().
		SetBearerAuthToken(conf.R.Twitter.Token).
		Get("https://api.twitter.com/2/users/by/username/" + screenName)
	if err != nil {
		return "", "", errx.WithStack(err, nil)
	}
	body, err := jv.UnmarshalString(resp.String())
	if err != nil {
		return "", "", errx.WithStack(err, map[string]any{"body": resp.String()})
	}
	errFields := map[string]any{"body": body}

	id, err := body.GetString("data", "id")
	if err != nil {
		return "", "", errx.WithStack(err, errFields)
	}

	name, err = body.GetString("data", "name")
	if err != nil {
		return "", "", errx.WithStack(err, errFields)
	}

	return id, name, nil
}

func (t *TwitterService) getUserName(ctx context.Context, userId string) (name string, screenName string, err error) {
	err = t.userApiLimiter.Wait(ctx)
	if err != nil {
		return "", "", errx.WithStack(err, nil)
	}

	resp, err := req.Client.R().
		SetBearerAuthToken(conf.R.Twitter.Token).
		Get("https://api.twitter.com/2/users/" + userId)
	if err != nil {
		return "", "", errx.WithStack(err, nil)
	}
	body, err := jv.UnmarshalString(resp.String())
	if err != nil {
		return "", "", errx.WithStack(err, map[string]any{"body": resp.String()})
	}
	errFields := map[string]any{"body": body}

	name, err = body.GetString("data", "name")
	if err != nil {
		return "", "", errx.WithStack(err, errFields)
	}
	userName, err := body.GetString("data", "username")
	if err != nil {
		return "", "", errx.WithStack(err, errFields)
	}

	return name, userName, nil
}

func (t *TwitterService) getMedia(
	ctx context.Context,
	body *jv.V,
	media *jv.V,
	text string,
	tweetId string,
) (string, []string, bool) {
	errFields := map[string]any{"body": body}

	var (
		hasVideo bool
		imgUrls  []string
	)

	for _, medium := range media.ForRangeArr() {
		mediaKey, err := medium.GetString("media_key")
		if errors.Is(err, jv.ErrNotFound) {
			continue
		} else if err != nil {
			log.Error().
				Stack().Err(errx.WithStack(err, errFields)).
				Msgf("Failed to get the medium from twitter user %s", tweetId)
			continue
		}

		// 去掉正文里重复的媒体url
		url, err := medium.GetString("url")
		if err != nil {
			log.Error().
				Stack().Err(errx.WithStack(err, errFields)).
				Msgf("Failed to get the medium from twitter user %s", tweetId)
			continue
		}
		text = strings.ReplaceAll(text, url, "")

		if strings.HasPrefix(mediaKey, "3_") {
			url, err := t.getImageOrVideoThumbnailUrl(ctx, body, mediaKey, tweetId)
			if err != nil {
				log.Error().Stack().Err(err).Msgf(err.Error())
				continue
			}
			imgUrls = append(imgUrls, url)
		} else if strings.HasPrefix(mediaKey, "7_") {
			hasVideo = true
			url, err := t.getImageOrVideoThumbnailUrl(ctx, body, mediaKey, tweetId)
			if err != nil {
				log.Error().Stack().Err(err).Msgf(err.Error())
				continue
			}
			imgUrls = append(imgUrls, url)
		} else if strings.HasPrefix(mediaKey, "13_") {
			hasVideo = true
			text = strings.ReplaceAll(text, medium.MustGet("expanded_url").String(), "")
		}
	}
	return text, imgUrls, hasVideo
}

func (t *TwitterService) getImageOrVideoThumbnailUrl(
	ctx context.Context,
	body *jv.V,
	mediaKey string,
	tweetId string,
) (string, error) {
	errFields := map[string]any{"body": body}

	media, err := body.Get("includes", "media")
	if err != nil {
		return "", errx.WithStack(err, errFields)
	}

	for _, medium := range media.ForRangeArr() {
		if v, err := medium.GetString("media_key"); err != nil || v != mediaKey {
			continue
		}

		if strings.HasPrefix(mediaKey, "3_") || strings.HasPrefix(mediaKey, "13_") {
			url, err := medium.GetString("url")
			if err != nil {
				log.Error().
					Stack().Err(errx.WithStack(err, errFields)).
					Msgf("Failed to get the medium from twitter user %s", tweetId)
				continue
			}
			urlList := strings.Split(url, ".")
			return strings.Join(urlList[:len(urlList)-1], ".") + "?format=jpg&name=large", nil
		} else if strings.HasPrefix(mediaKey, "7_") {
			previewImgUrl, err := medium.GetString("preview_image_url")
			if err != nil {
				log.Error().
					Stack().Err(errx.WithStack(err, errFields)).
					Msgf("Failed to get the medium from twitter user %s", tweetId)
				continue
			}
			previewImgUrlList := strings.Split(previewImgUrl, ".")
			return strings.Join(previewImgUrlList[:len(previewImgUrlList)-1], ".") + "?format=jpg&name=large", nil
		}
	}

	err = t.tweetApiLimiter.Wait(ctx)
	if err != nil {
		return "", errx.WithStack(err, nil)
	}

	resp, err := req.Client.R().
		SetBearerAuthToken(conf.R.Twitter.Token).
		SetQueryParams(map[string]string{
			"tweet.fields": "author_id,created_at,entities,in_reply_to_user_id,referenced_tweets,text",
			"expansions":   "author_id,in_reply_to_user_id,referenced_tweets.id,referenced_tweets.id.author_id,attachments.media_keys",
			"user.fields":  "username,name",
			"media.fields": "preview_image_url,type,url",
		}).
		Get("https://api.twitter.com/2/tweets/" + tweetId)
	if err != nil {
		return "", errx.WithStack(err, nil)
	}
	j, err := jv.UnmarshalString(resp.String())
	if err != nil {
		return "", errx.WithStack(err, map[string]any{"body": resp.String()})
	}
	errFields2 := map[string]any{"body": body}

	imgs, err := j.GetArray("includes", "media")
	if err != nil {
		return "", errx.WithStack(err, errFields2)
	}
	img, ok := lo.Find(imgs.ForRangeArr(), func(item *jv.V) bool {
		if v, err := item.GetString("media_key"); err == nil && v == mediaKey {
			return true
		}
		return false
	})
	if !ok {
		return "", errx.NewWithFields(
			errx.ErrTwitterApi,
			"Failed to get image url from twitter user %s",
			errFields2,
			tweetId,
		)
	}

	url, err := img.GetString("url")
	if err != nil {
		return "", errx.WithStack(err, errFields2)
	}
	urlList := strings.Split(url, ".")

	return strings.Join(urlList[:len(urlList)-1], ".") + "?format=jpg&name=large", nil
}
