using System;
using System.Collections.Generic;
using UnityEngine;

namespace BreathOfEclipse.Core
{
    /// <summary>
    /// Tiny service registry. Systems register themselves on Awake and are looked up by type.
    /// Keeps systems decoupled without turning GameManager into a god object.
    /// </summary>
    public static class Services
    {
        private static readonly Dictionary<Type, object> Registry = new Dictionary<Type, object>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Registry.Clear();

        public static void Register<T>(T service) where T : class
        {
            Registry[typeof(T)] = service;
        }

        public static void Unregister<T>(T service) where T : class
        {
            if (Registry.TryGetValue(typeof(T), out var current) && ReferenceEquals(current, service))
                Registry.Remove(typeof(T));
        }

        public static T Get<T>() where T : class
        {
            if (!Registry.TryGetValue(typeof(T), out var s)) return null;
            // Destroyed Unity objects compare equal to null through the overloaded operator.
            if (s is UnityEngine.Object uo && uo == null)
            {
                Registry.Remove(typeof(T));
                return null;
            }
            return s as T;
        }

        public static bool TryGet<T>(out T service) where T : class
        {
            service = Get<T>();
            return service != null;
        }
    }
}
