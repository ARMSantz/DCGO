using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

// Cliente HTTP do backend de decks (contas já são tratadas pela página web).
// Singleton MonoBehaviour: coloque em um GameObject persistente ou use Instance
// (que se auto-cria). Todas as chamadas usam o token de DcgoWebBridge.
public class DcgoWebClient : MonoBehaviour
{
    private static DcgoWebClient _instance;
    public static DcgoWebClient Instance
    {
        get
        {
            if (_instance == null)
            {
                var go = new GameObject("DcgoWebClient");
                _instance = go.AddComponent<DcgoWebClient>();
                DontDestroyOnLoad(go);
            }
            return _instance;
        }
    }

    private void Awake()
    {
        if (_instance != null && _instance != this) { Destroy(gameObject); return; }
        _instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // ---- Modelos (compatíveis com JsonUtility) ----
    [Serializable]
    public class CloudDeck
    {
        public int id;
        public string name;
        public string deckCode; // conteúdo de DeckCodeUtility.GetDeckBuilderFile
        public string updatedAt;
    }

    [Serializable] private class DeckListResponse { public CloudDeck[] decks; }
    [Serializable] private class DeckResponse { public CloudDeck deck; }
    [Serializable] private class DeckRequest { public string name; public string deckCode; }

    private string Url(string path) => DcgoWebBridge.ApiBase.TrimEnd('/') + path;

    private UnityWebRequest Authed(UnityWebRequest req)
    {
        if (DcgoWebBridge.IsLoggedIn)
            req.SetRequestHeader("Authorization", "Bearer " + DcgoWebBridge.Token);
        return req;
    }

    // ---- GET /api/decks ----
    public IEnumerator GetDecks(Action<List<CloudDeck>> onDone, Action<string> onError)
    {
        using var req = Authed(UnityWebRequest.Get(Url("/api/decks")));
        yield return req.SendWebRequest();
        if (req.result != UnityWebRequest.Result.Success)
        {
            onError?.Invoke(req.error);
            yield break;
        }
        var parsed = JsonUtility.FromJson<DeckListResponse>(req.downloadHandler.text);
        onDone?.Invoke(new List<CloudDeck>(parsed.decks ?? Array.Empty<CloudDeck>()));
    }

    // ---- POST /api/decks ----
    public IEnumerator CreateDeck(string name, string deckCode, Action<CloudDeck> onDone, Action<string> onError)
    {
        yield return Send("/api/decks", "POST", name, deckCode, onDone, onError);
    }

    // ---- PUT /api/decks/{id} ----
    public IEnumerator UpdateDeck(int id, string name, string deckCode, Action<CloudDeck> onDone, Action<string> onError)
    {
        yield return Send("/api/decks/" + id, "PUT", name, deckCode, onDone, onError);
    }

    private IEnumerator Send(string path, string method, string name, string deckCode,
                             Action<CloudDeck> onDone, Action<string> onError)
    {
        string json = JsonUtility.ToJson(new DeckRequest { name = name, deckCode = deckCode });
        using var req = new UnityWebRequest(Url(path), method);
        req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");
        Authed(req);
        yield return req.SendWebRequest();
        if (req.result != UnityWebRequest.Result.Success)
        {
            onError?.Invoke(req.error + " | " + req.downloadHandler.text);
            yield break;
        }
        var parsed = JsonUtility.FromJson<DeckResponse>(req.downloadHandler.text);
        onDone?.Invoke(parsed.deck);
    }

    // ---- DELETE /api/decks/{id} ----
    public IEnumerator DeleteDeck(int id, Action onDone, Action<string> onError)
    {
        using var req = Authed(UnityWebRequest.Delete(Url("/api/decks/" + id)));
        yield return req.SendWebRequest();
        if (req.result != UnityWebRequest.Result.Success)
        {
            onError?.Invoke(req.error);
            yield break;
        }
        onDone?.Invoke();
    }
}
