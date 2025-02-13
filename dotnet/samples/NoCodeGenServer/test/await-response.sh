mosquitto_sub \
-d  \
-t "myResponseTopic" \
-V mqttv5 \
-F "%%Topic: %t\n%%message-id: %m\n%%content-type: %C\n%%correlation-data %D\n%%user-properties: %P\n"
# -F "%% Topic: %%t \n %% message-id: %%m\n%% content-type: %%C\n%% correlation-data: %%D\n%% user-properties: %%P\n"

