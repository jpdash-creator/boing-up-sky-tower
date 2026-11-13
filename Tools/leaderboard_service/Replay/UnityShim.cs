// Compile shim only: ContentRepository.LoadFromResources mentions UnityEngine.Resources. The server never calls it (it uses LoadFromJson).
namespace UnityEngine
{
    public class TextAsset { public string text; }

    public static class Resources
    {
        public static T Load<T>(string path) where T : class { return null; }
    }
}
