#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
using UnityEngine;

namespace DcgoWebBuild
{
    // No WebGL, gravar em Application.persistentDataPath fica so na memoria (MEMFS)
    // ate um FS.syncfs persistir no IndexedDB. O Unity nao sincroniza apos cada
    // File.Write, entao decks salvos se perdiam ao recarregar a pagina. Este
    // bootstrap chama FS.syncfs periodicamente -> os decks (e qualquer dado em
    // persistentDataPath) sobrevivem ao reload no mesmo navegador.
    public class DcgoPersist : MonoBehaviour
    {
        [DllImport("__Internal")] static extern void DcgoSyncFS();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            var go = new GameObject("DcgoPersist");
            DontDestroyOnLoad(go);
            go.AddComponent<DcgoPersist>();
        }

        void Start() { InvokeRepeating(nameof(Flush), 3f, 3f); }
        void Flush() { DcgoSyncFS(); }
        void OnApplicationPause(bool paused) { if (paused) DcgoSyncFS(); }
        void OnApplicationQuit() { DcgoSyncFS(); }
    }
}
#endif
