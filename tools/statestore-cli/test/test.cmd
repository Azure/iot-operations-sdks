@REM echo OFF

set MQ_BROKER_HOSTNAME="1.2.3.4"
set "CAFILE=..\..\..\.session\broker-ca.crt"
set "CLIENT_CERT=..\..\..\.session\client.crt"
set "CLIENT_KEY=..\..\..\.session\client.key"

pushd ..\target\release

echo 12345 > .\value.txt

set "CAFILE=..\%CAFILE%"
set "CLIENT_CERT=..\%CLIENT_CERT%"
set "CLIENT_KEY=..\%CLIENT_KEY%"

@REM .\statestore-cli.exe set -n %MQ_BROKER_HOSTNAME% -k "someKey1" --valuefile .\value.txt --cafile %CAFILE% --certfile %CLIENT_CERT% --keyfile %CLIENT_KEY%
@REM call:assert_equals "01-set-from-file-x509-tls" 0 %ERRORLEVEL%

@REM .\statestore-cli.exe get -n %MQ_BROKER_HOSTNAME% -k "someKey1" -f ".\value2.txt" --cafile %CAFILE% --certfile %CLIENT_CERT% --keyfile %CLIENT_KEY%
@REM call:assert_equals "02-get-to-file-x509-tls" 0 %ERRORLEVEL%

@REM .\statestore-cli.exe delete -n %MQ_BROKER_HOSTNAME% -k "someKey1" --cafile %CAFILE% --certfile %CLIENT_CERT% --keyfile %CLIENT_KEY%
@REM call:assert_equals "03-delete-x509-tls" 0 %ERRORLEVEL%

@REM .\statestore-cli.exe set -n %MQ_BROKER_HOSTNAME% -k "someKey2" --value "hello" --cafile %CAFILE% --certfile %CLIENT_CERT% --keyfile %CLIENT_KEY%
@REM call:assert_equals "04-set-from-console-x509-tls" 0 %ERRORLEVEL%

@REM .\statestore-cli.exe get -n %MQ_BROKER_HOSTNAME% -k "someKey2" --cafile %CAFILE% --certfile %CLIENT_CERT% --keyfile %CLIENT_KEY% > .\value3.txt
@REM call:assert_equals "05-get-to-console-x509-tls" 0 %ERRORLEVEL%

.\statestore-cli.exe set -n %MQ_BROKER_HOSTNAME% -p 1883 -k "someKey3" --notls --value "no tls"
call:assert_equals "06-set-console-anon-no-tls" 0 %ERRORLEVEL%

.\statestore-cli.exe get -n %MQ_BROKER_HOSTNAME% -p 1883 -k "someKey3" --notls > .\value4.txt
call:assert_equals "07-get-console-anon-no-tls" 0 %ERRORLEVEL%

.\statestore-cli.exe delete -n %MQ_BROKER_HOSTNAME% -p 1883 -k "someKey3" --notls
call:assert_equals "08-delete-anon-no-tls" 0 %ERRORLEVEL%

.\statestore-cli.exe delete -n %MQ_BROKER_HOSTNAME% -p 1883 -k "someKey3" --notls
call:assert_equals "09-delete-repeated-anon-no-tls" 1 %ERRORLEVEL%

.\statestore-cli.exe get -n %MQ_BROKER_HOSTNAME% -p 1883 -k "someKey3" --notls
call:assert_equals "10-get-already-deleted-anon-no-tls" 1 %ERRORLEVEL%

.\statestore-cli.exe get -n "%MQ_BROKER_HOSTNAME%-invalid" -p 1883 -k "someKey3" --notls
call:assert_not_equals "11-get-invalid-hostname-anon-no-tls" 0 %ERRORLEVEL%

.\statestore-cli.exe get -n %MQ_BROKER_HOSTNAME% -p 1884 -k "someKey3" --notls
call:assert_not_equals "12-get-invalid-port-anon-no-tls" 0 %ERRORLEVEL%

popd

echo on

goto:eof


:assert_equals
set "test_name=%~1"
set /a "expected_result=%~2"
set /a "actual_result=%~3"

if %actual_result% NEQ %expected_result% (
    echo "> %test_name% FAILED (result: expected=%expected_result%, actual=%actual_result%)"
    exit /b 1
) else (
    echo "> %test_name% PASSED"
)
goto:eof

:assert_not_equals
set "test_name=%~1"
set /a "unexpected_result=%~2"
set /a "actual_result=%~3"

if %actual_result% EQU %unexpected_result% (
    echo "> %test_name% FAILED (result: actual=%actual_result%)"
    exit /b 1
) else (
    echo "> %test_name% PASSED"
)
goto:eof
