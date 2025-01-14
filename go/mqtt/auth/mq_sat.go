// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
package auth

import (
	"bytes"
	"os"
	"path/filepath"
	"sync"

	"github.com/fsnotify/fsnotify"
)

// AIOServiceAccountToken impelements an enhanced authentication provider that
// reads a Kubernetes Service Account Token for the AIO Broker.
type AIOServiceAccountToken struct {
	filename string
	watcher  *fsnotify.Watcher

	reauth func()
	token  []byte
	mu     sync.RWMutex
}

// NewAIOServiceAccountToken creates a new AIO SAT auth provider from the given
// filename.
func NewAIOServiceAccountToken(
	filename string,
) (*AIOServiceAccountToken, error) {
	token, err := os.ReadFile(filename)
	if err != nil {
		return nil, err
	}

	watcher, err := fsnotify.NewWatcher()
	if err != nil {
		return nil, err
	}

	if err := watcher.Add(filepath.Dir(filename)); err != nil {
		_ = watcher.Close()
		return nil, err
	}

	sat := &AIOServiceAccountToken{
		filename: filename,
		watcher:  watcher,
		token:    token,
	}

	go sat.watch()

	return sat, nil
}

func (sat *AIOServiceAccountToken) InitiateAuth(bool) (*Values, error) {
	return &Values{AuthMethod: "K8S-SAT", AuthData: sat.token}, nil
}

func (*AIOServiceAccountToken) ContinueAuth(*Values) (*Values, error) {
	return nil, ErrUnexpected
}

func (sat *AIOServiceAccountToken) AuthSuccess(requestReauth func()) {
	sat.mu.Lock()
	defer sat.mu.Unlock()
	sat.reauth = requestReauth
}

func (sat *AIOServiceAccountToken) Close() error {
	return sat.watcher.Close()
}

func (sat *AIOServiceAccountToken) watch() {
	for {
		select {
		case evt, ok := <-sat.watcher.Events:
			if !ok {
				return
			}

			// Since we're listening to the parent directory, only pay attention
			// to operations which could have reasonably modified the data the
			// SAT token file represents.
			switch evt.Op {
			case fsnotify.Write, fsnotify.Create, fsnotify.Rename:
			default:
				continue
			}

			// Some file writes (e.g. using > on the command line) will clear
			// the file before rewriting it. We see this as two write events,
			// so we need to ignore the first one in order to not send an empty
			// AUTH packet to the MQTT server.
			token, err := os.ReadFile(sat.filename)
			if err != nil || len(token) == 0 {
				continue
			}

			sat.attemptReauth(token)

		case _, ok := <-sat.watcher.Errors:
			if !ok {
				return
			}
			// Nothing useful to do; we just don't want to block.
		}
	}
}

func (sat *AIOServiceAccountToken) attemptReauth(token []byte) {
	sat.mu.RLock()
	defer sat.mu.RUnlock()

	// If the file changes but we don't have the reauth callback, we never
	// actually succeeded auth, so it will get retried anyways. In addition,
	// since we're listening to the folder, only request reauth if the token
	// has actually changed to cut down noise.
	if sat.reauth != nil && !bytes.Equal(sat.token, token) {
		sat.token = token
		sat.reauth()
	}
}
