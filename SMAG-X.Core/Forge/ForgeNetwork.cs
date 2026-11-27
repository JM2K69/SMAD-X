using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using SMAGX.Git;

namespace SMAGX.Forge;

/// <summary>Un objet Git en transit : identifiant, type et contenu brut (base64).</summary>
public sealed record WireObject(string Id, string Type, string Data);

public sealed record WireUpdate(string Ref, string? Old, string New);

public sealed record WireBranch(string Name, string Short);

public sealed record WireProject(string Path, List<WireBranch> Branches);

public sealed record WireForge(string Host, string Kind);

public sealed record WireKey(string Title, string Label, string Fingerprint, DateTimeOffset Added);

/// <summary>Une demande à la forge d'un autre SMAG-X : hello, list, create, open, push.</summary>
public sealed class ForgeRequest
{
    public string Op { get; set; } = "";
    public string? Code { get; set; }
    public string? Host { get; set; }
    public string? Project { get; set; }
    public string? Account { get; set; }
    public string? Title { get; set; }
    public string? Key { get; set; }
    public List<WireObject>? Objects { get; set; }
    public List<WireUpdate>? Updates { get; set; }
}

public sealed class ForgeResponse
{
    public bool Ok { get; set; }
    public string? Error { get; set; }
    public List<WireForge>? Forges { get; set; }
    public string? Account { get; set; }
    public List<WireKey>? Keys { get; set; }
    public string? Head { get; set; }
    public Dictionary<string, string>? Refs { get; set; }
    public List<WireObject>? Objects { get; set; }
    public List<string>? Rejected { get; set; }
    public List<WireProject>? Projects { get; set; }
}

/// <summary>Le format des échanges : une ligne JSON par demande et par réponse.</summary>
internal static class Wire
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public static List<WireObject> Objects(ObjectStore store) =>
        store.Ids.Select(id => { var o = store.Get(id); return new WireObject(id.Hex, o.TypeName, Convert.ToBase64String(o.Body())); }).ToList();

    /// <summary>Range les objets reçus après avoir vérifié que chacun a bien l'identifiant annoncé.</summary>
    public static void Load(ObjectStore store, IEnumerable<WireObject> objects)
    {
        foreach (var w in objects)
        {
            var obj = ObjectCodec.Decode(w.Type, Convert.FromBase64String(w.Data));
            var id = store.Put(obj);
            if (id.Hex != w.Id) throw new InvalidDataException($"objet altéré : {w.Id}");
        }
    }
}

/// <summary>
/// Le serveur d'un SMAG-X qui partage sa forge avec un camarade, sur le réseau de la salle (TCP, port 7878 par défaut).
/// Chaque demande porte le code affiché chez celui qui sert : sans lui, la forge ne répond pas.
/// </summary>
public sealed class ForgeServer(IReadOnlyList<Forge> forges, string code) : IDisposable
{
    public const int DefaultPort = 7878;

    private TcpListener? _listener;
    private readonly CancellationTokenSource _stop = new();

    public string Code { get; } = code;
    public int Port { get; private set; }

    /// <summary>Un camarade vient de joindre la forge (son adresse).</summary>
    public event Action<string>? Visited;

    /// <summary>
    /// Écoute sur toutes les adresses du poste (ou seulement <paramref name="bind"/>, la boucle locale pour les tests) ;
    /// si le port demandé est pris, sur un port libre.
    /// </summary>
    public void Start(int port = DefaultPort, IPAddress? bind = null)
    {
        bind ??= IPAddress.Any;
        try
        {
            _listener = new TcpListener(bind, port);
            _listener.Start();
        }
        catch (SocketException)
        {
            _listener = new TcpListener(bind, 0);
            _listener.Start();
        }
        Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
        _ = AcceptAsync(_stop.Token);
    }

