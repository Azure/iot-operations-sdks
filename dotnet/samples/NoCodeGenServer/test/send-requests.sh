#!/bin/bash

param=$1

case $param in
  write-simple)
    topic="AioNamespace/asset-operations/MyAssetId/DatasetName"
    payload="simple-types-request.json"
    correlation="$(printf '\xEC\x28\x35\xC9\x9B\x0D\x66\x46\x81\xB5\xC2\xAB\xB7\x01\xB2\x9D')"
    ;;
  write-simple-bad)
    topic="AioNamespace/asset-operations/MyAssetId/DatasetName"
    payload="bad-simple-types-request.json"
    correlation="$(printf '\xEC\x28\x35\xC9\x9B\x0D\x66\x46\x81\xB5\xC2\xAB\xB7\x01\xB2\xAF')"
    ;;
  write-complex)
    topic="AioNamespace/asset-operations/MyAssetId/DatasetName"
    payload="complex-types-request.json"
    correlation="$(printf '\xEC\x28\x35\xC9\x9B\x0D\x66\x46\x81\xB5\xC2\xAB\xB7\x01\xB2\xFE')"
    ;;
  process-control)
    topic="AioNamespace/asset-operations/MyAssetId/ProcessControlGroup/foobar"
    payload="process-control-request.json"
    correlation="$(printf '\xEC\x28\x35\xC9\x9B\x0D\x66\x46\x81\xB5\xC2\xAB\xB7\x01\xCA\xFE')"
    ;;
  process-control-bad)
    topic="AioNamespace/asset-operations/MyAssetId/ProcessControlGroup/foobar"
    payload="bad-process-control-request.json"
    correlation="$(printf '\xEC\x28\x35\xC9\x9B\x0D\x66\x46\x81\xB5\xC2\xAB\xB7\x01\x01\x23')"
    ;;
  *)
    echo "Unknown configuration: $param"
    exit 1
    ;;
esac

mosquitto_pub  \
-d \
-t $topic \
-V mqttv5 \
-q 1 \
-D Publish user-property "__invId" "tester" \
-D Publish user-property "__ts" "1739781943291.0132:00001:18faead4-b3eb-4b94-80e6-0c64855c3618" \
-D Publish user-property "__ft" "1739781943291.0132:00001:18faead4-b3eb-4b94-80e6-0c64855c3618" \
-D Publish correlation-data $correlation \
-D Publish payload-format-indicator 1 \
-D Publish content-type "application/json" \
-D Publish message-expiry-interval 3600 \
-D Publish response-topic "myResponseTopic" \
-f $payload
