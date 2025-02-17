mosquitto_sub \
-d  \
-t "#" \
-V mqttv5 \
-F "#Timestamp: %I\n#Topic: %t\n#message-id: %m\n#content-type: %C\n#correlation-data: %D\n#user-properties: %P\n#payload: %p\n"
# -F "%% Topic: %%t \n %% message-id: %%m\n%% content-type: %%C\n%% correlation-data: %%D\n%% user-properties: %%P\n#"

