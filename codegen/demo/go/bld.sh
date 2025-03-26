#!/bin/sh

cd telemclient
go build ./...
cd ..

cd telemserver
go build ./...
cd ..

cd cmdclient
go build ./...
cd ..

cd cmdserver
go build ./...
cd ..
