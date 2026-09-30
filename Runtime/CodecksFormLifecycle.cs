using System;
using UnityEngine;

namespace Codecks.Runtime
{
    public enum CodecksFormState { Closed, Opening, Open }

    /// <summary>Small session/ownership primitive shared by the runtime form and editable sample.</summary>
    public sealed class CodecksFormLifecycle
    {
        private sealed class Ownership
        {
            public bool AcquisitionStarted;
            public IDisposable Scope;
        }

        private Ownership ownership;
        private int session;
        public CodecksFormState State { get; private set; }
        public event Action<CodecksFormState> StateChanged;

        public bool IsCurrent(int candidate) => candidate == session && State != CodecksFormState.Closed;

        public int Begin(Action<int> reset, Func<IDisposable> acquire, Action abort)
        {
            var owner = ownership ?? (ownership = new Ownership());
            int current = ++session;
            reset(current);
            bool changed = State != CodecksFormState.Opening;
            State = CodecksFormState.Opening;
            if (changed) Notify(current);
            if (!IsCurrent(current)) return current;
            if (!owner.AcquisitionStarted)
            {
                owner.AcquisitionStarted = true;
                IDisposable acquired;
                try { acquired = acquire?.Invoke(); }
                catch (Exception)
                {
                    Debug.LogWarning("Codecks modal scope acquisition failed; opening was aborted.");
                    if (ownership == owner) abort();
                    return current;
                }
                // A replacement shares the owner, but closing during acquisition detaches it.
                if (ownership == owner) owner.Scope = acquired;
                else Release(acquired);
            }
            return current;
        }

        public void Open(int candidate)
        {
            if (!IsCurrent(candidate)) return;
            State = CodecksFormState.Open;
            Notify(candidate);
        }

        public void Close(Action<int> resetAndHide)
        {
            if (State == CodecksFormState.Closed && ownership == null) return;
            var released = ownership;
            ownership = null;
            int current = ++session;
            State = CodecksFormState.Closed;
            // Hide/reset before user callbacks. Detach ownership before any reentrant work.
            try { resetAndHide(current); }
            finally
            {
                try { Notify(current); }
                finally { Release(released?.Scope); }
            }
        }

        private void Notify(int candidate)
        {
            var handlers = StateChanged;
            if (handlers == null) return;
            var state = State;
            foreach (Action<CodecksFormState> handler in handlers.GetInvocationList())
            {
                if (candidate != session || State != state) break;
                try { handler(state); }
                catch (Exception) { Debug.LogWarning("Codecks modal state observer threw; remaining current observers will still be notified."); }
            }
        }

        private static void Release(IDisposable scope)
        {
            if (scope == null) return;
            try { scope.Dispose(); }
            catch (Exception) { Debug.LogWarning("Codecks modal scope disposal threw; ownership has been released."); }
        }
    }
}
