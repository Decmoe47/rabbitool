package service

import (
	"fmt"
	"io"
	"os"
	"os/exec"
	"time"

	"github.com/Decmoe47/rabbitool/conf"
	"github.com/Decmoe47/rabbitool/errx"
	"github.com/Decmoe47/rabbitool/util"
	"github.com/Decmoe47/rabbitool/util/req"
	"github.com/aliyun/aliyun-oss-go-sdk/oss"
	"github.com/rs/zerolog/log"
)

type UploaderService struct {
	bucket  *oss.Bucket
	baseUrl string
}

func NewUploaderService() (*UploaderService, error) {
	client, err := oss.New(conf.R.Oss.Endpoint, conf.R.Oss.AccessKeyId, conf.R.Oss.AccessKeySecret)
	if err != nil {
		return nil, errx.WithStack(err, nil)
	}
	bucket, err := client.Bucket(conf.R.Oss.BucketName)
	if err != nil {
		return nil, errx.WithStack(err, nil)
	}

	return &UploaderService{
		bucket:  bucket,
		baseUrl: fmt.Sprintf("https://%s.%s/", conf.R.Oss.BucketName, conf.R.Oss.Endpoint),
	}, nil
}

func (u *UploaderService) UploadImage(url string) (string, error) {
	fileName := time.Now().In(util.CST()).Format("20060102_150405.000") + ".png"
	errFields := map[string]any{"url": url, "fileName": fileName}

	resp, err := req.Client.R().Get(url)
	if err != nil {
		return "", errx.WithStack(err, errFields)
	}
	defer func(Body io.ReadCloser) {
		err := Body.Close()
		if err != nil {
			log.Error().Stack().Err(errx.WithStack(err, errFields)).Msg(err.Error())
		}
	}(resp.Body)

	uploadPath := "data/images/" + fileName
	err = u.bucket.PutObject(uploadPath, resp.Body)
	if err != nil {
		return "", errx.WithStack(err, errFields)
	}

	return u.baseUrl + uploadPath, nil
}

func (u *UploaderService) UploadVideo(url string, pubTime *time.Time) (string, error) {
	if _, err := os.Stat("./tmp"); os.IsNotExist(err) {
		err := os.Mkdir("./tmp", os.ModePerm)
		if err != nil {
			return "", errx.WithStack(err, nil)
		}
	}

	fileName := pubTime.Format("20060102_150405.000") + ".mp4"
	filePath := "./tmp/" + fileName

	errFields := map[string]any{"url": url, "filePath": filePath}

	cmd := exec.Command("youtube-dl", "-o", filePath, url)
	err := cmd.Run()
	if err != nil {
		return "", errx.WithStack(err, errFields)
	}

	uploadPath := `data/videos/` + fileName
	err = u.bucket.PutObjectFromFile(uploadPath, filePath)
	if err != nil {
		return "", errx.WithStack(err, errFields)
	}

	return u.baseUrl + uploadPath, nil
}
