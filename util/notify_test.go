package util

import (
	"fmt"
	"testing"

	"github.com/Decmoe47/rabbitool/conf"
	"github.com/Decmoe47/rabbitool/errx"
	"github.com/cockroachdb/errors"
	"github.com/stretchr/testify/require"
)

func Test_errorNotifier_checkAndSend(t *testing.T) {
	err := conf.Load("../configs.yml")
	require.NoError(t, err)

	notifier, err := newErrorNotifier()
	require.NoError(t, err)

	for i := 0; i < conf.R.Notifier.AllowedAmount+1; i++ {
		err = notifier.checkAndSend(fmt.Sprintf("%+v", errors.Wrapf(errx.ErrInvalidParam, "test")))
		require.NoError(t, err)
	}
}
