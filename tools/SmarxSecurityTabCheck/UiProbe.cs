using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Accessibility;

// Lecture et pilotage de la boîte Propriétés > Sécurité de l'Explorateur, ouverte dans un autre processus (ui-open.ps1).
// Les listes se lisent par MSAA (oleacc), les coches par UI Automation (dans les scripts). Les clics de souris ne partent
// que si la boîte visée est au premier plan.
public static class UiProbe
{
    [DllImport("oleacc.dll")] static extern int AccessibleObjectFromWindow(IntPtr hwnd, uint id, ref Guid iid, [MarshalAs(UnmanagedType.IUnknown)] out object obj);
    [DllImport("user32.dll")] static extern bool PostMessage(IntPtr hwnd, uint msg, IntPtr w, IntPtr l);
    [DllImport("user32.dll")] static extern int GetDlgCtrlID(IntPtr hwnd);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetClassName(IntPtr hwnd, StringBuilder s, int n);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetWindowText(IntPtr hwnd, StringBuilder s, int n);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr hwnd);
    delegate bool EnumProc(IntPtr hwnd, IntPtr l);
    [DllImport("user32.dll")] static extern bool EnumChildWindows(IntPtr parent, EnumProc proc, IntPtr l);
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc proc, IntPtr l);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern IntPtr FindWindowEx(IntPtr parent, IntPtr after, string cls, string title);
    [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [StructLayout(LayoutKind.Sequential)] struct RECT { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] static extern void mouse_event(uint flags, int dx, int dy, uint data, IntPtr extra);

    static Guid IID_IAccessible = new Guid("618736E0-3C3D-11CF-810C-00AA00389B71");

    static IAccessible Acc(IntPtr hwnd)
    {
        object o;
        AccessibleObjectFromWindow(hwnd, 0xFFFFFFFC, ref IID_IAccessible, out o);
        return (IAccessible)o;
    }

    public static string Text(IntPtr h) { StringBuilder s = new StringBuilder(512); GetWindowText(h, s, 512); return s.ToString(); }
    static string Class(IntPtr h) { StringBuilder s = new StringBuilder(256); GetClassName(h, s, 256); return s.ToString(); }

    // Fenêtre de premier niveau visible dont le titre commence par prefix
    public static IntPtr WaitTop(string prefix, int ms)
    {
        for (int t = 0; t < ms; t += 100)
        {
            IntPtr found = IntPtr.Zero;
            EnumWindows((h, l) => { if (IsWindowVisible(h) && Text(h).StartsWith(prefix)) { found = h; return false; } return true; }, IntPtr.Zero);
            if (found != IntPtr.Zero) { Thread.Sleep(500); return found; }
            Thread.Sleep(100);
        }
        return IntPtr.Zero;
    }

    // Descendant visible par identifiant de contrôle et classe (PowerShell passe $null comme chaîne vide)
    public static IntPtr Find(IntPtr root, int id, string cls)
    {
        IntPtr found = IntPtr.Zero;
        EnumChildWindows(root, (h, l) => { if (GetDlgCtrlID(h) == id && (string.IsNullOrEmpty(cls) || Class(h) == cls) && IsWindowVisible(h)) { found = h; return false; } return true; }, IntPtr.Zero);
        return found;
    }

    public static IntPtr FindText(IntPtr root, string cls, string text)
    {
        IntPtr found = IntPtr.Zero;
        EnumChildWindows(root, (h, l) => { if (Class(h) == cls && Text(h) == text && IsWindowVisible(h)) { found = h; return false; } return true; }, IntPtr.Zero);
        return found;
    }

    public static IntPtr FindClass(IntPtr root, string cls)
    {
        IntPtr found = IntPtr.Zero;
        EnumChildWindows(root, (h, l) => { if (Class(h) == cls && IsWindowVisible(h)) { found = h; return false; } return true; }, IntPtr.Zero);
        return found;
    }

    // Bouton enfant direct d'une boîte, par son texte : les cases à cocher partagent les identifiants 1, 2…
    public static IntPtr DirectButton(IntPtr dialog, string text) { return FindWindowEx(dialog, IntPtr.Zero, "Button", text); }

    static string Safe(Func<object> f) { try { object o = f(); return o == null ? "" : o.ToString(); } catch { return "?"; } }

    // Enfants MSAA d'une liste : n°|rôle|état (hex)|nom|description (colonnes de la fenêtre Avancé)
    public static List<string> Items(IntPtr hwnd)
    {
        IAccessible acc = Acc(hwnd);
        List<string> lines = new List<string>();
        int n = acc.accChildCount;
        for (int i = 1; i <= n; i++)
        {
            object id = i;
            lines.Add(i + "|" + Safe(() => acc.get_accRole(id)) + "|" + Safe(() => ((int)acc.get_accState(id)).ToString("X")) + "|"
                      + Safe(() => acc.get_accName(id)) + "|" + Safe(() => acc.get_accDescription(id)));
        }
        return lines;
    }

    public static void Select(IntPtr list, int child) { Acc(list).accSelect(3, child); Thread.Sleep(300); }
    public static void Close(IntPtr window) { PostMessage(window, 0x10, IntPtr.Zero, IntPtr.Zero); Thread.Sleep(500); }
    public static void Scroll(IntPtr hwnd, int code) { PostMessage(hwnd, 0x115, (IntPtr)code, IntPtr.Zero); Thread.Sleep(300); }

    // Clic au centre d'un bouton (BM_CLICK échoue sur une boîte inactive)
    public static bool MouseClick(IntPtr dialog, IntPtr button)
    {
        if (button == IntPtr.Zero) return false;
        RECT r; GetWindowRect(button, out r);
        return MouseClickAt(dialog, (r.Left + r.Right) / 2, (r.Top + r.Bottom) / 2);
    }

    public static bool MouseClickAt(IntPtr dialog, int x, int y)
    {
        SetForegroundWindow(dialog); Thread.Sleep(300);
        if (GetForegroundWindow() != dialog) return false;
        SetCursorPos(x, y); Thread.Sleep(100);
        mouse_event(0x2, 0, 0, 0, IntPtr.Zero); mouse_event(0x4, 0, 0, 0, IntPtr.Zero);
        Thread.Sleep(300);
        return true;
    }
}
