#!/bin/sh

cd protocol_compiler_demo/avro_comm
cargo build
cd ../..

cd protocol_compiler_demo/json_comm
cargo build
cd ../..

cd protocol_compiler_demo/raw_comm
cargo build
cd ../..

cd protocol_compiler_demo/custom_comm
cargo build
cd ../..

cd protocol_compiler_demo/counters
cargo build
cd ../..

cd protocol_compiler_demo/telem_client
cargo build
cd ../..

cd protocol_compiler_demo/telem_server
cargo build
cd ../..

cd protocol_compiler_demo/cmd_client
cargo build
cd ../..

cd protocol_compiler_demo/cmd_server
cargo build
cd ../..
