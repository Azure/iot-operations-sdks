namespace Azure.Iot.Operations.CodeGeneration
{
    using System;
    using System.IO;
    using System.Text.RegularExpressions;
    using Azure.Iot.Operations.TDParser;

    public class ErrorReporter
    {
        private static readonly Regex JsonMessageRegex = new(@"(.+)\. LineNumber: (\d+) \| BytePositionInLine: \d+\.", RegexOptions.Compiled);

        private ErrorLog errorLog;
        private string filename;
        private string basePath;
        private byte[] byteStream;

        public ErrorReporter(ErrorLog errorLog, string filePath, byte[] byteStream)
        {
            this.errorLog = errorLog;
            this.filename = Path.GetFileName(filePath);
            this.basePath = Path.GetDirectoryName(filePath)!;
            this.byteStream = byteStream;
        }

        public void RegisterReferenceFromThing(long byteIndex, string refValue)
        {
            string refPath = refValue.Contains('/') ? Path.GetFullPath(Path.Combine(this.basePath, refValue)).Replace('\\', '/') : refValue;
            this.errorLog.RegisterReferenceFromThing(refPath, this.filename, GetLineNumber(byteIndex), refValue);
        }

        public void RegisterTypedReferenceFromThing(long byteIndex, string type, string refValue)
        {
            string refPath = refValue.Contains('/') ? Path.GetFullPath(Path.Combine(this.basePath, refValue)).Replace('\\', '/') : refValue;
            this.errorLog.RegisterTypedReferenceFromThing(refPath, this.filename, GetLineNumber(byteIndex), type, refValue);
        }

        public void RegisterNameInThing(string name, long byteIndex)
        {
            this.errorLog.RegisterNameInThing(name, this.filename, GetLineNumber(byteIndex));
        }

        public void RegisterTopicInThing(string resolvedTopic, long byteIndex, string rawTopic)
        {
            this.errorLog.RegisterTopicInThing(resolvedTopic, this.filename, GetLineNumber(byteIndex), rawTopic);
        }

        public void RegisterSchemaName(string name, long byteIndex)
        {
            this.errorLog.RegisterSchemaName(name, this.filename, this.basePath, GetLineNumber(byteIndex));
        }

        public void ReportError(ErrorCondition condition, string message, long byteIndex, long cfByteIndex = -1, ErrorLevel level = ErrorLevel.Error)
        {
            this.errorLog.AddError(level, condition, message, this.filename, GetLineNumber(byteIndex), GetLineNumber(cfByteIndex));
        }

        public void ReportWarning(string message, long byteIndex, long cfByteIndex = -1)
        {
            ReportError(ErrorCondition.None, message, byteIndex, cfByteIndex, ErrorLevel.Warning);
        }

        public void ReportFatal(ErrorCondition condition, string message, long byteIndex, long cfByteIndex = -1)
        {
            ReportError(condition, message, byteIndex, cfByteIndex, ErrorLevel.Fatal);
        }

        public void ReportReferenceError(string description, string reason, string refValue, long byteIndex)
        {
            string refPath = Path.GetFullPath(Path.Combine(this.basePath, refValue)).Replace('\\', '/');
            this.errorLog.AddReferenceError(refPath, description, reason, this.filename, this.basePath, GetLineNumber(byteIndex), refValue);
        }

        public void ReportReferenceTypeError(string description, string refValue, long byteIndex, string refType, string actualType)
        {
            string refPath = Path.GetFullPath(Path.Combine(this.basePath, refValue)).Replace('\\', '/');
            this.errorLog.AddReferenceTypeError(refPath, description, this.filename, this.basePath, GetLineNumber(byteIndex), refValue, refType, actualType);
        }

        public void ReportJsonException(Exception ex)
        {
            Match? match = JsonMessageRegex.Match(ex.Message);
            if (match.Success)
            {
                string innerMessage = match.Groups[1].Captures[0].Value;
                string message = $"JSON syntax error: {innerMessage}.";
                int lineNumber = int.Parse(match.Groups[2].Captures[0].Value) + 1;
                this.errorLog.AddError(ErrorLevel.Fatal, ErrorCondition.JsonInvalid, message, this.filename, lineNumber);
            }
            else
            {
                this.errorLog.AddError(ErrorLevel.Fatal, ErrorCondition.JsonInvalid, ex.Message, this.filename, -1);
            }
        }

        private int GetLineNumber(long byteIndex)
        {
            if (byteIndex < 0 || byteIndex >= this.byteStream.Length)
            {
                return 0;
            }

            int lineNum = 1;
            for (long ix = 0; ix < byteIndex; ++ix)
            {
                if (this.byteStream[ix] == '\n')
                {
                    ++lineNum;
                }
            }

            return lineNum;
        }
    }
}
