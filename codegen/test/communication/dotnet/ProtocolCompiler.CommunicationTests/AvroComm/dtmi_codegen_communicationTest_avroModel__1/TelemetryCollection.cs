// ------------------------------------------------------------------------------
// <auto-generated>
//    Generated by avrogen, version 1.11.3
//    Changes to this file may cause incorrect behavior and will be lost if code
//    is regenerated
// </auto-generated>
// ------------------------------------------------------------------------------
namespace AvroComm.dtmi_codegen_communicationTest_avroModel__1
{
	using System;
	using System.Collections.Generic;
	using System.Text;
	using global::Avro;
	using global::Avro.Specific;
	
	[global::System.CodeDom.Compiler.GeneratedCodeAttribute("avrogen", "1.11.3")]
	public partial class TelemetryCollection : global::Avro.Specific.ISpecificRecord
	{
		public static global::Avro.Schema _SCHEMA = global::Avro.Schema.Parse(@"{""type"":""record"",""name"":""TelemetryCollection"",""namespace"":""AvroComm.dtmi_codegen_communicationTest_avroModel__1"",""fields"":[{""name"":""lengths"",""type"":[""null"",{""type"":""array"",""items"":""double""}]},{""name"":""proximity"",""type"":[""null"",{""type"":""enum"",""name"":""Enum_Proximity"",""namespace"":""AvroComm.dtmi_codegen_communicationTest_avroModel__1"",""symbols"":[""near"",""far""]}]},{""name"":""schedule"",""type"":[""null"",{""type"":""record"",""name"":""Object_Schedule"",""namespace"":""AvroComm.dtmi_codegen_communicationTest_avroModel__1"",""fields"":[{""name"":""course"",""type"":[""null"",""string""]},{""name"":""credit"",""type"":[""null"",""string""]}]}]}]}");
		private IList<System.Double> _lengths;
		private System.Nullable<AvroComm.dtmi_codegen_communicationTest_avroModel__1.Enum_Proximity> _proximity;
		private AvroComm.dtmi_codegen_communicationTest_avroModel__1.Object_Schedule _schedule;
		public virtual global::Avro.Schema Schema
		{
			get
			{
				return TelemetryCollection._SCHEMA;
			}
		}
		public IList<System.Double> lengths
		{
			get
			{
				return this._lengths;
			}
			set
			{
				this._lengths = value;
			}
		}
		public System.Nullable<AvroComm.dtmi_codegen_communicationTest_avroModel__1.Enum_Proximity> proximity
		{
			get
			{
				return this._proximity;
			}
			set
			{
				this._proximity = value;
			}
		}
		public AvroComm.dtmi_codegen_communicationTest_avroModel__1.Object_Schedule schedule
		{
			get
			{
				return this._schedule;
			}
			set
			{
				this._schedule = value;
			}
		}
		public virtual object Get(int fieldPos)
		{
			switch (fieldPos)
			{
			case 0: return this.lengths;
			case 1: return this.proximity;
			case 2: return this.schedule;
			default: throw new global::Avro.AvroRuntimeException("Bad index " + fieldPos + " in Get()");
			};
		}
		public virtual void Put(int fieldPos, object fieldValue)
		{
			switch (fieldPos)
			{
			case 0: this.lengths = (IList<System.Double>)fieldValue; break;
			case 1: this.proximity = fieldValue == null ? (System.Nullable<AvroComm.dtmi_codegen_communicationTest_avroModel__1.Enum_Proximity>)null : (AvroComm.dtmi_codegen_communicationTest_avroModel__1.Enum_Proximity)fieldValue; break;
			case 2: this.schedule = (AvroComm.dtmi_codegen_communicationTest_avroModel__1.Object_Schedule)fieldValue; break;
			default: throw new global::Avro.AvroRuntimeException("Bad index " + fieldPos + " in Put()");
			};
		}
	}
}
