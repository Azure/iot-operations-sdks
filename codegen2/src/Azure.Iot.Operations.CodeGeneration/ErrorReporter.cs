namespace Azure.Iot.Operations.CodeGeneration
{
    using System;
    using System.IO;
    using System.Text.RegularExpressions;

    public class ErrorReporter
    {
        private static readonly Regex JsonMessageRegex = new(@"(.+)\. LineNumber: (\d+) \| BytePositionInLine: \d+\.", RegexOptions.Compiled);

        private ErrorLog errorLog;
        private string filename;
        private string basePath;
        private byte[]? byteStream;

        public ErrorReporter(ErrorLog errorLog, string filePath, byte[]? byteStream = null)
        {
            this.errorLog = errorLog;
            this.filename = Path.GetFileName(filePath);
            this.basePath = Path.GetDirectoryName(filePath)!;
            this.byteStream = byteStream;
        }

        public void RegisterReferenceFromThing(long byteIndex, string refValue)
        {
            string refPath = Path.GetDirectoryName(refValue) == string.Empty ? refValue : Path.GetFullPath(Path.Combine(this.basePath, refValue)).Replace('\\', '/');
            this.errorLog.RegisterReferenceFromThing(refPath, this.filename, GetLineNumber(byteIndex), refValue);
        }

        public void RegisterNameInThing(string name, long byteIndex)
        {
            this.errorLog.RegisterNameInThing(name, this.filename, GetLineNumber(byteIndex));
        }

        public void RegisterSchemaName(string name, long byteIndex)
        {
            this.errorLog.RegisterSchemaName(name, this.filename, this.basePath, GetLineNumber(byteIndex));
        }

        public void ReportError(string message, long byteIndex, long cfByteIndex = -1, ErrorLevel level = ErrorLevel.Error)
        {
            this.errorLog.AddError(level, message, this.filename, GetLineNumber(byteIndex), GetLineNumber(cfByteIndex));
        }

        public void ReportWarning(string message, long byteIndex, long cfByteIndex = -1)
        {
            ReportError(message, byteIndex, cfByteIndex, ErrorLevel.Warning);
        }

        public void ReportFatal(string message, long byteIndex, long cfByteIndex = -1)
        {
            ReportError(message, byteIndex, cfByteIndex, ErrorLevel.Fatal);
        }

        public void ReportReferenceError(string description, string reason, string refValue, long byteIndex)
        {
            string refPath = Path.GetFullPath(Path.Combine(this.basePath, refValue)).Replace('\\', '/');
            this.errorLog.AddReferenceError(refPath, description, reason, this.filename, GetLineNumber(byteIndex), refValue);
        }

        public void ReportJsonException(Exception ex)
        {
            Match? match = JsonMessageRegex.Match(ex.Message);
            if (match.Success)
            {
                string innerMessage = match.Groups[1].Captures[0].Value;
                string message = $"JSON syntax error: {innerMessage}.";
                int lineNumber = int.Parse(match.Groups[2].Captures[0].Value) + 1;
                this.errorLog.AddError(ErrorLevel.Fatal, message, this.filename, lineNumber);
            }
            else
            {
                this.errorLog.AddError(ErrorLevel.Fatal, ex.Message, this.filename, -1);
            }
        }

        private int GetLineNumber(long byteIndex)
        {
            if (this.byteStream == null)
            {
                return byteIndex < 0 ? -1 : 0;
            }

            if (byteIndex < 0 || byteIndex >= this.byteStream.Length)
            {
                return -1;
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
