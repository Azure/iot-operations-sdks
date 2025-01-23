package envoy

import complex "github.com/Azure/iot-operations-sdks/go/samples/protocol/complex/envoy/dtmi_example_Complex__1"

var (
	tomorrow = complex.Tomorrow

	Request = complex.GetTemperaturesRequestPayload{
		Cities: complex.Object_GetTemperatures_Request{
			When:   &tomorrow,
			Cities: []string{"Seattle", "Portland"},
		},
	}

	Response = complex.GetTemperaturesResponsePayload{
		Temperatures: []complex.Object_GetTemperatures_Response_ElementSchema{
			response("Seattle", -5.6, complex.Success),
			response("Portland", -13.2, complex.Success),
		},
	}

	Telemetry = complex.TelemetryCollection{
		Temperatures: map[string]float64{
			"Seattle":       -2.22,
			"Los Angeles":   11.67,
			"Boston":        -12.22,
			"San Francisco": 11.67,
		},
	}
)

func response(
	city string,
	temp float64,
	res complex.Enum_Test_Result__1,
) complex.Object_GetTemperatures_Response_ElementSchema {
	return complex.Object_GetTemperatures_Response_ElementSchema{
		City:        &city,
		Temperature: &temp,
		Result:      &res,
	}
}
