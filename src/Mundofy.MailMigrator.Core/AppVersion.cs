namespace Mundofy.MailMigrator.Core;

public static class AppVersion
{
    public const string Version = "1.4.0";
    public const string FullVersion = "v1.4.0";
    public const string DisplayName = "Mundofy MailMigrator";

    /// <summary>
    /// Gets the current version string, dynamically resolving from assembly metadata or falling back to Version constant.
    /// </summary>
    public static string Current
    {
        get
        {
            var asm = System.Reflection.Assembly.GetEntryAssembly() ?? typeof(AppVersion).Assembly;
            var ver = asm.GetName().Version;
            return ver != null ? $"{ver.Major}.{ver.Minor}.{ver.Build}" : Version;
        }
    }
}
