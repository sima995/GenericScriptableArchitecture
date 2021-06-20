﻿namespace GenericScriptableArchitecture
{
    using System;
    using UnityEngine;
    using UnityEngine.Events;

    [Serializable]
    public class ScriptableEventListener<T> : ScriptableEventListenerBase
    {
        [SerializeField] private EventHolder<T> _event;
        [SerializeField] private UnityEvent<T> _response;

        protected virtual void OnEnable() => _event?.AddListener(this);

        protected virtual void OnDisable() => _event?.RemoveListener(this);

        public void OnEventRaised(T arg0)
        {
            AddStackTrace(arg0);
            _response.Invoke(arg0);
        }
    }
}