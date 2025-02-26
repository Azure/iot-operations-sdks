# User-Managed Connection Management Sample

This sample code demonstrates the differences between code using a user-owned MQTT connection and [code that relies on the MQTT session client to manage the connection](../SessionClientConnectionManagementSample/).

By running this sample, you will create an instance of an unmanaged MQTT client, connect it to the broker configured in [appsettings.json](./appsettings.json), and begin sending messages.

Note that this sample does not demonstrate robust, production-ready user-managed connection MQTT code. Rather, it demonstrates all the different points in code where you need to consider adding retry logic and highlights that the session client does this for you.