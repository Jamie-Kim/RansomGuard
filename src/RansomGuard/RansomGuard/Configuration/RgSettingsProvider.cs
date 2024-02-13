using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Configuration;
using System.Reflection;

namespace RansomGuard
{
    public class RgSettingsProvider : SettingsProvider, IApplicationSettingsProvider
    {
        RgSettingsStore store = new RgSettingsStore();

        public override void Initialize(string name, NameValueCollection config)
        {
            base.Initialize(this.ApplicationName, config);
        }

        public override string Name
        {
            get { return base.Name; }
        }

        public override string Description
        {
            get { return base.Description; }
        }

        public override string ApplicationName
        {
            get { return Assembly.GetExecutingAssembly().GetName().Name; }
            set { }
        }

        public override SettingsPropertyValueCollection GetPropertyValues(SettingsContext context, SettingsPropertyCollection properties)
        {
            var values = new SettingsPropertyValueCollection();

            var settings = store.ReadConfiguration();

            foreach (SettingsProperty setting in properties)
            {
                var value = new SettingsPropertyValue(setting);

                if (settings.ContainsKey(setting.Name))
                    value.SerializedValue = settings[setting.Name];
                else if (setting.DefaultValue != null)
                    value.SerializedValue = setting.DefaultValue;
                else
                    value.SerializedValue = string.Empty;

                value.IsDirty = false;

                values.Add(value);
            }

            return values;
        }

        public override void SetPropertyValues(SettingsContext context, SettingsPropertyValueCollection values)
        {
            var settings = new Dictionary<string, string>();

            int cnt = 0;
            foreach (SettingsPropertyValue value in values)
            {
                SettingsProperty setting = value.Property;
                settings[setting.Name] = value.SerializedValue as string;

                if (value.IsDirty)
                {
                    value.IsDirty = false;
                    cnt++;
                }
            }

            if (cnt > 0)
                store.WriteConfiguration(settings);
        }

        // TODO: This resets settings to the values in AssemblyName.exe.config. Does nothing for now
        public void Reset(SettingsContext context)
        {
        }

        public SettingsPropertyValue GetPreviousVersion(SettingsContext context, SettingsProperty property)
        {
            throw new NotImplementedException("Not Supported");
        }

        public void Upgrade(SettingsContext context, SettingsPropertyCollection properties)
        {
            throw new NotImplementedException("Not Supported");
        }

    }
}
