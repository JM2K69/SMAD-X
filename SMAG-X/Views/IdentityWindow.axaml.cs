using System.Text.RegularExpressions;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace SMAGX.Views;

/// <summary>
/// L'identité de l'élève : nom et adresse, nom d'utilisateur du poste. Git ne la reçoit pas : c'est à l'élève de la lui
/// donner avec git config, comme sur un vrai poste.
/// </summary>
public partial class IdentityWindow : Window
{
    private bool _loginEdited;
    private bool _settingLogin;
    private readonly IReadOnlyCollection<string> _takenLogins = [];

    public IdentityWindow() => InitializeComponent();

    /// <param name="takenLogins">Les noms d'utilisateur des autres postes de la fenêtre, refusés.</param>
    /// <param name="another">Un poste de plus (un autre élève, ou le même dans un autre rôle) plutôt que celui de l'élève.</param>
    public IdentityWindow(SmagxSettings current, IReadOnlyCollection<string> takenLogins, bool another) : this()
    {
        _takenLogins = takenLogins;
        NameBox.Text = current.Name;
        EmailBox.Text = current.Email;
        LoginBox.Text = current.Login;
        _loginEdited = !string.IsNullOrEmpty(current.Login);
        if (!another) return;
        var text = SmagxText.Instance;
        Title = Heading.Text = text["Identity.AnotherTitle"];
        Intro.Text = text["Identity.AnotherIntro"];
        if (string.IsNullOrEmpty(current.Login)) OkButton.Content = text["Identity.AnotherOk"];
        NameBox.PlaceholderText = "Bob Durand";
        EmailBox.PlaceholderText = "bob@lycee.example";
        LoginBox.PlaceholderText = "bob";
    }

    private void OnNameChanged(object? sender, TextChangedEventArgs e)
    {
        if (_loginEdited) return;
        _settingLogin = true;
        LoginBox.Text = SmagxSettings.LoginFrom(NameBox.Text ?? "");
        _settingLogin = false;
    }

    private void OnLoginChanged(object? sender, TextChangedEventArgs e)
    {
        if (!_settingLogin) _loginEdited = true;
    }

    private void OnOk(object? sender, RoutedEventArgs e)
    {
        var text = SmagxText.Instance;
        var name = NameBox.Text?.Trim() ?? "";
        var email = EmailBox.Text?.Trim() ?? "";
        var login = LoginBox.Text?.Trim() ?? "";
        string? error = null;
        if (name.Length == 0) error = text.T("Indiquez votre prénom et votre nom.", "Enter your first and last name.");
        else if (!Regex.IsMatch(email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$")) error = text.T("Indiquez une adresse électronique (nom@domaine.fr).", "Enter an email address (name@domain.com).");
        else if (!Regex.IsMatch(login, "^[a-z][a-z0-9_-]{0,31}$")) error = text.T("Le nom d'utilisateur : des minuscules sans accent, des chiffres, « - » ou « _ ».", "The user name: lowercase letters without accents, digits, \"-\" or \"_\".");
        else if (_takenLogins.Contains(login)) error = text.T($"« {login} » est déjà le nom d'utilisateur d'un autre poste.", $"\"{login}\" is already the user name of another workstation.");
        if (error is not null)
        {
            ErrorText.Text = error;
            ErrorText.IsVisible = true;
            return;
        }
        Close(new SmagxSettings { Name = name, Email = email, Login = login });
    }

    private void OnCancel(object? sender, RoutedEventArgs e) => Close(null);
}
