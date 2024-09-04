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
    using System;
    
    /// <summary>
    /// Class to produce the template output
    /// </summary>
    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("Microsoft.VisualStudio.TextTemplating", "17.0.0.0")]
    public partial class GoCommandInvoker : GoCommandInvokerBase
    {
        /// <summary>
        /// Create the template output
        /// </summary>
        public virtual string TransformText()
        {
            this.Write("/* This is an auto-generated file.  Do not modify. */\r\npackage ");
            this.Write(this.ToStringHelper.ToStringWithCulture(this.genNamespace));
            this.Write("\r\n\r\nimport (\r\n\t\"context\"\r\n\r\n\t\"github.com/Azure/iot-operations-sdks/go/protocol\"" +
                    "\r\n\t\"github.com/Azure/iot-operations-sdks/go/protocol/mqtt\"\r\n)\r\n\r\ntype ");
            this.Write(this.ToStringHelper.ToStringWithCulture(this.capitalizedCommandName));
            this.Write("CommandInvoker struct {\r\n\t*protocol.CommandInvoker[");
            this.Write(this.ToStringHelper.ToStringWithCulture(this.reqSchema ?? "any"));
            this.Write(", ");
            this.Write(this.ToStringHelper.ToStringWithCulture(this.respSchema ?? "any"));
            this.Write("]\r\n}\r\n\r\nfunc New");
            this.Write(this.ToStringHelper.ToStringWithCulture(this.capitalizedCommandName));
            this.Write("CommandInvoker(\r\n\tclient mqtt.Client,\r\n\trequestTopic string,\r\n\topt ...prot" +
                    "ocol.CommandInvokerOption,\r\n) (*");
            this.Write(this.ToStringHelper.ToStringWithCulture(this.capitalizedCommandName));
            this.Write("CommandInvoker, error) {\r\n\tvar err error\r\n\tinvoker := &");
            this.Write(this.ToStringHelper.ToStringWithCulture(this.capitalizedCommandName));
            this.Write("CommandInvoker{}\r\n\r\n\tvar opts protocol.CommandInvokerOptions\r\n\topts.Apply(\r\n\t\topt" +
                    ",\r\n\t\tprotocol.WithTopicTokenNamespace(\"ex:\"),\r\n\t\tprotocol.WithTopicTokens{\r\n\t\t\t\"" +
                    "commandName\":     \"");
            this.Write(this.ToStringHelper.ToStringWithCulture(this.commandName));
            this.Write("\",\r\n\t\t\t\"invokerClientId\": client.ClientID(),\r\n\t\t},\r\n\t)\r\n\r\n\tinvoker.CommandInvoker" +
                    ", err = protocol.NewCommandInvoker(\r\n\t\tclient,\r\n");
 if (this.reqSchema != null) { 
            this.Write("\t\tprotocol.");
            this.Write(this.ToStringHelper.ToStringWithCulture(this.serializerSubNamespace));
            this.Write("[");
            this.Write(this.ToStringHelper.ToStringWithCulture(this.reqSchema));
            this.Write("]{},\r\n");
 } else { 
            this.Write("\t\tprotocol.Empty{},\r\n");
 } 
 if (this.respSchema != null) { 
            this.Write("\t\tprotocol.");
            this.Write(this.ToStringHelper.ToStringWithCulture(this.serializerSubNamespace));
            this.Write("[");
            this.Write(this.ToStringHelper.ToStringWithCulture(this.respSchema));
            this.Write("]{},\r\n");
 } else { 
            this.Write("\t\tprotocol.Empty{},\r\n");
 } 
            this.Write("\t\trequestTopic,\r\n\t\t&opts,\r\n\t)\r\n\r\n\treturn invoker, err\r\n}\r\n\r\nfunc (invoker ");
            this.Write(this.ToStringHelper.ToStringWithCulture(this.capitalizedCommandName));
            this.Write("CommandInvoker) ");
            this.Write(this.ToStringHelper.ToStringWithCulture(this.capitalizedCommandName));
            this.Write("(\r\n\tctx context.Context,\r\n");
 if (this.doesCommandTargetExecutor) { 
            this.Write("\texecutorId string,\r\n");
 } 
 if (this.reqSchema != null) { 
            this.Write("\trequest ");
            this.Write(this.ToStringHelper.ToStringWithCulture(this.AsSchema(this.reqSchema)));
            this.Write(",\r\n");
 } 
            this.Write("\topt ...protocol.InvokeOption,\r\n");
 if (this.respSchema != null) { 
            this.Write(") (*protocol.CommandResponse[");
            this.Write(this.ToStringHelper.ToStringWithCulture(this.AsSchema(this.respSchema)));
            this.Write("], error) {\r\n");
 } else { 
            this.Write(") error {\r\n");
 } 
 if (this.doesCommandTargetExecutor) { 
            this.Write("\tvar opts protocol.InvokeOptions\r\n\topts.Apply(\r\n\t\topt,\r\n\t\tprotocol.WithTopicToken" +
                    "s{\r\n\t\t\t\"executorId\": executorId,\r\n\t\t},\r\n\t)\r\n\r\n");
 } 
            this.Write("\t");
            this.Write(this.ToStringHelper.ToStringWithCulture(this.respSchema != null ? "response" : "_"));
            this.Write(", err := invoker.Invoke(\r\n\t\tctx,\r\n\t\t");
            this.Write(this.ToStringHelper.ToStringWithCulture(this.reqSchema != null ? "request" : "nil"));
            this.Write(",\r\n");
 if (this.doesCommandTargetExecutor) { 
            this.Write("\t\t&opts,\r\n");
 } else { 
            this.Write("\t\topt...,\r\n");
 } 
            this.Write("\t)\r\n\r\n\treturn ");
            this.Write(this.ToStringHelper.ToStringWithCulture(this.respSchema != null ? "response, " : ""));
            this.Write("err\r\n}\r\n");
            return this.GenerationEnvironment.ToString();
        }

    private string AsSchema(string schema) => schema == "" ? "[]byte" : schema;

    }
    #region Base class
    /// <summary>
    /// Base class for this transformation
    /// </summary>
    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("Microsoft.VisualStudio.TextTemplating", "17.0.0.0")]
    public class GoCommandInvokerBase
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
