using System;
using UnityEngine;
#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

// Leitura de clipboard que funciona no WebGL. GUIUtility.systemCopyBuffer nao
// acessa o clipboard do navegador (sandbox); aqui usamos a API assincrona
// navigator.clipboard.readText via jslib, com fallback de window.prompt para
// navegadores sem permissao (o usuario cola manualmente com Ctrl+V).
public class DcgoClipboard : MonoBehaviour
{
#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")] static extern void DcgoClipboard_Read(string goName, string method);
#endif

    static DcgoClipboard _inst;
    Action<string> _pending;

    static void Ensure()
    {
        if (_inst != null) return;
        var go = new GameObject("DcgoClipboard");
        DontDestroyOnLoad(go);
        _inst = go.AddComponent<DcgoClipboard>();
    }

    // Le o clipboard e chama cb(texto). No desktop/editor eh sincrono.
    public static void Read(Action<string> cb)
    {
        Ensure();
#if UNITY_WEBGL && !UNITY_EDITOR
        _inst._pending = cb;
        DcgoClipboard_Read("DcgoClipboard", "OnClipboardText");
#else
        cb?.Invoke(GUIUtility.systemCopyBuffer);
#endif
    }

    // Callback vindo do jslib (via SendMessage).
    public void OnClipboardText(string text)
    {
        var cb = _pending;
        _pending = null;
        cb?.Invoke(text ?? "");
    }
}
