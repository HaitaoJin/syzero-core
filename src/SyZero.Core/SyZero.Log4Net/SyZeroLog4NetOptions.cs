using System;

namespace SyZero.Log4Net
{
    public sealed class SyZeroLog4NetOptions
    {
        public string ConfigFile { get; set; } = "log4net.config";

        public bool Watch { get; set; }

        public string RepositoryName { get; set; }

        internal void Validate()
        {
            if (string.IsNullOrWhiteSpace(ConfigFile))
            {
                throw new ArgumentException("Log4Net configuration file path cannot be empty.", nameof(ConfigFile));
            }
        }
    }
}
