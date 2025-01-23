using System;
using UnityEngine;

namespace Utils
{
    public abstract class MonoSingleton<T> : MonoBehaviour where T : MonoBehaviour
    {
        public static Action OnStarted;
        public static Action OnDestroyed;

        private static T instance;
        public static T Instance => instance;

#pragma warning disable CS0414 // Field is assigned but its value is never used
        private bool exist = false;
#pragma warning restore CS0414 // Field is assigned but its value is never used
        public bool Exist => instance;
        protected virtual void Awake()
        {
            if (instance)
            {
                Debug.LogError($"Only one instance of {typeof(T)} available");
            }
            instance = FindFirstObjectByType<T>();
            exist = true;
            OnStarted?.Invoke();
        }

        protected void OnDestroy()
        {
            exist = false;
            instance = null;
            OnDestroyed?.Invoke();
        }
    }
}