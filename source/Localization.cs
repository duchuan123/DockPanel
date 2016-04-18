using System.ComponentModel;

namespace System.Windows.Forms.DockPanel
{
    [AttributeUsage(AttributeTargets.All)]
    internal sealed class LocalizedDescriptionAttribute : DescriptionAttribute
    {
        private bool m_initialized;

        public LocalizedDescriptionAttribute(string key) : base(key)
        {
        }

        public override string Description
        {
            get
            {
                if (m_initialized) return DescriptionValue;
                var key = base.Description;
                DescriptionValue = ResourceHelper.GetString(key) ?? string.Empty;

                m_initialized = true;

                return DescriptionValue;
            }
        }
    }

    [AttributeUsage(AttributeTargets.All)]
    internal sealed class LocalizedCategoryAttribute : CategoryAttribute
    {
        public LocalizedCategoryAttribute(string key) : base(key)
        {
        }

        protected override string GetLocalizedString(string value)
        {
            return ResourceHelper.GetString(value);
        }
    }
}
