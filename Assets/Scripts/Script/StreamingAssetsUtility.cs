
using System;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using WebP;

public class StreamingAssetsUtility
{
    public static async Task<byte[]> ReadFile(string path)
    {
        using (FileStream fileStream = new FileStream(
            path, FileMode.Open, FileAccess.Read))
        {
            var resultBytes = new byte[fileStream.Length];
            await fileStream.ReadAsync(resultBytes, 0, (int)fileStream.Length);
            return resultBytes;
        }
    }

    #region 画像の取得
    public static Texture2D BinaryToTexture(byte[] bytes)
    {
        Texture2D texture = new Texture2D(1, 1);
        texture.LoadImage(bytes);
        return texture;
    }

    public static async Task<Sprite> GetSprite(string fileName, bool isCard = false, bool isLauncher = false)
    {
        string path = "";

        if (isCard)
        {
            if (fileName.Contains("-token"))
            {
                return await GetTokenImageData(Path.Combine(GetStreamingAssetPath("Textures", isLauncher), $"Card/{fileName}.png").Replace("\\", "/"));
            }
            else
            {
                path = Path.Combine(GetStreamingAssetPath("Textures", isLauncher), $"Card/{fileName}.webp").Replace("\\", "/");

                if (!File.Exists(path))
                {
                    return await GetCardImageData(fileName, path);
                }
                else
                {
                    return await GetCardImageDataLocal(path);
                }
            }
        }
        else
        {
            return await GetSpriteImage(fileName, isLauncher);
        }
    }

    public static async Task<Sprite> GetSpriteImage(string fileName, bool isLauncher = false)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        // WebGL: StreamingAssets eh servido por HTTP -> carrega via UnityWebRequest
        // (File.Exists/ReadFile nao funcionam com URL no navegador).
        string url = Application.streamingAssetsPath + "/Textures/" + fileName + ".png";
        using (UnityWebRequest req = UnityWebRequestTexture.GetTexture(url))
        {
            UnityWebRequestAsyncOperation op = req.SendWebRequest();
            while (!op.isDone) await Task.Yield();
            if (req.result == UnityWebRequest.Result.Success)
            {
                Texture2D wtex = DownloadHandlerTexture.GetContent(req);
                return Sprite.Create(wtex, new Rect(0, 0, wtex.width, wtex.height), Vector2.zero);
            }
            return null;
        }
#else
        string path = Path.Combine(GetStreamingAssetPath("Textures", isLauncher), $"{fileName}.jpg").Replace("\\", "/");

        if (!File.Exists(path))
            path = Path.Combine(GetStreamingAssetPath("Textures", isLauncher), $"{fileName}.png").Replace("\\", "/");

        if (File.Exists(path))
        {
            byte[] imageBuff = await ReadFile(path);
            Texture2D tex = BinaryToTexture(imageBuff);

            Sprite sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), Vector2.zero);

            return sprite;
        }

        return null;
