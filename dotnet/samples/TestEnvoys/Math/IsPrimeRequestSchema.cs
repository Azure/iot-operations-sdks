// <auto-generated>
//     Generated by the protocol buffer compiler.  DO NOT EDIT!
//     source: IsPrimeRequestSchema.proto
// </auto-generated>
#pragma warning disable 1591, 0612, 3021, 8981
#region Designer generated code

using pb = global::Google.Protobuf;
using pbc = global::Google.Protobuf.Collections;
using pbr = global::Google.Protobuf.Reflection;
using scg = global::System.Collections.Generic;
namespace TestEnvoys.Math {

  /// <summary>Holder for reflection information generated from IsPrimeRequestSchema.proto</summary>
  public static partial class IsPrimeRequestSchemaReflection {

    #region Descriptor
    /// <summary>File descriptor for IsPrimeRequestSchema.proto</summary>
    public static pbr::FileDescriptor Descriptor {
      get { return descriptor; }
    }
    private static pbr::FileDescriptor descriptor;

    static IsPrimeRequestSchemaReflection() {
      byte[] descriptorData = global::System.Convert.FromBase64String(
          string.Concat(
            "ChpJc1ByaW1lUmVxdWVzdFNjaGVtYS5wcm90bxIETWF0aCKQAQoUSXNQcmlt",
            "ZVJlcXVlc3RTY2hlbWESFgoJaW52b2tlcklkGAMgASgJSACIAQESHQoQaW52",
            "b2tlclN0YXJ0VGltZRgCIAEoEUgBiAEBEhMKBm51bWJlchgBIAEoEUgCiAEB",
            "QgwKCl9pbnZva2VySWRCEwoRX2ludm9rZXJTdGFydFRpbWVCCQoHX251bWJl",
            "ckIaCgRNYXRoUAGqAg9UZXN0RW52b3lzLk1hdGhiBnByb3RvMw=="));
      descriptor = pbr::FileDescriptor.FromGeneratedCode(descriptorData,
          new pbr::FileDescriptor[] { },
          new pbr::GeneratedClrTypeInfo(null, null, new pbr::GeneratedClrTypeInfo[] {
            new pbr::GeneratedClrTypeInfo(typeof(global::TestEnvoys.Math.IsPrimeRequestSchema), global::TestEnvoys.Math.IsPrimeRequestSchema.Parser, new[]{ "InvokerId", "InvokerStartTime", "Number" }, new[]{ "InvokerId", "InvokerStartTime", "Number" }, null, null, null)
          }));
    }
    #endregion

  }
  #region Messages
  [global::System.Diagnostics.DebuggerDisplayAttribute("{ToString(),nq}")]
  public sealed partial class IsPrimeRequestSchema : pb::IMessage<IsPrimeRequestSchema>
  #if !GOOGLE_PROTOBUF_REFSTRUCT_COMPATIBILITY_MODE
      , pb::IBufferMessage
  #endif
  {
    private static readonly pb::MessageParser<IsPrimeRequestSchema> _parser = new pb::MessageParser<IsPrimeRequestSchema>(() => new IsPrimeRequestSchema());
    private pb::UnknownFieldSet _unknownFields;
    private int _hasBits0;
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public static pb::MessageParser<IsPrimeRequestSchema> Parser { get { return _parser; } }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public static pbr::MessageDescriptor Descriptor {
      get { return global::TestEnvoys.Math.IsPrimeRequestSchemaReflection.Descriptor.MessageTypes[0]; }
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    pbr::MessageDescriptor pb::IMessage.Descriptor {
      get { return Descriptor; }
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public IsPrimeRequestSchema() {
      OnConstruction();
    }

    partial void OnConstruction();

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public IsPrimeRequestSchema(IsPrimeRequestSchema other) : this() {
      _hasBits0 = other._hasBits0;
      invokerId_ = other.invokerId_;
      invokerStartTime_ = other.invokerStartTime_;
      number_ = other.number_;
      _unknownFields = pb::UnknownFieldSet.Clone(other._unknownFields);
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public IsPrimeRequestSchema Clone() {
      return new IsPrimeRequestSchema(this);
    }

    /// <summary>Field number for the "invokerId" field.</summary>
    public const int InvokerIdFieldNumber = 3;
    private readonly static string InvokerIdDefaultValue = "";

    private string invokerId_;
    /// <summary>
    /// The 'invokerId' Field.
    /// </summary>
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public string InvokerId {
      get { return invokerId_ ?? InvokerIdDefaultValue; }
      set {
        invokerId_ = pb::ProtoPreconditions.CheckNotNull(value, "value");
      }
    }
    /// <summary>Gets whether the "invokerId" field is set</summary>
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public bool HasInvokerId {
      get { return invokerId_ != null; }
    }
    /// <summary>Clears the value of the "invokerId" field</summary>
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public void ClearInvokerId() {
      invokerId_ = null;
    }

    /// <summary>Field number for the "invokerStartTime" field.</summary>
    public const int InvokerStartTimeFieldNumber = 2;
    private readonly static int InvokerStartTimeDefaultValue = 0;

    private int invokerStartTime_;
    /// <summary>
    /// The 'invokerStartTime' Field.
    /// </summary>
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public int InvokerStartTime {
      get { if ((_hasBits0 & 2) != 0) { return invokerStartTime_; } else { return InvokerStartTimeDefaultValue; } }
      set {
        _hasBits0 |= 2;
        invokerStartTime_ = value;
      }
    }
    /// <summary>Gets whether the "invokerStartTime" field is set</summary>
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public bool HasInvokerStartTime {
      get { return (_hasBits0 & 2) != 0; }
    }
    /// <summary>Clears the value of the "invokerStartTime" field</summary>
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public void ClearInvokerStartTime() {
      _hasBits0 &= ~2;
    }

    /// <summary>Field number for the "number" field.</summary>
    public const int NumberFieldNumber = 1;
    private readonly static int NumberDefaultValue = 0;

    private int number_;
    /// <summary>
    /// The 'number' Field.
    /// </summary>
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public int Number {
      get { if ((_hasBits0 & 1) != 0) { return number_; } else { return NumberDefaultValue; } }
      set {
        _hasBits0 |= 1;
        number_ = value;
      }
    }
    /// <summary>Gets whether the "number" field is set</summary>
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public bool HasNumber {
      get { return (_hasBits0 & 1) != 0; }
    }
    /// <summary>Clears the value of the "number" field</summary>
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public void ClearNumber() {
      _hasBits0 &= ~1;
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public override bool Equals(object other) {
      return Equals(other as IsPrimeRequestSchema);
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public bool Equals(IsPrimeRequestSchema other) {
      if (ReferenceEquals(other, null)) {
        return false;
      }
      if (ReferenceEquals(other, this)) {
        return true;
      }
      if (InvokerId != other.InvokerId) return false;
      if (InvokerStartTime != other.InvokerStartTime) return false;
      if (Number != other.Number) return false;
      return Equals(_unknownFields, other._unknownFields);
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public override int GetHashCode() {
      int hash = 1;
      if (HasInvokerId) hash ^= InvokerId.GetHashCode();
      if (HasInvokerStartTime) hash ^= InvokerStartTime.GetHashCode();
      if (HasNumber) hash ^= Number.GetHashCode();
      if (_unknownFields != null) {
        hash ^= _unknownFields.GetHashCode();
      }
      return hash;
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public override string ToString() {
      return pb::JsonFormatter.ToDiagnosticString(this);
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public void WriteTo(pb::CodedOutputStream output) {
    #if !GOOGLE_PROTOBUF_REFSTRUCT_COMPATIBILITY_MODE
      output.WriteRawMessage(this);
    #else
      if (HasNumber) {
        output.WriteRawTag(8);
        output.WriteSInt32(Number);
      }
      if (HasInvokerStartTime) {
        output.WriteRawTag(16);
        output.WriteSInt32(InvokerStartTime);
      }
      if (HasInvokerId) {
        output.WriteRawTag(26);
        output.WriteString(InvokerId);
      }
      if (_unknownFields != null) {
        _unknownFields.WriteTo(output);
      }
    #endif
    }

    #if !GOOGLE_PROTOBUF_REFSTRUCT_COMPATIBILITY_MODE
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    void pb::IBufferMessage.InternalWriteTo(ref pb::WriteContext output) {
      if (HasNumber) {
        output.WriteRawTag(8);
        output.WriteSInt32(Number);
      }
      if (HasInvokerStartTime) {
        output.WriteRawTag(16);
        output.WriteSInt32(InvokerStartTime);
      }
      if (HasInvokerId) {
        output.WriteRawTag(26);
        output.WriteString(InvokerId);
      }
      if (_unknownFields != null) {
        _unknownFields.WriteTo(ref output);
      }
    }
    #endif

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public int CalculateSize() {
      int size = 0;
      if (HasInvokerId) {
        size += 1 + pb::CodedOutputStream.ComputeStringSize(InvokerId);
      }
      if (HasInvokerStartTime) {
        size += 1 + pb::CodedOutputStream.ComputeSInt32Size(InvokerStartTime);
      }
      if (HasNumber) {
        size += 1 + pb::CodedOutputStream.ComputeSInt32Size(Number);
      }
      if (_unknownFields != null) {
        size += _unknownFields.CalculateSize();
      }
      return size;
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public void MergeFrom(IsPrimeRequestSchema other) {
      if (other == null) {
        return;
      }
      if (other.HasInvokerId) {
        InvokerId = other.InvokerId;
      }
      if (other.HasInvokerStartTime) {
        InvokerStartTime = other.InvokerStartTime;
      }
      if (other.HasNumber) {
        Number = other.Number;
      }
      _unknownFields = pb::UnknownFieldSet.MergeFrom(_unknownFields, other._unknownFields);
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public void MergeFrom(pb::CodedInputStream input) {
    #if !GOOGLE_PROTOBUF_REFSTRUCT_COMPATIBILITY_MODE
      input.ReadRawMessage(this);
    #else
      uint tag;
      while ((tag = input.ReadTag()) != 0) {
        switch(tag) {
          default:
            _unknownFields = pb::UnknownFieldSet.MergeFieldFrom(_unknownFields, input);
            break;
          case 8: {
            Number = input.ReadSInt32();
            break;
          }
          case 16: {
            InvokerStartTime = input.ReadSInt32();
            break;
          }
          case 26: {
            InvokerId = input.ReadString();
            break;
          }
        }
      }
    #endif
    }

    #if !GOOGLE_PROTOBUF_REFSTRUCT_COMPATIBILITY_MODE
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    void pb::IBufferMessage.InternalMergeFrom(ref pb::ParseContext input) {
      uint tag;
      while ((tag = input.ReadTag()) != 0) {
        switch(tag) {
          default:
            _unknownFields = pb::UnknownFieldSet.MergeFieldFrom(_unknownFields, ref input);
            break;
          case 8: {
            Number = input.ReadSInt32();
            break;
          }
          case 16: {
            InvokerStartTime = input.ReadSInt32();
            break;
          }
          case 26: {
            InvokerId = input.ReadString();
            break;
          }
        }
      }
    }
    #endif

  }

  #endregion

}

#endregion Designer generated code
