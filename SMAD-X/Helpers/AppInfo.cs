using System.Reflection;

namespace SMADX.Helpers
{
    /// <summary>Version de l'application, lue dans l'assembly : la seule source est &lt;Version&gt; dans SMAD-X.csproj.</summary>
    public static class AppInfo
    {
        public static string Version { get; } =
            typeof(AppInfo).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? typeof(AppInfo).Assembly.GetName().Version?.ToString(3)
            ?? "0.0.0";
    }
}
