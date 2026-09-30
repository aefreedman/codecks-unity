using System;
using UnityEngine;

namespace Codecks.Runtime
{
    // The uGUI view stays inactive until capture, so Unity may omit its disable/destroy
    // callbacks. This temporary guard lives on the active coroutine host, not the view.
    internal sealed class CodecksPendingOpening : MonoBehaviour
    {
        private Func<bool> isValid;
        private Action abort;

        internal void Initialize(Func<bool> valid, Action aborted)
        {
            hideFlags = HideFlags.HideAndDontSave;
            isValid = valid;
            abort = aborted;
        }

        internal void Disarm()
        {
            isValid = null;
            abort = null;
            Destroy(this);
        }

        private void Update()
        {
            if (isValid != null && !isValid()) Abort();
        }

        private void OnDisable() => Abort();
        private void OnDestroy() => Abort();

        private void Abort()
        {
            var callback = abort;
            isValid = null;
            abort = null;
            try { callback?.Invoke(); }
            finally { if (this != null) Destroy(this); }
        }
    }
}
