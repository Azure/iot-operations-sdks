// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
package auth

import (
	"os"
	"sync"

	"github.com/fsnotify/fsnotify"
)

// AIOServiceAccountToken impelements an enhanced authentication provider that
// reads a Kubernetes Service Account Token for the AIO Broker.
type AIOServiceAccountToken struct {
	filename string
	watcher  *fsnotify.Watcher

	requestReauth   func()
	requestReauthMu sync.RWMutex
}

// NewAIOServiceAccountToken creates a new AIO SAT auth provider from the given
// filename.
func NewAIOServiceAccountToken(
	filename string,
) (*AIOServiceAccountToken, error) {
	watcher, err := fsnotify.NewWatcher()
	if err != nil {
		return nil, err
	}

	if err := watcher.Add(filename); err != nil {
		_ = watcher.Close()
		return nil, err
	}

	sat := &AIOServiceAccountToken{filename: filename, watcher: watcher}

	go sat.watch()

	return sat, nil
}

func (sat *AIOServiceAccountToken) InitiateAuth(bool) (*Values, error) {
	token, err := os.ReadFile(sat.filename)
	if err != nil {
		return nil, err
	}
	return &Values{
		AuthMethod: "K8S-SAT",
		AuthData:   token,
	}, nil
}

func (*AIOServiceAccountToken) ContinueAuth(*Values) (*Values, error) {
	return nil, ErrUnexpected
}

func (sat *AIOServiceAccountToken) AuthSuccess(requestReauth func()) {
	sat.requestReauthMu.Lock()
	defer sat.requestReauthMu.Unlock()
	sat.requestReauth = requestReauth
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

			if evt.Op != fsnotify.Write {
				continue
			}

			// Some file writes (e.g. using > on the command line) will clear
			// the file before rewriting it. We see this as two write events,
			// so we need to ignore the first one in order to not send an empty
			// AUTH packet to the MQTT server.
			if fi, err := os.Stat(sat.filename); err != nil || fi.Size() == 0 {
				continue
			}

			sat.attemptReauth()

		case _, ok := <-sat.watcher.Errors:
			if !ok {
				return
			}
			// Nothing useful to do; we just don't want to block.
		}
	}
}

func (sat *AIOServiceAccountToken) attemptReauth() {
	sat.requestReauthMu.RLock()
	defer sat.requestReauthMu.RUnlock()

	// If the file changes but we don't have the reauth callback, we never
	// actually succeeded auth, so it will get retried anyways.
	if sat.requestReauth != nil {
		sat.requestReauth()
	}
}