    private async Task AcceptAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            TcpClient client;
            try { client = await _listener!.AcceptTcpClientAsync(token); }
            catch (Exception e) when (e is OperationCanceledException or ObjectDisposedException or SocketException) { return; }
            _ = Task.Run(() => HandleAsync(client), token);
        }
    }

    private async Task HandleAsync(TcpClient client)
    {
        using (client)
        {
            try
            {
                var stream = client.GetStream();
                using var reader = new StreamReader(stream, new UTF8Encoding(false));
                await using var writer = new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = true };
                var line = await reader.ReadLineAsync();
                if (line is null) return;
                var request = JsonSerializer.Deserialize<ForgeRequest>(line, Wire.Options) ?? new ForgeRequest();
                var response = Process(request);
                if (response.Ok && client.Client.RemoteEndPoint is IPEndPoint remote) Visited?.Invoke(remote.Address.ToString());
                await writer.WriteLineAsync(JsonSerializer.Serialize(response, Wire.Options));
            }
            catch (Exception e) when (e is IOException or JsonException or SocketException or InvalidDataException or FormatException) { }
        }
    }

    internal ForgeResponse Process(ForgeRequest request)
    {
        if (request.Code != Code) return new ForgeResponse { Error = "code" };
        if (request.Op == "hello")
            return new ForgeResponse { Ok = true, Forges = forges.Select(f => new WireForge(f.Host, f.Kind.ToString())).ToList() };
        if (forges.FirstOrDefault(f => f.Host == request.Host) is not { } forge) return new ForgeResponse { Error = "host" };
        switch (request.Op)
        {
            case "ping":
                return new ForgeResponse { Ok = true };
            case "auth":
                return new ForgeResponse { Ok = true, Account = forge.Authenticate(request.Key ?? "") };
            case "keys":
                return new ForgeResponse { Ok = true, Keys = forge.Keys(request.Account ?? "").Select(k => new WireKey(k.Title, k.Label, k.Fingerprint, k.Added)).ToList() };
            case "addkey":
                var refused = forge.AddKey(request.Account ?? "", request.Title ?? "", request.Key ?? "");
                return new ForgeResponse { Ok = refused is null, Error = refused };
            case "delkey":
                forge.RemoveKey(request.Account ?? "", request.Key ?? "");
                return new ForgeResponse { Ok = true };
            case "list":
                return new ForgeResponse
                {
                    Ok = true,
                    Projects = forge.Projects().Select(p => new WireProject(p.Path, p.Branches.Select(b => new WireBranch(b.Branch, b.Short)).ToList())).ToList()
                };
            case "create":
                return new ForgeResponse { Ok = ((IForge)forge).Create(request.Project ?? "") };
            case "open":
                if (forge.Open(request.Project ?? "") is not { } repo) return new ForgeResponse { Error = "missing" };
                return new ForgeResponse
                {
                    Ok = true, Head = repo.HeadRef,
                    Refs = repo.Refs.ToDictionary(r => r.Key, r => r.Value.Hex),
                    Objects = Wire.Objects(repo.Objects)
                };
            case "push":
                var objects = new ObjectStore();
                Wire.Load(objects, request.Objects ?? []);
                var updates = (request.Updates ?? []).Select(u => new RefUpdate(u.Ref, u.Old is null ? null : new ObjectId(u.Old), new ObjectId(u.New))).ToList();
                return new ForgeResponse { Ok = true, Rejected = forge.Push(request.Project ?? "", objects, updates).ToList() };
            default:
                return new ForgeResponse { Error = "op" };
        }
    }

    /// <summary>
    /// Les adresses IPv4 du poste sur le réseau (hors boucle locale), à donner au camarade : d'abord celles des cartes
    /// qui ont une passerelle (le réseau de la salle), puis les autres (cartes virtuelles, VPN).
    /// </summary>
    public static IReadOnlyList<string> LocalAddresses() =>
        NetworkInterface.GetAllNetworkInterfaces()
            .Where(n => n.OperationalStatus == OperationalStatus.Up && n.NetworkInterfaceType is not (NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel))
            .Select(n => (Properties: n.GetIPProperties(), Card: n))
            .OrderByDescending(n => n.Properties.GatewayAddresses.Any(g => g.Address.AddressFamily == AddressFamily.InterNetwork && !g.Address.Equals(IPAddress.Any)))
            .SelectMany(n => n.Properties.UnicastAddresses)
            .Where(a => a.Address.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(a.Address))
            .Select(a => a.Address.ToString()).Distinct().ToList();

    public void Dispose()
    {
        _stop.Cancel();
        _listener?.Stop();
    }
}

/// <summary>Une forge d'un autre SMAG-X (gitlab.example ou github.example), jointe par le réseau : chaque opération est une demande au serveur.</summary>
public sealed class RemoteForge(string address, int port, string code, string host = "gitlab.example", ForgeKind kind = ForgeKind.GitLab) : IForge
{
    public string Address { get; } = address;
    public int Port { get; } = port;
    public string Host { get; } = host;
    public ForgeKind Kind { get; } = kind;
    public bool Ssh => true;

