// ------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Runtime Version: 17.0.0.0
//  
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
// ------------------------------------------------------------------------------
namespace Akri.Dtdl.Codegen
{
    using Akri.Dtdl.Codegen;
    using System;
    
    /// <summary>
    /// Class to produce the template output
    /// </summary>
    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("Microsoft.VisualStudio.TextTemplating", "17.0.0.0")]
    public partial class RustTelemetryReceiver : RustTelemetryReceiverBase
    {
        /// <summary>
        /// Create the template output
        /// </summary>
        public virtual string TransformText()
        {
            this.Write(@"/* Code generated by Akri.Dtdl.Codegen; DO NOT EDIT. */

use std::ops::{Deref, DerefMut};

use azure_iot_operations_mqtt::interface::{
    MqttProvider, MqttPubSub, MqttPubReceiver, MqttAck,
};
use azure_iot_operations_protocol::telemetry::telemetry_receiver::{
    TelemetryReceiver, TelemetryReceiverOptionsBuilder,
};
use azure_iot_operations_protocol::common::aio_protocol_error::AIOProtocolError;

use super::super::common_types::common_options::CommonOptions;
use super::");
            this.Write(this.ToStringHelper.ToStringWithCulture(NamingSupport.ToSnakeCase(this.schemaClassName)));
            this.Write("::");
            this.Write(this.ToStringHelper.ToStringWithCulture(this.schemaClassName));
            this.Write(";\r\n\r\nuse super::MODEL_ID;\r\nuse super::TELEMETRY_TOPIC_PATTERN;\r\n\r\npub struct ");
            this.Write(this.ToStringHelper.ToStringWithCulture(this.schemaClassName));
            this.Write("Receiver<PS: MqttPubSub + Clone + Send + Sync + \'static, PR: MqttPubReceiver + Mq" +
                    "ttAck + Send + Sync + \'static>(TelemetryReceiver<");
            this.Write(this.ToStringHelper.ToStringWithCulture(this.schemaClassName));
            this.Write(", PS, PR>);\r\n\r\nimpl<PS: MqttPubSub + Clone + Send + Sync + \'static, PR: MqttPubRe" +
                    "ceiver + MqttAck + Send + Sync + \'static> ");
            this.Write(this.ToStringHelper.ToStringWithCulture(this.schemaClassName));
            this.Write(@"Receiver<PS, PR> {
    pub fn new(
        mqtt_provider: &mut impl MqttProvider<PS, PR>,
        common_options: &CommonOptions,
    ) -> Result<Self, AIOProtocolError> {
        let mut receiver_options_builder = TelemetryReceiverOptionsBuilder::default();
        if let Some(topic_namespace) = &common_options.topic_namespace {
            receiver_options_builder.topic_namespace(topic_namespace.clone());
        }
        let receiver_options = receiver_options_builder
            .model_id(MODEL_ID.to_string())
            .topic_pattern(TELEMETRY_TOPIC_PATTERN)
");
 if (this.telemetryName != null) { 
            this.Write("            .telemetry_name(\"");
            this.Write(this.ToStringHelper.ToStringWithCulture(this.telemetryName));
            this.Write("\")\r\n");
 } 
            this.Write(@"            .custom_topic_token_map(common_options.custom_topic_token_map.clone())
            .build()
            .unwrap();
        TelemetryReceiver::new(mqtt_provider, receiver_options).map(|ce| Self(ce))
    }
}

impl<PS: MqttPubSub + Clone + Send + Sync + 'static, PR: MqttPubReceiver + MqttAck + Send + Sync + 'static> Deref for ");
            this.Write(this.ToStringHelper.ToStringWithCulture(this.schemaClassName));
            this.Write("Receiver<PS, PR> {\r\n    type Target = TelemetryReceiver<");
            this.Write(this.ToStringHelper.ToStringWithCulture(this.schemaClassName));
            this.Write(", PS, PR>;\r\n\r\n    fn deref(&self) -> &Self::Target {\r\n        &self.0\r\n    }\r\n}\r\n" +
                    "\r\nimpl<PS: MqttPubSub + Clone + Send + Sync + \'static, PR: MqttPubReceiver + Mqt" +
                    "tAck + Send + Sync + \'static> DerefMut for ");
            this.Write(this.ToStringHelper.ToStringWithCulture(this.schemaClassName));
            this.Write("Receiver<PS, PR> {\r\n    fn deref_mut(&mut self) -> &mut Self::Target {\r\n        &" +
                    "mut self.0\r\n    }\r\n}\r\n");
            return this.GenerationEnvironment.ToString();
        }
    }
    #region Base class
    /// <summary>
    /// Base class for this transformation
    /// </summary>
    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("Microsoft.VisualStudio.TextTemplating", "17.0.0.0")]
    public class RustTelemetryReceiverBase
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
