#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
using UnityEngine;

namespace DcgoWebBuild
{
    // No WebGL, gravar em Application.persistentDataPath fica so na memoria (MEMFS)
    // ate um FS.syncfs persistir no IndexedDB. Este bootstrap sincroniza de tempos
    // em tempos (sem sobrepor chamadas - ver o jslib) para os decks sobreviverem ao
    // reload, sem floodar o IndexedDB.
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

        void Start() { InvokeRepeating(nameof(Flush), 6f, 6f); }
        void Flush() { DcgoSyncFS(); }
        void OnApplicationPause(bool paused) { if (paused) DcgoSyncFS(); }
        void OnApplicationQuit() { DcgoSyncFS(); }
    }
}
#endif
