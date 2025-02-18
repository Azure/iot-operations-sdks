// <auto-generated>
//     Generated by the protocol buffer compiler.  DO NOT EDIT!
//     source: IsPrimeResponseSchema.proto
// </auto-generated>
#pragma warning disable 1591, 0612, 3021, 8981
#region Designer generated code

using pb = global::Google.Protobuf;
using pbc = global::Google.Protobuf.Collections;
using pbr = global::Google.Protobuf.Reflection;
using scg = global::System.Collections.Generic;
namespace TestEnvoys.Math {

  /// <summary>Holder for reflection information generated from IsPrimeResponseSchema.proto</summary>
  public static partial class IsPrimeResponseSchemaReflection {

    #region Descriptor
    /// <summary>File descriptor for IsPrimeResponseSchema.proto</summary>
    public static pbr::FileDescriptor Descriptor {
      get { return descriptor; }
    }
    private static pbr::FileDescriptor descriptor;

    static IsPrimeResponseSchemaReflection() {
      byte[] descriptorData = global::System.Convert.FromBase64String(
          string.Concat(
            "ChtJc1ByaW1lUmVzcG9uc2VTY2hlbWEucHJvdG8SBE1hdGgaD09wc1NjaGVt",
            "YS5wcm90byKcAgoVSXNQcmltZVJlc3BvbnNlU2NoZW1hEhUKCHRocmVhZElk",
            "GAcgASgRSACIAQESFgoJY29tcHV0ZU1TGAYgASgRSAGIAQESFwoKZXhlY3V0",
            "b3JJZBgFIAEoCUgCiAEBEhYKCWludm9rZXJJZBgEIAEoCUgDiAEBEiEKA29w",
            "cxgDIAEoCzIPLk1hdGguT3BzU2NoZW1hSASIAQESFAoHaXNQcmltZRgCIAEo",
            "CEgFiAEBEhMKBm51bWJlchgBIAEoEUgGiAEBQgsKCV90aHJlYWRJZEIMCgpf",
            "Y29tcHV0ZU1TQg0KC19leGVjdXRvcklkQgwKCl9pbnZva2VySWRCBgoEX29w",
            "c0IKCghfaXNQcmltZUIJCgdfbnVtYmVyQhoKBE1hdGhQAaoCD1Rlc3RFbnZv",
            "eXMuTWF0aGIGcHJvdG8z"));
      descriptor = pbr::FileDescriptor.FromGeneratedCode(descriptorData,
          new pbr::FileDescriptor[] { global::TestEnvoys.Math.OpsSchemaReflection.Descriptor, },
          new pbr::GeneratedClrTypeInfo(null, null, new pbr::GeneratedClrTypeInfo[] {
            new pbr::GeneratedClrTypeInfo(typeof(global::TestEnvoys.Math.IsPrimeResponseSchema), global::TestEnvoys.Math.IsPrimeResponseSchema.Parser, new[]{ "ThreadId", "ComputeMS", "ExecutorId", "InvokerId", "Ops", "IsPrime", "Number" }, new[]{ "ThreadId", "ComputeMS", "ExecutorId", "InvokerId", "Ops", "IsPrime", "Number" }, null, null, null)
          }));
    }
    #endregion

  }
  #region Messages
  [global::System.Diagnostics.DebuggerDisplayAttribute("{ToString(),nq}")]
  public sealed partial class IsPrimeResponseSchema : pb::IMessage<IsPrimeResponseSchema>
  #if !GOOGLE_PROTOBUF_REFSTRUCT_COMPATIBILITY_MODE
      , pb::IBufferMessage
  #endif
  {
    private static readonly pb::MessageParser<IsPrimeResponseSchema> _parser = new pb::MessageParser<IsPrimeResponseSchema>(() => new IsPrimeResponseSchema());
    private pb::UnknownFieldSet _unknownFields;
    private int _hasBits0;
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public static pb::MessageParser<IsPrimeResponseSchema> Parser { get { return _parser; } }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public static pbr::MessageDescriptor Descriptor {
      get { return global::TestEnvoys.Math.IsPrimeResponseSchemaReflection.Descriptor.MessageTypes[0]; }
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    pbr::MessageDescriptor pb::IMessage.Descriptor {
      get { return Descriptor; }
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public IsPrimeResponseSchema() {
      OnConstruction();
    }

    partial void OnConstruction();

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public IsPrimeResponseSchema(IsPrimeResponseSchema other) : this() {
      _hasBits0 = other._hasBits0;
      threadId_ = other.threadId_;
      computeMS_ = other.computeMS_;
      executorId_ = other.executorId_;
      invokerId_ = other.invokerId_;
      ops_ = other.ops_ != null ? other.ops_.Clone() : null;
      isPrime_ = other.isPrime_;
      number_ = other.number_;
      _unknownFields = pb::UnknownFieldSet.Clone(other._unknownFields);
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public IsPrimeResponseSchema Clone() {
      return new IsPrimeResponseSchema(this);
    }

    /// <summary>Field number for the "threadId" field.</summary>
    public const int ThreadIdFieldNumber = 7;
    private readonly static int ThreadIdDefaultValue = 0;

    private int threadId_;
    /// <summary>
    /// The 'threadId' Field.
    /// </summary>
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public int ThreadId {
      get { if ((_hasBits0 & 8) != 0) { return threadId_; } else { return ThreadIdDefaultValue; } }
      set {
        _hasBits0 |= 8;
        threadId_ = value;
      }
    }
    /// <summary>Gets whether the "threadId" field is set</summary>
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public bool HasThreadId {
      get { return (_hasBits0 & 8) != 0; }
    }
    /// <summary>Clears the value of the "threadId" field</summary>
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public void ClearThreadId() {
      _hasBits0 &= ~8;
    }

    /// <summary>Field number for the "computeMS" field.</summary>
    public const int ComputeMSFieldNumber = 6;
    private readonly static int ComputeMSDefaultValue = 0;

    private int computeMS_;
    /// <summary>
    /// The 'computeMS' Field.
    /// </summary>
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public int ComputeMS {
      get { if ((_hasBits0 & 4) != 0) { return computeMS_; } else { return ComputeMSDefaultValue; } }
      set {
        _hasBits0 |= 4;
        computeMS_ = value;
      }
    }
    /// <summary>Gets whether the "computeMS" field is set</summary>
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public bool HasComputeMS {
      get { return (_hasBits0 & 4) != 0; }
    }
    /// <summary>Clears the value of the "computeMS" field</summary>
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public void ClearComputeMS() {
      _hasBits0 &= ~4;
    }

    /// <summary>Field number for the "executorId" field.</summary>
    public const int ExecutorIdFieldNumber = 5;
    private readonly static string ExecutorIdDefaultValue = "";

    private string executorId_;
    /// <summary>
    /// The 'executorId' Field.
    /// </summary>
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public string ExecutorId {
      get { return executorId_ ?? ExecutorIdDefaultValue; }
      set {
        executorId_ = pb::ProtoPreconditions.CheckNotNull(value, "value");
      }
    }
    /// <summary>Gets whether the "executorId" field is set</summary>
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public bool HasExecutorId {
      get { return executorId_ != null; }
    }
    /// <summary>Clears the value of the "executorId" field</summary>
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public void ClearExecutorId() {
      executorId_ = null;
    }

    /// <summary>Field number for the "invokerId" field.</summary>
    public const int InvokerIdFieldNumber = 4;
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

    /// <summary>Field number for the "ops" field.</summary>
    public const int OpsFieldNumber = 3;
    private global::TestEnvoys.Math.OpsSchema ops_;
    /// <summary>
    /// The 'ops' Field.
    /// </summary>
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public global::TestEnvoys.Math.OpsSchema Ops {
      get { return ops_; }
      set {
        ops_ = value;
      }
    }

    /// <summary>Field number for the "isPrime" field.</summary>
    public const int IsPrimeFieldNumber = 2;
    private readonly static bool IsPrimeDefaultValue = false;

    private bool isPrime_;
    /// <summary>
    /// The 'isPrime' Field.
    /// </summary>
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public bool IsPrime {
      get { if ((_hasBits0 & 2) != 0) { return isPrime_; } else { return IsPrimeDefaultValue; } }
      set {
        _hasBits0 |= 2;
        isPrime_ = value;
      }
    }
    /// <summary>Gets whether the "isPrime" field is set</summary>
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public bool HasIsPrime {
      get { return (_hasBits0 & 2) != 0; }
    }
    /// <summary>Clears the value of the "isPrime" field</summary>
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public void ClearIsPrime() {
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
      return Equals(other as IsPrimeResponseSchema);
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public bool Equals(IsPrimeResponseSchema other) {
      if (ReferenceEquals(other, null)) {
        return false;
      }
      if (ReferenceEquals(other, this)) {
        return true;
      }
      if (ThreadId != other.ThreadId) return false;
      if (ComputeMS != other.ComputeMS) return false;
      if (ExecutorId != other.ExecutorId) return false;
      if (InvokerId != other.InvokerId) return false;
      if (!object.Equals(Ops, other.Ops)) return false;
      if (IsPrime != other.IsPrime) return false;
      if (Number != other.Number) return false;
      return Equals(_unknownFields, other._unknownFields);
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public override int GetHashCode() {
      int hash = 1;
      if (HasThreadId) hash ^= ThreadId.GetHashCode();
      if (HasComputeMS) hash ^= ComputeMS.GetHashCode();
      if (HasExecutorId) hash ^= ExecutorId.GetHashCode();
      if (HasInvokerId) hash ^= InvokerId.GetHashCode();
      if (ops_ != null) hash ^= Ops.GetHashCode();
      if (HasIsPrime) hash ^= IsPrime.GetHashCode();
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
      if (HasIsPrime) {
        output.WriteRawTag(16);
        output.WriteBool(IsPrime);
      }
      if (ops_ != null) {
        output.WriteRawTag(26);
        output.WriteMessage(Ops);
      }
      if (HasInvokerId) {
        output.WriteRawTag(34);
        output.WriteString(InvokerId);
      }
      if (HasExecutorId) {
        output.WriteRawTag(42);
        output.WriteString(ExecutorId);
      }
      if (HasComputeMS) {
        output.WriteRawTag(48);
        output.WriteSInt32(ComputeMS);
      }
      if (HasThreadId) {
        output.WriteRawTag(56);
        output.WriteSInt32(ThreadId);
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
      if (HasIsPrime) {
        output.WriteRawTag(16);
        output.WriteBool(IsPrime);
      }
      if (ops_ != null) {
        output.WriteRawTag(26);
        output.WriteMessage(Ops);
      }
      if (HasInvokerId) {
        output.WriteRawTag(34);
        output.WriteString(InvokerId);
      }
      if (HasExecutorId) {
        output.WriteRawTag(42);
        output.WriteString(ExecutorId);
      }
      if (HasComputeMS) {
        output.WriteRawTag(48);
        output.WriteSInt32(ComputeMS);
      }
      if (HasThreadId) {
        output.WriteRawTag(56);
        output.WriteSInt32(ThreadId);
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
      if (HasThreadId) {
        size += 1 + pb::CodedOutputStream.ComputeSInt32Size(ThreadId);
      }
      if (HasComputeMS) {
        size += 1 + pb::CodedOutputStream.ComputeSInt32Size(ComputeMS);
      }
      if (HasExecutorId) {
        size += 1 + pb::CodedOutputStream.ComputeStringSize(ExecutorId);
      }
      if (HasInvokerId) {
        size += 1 + pb::CodedOutputStream.ComputeStringSize(InvokerId);
      }
      if (ops_ != null) {
        size += 1 + pb::CodedOutputStream.ComputeMessageSize(Ops);
      }
      if (HasIsPrime) {
        size += 1 + 1;
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
    public void MergeFrom(IsPrimeResponseSchema other) {
      if (other == null) {
        return;
      }
      if (other.HasThreadId) {
        ThreadId = other.ThreadId;
      }
      if (other.HasComputeMS) {
        ComputeMS = other.ComputeMS;
      }
      if (other.HasExecutorId) {
        ExecutorId = other.ExecutorId;
      }
      if (other.HasInvokerId) {
        InvokerId = other.InvokerId;
      }
      if (other.ops_ != null) {
        if (ops_ == null) {
          Ops = new global::TestEnvoys.Math.OpsSchema();
        }
        Ops.MergeFrom(other.Ops);
      }
      if (other.HasIsPrime) {
        IsPrime = other.IsPrime;
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
      if ((tag & 7) == 4) {
        // Abort on any end group tag.
        return;
      }
      switch(tag) {
          default:
            _unknownFields = pb::UnknownFieldSet.MergeFieldFrom(_unknownFields, input);
            break;
          case 8: {
            Number = input.ReadSInt32();
            break;
          }
          case 16: {
            IsPrime = input.ReadBool();
            break;
          }
          case 26: {
            if (ops_ == null) {
              Ops = new global::TestEnvoys.Math.OpsSchema();
            }
            input.ReadMessage(Ops);
            break;
          }
          case 34: {
            InvokerId = input.ReadString();
            break;
          }
          case 42: {
            ExecutorId = input.ReadString();
            break;
          }
          case 48: {
            ComputeMS = input.ReadSInt32();
            break;
          }
          case 56: {
            ThreadId = input.ReadSInt32();
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
      if ((tag & 7) == 4) {
        // Abort on any end group tag.
        return;
      }
      switch(tag) {
          default:
            _unknownFields = pb::UnknownFieldSet.MergeFieldFrom(_unknownFields, ref input);
            break;
          case 8: {
            Number = input.ReadSInt32();
            break;
          }
          case 16: {
            IsPrime = input.ReadBool();
            break;
          }
          case 26: {
            if (ops_ == null) {
              Ops = new global::TestEnvoys.Math.OpsSchema();
            }
            input.ReadMessage(Ops);
            break;
          }
          case 34: {
            InvokerId = input.ReadString();
            break;
          }
          case 42: {
            ExecutorId = input.ReadString();
            break;
          }
          case 48: {
            ComputeMS = input.ReadSInt32();
            break;
          }
          case 56: {
            ThreadId = input.ReadSInt32();
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
