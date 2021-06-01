using System;
using System.IO;

namespace FDA.SRS.Utils
{
    public class TempFile : IDisposable
    {
        private string m_Path;

        public string FullPath
        {
            get
            {
                return this.m_Path;
            }
        }

        public TempFile() : this(null, null)
        {
        }

        public TempFile(string directory) : this(directory, null)
        {
        }

        public TempFile(string directory, string extension)
        {
            if (string.IsNullOrEmpty(directory))
            {
                directory = Path.GetTempPath();
            }
            do
            {
                this.m_Path = Path.Combine(directory, Path.GetRandomFileName());
                if ((string.IsNullOrEmpty(extension) ? true : extension == "."))
                {
                    continue;
                }
                if (!extension.StartsWith("."))
                {
                    this.m_Path = string.Concat(this.m_Path, ".", extension);
                }
                else
                {
                    this.m_Path = string.Concat(this.m_Path, extension);
                }
            }
            while (File.Exists(this.m_Path));
        }

        public void Dispose()
        {
            if (File.Exists(this.m_Path))
            {
                File.Delete(this.m_Path);
            }
            GC.SuppressFinalize(this);
        }

        public override string ToString()
        {
            return this.m_Path;
        }
    }
}