/// ColorThemeContext

namespace SafeExchange.Client.Web.Components.Classes.Model
{
    using System;
    using System.Threading.Tasks;

    public class ColorThemeContext
    {
        public ColorTheme Value { get; set; }

        public Func<ColorTheme, Task> OnValueChangedAsync { get; set; }

        public bool IsLightSelected
        {
            get => this.Value == ColorTheme.Light;
            set
            {
                this.Value = ColorTheme.Light;
                this.OnValueChangedAsync?.Invoke(this.Value);
            }
        }

        public bool IsDarkSelected
        {
            get => this.Value == ColorTheme.Dark;
            set
            {
                this.Value = ColorTheme.Dark;
                this.OnValueChangedAsync?.Invoke(this.Value);
            }
        }

        public bool IsAutoSelected
        {
            get => this.Value == ColorTheme.Auto;
            set
            {
                this.Value = ColorTheme.Auto;
                this.OnValueChangedAsync?.Invoke(this.Value);
            }
        }
    }
}
