// Vérifie l'accès par les partages Samba de SMAR-X sur un vrai Samba. Écrit un script bash qui recrée le serveur
// d'exemple (LinuxSamples.FileServer) sous /w, écrit smb.conf avec ses partages, lance smbd, donne à chaque compte un
// mot de passe Samba tiré au hasard (il ne sort pas du conteneur), puis, sous chaque compte et par chaque partage, lit,
// écrit et supprime chaque fichier, liste et supprime chaque dossier, et y crée un fichier et un dossier (leur mode,
// propriétaire, groupe et ACL sont relevés). Sa sortie est SMAR-X.Tests/Fidelity/linux-samba.txt.
//
//   dotnet run --project tools/SmarxSambaCheck > samba.sh
//   podman run --rm -v ./samba.sh:/samba.sh:ro,Z --tmpfs /w:rw,mode=0755 debian:trixie \
//     sh -c 'apt-get -qq update && apt-get -qq install -y acl samba smbclient >/dev/null && bash /samba.sh' > linux-samba.txt
//
// /w est un tmpfs : il porte les ACL POSIX et les attributs étendus où Samba range les attributs DOS.
using SMARX.Models;
using SMARX.Samples;
using SMARX.Services.Linux;

const string Prefix = "/w";
var host = LinuxSamples.FileServer();
var users = host.Users.Where(u => !u.IsRoot).ToList();
string Q(string text) => LinuxScripts.Quote(text);

var output = new List<string> { "#!/bin/bash", "set -u", "setup() {" };
output.AddRange(LinuxScripts.Setup(host, Prefix).Select(l => "  " + l));
output.Add("}");
output.Add("setup");

// smb.conf : un serveur autonome, sans invité ni NetBIOS ; les partages de l'exemple sous /w
output.Add("cat > /etc/samba/smb.conf <<'EOF'");
output.AddRange(["[global]", "   server role = standalone server", "   map to guest = never", "   disable netbios = yes", "   smb ports = 445",
                 "   log level = 0", "   passdb backend = tdbsam"]);
foreach (var share in host.Shares)
    output.AddRange(share.Config(Prefix));
output.Add("EOF");

output.Add("declare -A PW");
foreach (var user in users)
{
    output.Add($"PW[{user.Name}]=$(head -c 24 /dev/urandom | base64 | tr -d '/+=')");
    output.Add($"printf '%s\\n%s\\n' \"${{PW[{user.Name}]}}\" \"${{PW[{user.Name}]}}\" | smbpasswd -a -s {user.Name} >/dev/null");
}
output.Add("smbd -D && sleep 2");
output.Add("echo \"# $(uname -sr), $(. /etc/os-release && echo $PRETTY_NAME), Samba $(smbd --version | cut -d' ' -f2)\"");
output.Add("printf 'smar-x\\n' > /tmp/payload");
// Une commande smbclient sous un compte, par un partage : « yes », ou le premier NT_STATUS de la réponse
output.Add("smb() { out=$(smbclient \"//localhost/$2\" -U \"$1%${PW[$1]}\" -c \"$3\" 2>&1); st=$(printf '%s' \"$out\" | grep -o 'NT_STATUS_[A-Z_]*' | head -1); if [ -n \"$st\" ]; then echo \"$st\"; else echo yes; fi; }");
// La liste d'un dossier : les noms (sans . et ..), ou le NT_STATUS
output.Add("smbls() { out=$(smbclient \"//localhost/$2\" -U \"$1%${PW[$1]}\" -c \"$3\" 2>&1); st=$(printf '%s' \"$out\" | grep -o 'NT_STATUS_[A-Z_]*' | head -1); if [ -n \"$st\" ]; then echo \"$st\"; else printf '%s\\n' \"$out\" | awk '/^  [^ ]/ {print $1}' | grep -v '^\\.\\.\\?$' | sort | paste -sd/; fi; }");
output.Add("meta() { echo \"$(stat -c '%a %U %G' \"$1\") $(getfacl -cp \"$1\" 2>/dev/null | sed 's/[[:space:]]*#.*//' | grep -v '^$' | paste -sd, -)\"; }");

static IEnumerable<(LinuxNode Node, string Path)> Walk(LinuxNode node, string path)
{
    yield return (node, path);
    foreach (var child in node.Children)
        foreach (var item in Walk(child, path == "/" ? "/" + child.Name : path + "/" + child.Name))
            yield return item;
}
var nodes = Walk(host.Root, "/").ToList();

foreach (var share in host.Shares)
    foreach (var user in users)
    {
        output.Add($"echo \"CONNECT|{user.Name}|{share.Name}|$(smb {user.Name} {share.Name} 'ls')\"");
        foreach (var (node, path) in nodes.Where(n => share.Contains(n.Path)))
        {
            // Le chemin dans le partage, avec les séparateurs de Windows
            var rest = path.Length > share.Path.Length ? path[(share.Path.Length + 1)..].Replace('/', '\\') : "";
            var real = Prefix + path;
            if (node.IsDirectory)
            {
                var pattern = rest.Length == 0 ? "*" : rest + "\\*";
                output.Add($"echo \"SMB|{user.Name}|{share.Name}|List|{path}|$(smbls {user.Name} {share.Name} {Q($"ls {pattern}")})\"");
                foreach (var (kind, command) in new[] { ("File", "put /tmp/payload "), ("Directory", "mkdir ") })
                {
                    var target = rest.Length == 0 ? ".new" : rest + "\\.new";
                    output.Add($"r=$(smb {user.Name} {share.Name} {Q(command + target)}); if [ \"$r\" = yes ]; then r=$(meta {Q(real + "/.new")}); rm -rf {Q(real + "/.new")}; fi; echo \"SCREATE|{user.Name}|{share.Name}|{kind}|{path}|$r\"");
                }
                if (rest.Length > 0)
                    output.Add($"r=$(smb {user.Name} {share.Name} {Q("rmdir " + rest)}); echo \"SMB|{user.Name}|{share.Name}|Delete|{path}|$r\"; [ \"$r\" = yes ] && setup");
            }
            else
            {
                output.Add($"echo \"SMB|{user.Name}|{share.Name}|Read|{path}|$(smb {user.Name} {share.Name} {Q($"get {rest} /dev/null")})\"");
                output.Add($"r=$(smb {user.Name} {share.Name} {Q($"put /tmp/payload {rest}")}); echo \"SMB|{user.Name}|{share.Name}|Write|{path}|$r\"; [ \"$r\" = yes ] && setup");
                output.Add($"r=$(smb {user.Name} {share.Name} {Q("del " + rest)}); echo \"SMB|{user.Name}|{share.Name}|Delete|{path}|$r\"; [ \"$r\" = yes ] && setup");
            }
        }
    }
output.Add("echo '# done'");

Console.Out.NewLine = "\n";
foreach (var line in output)
    Console.WriteLine(line);
