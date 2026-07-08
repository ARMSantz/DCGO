using System.Runtime.InteropServices;

// Acesso, do C#, aos dados de login fornecidos pela página web.
// Em WebGL lê de window.DCGO_AUTH (via DcgoWebBridge.jslib).
// No Editor/desktop usa valores configuráveis (para testar sem a página).
public static class DcgoWebBridge
{
#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")] private static extern string DcgoWeb_GetToken();
    [DllImport("__Internal")] private static extern string DcgoWeb_GetUsername();
    [DllImport("__Internal")] private static extern string DcgoWeb_GetApiBase();
    [DllImport("__Internal")] private static extern void DcgoWeb_Logout();

    // Desloga: limpa o token e volta para a tela de login.
    public static void Logout() => DcgoWeb_Logout();

    public static string Token => DcgoWeb_GetToken();

    public static string Username
    {
        get
        {
            string u = DcgoWeb_GetUsername();
            return string.IsNullOrEmpty(u) ? "Player" : u;
        }
    }

    public static string ApiBase
    {
        get
        {
            string a = DcgoWeb_GetApiBase();
            return string.IsNullOrEmpty(a) ? "http://localhost:8080" : a;
        }
    }
#else
    // Valores de teste no Editor/desktop. Defina via Inspector ou código.
    public static string Token { get; set; } = "";
    public static string Username { get; set; } = "Player";
    public static string ApiBase { get; set; } = "http://localhost:8080";

    public static void Logout() { Token = ""; }
#endif

    public static bool IsLoggedIn => !string.IsNullOrEmpty(Token);
}
