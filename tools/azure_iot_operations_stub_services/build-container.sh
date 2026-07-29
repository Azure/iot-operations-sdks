#!/bin/bash

cargo build --release --all-features
docker build -t localhost:5000/schema-registry-stub:latest .
