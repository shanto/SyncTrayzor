using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using Microsoft.Win32;
using System.Configuration;
using System.Text.RegularExpressions;
using System.Windows;
using Stylet;
using SyncTrayzor.Services.Config;

namespace SyncTrayzor.Services
{
    // Implementing INPC is hacky, but convenient
    public interface IThemeManager : INotifyPropertyChanged, IDisposable
    {
        string Theme { get; set; }
        void ApplyTheme();
        void SetTheme(string theme);
    }

    public class ThemeManager : PropertyChangedBase, IThemeManager
    {
        public string Theme { get; set; }
        private readonly IConfigurationProvider configurationProvider;

        public ThemeManager(IConfigurationProvider configurationProvider, IProcessStartProvider processStartProvider)
        {
            this.configurationProvider = configurationProvider;
            this.configurationProvider.ConfigurationChanged += this.ConfigurationChanged;
        }

        private void ConfigurationChanged(object sender, ConfigurationChangedEventArgs e)
        {
            this.Theme = e.NewConfiguration.Theme;
            this.ApplyTheme();
        }

        public void SetTheme(string theme)
        {
            this.Theme = theme;
            this.configurationProvider.AtomicLoadAndSave(c => c.Theme = theme);
        }

        public void ApplyTheme()
        {
            RegistryKey personalized = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            bool usingLightTheme = (bool)personalized?.GetValue("AppsUseLightTheme").Equals(1);
            string activeTheme = this.Theme;
            string reColDir = @"^(?<dir>.+/ColourDictionaries)/(?<theme>\w+)(?<ext>\.xaml)$";
            NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();
            if (activeTheme.Equals("SystemTheme"))
            {
                logger.Debug($"Substituting theme {activeTheme} to follow SystemTheme");
                activeTheme = usingLightTheme ? "LightTheme" : "DarkGreyTheme";
            }
            logger.Debug($"Replacing App resource dictinary with {activeTheme}");
            int i = 0;
            Collection<ResourceDictionary> merged = SyncTrayzor.App.Current.Resources.MergedDictionaries;
            foreach (ResourceDictionary d in merged)
            {
                logger.Debug(d.Source?.ToString());
                if ((d.Source != null))
                {
                    string s0 = Regex.Replace(d.Source.ToString(), reColDir, $"${{dir}}/{activeTheme}${{ext}}", RegexOptions.IgnoreCase);
                    if (!d.Source.Equals(s0))
                    {
                        merged[i] = new ResourceDictionary() { Source = new Uri(s0, UriKind.Relative) };
                        merged[i+1] = new ResourceDictionary() { Source = merged[i + 1].Source };
                        merged[i+2] = new ResourceDictionary() { Source = merged[i + 2].Source };
                        merged[i+3] = new ResourceDictionary() { Source = merged[i + 3].Source };
                        logger.Debug($"Injected {s0}");
                        break;
                    }
                }
                i++;
            }
        }

        public void Dispose()
        {
            this.configurationProvider.ConfigurationChanged -= this.ConfigurationChanged;
        }
    }
}
