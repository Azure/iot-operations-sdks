/* This is an auto-generated file.  Do not modify. */

#nullable enable

namespace TestModel.dtmi_test_TestModel__1
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json.Serialization;

    public class Object_Test_Request
    {
        /// <summary>
        /// The 'request' Field.
        /// </summary>
        [JsonPropertyName("request")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public string? Request { get; set; } = default;

        /// <summary>
        /// The 'testCaseIndex' Field.
        /// </summary>
        [JsonPropertyName("testCaseIndex")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public int? TestCaseIndex { get; set; } = default;

    }
}
