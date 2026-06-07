using System;
using System.Linq;
using System.Windows;

namespace PowerCapsule.Services
{
    // 简单的中/英文切换：替换合并到 App 资源里的字符串字典
    public static class LocalizationManager
    {
        public static bool IsEnglish { get; private set; }

        public static event Action LanguageChanged;

        public static void Initialize(bool english)
        {
            IsEnglish = english;
            ApplyDictionary();
        }

        public static void Toggle()
        {
            IsEnglish = !IsEnglish;
            ApplyDictionary();
            LanguageChanged?.Invoke();
        }

        private static void ApplyDictionary()
        {
            var dicts = Application.Current.Resources.MergedDictionaries;

            // 移除已有的字符串字典
            for (int i = dicts.Count - 1; i >= 0; i--)
            {
                var src = dicts[i].Source?.OriginalString;
                if (src != null && src.Contains("/Strings/"))
                    dicts.RemoveAt(i);
            }

            var file = IsEnglish ? "en-US.xaml" : "zh-CN.xaml";
            dicts.Add(new ResourceDictionary
            {
                Source = new Uri($"/PowerCapsule;component/Resources/Strings/{file}", UriKind.Relative)
            });
        }

        // 供代码（非 XAML）取本地化字符串
        public static string L(string key)
        {
            return Application.Current.TryFindResource(key) as string ?? key;
        }

        public static string LF(string key, params object[] args)
        {
            return string.Format(L(key), args);
        }
    }
}
