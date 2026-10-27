using System;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using SMADX.Services.Audit;

namespace SMADX.Services
{
    /// <summary>
    /// Version du moteur écrite dans les exercices et les copies : la correction dépend des règles d'audit
    /// et du calcul des chemins d'attaque, qui évoluent d'une version à l'autre.
    /// </summary>
    public static class EngineInfo
    {
        /// <summary>Version de SMAD-X (la même pour SMAD-X, Studio et le noyau, voir Directory.Build.props).</summary>
        public static string Version { get; } =
            typeof(EngineInfo).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? typeof(EngineInfo).Assembly.GetName().Version?.ToString(3)
            ?? "0.0.0";

        /// <summary>Empreinte courte des règles d'audit (identifiants et gravités) : elle change quand une règle est ajoutée ou revue.</summary>
        public static string RulesVersion { get; } =
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
                string.Join(";", AuditRules.All.OrderBy(r => r.Id, StringComparer.Ordinal).Select(r => $"{r.Id}:{r.Severity}")))))[..12];
    }
}
