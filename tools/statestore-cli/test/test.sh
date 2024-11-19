#!/bin/bash

MQ_BROKER_HOSTNAME="localhost"
CAFILE=~/iot-operations-sdks/.session/broker-ca.crt
CLIENT_CERT=~/iot-operations-sdks/.session/client.crt
CLIENT_KEY=~/iot-operations-sdks/.session/client.key

export RUST_BACKTRACE=0

if [ -d ../target/release ]
then
    pushd ../target/release
else
    pushd ../target/debug
fi

assert_equals()
{
    test_name=$1
    expected_result=$2
    actual_result=$3

    if [ $actual_result != $expected_result ]
    then
        echo "> $test_name FAILED (result: expected=$expected_result, actual=$actual_result)"
        exit 1
    else
        echo "> $test_name PASSED"
    fi 
}

assert_not_equals()
{
    test_name=$1
    unexpected_result=$2
    actual_result=$3

    if [ $actual_result == $unexpected_result ]
    then
        echo "> $test_name FAILED (result: actual=$actual_result)"
        exit 1
    else
        echo "> $test_name PASSED"
    fi 
}

assert_files()
{
    test_name=$1
    file1=$2
    file2=$3

    diff $file1 $file2
    if [ $? != 0 ]
    then
        echo "> $test_name FAILED (files are different)"
        exit 1
    else
        echo "> $test_name PASSED (files match)"
    fi 
}

assert_file_content()
{
    test_name=$1
    file1=$2
    content=$3

    if [ "$(cat $file1)" != "$content" ]
    then
        echo "> $test_name FAILED (file content different)"
        exit 1
    else
        echo "> $test_name PASSED (file content matches)"
    fi 
}

echo "12345" > ./value.txt

./statestore-cli set -n $MQ_BROKER_HOSTNAME -k "someKey1" --valuefile ./value.txt --cafile $CAFILE --certfile $CLIENT_CERT --keyfile $CLIENT_KEY
assert_equals "01-set-from-file-x509-tls" 0 $?

./statestore-cli get -n $MQ_BROKER_HOSTNAME -k "someKey1" -f "./value2.txt" --cafile $CAFILE --certfile $CLIENT_CERT --keyfile $CLIENT_KEY
assert_equals "02-get-to-file-x509-tls" 0 $?
assert_files "02-get-to-file-x509-tls" ./value.txt ./value2.txt

./statestore-cli delete -n $MQ_BROKER_HOSTNAME -k "someKey1" --cafile $CAFILE --certfile $CLIENT_CERT --keyfile $CLIENT_KEY
assert_equals "03-delete-x509-tls" 0 $?

./statestore-cli set -n $MQ_BROKER_HOSTNAME -k "someKey2" --value "hello" --cafile $CAFILE --certfile $CLIENT_CERT --keyfile $CLIENT_KEY
assert_equals "04-set-from-console-x509-tls" 0 $?

./statestore-cli get -n $MQ_BROKER_HOSTNAME -k "someKey2" --cafile $CAFILE --certfile $CLIENT_CERT --keyfile $CLIENT_KEY > ./value3.txt
assert_equals "05-get-to-console-x509-tls" 0 $?
assert_file_content "05-get-to-console-x509-tls" ./value3.txt "hello"

./statestore-cli set -n $MQ_BROKER_HOSTNAME -p 1883 -k "someKey3" --notls --value "no tls"
assert_equals "06-set-console-anon-no-tls" 0 $?

./statestore-cli get -n $MQ_BROKER_HOSTNAME -p 1883 -k "someKey3" --notls > ./value4.txt
assert_equals "07-get-console-anon-no-tls" 0 $?

./statestore-cli delete -n $MQ_BROKER_HOSTNAME -p 1883 -k "someKey3" --notls
assert_equals "08-delete-anon-no-tls" 0 $?

./statestore-cli delete -n $MQ_BROKER_HOSTNAME -p 1883 -k "someKey3" --notls
assert_equals "09-delete-repeated-anon-no-tls" 1 $?

./statestore-cli get -n $MQ_BROKER_HOSTNAME -p 1883 -k "someKey3" --notls
assert_equals "10-get-already-deleted-anon-no-tls" 1 $?

./statestore-cli get -n "$MQ_BROKER_HOSTNAME-invalid" -p 1883 -k "someKey3" --notls
assert_not_equals "11-get-invalid-hostname-anon-no-tls" 0 $?

./statestore-cli get -n $MQ_BROKER_HOSTNAME -p 1884 -k "someKey3" --notls
assert_not_equals "12-get-invalid-port-anon-no-tls" 0 $?

popd
