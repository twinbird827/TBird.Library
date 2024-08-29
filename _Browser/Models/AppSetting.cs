using TBird.Core;

namespace Browser.Models
{
    public class AppSetting : JsonBase<AppSetting>
    {
        private const string _path = @".\app-setting.json";

        public static AppSetting Instance
        {
            get => _Instance = _Instance ?? new AppSetting();
        }
        private static AppSetting? _Instance;

        public AppSetting() : base(_path)
        {
            if (!Load())
            {
                TargetDirs = Arr(@"C:\Work");
            }
        }

        public string[] TargetDirs
        {
            get => GetProperty(_TargetDirs.NotNull());
            set => SetProperty(ref _TargetDirs, value);
        }
        private string[]? _TargetDirs;

    }
}