    public void Ping() => Call(new ForgeRequest { Op = "ping" });

    /// <summary>Première prise de contact : vérifie le code et rend les forges du camarade (une RemoteForge par forge).</summary>
    public static IReadOnlyList<RemoteForge> Connect(string address, int port, string code)
    {
        var response = new RemoteForge(address, port, code).Call(new ForgeRequest { Op = "hello" });
        return (response.Forges ?? [])
            .Select(f => new RemoteForge(address, port, code, f.Host, Enum.TryParse<ForgeKind>(f.Kind, out var k) ? k : ForgeKind.GitLab))
            .ToList();
    }

    private ForgeResponse Call(ForgeRequest request)
    {
        request.Code = code;
        request.Host = Host;
        try
        {
            using var client = new TcpClient();
            if (!client.ConnectAsync(Address, Port).Wait(TimeSpan.FromSeconds(4)) || !client.Connected)
                throw new ForgeUnavailableException($"{Address}:{Port}");
            var stream = client.GetStream();
            stream.ReadTimeout = 15000;
            using var writer = new StreamWriter(stream, new UTF8Encoding(false), leaveOpen: true) { AutoFlush = true };
            using var reader = new StreamReader(stream, new UTF8Encoding(false));
            writer.WriteLine(JsonSerializer.Serialize(request, Wire.Options));
            var line = reader.ReadLine() ?? throw new ForgeUnavailableException($"{Address}:{Port}");
            var response = JsonSerializer.Deserialize<ForgeResponse>(line, Wire.Options) ?? throw new ForgeUnavailableException("réponse vide");
            if (response.Error == "code") throw new ForgeRefusedException();
            return response;
        }
        catch (Exception e) when (e is SocketException or IOException or AggregateException or JsonException or InvalidOperationException)
        {
            var socket = e as SocketException ?? e.InnerException as SocketException;
            throw new ForgeUnavailableException(e.Message, socket?.SocketErrorCode == SocketError.ConnectionRefused);
        }
    }

    public Repository? Open(string project)
    {
        var response = Call(new ForgeRequest { Op = "open", Project = project });
        if (!response.Ok) return null;
        var repo = new Repository("/" + project + ".git", bare: true) { HeadRef = response.Head };
        Wire.Load(repo.Objects, response.Objects ?? []);
        foreach (var (name, id) in response.Refs ?? []) repo.Refs[name] = new ObjectId(id);
        return repo;
    }

    public IReadOnlyList<string> Push(string project, ObjectStore objects, IReadOnlyList<RefUpdate> updates) =>
        Call(new ForgeRequest
        {
            Op = "push", Project = project, Objects = Wire.Objects(objects),
            Updates = updates.Select(u => new WireUpdate(u.Ref, u.Old?.Hex, u.New.Hex)).ToList()
        }).Rejected ?? [];

    public IReadOnlyList<ProjectInfo> Projects() =>
        (Call(new ForgeRequest { Op = "list" }).Projects ?? [])
        .Select(p => new ProjectInfo(p.Path, p.Branches.Select(b => (b.Name, b.Short)).ToList())).ToList();

    public bool Create(string project) => Call(new ForgeRequest { Op = "create", Project = project }).Ok;

    public string NewBranchMessage(RemoteUrl url, string branch) => Forge.BranchMessage(Kind, Host, url, branch);

    public string? Authenticate(string publicKey) => Call(new ForgeRequest { Op = "auth", Key = publicKey }).Account;

    public IReadOnlyList<ForgeKey> Keys(string account) =>
        (Call(new ForgeRequest { Op = "keys", Account = account }).Keys ?? []).Select(k => new ForgeKey(k.Title, k.Label, k.Fingerprint, k.Added)).ToList();

    public string? AddKey(string account, string title, string publicKey)
    {
        var response = Call(new ForgeRequest { Op = "addkey", Account = account, Title = title, Key = publicKey });
        return response.Ok ? null : response.Error ?? "invalid";
    }

    public void RemoveKey(string account, string fingerprint) => Call(new ForgeRequest { Op = "delkey", Account = account, Key = fingerprint });
}
