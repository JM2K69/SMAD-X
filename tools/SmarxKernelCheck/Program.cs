// Vérifie le moteur de SMAR-X sur un vrai noyau Linux. Écrit un script bash qui recrée le serveur d'exemple
// (LinuxSamples.FileServer) sous /w, puis y tente chaque opération sous chaque compte, et y crée un fichier et un
// dossier dans chaque dossier. Sa sortie est le fichier de fidélité SMAR-X.Tests/Fidelity/linux-file-server.txt.
//
//   dotnet run --project tools/SmarxKernelCheck > check.sh
//   podman run --rm -v ./check.sh:/check.sh:ro --tmpfs /w:rw,mode=0755 debian:trixie \
//     sh -c 'apt-get -qq update && apt-get -qq install -y acl >/dev/null && bash /check.sh' > linux-file-server.txt
//
// /w est un tmpfs : il porte les ACL POSIX, comme ext4 ou xfs.
using SMARX.Models;
using SMARX.Samples;
using SMARX.Services.Linux;

const string Prefix = "/w";
var host = LinuxSamples.FileServer();
var output = new List<string> { "#!/bin/bash", "set -u", "setup() {" };
output.AddRange(LinuxScripts.Setup(host, Prefix).Select(l => "  " + l));
output.Add("}");
output.Add("setup");
output.Add("echo \"# $(uname -sr), $(. /etc/os-release && echo $PRETTY_NAME), $(setfacl --version | head -1)\"");

static IEnumerable<(LinuxNode Node, string Path)> Walk(LinuxNode node, string path)
{
    yield return (node, path);
    foreach (var child in node.Children)
        foreach (var item in Walk(child, path == "/" ? "/" + child.Name : path + "/" + child.Name))
            yield return item;
}

var nodes = Walk(host.Root, "/").ToList();
LinuxOperation[] fileOperations = [.. LinuxAccess.FileOperations, LinuxOperation.ExecuteScript, LinuxOperation.ChangeOwner, LinuxOperation.ChangeGroup];
LinuxOperation[] directoryOperations = [.. LinuxAccess.DirectoryOperations, LinuxOperation.ChangeOwner, LinuxOperation.ChangeGroup];

foreach (var (node, path) in nodes)
    foreach (var user in host.Users)
        foreach (var operation in (node.IsDirectory ? directoryOperations : fileOperations).Distinct().Where(o => LinuxAccess.Applies(o, node, path)))
        {
            var probe = LinuxScripts.Probe(host, user, path, operation, Prefix);
            var after = LinuxScripts.Changes(operation) ? "; setup" : "";
            output.Add($"if {probe} >/dev/null 2>&1; then echo 'ACCESS {user.Name} {operation} {path} yes'{after}; else echo 'ACCESS {user.Name} {operation} {path} no'; fi");
        }

// Création : mode, propriétaire, groupe et ACL de ce qui est créé (umask de la session, setgid, ACL par défaut)
foreach (var (node, path) in nodes.Where(n => n.Node.IsDirectory))
    foreach (var user in host.Users)
        foreach (var kind in new[] { LinuxNodeKind.File, LinuxNodeKind.Directory })
        {
            var target = LinuxScripts.Quote(Prefix + (path == "/" ? "" : path) + "/.new");
            var make = kind == LinuxNodeKind.File ? $"touch -- {target}" : $"mkdir -- {target}";
            var command = $"umask {user.Umask}; {make}";
            var run = user.IsRoot ? $"sh -c {LinuxScripts.Quote(command)}" : $"runuser -u {user.Name} -- sh -c {LinuxScripts.Quote(command)}";
            output.Add($"if {run} >/dev/null 2>&1; then echo \"CREATE {user.Name} {kind} {path} $(stat -c '%a %U %G' {target}) $(getfacl -cp {target} 2>/dev/null | sed 's/[[:space:]]*#.*//' | grep -v '^$' | paste -sd, -)\"; rm -rf {target}; else echo 'CREATE {user.Name} {kind} {path} denied'; fi");
        }

Console.Out.NewLine = "\n";
foreach (var line in output)
    Console.WriteLine(line);
