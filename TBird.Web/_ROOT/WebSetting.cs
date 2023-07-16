using System;
using System.Collections.Generic;
using System.Text;
using TBird.Core;

namespace TBird.Web
{
    public class WebSetting : JsonBase<WebSetting>
    {
        private const string _path = @"lib\web-setting.json";

        public static WebSetting Instance
        {
            get => _Instance = _Instance ?? new WebSetting();
        }
        private static WebSetting _Instance;

        public WebSetting() : base(_path)
        {
            if (!Load())
            {
                BrowserPath = @"C:\Program Files\Google\Chrome\Application\chrome.exe";
            }
        }

        public string BrowserPath
        {
            get => GetProperty(_BrowserPath);
            set => SetProperty(ref _BrowserPath, value);
        }
        private string _BrowserPath;

    }
}