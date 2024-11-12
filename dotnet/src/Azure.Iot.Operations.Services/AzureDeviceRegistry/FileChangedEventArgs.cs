using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Azure.Iot.Operations.Services.Assets
{
    internal class FileChangedEventArgs : EventArgs
    {
        internal ChangeType ChangeType { get; init; }

        internal string FilePath { get; init; }

        internal string FileName 
        {
            get
            { 
                return Path.GetFileName(FilePath);
            }
        }

        internal FileChangedEventArgs(string filePath, ChangeType changeType)
        {
            FilePath = filePath;
            ChangeType = changeType;
        }
    }
}
