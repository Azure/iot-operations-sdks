// <auto-generated>
//     Generated by the protocol buffer compiler.  DO NOT EDIT!
//     source: IsPrimeResponsePayload.proto
// </auto-generated>
#pragma warning disable 1591, 0612, 3021, 8981
#region Designer generated code

using pb = global::Google.Protobuf;
using pbc = global::Google.Protobuf.Collections;
using pbr = global::Google.Protobuf.Reflection;
using scg = global::System.Collections.Generic;
namespace TestEnvoys.dtmi_rpc_samples_math__1 {

  /// <summary>Holder for reflection information generated from IsPrimeResponsePayload.proto</summary>
  public static partial class IsPrimeResponsePayloadReflection {

    #region Descriptor
    /// <summary>File descriptor for IsPrimeResponsePayload.proto</summary>
    public static pbr::FileDescriptor Descriptor {
      get { return descriptor; }
    }
    private static pbr::FileDescriptor descriptor;

    static IsPrimeResponsePayloadReflection() {
      byte[] descriptorData = global::System.Convert.FromBase64String(
          string.Concat(
            "ChxJc1ByaW1lUmVzcG9uc2VQYXlsb2FkLnByb3RvEhhkdG1pX3JwY19zYW1w",
            "bGVzX21hdGhfXzEaHU9iamVjdF9Jc1ByaW1lX1Jlc3BvbnNlLnByb3RvImQK",
            "FklzUHJpbWVSZXNwb25zZVBheWxvYWQSSgoPaXNQcmltZVJlc3BvbnNlGAEg",
            "ASgLMjEuZHRtaV9ycGNfc2FtcGxlc19tYXRoX18xLk9iamVjdF9Jc1ByaW1l",
            "X1Jlc3BvbnNlQkIKGGR0bWlfcnBjX3NhbXBsZXNfbWF0aF9fMVABqgIjVGVz",
            "dEVudm95cy5kdG1pX3JwY19zYW1wbGVzX21hdGhfXzFiBnByb3RvMw=="));
      descriptor = pbr::FileDescriptor.FromGeneratedCode(descriptorData,
          new pbr::FileDescriptor[] { global::TestEnvoys.dtmi_rpc_samples_math__1.ObjectIsPrimeResponseReflection.Descriptor, },
          new pbr::GeneratedClrTypeInfo(null, null, new pbr::GeneratedClrTypeInfo[] {
            new pbr::GeneratedClrTypeInfo(typeof(global::TestEnvoys.dtmi_rpc_samples_math__1.IsPrimeResponsePayload), global::TestEnvoys.dtmi_rpc_samples_math__1.IsPrimeResponsePayload.Parser, new[]{ "IsPrimeResponse" }, null, null, null, null)
          }));
    }
    #endregion

  }
  #region Messages
  public sealed partial class IsPrimeResponsePayload : pb::IMessage<IsPrimeResponsePayload>
  #if !GOOGLE_PROTOBUF_REFSTRUCT_COMPATIBILITY_MODE
      , pb::IBufferMessage
  #endif
  {
    private static readonly pb::MessageParser<IsPrimeResponsePayload> _parser = new pb::MessageParser<IsPrimeResponsePayload>(() => new IsPrimeResponsePayload());
    private pb::UnknownFieldSet _unknownFields;
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public static pb::MessageParser<IsPrimeResponsePayload> Parser { get { return _parser; } }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public static pbr::MessageDescriptor Descriptor {
      get { return global::TestEnvoys.dtmi_rpc_samples_math__1.IsPrimeResponsePayloadReflection.Descriptor.MessageTypes[0]; }
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    pbr::MessageDescriptor pb::IMessage.Descriptor {
      get { return Descriptor; }
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public IsPrimeResponsePayload() {
      OnConstruction();
    }

    partial void OnConstruction();

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public IsPrimeResponsePayload(IsPrimeResponsePayload other) : this() {
      isPrimeResponse_ = other.isPrimeResponse_ != null ? other.isPrimeResponse_.Clone() : null;
      _unknownFields = pb::UnknownFieldSet.Clone(other._unknownFields);
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public IsPrimeResponsePayload Clone() {
      return new IsPrimeResponsePayload(this);
    }

    /// <summary>Field number for the "isPrimeResponse" field.</summary>
    public const int IsPrimeResponseFieldNumber = 1;
    private global::TestEnvoys.dtmi_rpc_samples_math__1.Object_IsPrime_Response isPrimeResponse_;
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public global::TestEnvoys.dtmi_rpc_samples_math__1.Object_IsPrime_Response IsPrimeResponse {
      get { return isPrimeResponse_; }
      set {
        isPrimeResponse_ = value;
      }
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public override bool Equals(object other) {
      return Equals(other as IsPrimeResponsePayload);
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public bool Equals(IsPrimeResponsePayload other) {
      if (ReferenceEquals(other, null)) {
        return false;
      }
      if (ReferenceEquals(other, this)) {
        return true;
      }
      if (!object.Equals(IsPrimeResponse, other.IsPrimeResponse)) return false;
      return Equals(_unknownFields, other._unknownFields);
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public override int GetHashCode() {
      int hash = 1;
      if (isPrimeResponse_ != null) hash ^= IsPrimeResponse.GetHashCode();
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
      if (isPrimeResponse_ != null) {
        output.WriteRawTag(10);
        output.WriteMessage(IsPrimeResponse);
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
      if (isPrimeResponse_ != null) {
        output.WriteRawTag(10);
        output.WriteMessage(IsPrimeResponse);
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
      if (isPrimeResponse_ != null) {
        size += 1 + pb::CodedOutputStream.ComputeMessageSize(IsPrimeResponse);
      }
      if (_unknownFields != null) {
        size += _unknownFields.CalculateSize();
      }
      return size;
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public void MergeFrom(IsPrimeResponsePayload other) {
      if (other == null) {
        return;
      }
      if (other.isPrimeResponse_ != null) {
        if (isPrimeResponse_ == null) {
          IsPrimeResponse = new global::TestEnvoys.dtmi_rpc_samples_math__1.Object_IsPrime_Response();
        }
        IsPrimeResponse.MergeFrom(other.IsPrimeResponse);
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
          case 10: {
            if (isPrimeResponse_ == null) {
              IsPrimeResponse = new global::TestEnvoys.dtmi_rpc_samples_math__1.Object_IsPrime_Response();
            }
            input.ReadMessage(IsPrimeResponse);
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
          case 10: {
            if (isPrimeResponse_ == null) {
              IsPrimeResponse = new global::TestEnvoys.dtmi_rpc_samples_math__1.Object_IsPrime_Response();
            }
            input.ReadMessage(IsPrimeResponse);
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
