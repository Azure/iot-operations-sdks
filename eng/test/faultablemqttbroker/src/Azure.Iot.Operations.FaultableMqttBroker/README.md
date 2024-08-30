# Faultable MQTT Broker

This folder contains .NET code for running an MQTT broker that can be manipulated to reject packets and/or drop connections with a configurable delay. It is intended only for use in fault-injection tests and should not be used in any production setting.

The basic way that a client can manipulate this MQTT broker is by including special MQTT user properties in the connect/publish/subscribe/unsubscribe packet that instruct the broker on what to do. In the below table, you can find details on what these special MQTT user properties are.

| MQTT User property name | MQTT User property value | Applicable MQTT packets | Description |
| --- | --- | --- | --- |
| `fault:disconnect` | Integer, the desired MQTT disconnect reason code | publish, subscribe, unsubscribe | Causes the MQTT broker to drop the connection this packet was sent on. Happens immediately and prior to sending a PUBACK/SUBACK/UNSUBACK if no delay is specified. |
| `fault:delay` | Integer, the number of seconds to delay the fault. | publish, subscribe, unsubscribe | Causes the MQTT broker to delay the requested fault. Only applicable for disconnect faults. If specified, the broker will still send the PUBACK/SUBACK/UNSUBACK for the packet. |
| `fault:rejectconnect` | Integer, the desired MQTT CONNACK reason code | connect | Causes the MQTT broker to reject the connect attempt with the provided reason. Cannot be delayed. |
| `fault:rejectpublish` | Integer, the desired MQTT PUBACK reason code | publish | Causes the MQTT broker to reject the publish attempt with the provided reason. Cannot be delayed. |
| `fault:rejectsubscribe` | Integer, the desired MQTT SUBACK reason code | subscribe | Causes the MQTT broker to reject the subscribe attempt with the provided reason. Cannot be delayed. |
| `fault:rejectunsubscribe` | Integer, the desired MQTT UNSUBACK reason code | unsubscribe | Causes the MQTT broker to reject the unsubscribe attempt with the provided reason. Cannot be delayed. |
| `fault:requestid` | Any unique string | connect, publish, subscribe, unsubscribe | The ID of the fault request. See below for details. |

## Fault request ids

In a typical fault injection test, a client will attempt some operation and respond to an expected failure by re-sending that request.

In order to prevent a fault injection connect/publish/subscribe/unsubscribe from triggering a fault again, each packet must include a common MQTT user property with a name of "fault:requestid" and a unique value (such as a GUID). 

If this user property is included, then the first time that packet is seen by this MQTT broker it will trigger the requested fault. Any time the packet is re-delivered with the same requestId, the broker will treat it like a normal message instead.

If this user property is not included, then the MQTT broker will trigger the fault every time it sees the packet.