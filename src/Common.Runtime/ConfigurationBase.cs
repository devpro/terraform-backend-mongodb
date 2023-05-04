using System;
using Microsoft.Extensions.Configuration;

namespace Devpro.Common.Runtime
{
    public abstract class ConfigurationBase
    {
        protected ConfigurationBase(IConfigurationRoot configurationRoot)
        {
            ConfigurationRoot = configurationRoot;
        }

        protected IConfigurationRoot ConfigurationRoot { get; }

        protected IConfigurationSection TryGetSection(string sectionKey)
        {
            var section = ConfigurationRoot.GetSection(sectionKey);
            if (section == null)
            {
                throw new ArgumentException($"Missing section \"{sectionKey}\" in configuration");
            }

            return section;
        }
    }
}
