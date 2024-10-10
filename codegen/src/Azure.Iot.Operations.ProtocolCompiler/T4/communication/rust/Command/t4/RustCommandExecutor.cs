// ------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Runtime Version: 17.0.0.0
//  
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
// ------------------------------------------------------------------------------
namespace Azure.Iot.Operations.ProtocolCompiler
{
    using Azure.Iot.Operations.ProtocolCompiler;
    using System;
    
    /// <summary>
    /// Class to produce the template output
    /// </summary>
    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("Microsoft.VisualStudio.TextTemplating", "17.0.0.0")]
    public partial class RustCommandExecutor : RustCommandExecutorBase
    {
        /// <summary>
        /// Create the template output
        /// </summary>
        public virtual string TransformText()
        {
            this.Write("/* Code generated by Azure.Iot.Operations.ProtocolCompiler; DO NOT EDIT. */\r\n");
 if (this.ttl != null) { 
            this.Write("\r\nuse iso8601_duration;\r\n");
 } 
            this.Write("\r\nuse super::super::common_types::common_options::CommonOptions;\r\n");
 if (this.reqSchema == null || this.respSchema == null) { 
            this.Write("use super::super::common_types::");
            this.Write(this.ToStringHelper.ToStringWithCulture(NamingSupport.ToSnakeCase(this.serializerEmptyType)));
            this.Write("::");
            this.Write(this.ToStringHelper.ToStringWithCulture(this.serializerEmptyType));
            this.Write(";\r\n");
 } 
 if (this.reqSchema == "Bytes" || this.respSchema == "Bytes") { 
            this.Write("use super::super::common_types::bytes::Bytes;\r\n");
 } 
 if (this.reqSchema != null && this.reqSchema != "Bytes") { 
            this.Write("use super::");
            this.Write(this.ToStringHelper.ToStringWithCulture(NamingSupport.ToSnakeCase(this.reqSchema)));
            this.Write("::");
            this.Write(this.ToStringHelper.ToStringWithCulture(this.reqSchema));
            this.Write(";\r\n");
 } 
 if (this.respSchema != null && this.respSchema != "Bytes") { 
            this.Write("use super::");
            this.Write(this.ToStringHelper.ToStringWithCulture(NamingSupport.ToSnakeCase(this.respSchema)));
            this.Write("::");
            this.Write(this.ToStringHelper.ToStringWithCulture(this.respSchema));
            this.Write(";\r\n");
 } 
            this.Write("use super::MODEL_ID;\r\nuse super::REQUEST_TOPIC_PATTERN;\r\nuse azure_iot_operations" +
                    "_mqtt::interface::ManagedClient;\r\nuse azure_iot_operations_protocol::common::aio" +
                    "_protocol_error::AIOProtocolError;\r\n");
 if (this.respSchema != null) { 
            this.Write("use azure_iot_operations_protocol::common::payload_serialize::PayloadSerialize;\r\n" +
                    "");
 } 
            this.Write("use azure_iot_operations_protocol::rpc::command_executor::{\r\n    CommandExecutor," +
                    " CommandExecutorOptionsBuilder, CommandRequest, CommandResponse,\r\n    CommandRes" +
                    "ponseBuilder, CommandResponseBuilderError,\r\n};\r\n\r\npub type ");
            this.Write(this.ToStringHelper.ToStringWithCulture(this.capitalizedCommandName));
            this.Write("Request =\r\n    CommandRequest<");
            this.Write(this.ToStringHelper.ToStringWithCulture(this.RequestType()));
            this.Write(", ");
            this.Write(this.ToStringHelper.ToStringWithCulture(this.ResponseType()));
            this.Write(">;\r\npub type ");
            this.Write(this.ToStringHelper.ToStringWithCulture(this.capitalizedCommandName));
            this.Write("Response = CommandResponse<");
            this.Write(this.ToStringHelper.ToStringWithCulture(this.ResponseType()));
            this.Write(">;\r\npub type ");
            this.Write(this.ToStringHelper.ToStringWithCulture(this.capitalizedCommandName));
            this.Write("ResponseBuilderError = CommandResponseBuilderError;\r\n\r\n#[derive(Default)]\r\npub st" +
                    "ruct ");
            this.Write(this.ToStringHelper.ToStringWithCulture(this.capitalizedCommandName));
            this.Write("ResponseBuilder {\r\n    inner_builder: CommandResponseBuilder<");
            this.Write(this.ToStringHelper.ToStringWithCulture(this.ResponseType()));
            this.Write(">,\r\n}\r\n\r\nimpl ");
            this.Write(this.ToStringHelper.ToStringWithCulture(this.capitalizedCommandName));
            this.Write("ResponseBuilder {\r\n    pub fn custom_user_data(&mut self, custom_user_data: Vec<(" +
                    "String, String)>) -> &mut Self {\r\n        self.inner_builder.custom_user_data(cu" +
                    "stom_user_data);\r\n        self\r\n    }\r\n\r\n");
 if (this.respSchema != null) { 
            this.Write("    pub fn payload(\r\n        &mut self,\r\n        payload: &");
            this.Write(this.ToStringHelper.ToStringWithCulture(this.respSchema));
            this.Write(",\r\n    ) -> Result<&mut Self, <");
            this.Write(this.ToStringHelper.ToStringWithCulture(this.respSchema));
            this.Write(" as PayloadSerialize>::Error> {\r\n        self.inner_builder.payload(payload)?;\r\n " +
                    "       Ok(self)\r\n    }\r\n\r\n");
 } 
            this.Write("    pub fn build(&mut self) -> Result<");
            this.Write(this.ToStringHelper.ToStringWithCulture(this.capitalizedCommandName));
            this.Write("Response, ");
            this.Write(this.ToStringHelper.ToStringWithCulture(this.capitalizedCommandName));
            this.Write("ResponseBuilderError> {\r\n");
 if (this.respSchema == null) { 
            this.Write("        self.inner_builder.payload(&EmptyJson {}).unwrap();\r\n\r\n");
 } 
            this.Write("        self.inner_builder.build()\r\n    }\r\n}\r\n\r\npub struct ");
            this.Write(this.ToStringHelper.ToStringWithCulture(this.capitalizedCommandName));
            this.Write("CommandExecutor<C>(\r\n    CommandExecutor<");
            this.Write(this.ToStringHelper.ToStringWithCulture(this.RequestType()));
            this.Write(", ");
            this.Write(this.ToStringHelper.ToStringWithCulture(this.ResponseType()));
            this.Write(", C>,\r\n)\r\nwhere\r\n    C: ManagedClient + Clone + Send + Sync + \'static,\r\n    C::Pu" +
                    "bReceiver: Send + Sync + \'static;\r\n\r\nimpl<C> ");
            this.Write(this.ToStringHelper.ToStringWithCulture(this.capitalizedCommandName));
            this.Write(@"CommandExecutor<C>
where
    C: ManagedClient + Clone + Send + Sync + 'static,
    C::PubReceiver: Send + Sync + 'static,
{
    pub fn new(client: C, options: &CommonOptions) -> Self {
        let mut executor_options_builder = CommandExecutorOptionsBuilder::default();
        if let Some(topic_namespace) = &options.topic_namespace {
            executor_options_builder.topic_namespace(topic_namespace.clone());
        }
        let executor_options = executor_options_builder
            .model_id(MODEL_ID.to_string())
            .request_topic_pattern(REQUEST_TOPIC_PATTERN)
            .command_name(""");
            this.Write(this.ToStringHelper.ToStringWithCulture(this.commandName));
            this.Write("\")\r\n");
 if (this.ttl != null) { 
            this.Write("            .cacheable_duration(\r\n                \"");
            this.Write(this.ToStringHelper.ToStringWithCulture(this.ttl));
            this.Write("\"\r\n                    .parse::<iso8601_duration::Duration>()\r\n                  " +
                    "  .unwrap()\r\n                    .to_std()\r\n                    .unwrap(),\r\n    " +
                    "        )\r\n");
 } 
            this.Write("            .is_idempotent(");
            this.Write(this.ToStringHelper.ToStringWithCulture(this.isIdempotent ? "true" : "false"));
            this.Write(@")
            .custom_topic_token_map(options.custom_topic_token_map.clone())
            .build()
            .expect(""DTDL schema generated invalid arguments"");
        Self(
            CommandExecutor::new(client, executor_options)
                .expect(""DTDL schema generated invalid arguments""),
        )
    }

    pub async fn recv(&mut self) -> Result<");
            this.Write(this.ToStringHelper.ToStringWithCulture(this.capitalizedCommandName));
            this.Write("Request, AIOProtocolError> {\r\n        self.0.recv().await\r\n    }\r\n}\r\n");
            return this.GenerationEnvironment.ToString();
        }

    private string RequestType() => this.reqSchema == "Bytes" ? "Bytes" : this.reqSchema ?? this.serializerEmptyType;

    private string ResponseType() => this.respSchema == "Bytes" ? "Bytes" : this.respSchema ?? this.serializerEmptyType;

    }
    #region Base class
    /// <summary>
    /// Base class for this transformation
    /// </summary>
    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("Microsoft.VisualStudio.TextTemplating", "17.0.0.0")]
    public class RustCommandExecutorBase
    {
        #region Fields
        private global::System.Text.StringBuilder generationEnvironmentField;
        private global::System.CodeDom.Compiler.CompilerErrorCollection errorsField;
        private global::System.Collections.Generic.List<int> indentLengthsField;
        private string currentIndentField = "";
        private bool endsWithNewline;
        private global::System.Collections.Generic.IDictionary<string, object> sessionField;
        #endregion
        #region Properties
        /// <summary>
        /// The string builder that generation-time code is using to assemble generated output
        /// </summary>
        public System.Text.StringBuilder GenerationEnvironment
        {
            get
            {
                if ((this.generationEnvironmentField == null))
                {
                    this.generationEnvironmentField = new global::System.Text.StringBuilder();
                }
                return this.generationEnvironmentField;
            }
            set
            {
                this.generationEnvironmentField = value;
            }
        }
        /// <summary>
        /// The error collection for the generation process
        /// </summary>
        public System.CodeDom.Compiler.CompilerErrorCollection Errors
        {
            get
            {
                if ((this.errorsField == null))
                {
                    this.errorsField = new global::System.CodeDom.Compiler.CompilerErrorCollection();
                }
                return this.errorsField;
            }
        }
        /// <summary>
        /// A list of the lengths of each indent that was added with PushIndent
        /// </summary>
        private System.Collections.Generic.List<int> indentLengths
        {
            get
            {
                if ((this.indentLengthsField == null))
                {
                    this.indentLengthsField = new global::System.Collections.Generic.List<int>();
                }
                return this.indentLengthsField;
            }
        }
        /// <summary>
        /// Gets the current indent we use when adding lines to the output
        /// </summary>
        public string CurrentIndent
        {
            get
            {
                return this.currentIndentField;
            }
        }
        /// <summary>
        /// Current transformation session
        /// </summary>
        public virtual global::System.Collections.Generic.IDictionary<string, object> Session
        {
            get
            {
                return this.sessionField;
            }
            set
            {
                this.sessionField = value;
            }
        }
        #endregion
        #region Transform-time helpers
        /// <summary>
        /// Write text directly into the generated output
        /// </summary>
        public void Write(string textToAppend)
        {
            if (string.IsNullOrEmpty(textToAppend))
            {
                return;
            }
            // If we're starting off, or if the previous text ended with a newline,
            // we have to append the current indent first.
            if (((this.GenerationEnvironment.Length == 0) 
                        || this.endsWithNewline))
            {
                this.GenerationEnvironment.Append(this.currentIndentField);
                this.endsWithNewline = false;
            }
            // Check if the current text ends with a newline
            if (textToAppend.EndsWith(global::System.Environment.NewLine, global::System.StringComparison.CurrentCulture))
            {
                this.endsWithNewline = true;
            }
            // This is an optimization. If the current indent is "", then we don't have to do any
            // of the more complex stuff further down.
            if ((this.currentIndentField.Length == 0))
            {
                this.GenerationEnvironment.Append(textToAppend);
                return;
            }
            // Everywhere there is a newline in the text, add an indent after it
            textToAppend = textToAppend.Replace(global::System.Environment.NewLine, (global::System.Environment.NewLine + this.currentIndentField));
            // If the text ends with a newline, then we should strip off the indent added at the very end
            // because the appropriate indent will be added when the next time Write() is called
            if (this.endsWithNewline)
            {
                this.GenerationEnvironment.Append(textToAppend, 0, (textToAppend.Length - this.currentIndentField.Length));
            }
            else
            {
                this.GenerationEnvironment.Append(textToAppend);
            }
        }
        /// <summary>
        /// Write text directly into the generated output
        /// </summary>
        public void WriteLine(string textToAppend)
        {
            this.Write(textToAppend);
            this.GenerationEnvironment.AppendLine();
            this.endsWithNewline = true;
        }
        /// <summary>
        /// Write formatted text directly into the generated output
        /// </summary>
        public void Write(string format, params object[] args)
        {
            this.Write(string.Format(global::System.Globalization.CultureInfo.CurrentCulture, format, args));
        }
        /// <summary>
        /// Write formatted text directly into the generated output
        /// </summary>
        public void WriteLine(string format, params object[] args)
        {
            this.WriteLine(string.Format(global::System.Globalization.CultureInfo.CurrentCulture, format, args));
        }
        /// <summary>
        /// Raise an error
        /// </summary>
        public void Error(string message)
        {
            System.CodeDom.Compiler.CompilerError error = new global::System.CodeDom.Compiler.CompilerError();
            error.ErrorText = message;
            this.Errors.Add(error);
        }
        /// <summary>
        /// Raise a warning
        /// </summary>
        public void Warning(string message)
        {
            System.CodeDom.Compiler.CompilerError error = new global::System.CodeDom.Compiler.CompilerError();
            error.ErrorText = message;
            error.IsWarning = true;
            this.Errors.Add(error);
        }
        /// <summary>
        /// Increase the indent
        /// </summary>
        public void PushIndent(string indent)
        {
            if ((indent == null))
            {
                throw new global::System.ArgumentNullException("indent");
            }
            this.currentIndentField = (this.currentIndentField + indent);
            this.indentLengths.Add(indent.Length);
        }
        /// <summary>
        /// Remove the last indent that was added with PushIndent
        /// </summary>
        public string PopIndent()
        {
            string returnValue = "";
            if ((this.indentLengths.Count > 0))
            {
                int indentLength = this.indentLengths[(this.indentLengths.Count - 1)];
                this.indentLengths.RemoveAt((this.indentLengths.Count - 1));
                if ((indentLength > 0))
                {
                    returnValue = this.currentIndentField.Substring((this.currentIndentField.Length - indentLength));
                    this.currentIndentField = this.currentIndentField.Remove((this.currentIndentField.Length - indentLength));
                }
            }
            return returnValue;
        }
        /// <summary>
        /// Remove any indentation
        /// </summary>
        public void ClearIndent()
        {
            this.indentLengths.Clear();
            this.currentIndentField = "";
        }
        #endregion
        #region ToString Helpers
        /// <summary>
        /// Utility class to produce culture-oriented representation of an object as a string.
        /// </summary>
        public class ToStringInstanceHelper
        {
            private System.IFormatProvider formatProviderField  = global::System.Globalization.CultureInfo.InvariantCulture;
            /// <summary>
            /// Gets or sets format provider to be used by ToStringWithCulture method.
            /// </summary>
            public System.IFormatProvider FormatProvider
            {
                get
                {
                    return this.formatProviderField ;
                }
                set
                {
                    if ((value != null))
                    {
                        this.formatProviderField  = value;
                    }
                }
            }
            /// <summary>
            /// This is called from the compile/run appdomain to convert objects within an expression block to a string
            /// </summary>
            public string ToStringWithCulture(object objectToConvert)
            {
                if ((objectToConvert == null))
                {
                    throw new global::System.ArgumentNullException("objectToConvert");
                }
                System.Type t = objectToConvert.GetType();
                System.Reflection.MethodInfo method = t.GetMethod("ToString", new System.Type[] {
                            typeof(System.IFormatProvider)});
                if ((method == null))
                {
                    return objectToConvert.ToString();
                }
                else
                {
                    return ((string)(method.Invoke(objectToConvert, new object[] {
                                this.formatProviderField })));
                }
            }
        }
        private ToStringInstanceHelper toStringHelperField = new ToStringInstanceHelper();
        /// <summary>
        /// Helper to produce culture-oriented representation of an object as a string
        /// </summary>
        public ToStringInstanceHelper ToStringHelper
        {
            get
            {
                return this.toStringHelperField;
            }
        }
        #endregion
    }
    #endregion
}
