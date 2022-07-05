﻿namespace GenericScriptableArchitecture.Editor
{
    using System;
    using System.Linq;
    using GenericUnityObjects.Editor;
    using GenericUnityObjects.Editor.MonoBehaviours;
    using GenericUnityObjects.Editor.Util;
    using SolidUtilities.Editor;
    using TypeReferences;
    using UnityEditor;
    using UnityEngine;
    using Object = UnityEngine.Object;

    [CustomEditor(typeof(VariableInstancer))]
    public class WildcardVariableInstancerEditor : Editor
    {
        private VariableInstancer _target;
        private BaseVariableInstancer _component;
        private GeneratedComponentsDebugDrawer _debugDrawer;

        private void OnEnable()
        {
            _target = (VariableInstancer) target;
            if (target == null) return;

            var componentProperty = serializedObject.FindProperty(nameof(VariableInstancer._component));
            _debugDrawer = new GeneratedComponentsDebugDrawer(componentProperty, _target.gameObject,
                () => _target.GetComponents<VariableInstancer>(),
                () => _target.GetComponents<BaseVariableInstancer>());
        }

        private void OnDisable()
        {
            if (target == null)
            {
                RemoveComponent(_component);
            }
        }

        private void RemoveComponent(Component component)
        {
            if (component == null)
                return;

            Undo.DestroyObjectImmediate(component);
        }

        public override void OnInspectorGUI()
        {
            if (_target == null)
            {
                var scriptProperty = serializedObject.FindProperty("m_Script");
                if (scriptProperty != null) EditorGUILayout.PropertyField(scriptProperty);
                return;
            }

            serializedObject.UpdateIfRequiredOrScript();

            _debugDrawer.DrawComponentProperty();

            // TODO: when reassigning variable in play mode, invoke the setter properly
            // What if component references VariableInstancer explicitly? We can't use wildcard then, but we can add the + button.
            DrawObjectField();


            DrawObjectField();
            // draw variable in foldout
            _debugDrawer.DrawDebugFlag();
            serializedObject.ApplyModifiedProperties();
        }

        #region similar code

        private const string WildcardListenerKey = "ListenerEditor_WildcardListener";
        private const string ListenerTypeKey = "ListenerEditor_ListenerType";
        private const string EventKey = "ListenerEditor_EventReference";

        private void DrawObjectField()
        {
            var component = _target._component;
            var oldVariable = component == null ? null : component.Variable;

            if (_target._component != null)
                Undo.RecordObject(_target._component, "Changed variable field");

            var newVariable = GenericObjectDrawer.ObjectField("Variable", oldVariable, typeof(BaseVariable), false);

            if (newVariable != oldVariable)
                ChangeVariable(newVariable);
        }

        private void ChangeVariable(Object newVariable)
        {
            RemoveComponent(_target._component);

            var listenerType = GetGenericComponentType(newVariable);

            if (listenerType.GenericTypeDefinition == null)
                return;

            var component = AddComponent(listenerType.GenericTypeDefinition, listenerType.GenericArgs, out bool reloadRequired) as BaseVariableInstancer;

            if (reloadRequired)
            {
                PersistentStorage.SaveData(WildcardListenerKey, _target);
                PersistentStorage.SaveData(ListenerTypeKey, new TypeReference(listenerType.GenericTypeDefinition.MakeGenericType(listenerType.GenericArgs)));
                PersistentStorage.SaveData(EventKey, newVariable);
                PersistentStorage.ExecuteOnScriptsReload(OnAfterComponentAdded);
                AssetDatabase.Refresh();
            }
            else
            {
                ChangeHiddenComponent(_target, component, newVariable as BaseVariable);
            }
        }

        private Component AddComponent(Type genericTypeDefinition, Type[] genericArgs, out bool reloadRequired)
        {
            if (genericArgs == null)
            {
                reloadRequired = false;
                return Undo.AddComponent(_target.gameObject, genericTypeDefinition);
            }

            return GenericBehaviourCreator.AddComponent(null, _target.gameObject, genericTypeDefinition, genericArgs, out reloadRequired);
        }

        private static void OnAfterComponentAdded()
        {
            try
            {
                var thisListener = PersistentStorage.GetData<VariableInstancer>(WildcardListenerKey);
                var componentType = PersistentStorage.GetData<TypeReference>(ListenerTypeKey).Type;
                var variable = PersistentStorage.GetData<Object>(EventKey) as BaseVariable;

                var component = thisListener.gameObject.GetComponent(componentType) as BaseVariableInstancer;

                ChangeHiddenComponent(thisListener, component, variable);
            }
            finally
            {
                PersistentStorage.DeleteData(WildcardListenerKey);
                PersistentStorage.DeleteData(ListenerTypeKey);
                PersistentStorage.DeleteData(EventKey);
            }
        }

        private static void ChangeHiddenComponent(VariableInstancer wildcardComponent, BaseVariableInstancer genericComponent, BaseVariable variable)
        {
            // The flags are set persistently only through serializedObject, we do it when an editor is created.
            // However, if we don't set the flags here, the component will appear for a frame and we don't want that.
            genericComponent.hideFlags = HideFlags.HideInInspector;

            Undo.RecordObject(wildcardComponent, "Change component");
            wildcardComponent._component = genericComponent;

            Undo.RecordObject(genericComponent, "Change event");
            genericComponent.Variable = variable; // this is not same

            PrefabUtility.RecordPrefabInstancePropertyModifications(genericComponent);
            PrefabUtility.RecordPrefabInstancePropertyModifications(wildcardComponent);
        }

        #endregion

        private (Type GenericTypeDefinition, Type[] GenericArgs) GetGenericComponentType(Object newVariable)
        {
            if (newVariable == null)
                return (null, null);

            var concreteVariableType = newVariable.GetType();
            var genericVariableType = concreteVariableType.BaseType;

            return (genericVariableType.GetGenericTypeDefinition(), genericVariableType.GenericTypeArguments);
        }

        private void CreateVariableEditorIfNeeded()
        {
            if (_component == _target._component && _componentEditor != null)
                return;

            _component = _target._component;

            if (_component == null)
                return;

            _componentEditor = (ScriptableEventListenerEditor) CreateEditor(_component, typeof(ScriptableEventListenerEditor));
            _componentEditor.ShowEventField = false;  // for debug, we want to draw object field in normal editors, but still not draw them in inlined ones

            if (!_showGeneratedComponents)
                SetHideFlagsPersistently(_componentEditor.serializedObject, HideFlags.HideInInspector);
        }
    }

    [CustomEditor(typeof(ScriptableEventListener))]
    public class WildcardScriptableEventListenerEditor : Editor
    {
        private const string WildcardListenerKey = "ListenerEditor_WildcardListener";
        private const string ListenerTypeKey = "ListenerEditor_ListenerType";
        private const string EventKey = "ListenerEditor_EventReference";

        private ScriptableEventListener _target;
        private ScriptableEventListenerEditor _componentEditor;
        private BaseScriptableEventListener _component;
        private GeneratedComponentsDebugDrawer _debugDrawer;

        private void OnEnable()
        {
            _target = (ScriptableEventListener) target;

            if (_target != null)
            {
                var componentProperty = serializedObject.FindProperty(nameof(ScriptableEventListener._component));
                _debugDrawer = new GeneratedComponentsDebugDrawer(componentProperty, _target.gameObject,
                    () => _target.GetComponents<ScriptableEventListener>(),
                    () => _target.GetComponents<BaseScriptableEventListener>());
            }
        }

        private void OnDisable()
        {
            if (target == null)
            {
                RemoveComponent(_component);
            }
        }

        public override void OnInspectorGUI()
        {
            if (_target == null)
            {
                var scriptProperty = serializedObject.FindProperty("m_Script");
                if (scriptProperty != null) EditorGUILayout.PropertyField(scriptProperty);
                return;
            }

            serializedObject.UpdateIfRequiredOrScript();

            _debugDrawer.DrawComponentProperty();
            CreateComponentEditorIfNeeded();
            DrawObjectField();
            DrawUnityEvent();
            _debugDrawer.DrawDebugFlag();
            serializedObject.ApplyModifiedProperties();
        }

        private void CreateComponentEditorIfNeeded()
        {
            if (_component == _target._component && _componentEditor != null)
                return;

            _component = _target._component;

            if (_component == null)
                return;

            _componentEditor = (ScriptableEventListenerEditor) CreateEditor(_component, typeof(ScriptableEventListenerEditor));
            _componentEditor.ShowEventField = false;  // for debug, we want to draw object field in normal editors, but still not draw them in inlined ones

            if (!_debugDrawer.ShowGeneratedComponents)
                _componentEditor.serializedObject.SetHideFlagsPersistently(HideFlags.HideInInspector);

            // It is necessary to change DrawObjectField after HideFlags because serializedObject.ApplyModifiedProperties() inside SetHideFlags discards the changed value of DrawObjectField
            _component.DrawObjectField = _debugDrawer.ShowGeneratedComponents;
        }

        private void DrawObjectField()
        {
            var oldEvent = GetCurrentEvent();

            if (_target._component != null)
                Undo.RecordObject(_target._component, "Changed event field");

            var newEvent = GenericObjectDrawer.ObjectField("Event", oldEvent, typeof(BaseEvent), true);

            if (newEvent != oldEvent)
                ChangeEvent(newEvent);
        }

        private void ChangeEvent(Object newEvent)
        {
            RemoveComponent(_target._component);

            var listenerType = GetListenerComponentType(newEvent);

            if (listenerType.GenericTypeDefinition == null)
                return;

            var component = AddComponent(listenerType.GenericTypeDefinition, listenerType.GenericArgs, out bool reloadRequired) as BaseScriptableEventListener;

            if (reloadRequired)
            {
                PersistentStorage.SaveData(WildcardListenerKey, _target);
                PersistentStorage.SaveData(ListenerTypeKey, new TypeReference(listenerType.GenericTypeDefinition.MakeGenericType(listenerType.GenericArgs)));
                PersistentStorage.SaveData(EventKey, newEvent);
                PersistentStorage.ExecuteOnScriptsReload(OnAfterComponentAdded);
                AssetDatabase.Refresh();
            }
            else
            {
                ChangeHiddenComponent(_target, component, newEvent as BaseEvent);
            }
        }

        private static void OnAfterComponentAdded()
        {
            try
            {
                var thisListener = PersistentStorage.GetData<ScriptableEventListener>(WildcardListenerKey);
                var componentType = PersistentStorage.GetData<TypeReference>(ListenerTypeKey).Type;
                var @event = PersistentStorage.GetData<Object>(EventKey) as BaseEvent;

                var component = thisListener.gameObject.GetComponent(componentType) as BaseScriptableEventListener;

                ChangeHiddenComponent(thisListener, component, @event);
            }
            finally
            {
                PersistentStorage.DeleteData(WildcardListenerKey);
                PersistentStorage.DeleteData(ListenerTypeKey);
                PersistentStorage.DeleteData(EventKey);
            }
        }

        private static void ChangeHiddenComponent(ScriptableEventListener listener, BaseScriptableEventListener component, BaseEvent @event)
        {
            // The flags are set persistently only through serializedObject, we do it when an editor is created.
            // However, if we don't set the flags here, the component will appear for a frame and we don't want that.
            component.hideFlags = HideFlags.HideInInspector;

            Undo.RecordObject(listener, "Change component");
            listener._component = component;

            Undo.RecordObject(component, "Change event");
            component.Event = @event;

            PrefabUtility.RecordPrefabInstancePropertyModifications(component);
            PrefabUtility.RecordPrefabInstancePropertyModifications(listener);
        }

        private void RemoveComponent(Component component)
        {
            if (component == null)
                return;

            Undo.DestroyObjectImmediate(component);
        }

        private void DrawUnityEvent()
        {
            if (_componentEditor == null)
                return;

            _componentEditor.OnInspectorGUI();
        }

        private Object GetCurrentEvent()
        {
            var component = _target._component;
            return component == null ? null : component.Event;
        }

        private Component AddComponent(Type genericTypeDefinition, Type[] genericArgs, out bool reloadRequired)
        {
            if (genericArgs == null)
            {
                reloadRequired = false;
                return Undo.AddComponent(_target.gameObject, genericTypeDefinition);
            }

            return GenericBehaviourCreator.AddComponent(null, _target.gameObject, genericTypeDefinition, genericArgs, out reloadRequired);
        }

        private (Type GenericTypeDefinition, Type[] GenericArgs) GetListenerComponentType(Object newEvent)
        {
            if (newEvent == null)
                return (null, null);

            Type definition = null;
            Type[] genericArgs = null;

            var concreteEventType = newEvent.GetType();

            if (!concreteEventType.BaseType.IsGenericType)
            {
                if (concreteEventType == typeof(ScriptableEvent))
                {
                    definition = typeof(VoidScriptableEventListener);
                    return (definition, genericArgs);
                }

                Debug.LogError($"The type is not a known type: {concreteEventType}");
                return (definition, genericArgs);
            }

            var eventType = concreteEventType.BaseType.IsGenericType ? concreteEventType.BaseType : concreteEventType;
            var eventTypeDefinition = eventType.GetGenericTypeDefinition();

            if (eventTypeDefinition == typeof(Constant<>))
            {
                Debug.LogWarning("Constants don't change, so the event listener will never receive an event from them.");
            }
            else if (eventTypeDefinition == typeof(VariableWithHistory<>))
            {
                definition = typeof(ScriptableEventListener<,>);
                var genericArg = eventType.GenericTypeArguments[0];
                genericArgs = new [] { genericArg, genericArg };
            }
            else if (eventTypeDefinition == typeof(Variable<>) || eventTypeDefinition == typeof(ScriptableEvent<>))
            {
                definition = typeof(ScriptableEventListener<>);
                genericArgs = eventType.GenericTypeArguments;
            }
            else if (eventTypeDefinition == typeof(ScriptableEvent<,>))
            {
                definition = typeof(ScriptableEventListener<,>);
                genericArgs = eventType.GenericTypeArguments;
            }
            else if (eventTypeDefinition == typeof(ScriptableEvent<,,>))
            {
                definition = typeof(ScriptableEventListener<,,>);
                genericArgs = eventType.GenericTypeArguments;
            }

            return (definition, genericArgs);
        }
    }
}