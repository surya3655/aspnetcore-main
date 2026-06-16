// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Concurrent;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Components.Reflection;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Logging;

namespace Microsoft.AspNetCore.Components.Endpoints;

// Source-generated serializer context for session cascading values.
// This includes common types that can be used with [SupplyParameterFromSession].
[JsonSourceGenerationOptions(WriteIndented = false, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(int))]
[JsonSerializable(typeof(bool))]
[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(Guid))]
[JsonSerializable(typeof(DateTime))]
[JsonSerializable(typeof(int?))]
[JsonSerializable(typeof(bool?))]
[JsonSerializable(typeof(Guid?))]
[JsonSerializable(typeof(DateTime?))]
[JsonSerializable(typeof(List<int>))]
[JsonSerializable(typeof(List<bool>))]
[JsonSerializable(typeof(List<string>))]
[JsonSerializable(typeof(List<Guid>))]
[JsonSerializable(typeof(List<DateTime>))]
internal sealed partial class SessionCascadingValueSerializerContext : JsonSerializerContext
{
}

internal partial class SessionCascadingValueSupplier
{
    private static readonly ConcurrentDictionary<(Type, string), PropertyGetter> _propertyGetterCache = new();
    private HttpContext? _httpContext;
    private bool _onStartingRegistered;
    private readonly Dictionary<string, (Func<object?> ValueGetter, Type DeclaredType)> _valueCallbacks = new(StringComparer.OrdinalIgnoreCase);
    private readonly ILogger<SessionCascadingValueSupplier> _logger;

    public SessionCascadingValueSupplier(ILogger<SessionCascadingValueSupplier> logger)
    {
        _logger = logger;
    }

    internal void SetRequestContext(HttpContext httpContext)
    {
        _httpContext = httpContext;
    }

    internal CascadingParameterSubscription CreateSubscription(
        ComponentState componentState,
        SupplyParameterFromSessionAttribute attribute,
        CascadingParameterInfo parameterInfo)
    {
        if (!_onStartingRegistered && _httpContext is not null)
        {
            _onStartingRegistered = true;
            _httpContext.Response.OnStarting(PersistAllValues);
        }

        var sessionKey = attribute.Name ?? parameterInfo.PropertyName;
        var componentType = componentState.Component.GetType();
        var getter = _propertyGetterCache.GetOrAdd((componentType, parameterInfo.PropertyName), PropertyGetterFactory);
        Func<object?> valueGetter = () => getter.GetValue(componentState.Component);
        RegisterValueCallback(sessionKey, valueGetter, parameterInfo.PropertyType);
        return new SessionSubscription(this, sessionKey, parameterInfo.PropertyType, valueGetter);
    }

    private static PropertyGetter PropertyGetterFactory((Type type, string propertyName) key)
    {
        var (type, propertyName) = key;
        var propertyInfo = type.GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (propertyInfo is null)
        {
            throw new InvalidOperationException($"A property '{propertyName}' on component type '{type.FullName}' wasn't found.");
        }
        return new PropertyGetter(type, propertyInfo);
    }

    internal ISession? GetSession() => _httpContext?.Features.Get<ISessionFeature>()?.Session;

    internal void RegisterValueCallback(string sessionKey, Func<object?> valueGetter, Type declaredType)
    {
        if (!_valueCallbacks.TryAdd(sessionKey, (valueGetter, declaredType)))
        {
            throw new InvalidOperationException($"A callback is already registered for the session key '{sessionKey}'. Multiple components cannot use the same session key for multiple [SupplyParameterFromSession] attributes.");
        }
    }

    internal Task PersistAllValues()
    {
        if (_valueCallbacks.Count == 0)
        {
            return Task.CompletedTask;
        }

        var session = GetSession();
        if (session is null)
        {
            return Task.CompletedTask;
        }

        foreach (var (key, callback) in _valueCallbacks)
        {
            var sessionKey = key.ToLowerInvariant();
            try
            {
                var value = callback.ValueGetter();
                if (value is not null)
                {
                    var json = SerializeUsingContext(value, callback.DeclaredType);
                    session.SetString(sessionKey, json);
                }
                else
                {
                    session.Remove(sessionKey);
                }
            }
            catch (Exception ex)
            {
                Log.SessionPersistFail(_logger, ex);
            }
        }
        return Task.CompletedTask;
    }

    private static string SerializeUsingContext(object value, Type declaredType)
    {
        var typeInfo = SessionCascadingValueSerializerContext.Default.GetTypeInfo(declaredType);
        if (typeInfo is not null)
        {
            // Use source-generated serialization when the type is registered
            using var stream = new MemoryStream();
            using var writer = new Utf8JsonWriter(stream);
            JsonSerializer.Serialize(writer, value, typeInfo);
            return Encoding.UTF8.GetString(stream.ToArray());
        }

        // Fallback for types not in the context - use reflection-based serialization
        // This should rarely happen in trimming scenarios with properly registered types
        var options = new JsonSerializerOptions();
        return JsonSerializer.Serialize(value, declaredType, options);
    }

    private static object? DeserializeUsingContext(string json, Type declaredType)
    {
        var typeInfo = SessionCascadingValueSerializerContext.Default.GetTypeInfo(declaredType);
        if (typeInfo is not null)
        {
            // Use source-generated deserialization when the type is registered
            var reader = new Utf8JsonReader(Encoding.UTF8.GetBytes(json));
            return JsonSerializer.Deserialize(ref reader, typeInfo);
        }

        // Fallback for types not in the context
        var options = new JsonSerializerOptions();
        return JsonSerializer.Deserialize(json, declaredType, options);
    }

    internal void DeleteValueCallback(string sessionKey)
    {
        _valueCallbacks.Remove(sessionKey);
    }

    private static partial class Log
    {
        [LoggerMessage(1, LogLevel.Warning, "Persisting of the session element failed.", EventName = "SessionPersistFail")]
        public static partial void SessionPersistFail(ILogger logger, Exception exception);

        [LoggerMessage(2, LogLevel.Warning, "Deserialization of the element from session failed.", EventName = "SessionDeserializeFail")]
        public static partial void SessionDeserializeFail(ILogger logger, Exception exception);
    }

    internal partial class SessionSubscription : CascadingParameterSubscription
    {
        private readonly SessionCascadingValueSupplier _owner;
        private readonly string _sessionKey;
        private readonly Type _propertyType;
        private readonly Func<object?> _currentValueGetter;
        private bool _delivered;

        public SessionSubscription(
            SessionCascadingValueSupplier owner,
            string sessionKey,
            Type propertyType,
            Func<object?> currentValueGetter)
        {
            _owner = owner;
            _sessionKey = sessionKey;
            _propertyType = propertyType;
            _currentValueGetter = currentValueGetter;
        }

        public override object? GetCurrentValue()
        {
            if (_delivered)
            {
                return _currentValueGetter();
            }

            _delivered = true;
            var session = _owner.GetSession();
            if (session is null)
            {
                return null;
            }

            try
            {
                var json = session.GetString(_sessionKey.ToLowerInvariant());
                if (string.IsNullOrEmpty(json))
                {
                    return null;
                }
                return DeserializeUsingContext(json, _propertyType);
            }
            catch (Exception ex)
            {
                Log.SessionDeserializeFail(_owner._logger, ex);
                return null;
            }
        }

        public override void Dispose()
        {
            _owner.DeleteValueCallback(_sessionKey);
        }
    }
}
