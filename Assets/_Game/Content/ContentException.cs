using System;

namespace BoingUpSkyTower.Content
{
    /// <summary>Thrown for malformed, missing or inconsistent content. Message always names the offending id.</summary>
    public sealed class ContentException : Exception
    {
        public string ContentId { get; }

        public ContentException(string contentId, string message, Exception inner = null)
            : base("[" + contentId + "] " + message, inner)
        {
            ContentId = contentId;
        }
    }
}
