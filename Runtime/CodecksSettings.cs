using UnityEngine;

namespace Codecks.Runtime
{
    /// <summary>Shared report configuration. Report tokens in client assets are extractable.</summary>
    [CreateAssetMenu(fileName = "CodecksSettings", menuName = "Codecks/Report Settings")]
    public sealed class CodecksSettings : ScriptableObject
    {
        public const string DefaultEndpoint = "https://api.codecks.io/user-report/v1/create-report";
        public const string ResourcePath = "Codecks/CodecksSettings";

        [Tooltip("Scoped, revocable report token, not a Codecks access key. Do not commit a configured asset.")]
        public string reportToken;

        [Tooltip("Absolute HTTP(S) create-report endpoint. Invalid/empty values fail without fallback.")]
        public string endpoint = DefaultEndpoint;
    }
}