#endif
    }

    public static async Task<Sprite> GetTokenImageData(string path)
    {
        if (File.Exists(path))
        {
            byte[] imageBuff = await ReadFile(path);
            Texture2D tex = BinaryToTexture(imageBuff);

            Sprite sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), Vector2.zero);
            return sprite;
        }

        return null;
    }

    public static async Task<Sprite> GetCardImageDataLocal(string path)
    {
        if (File.Exists(path))
        {
            Debug.Log($"File Exists Locally: {path}");
            byte[] imageBuff = await ReadFile(path);
            Debug.Log($"Grabbing image bytes: {imageBuff}");
            Texture2D texture = Texture2DExt.CreateTexture2DFromWebP(imageBuff, lMipmaps: true, lLinear: false, lError: out WebP.Error lError);
            Debug.Log($"Converting WebP to Texture2D: {texture}");
            if (lError == WebP.Error.Success)
            {
                Sprite sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), Vector2.zero);

                return sprite;
            }
            else
            {
                Debug.Log(lError.ToString());
            }
        }

        return null;
    }

    public static async Task<Sprite> GetCardImageData(string fileName, string filePath)
    {
        Sprite sprite;

        // Attempt to get the card image from repo
        sprite = await HandleCardImage(fileName, filePath);

        if (sprite != null) return sprite;
        else
        {

            // Attempt to get the card image from repo, this time with the sample suffix
            sprite = await HandleCardImage(fileName, filePath, isSample: true);

            if (sprite != null) return sprite;
            return null;
        }

    }

    public static async Task<Sprite> HandleCardImage(string fileName, string filePath, bool isSample = false)
    {
        string urlPath = $"https://raw.githubusercontent.com/TakaOtaku/Digimon-Card-App/main/src/assets/images/cards/{fileName}";
        if (isSample) urlPath += $"-Sample.webp";
        else urlPath += $".webp";

        UnityWebRequest webReq_CardImage = UnityWebRequest.Get(urlPath);
        UnityWebRequestAsyncOperation operation = webReq_CardImage.SendWebRequest();

        while (!operation.isDone)
        {
            await Task.Yield();
        }

        Debug.Log($"WebRequest isDone: {fileName}");
        if (webReq_CardImage.result == UnityWebRequest.Result.ConnectionError)
            return null;
        else if (webReq_CardImage.result == UnityWebRequest.Result.ProtocolError)
            return null;
        else
        {
            Debug.Log($"WebRequest Successful: Checking local file - {File.Exists(filePath)}");
            if (!File.Exists(filePath))
                File.WriteAllBytes(filePath, webReq_CardImage.downloadHandler.data);

            Texture2D texture = Texture2DExt.CreateTexture2DFromWebP(webReq_CardImage.downloadHandler.data, lMipmaps: true, lLinear: false, lError: out WebP.Error lError);

            if (lError == WebP.Error.Success)
            {
                Sprite sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), Vector2.zero);
                return sprite;
            }
            else Debug.Log($"Failed to convert: {lError.ToString()}");
            return null;
        }
    }

    #endregion

    public static bool IsCardExists(CEntity_Base cEntity_Base)
    {
        string path = Path.Combine(GetStreamingAssetPath("Textures", false), $"Card/{cEntity_Base.CardSpriteName}.webp").Replace("\\", "/");

        if (cEntity_Base.CardSpriteName.Contains("token"))
            path = Path.Combine(GetStreamingAssetPath("Textures", false), $"Card/{cEntity_Base.CardSpriteName}.png").Replace("\\", "/");

        return File.Exists(path);
    }

    #region テキストファイルの取得
    public static string GetText(string fileName)
    {
        string path = Path.Combine(GetStreamingAssetPath("", false), $"{fileName}.txt").Replace("\\", "/");

        if (File.Exists(path))
        {
            return File.ReadAllText(path);
        }

        return "";
    }
    #endregion

    public static string GetStreamingAssetPath(string subPath, bool isLauncher)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        // No WebGL o StreamingAssets eh somente-leitura (HTTP) e subir diretorios
        // numa URL gera caminhos quebrados ("/https:/Assets/..."). Redireciona os
        // dados gravaveis (decks e cache de texturas) para persistentDataPath.
        if (subPath == "Decks" || subPath == "Textures")
        {
            string p = Path.Combine(Application.persistentDataPath, subPath).Replace("\\", "/");
            if (!Directory.Exists(p)) Directory.CreateDirectory(p);
            if (subPath == "Textures")
            {
                string cardDir = Path.Combine(p, "Card");
                if (!Directory.Exists(cardDir)) Directory.CreateDirectory(cardDir);
            }
            return p;
        }
#endif
        if (isLauncher)
        {
            string path = Application.streamingAssetsPath;

            path = GetOneUpperDirectoryPath(path);

            path = Path.Combine(path, $"Assets/{subPath}").Replace("\\", "/");

            return path;
        }

        else
        {
            string path = Application.streamingAssetsPath;

            path = GetOneUpperDirectoryPath(path);

            path = GetOneUpperDirectoryPath(path);

            path = Path.Combine(path, $"Assets/{subPath}").Replace("\\", "/");

            return path;
        }
    }

    static string GetOneUpperDirectoryPath(string path)
    {
        if (String.IsNullOrEmpty(path)) return "";
        path = path.Replace("\\", "/");
        if (!path.Contains("/")) return path;

        path = path.Substring(0, path.LastIndexOf("/") + 1);

        if (path.Length >= 1)
        {
            if (path[path.Length - 1] == '/')
            {
                path = path.Substring(0, path.LastIndexOf("/"));
            }
        }

        return path.Substring(0, path.LastIndexOf("/") + 1);
    }
}
