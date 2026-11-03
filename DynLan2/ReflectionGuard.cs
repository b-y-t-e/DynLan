using System;
using System.Reflection;

namespace DynLan2
{
    /// <summary>
    /// Keeps scripts inside the sandbox: hides .NET members that would let a script reach reflection
    /// (Type/MemberInfo/Module/Assembly, Delegate.Method, GetType) and from there arbitrary types such as File or Process.
    /// </summary>
    internal static class ReflectionGuard
    {
        public static bool IsForbiddenType(Type type) =>
            typeof(MemberInfo).IsAssignableFrom(type)
            || typeof(Module).IsAssignableFrom(type)
            || typeof(Assembly).IsAssignableFrom(type)
            || IsReflectionNamespace(type);

        public static bool IsForbidden(MemberInfo member, Type valueType) =>
            IsDeclaredOnDelegateOrObjectReflection(member)
            || IsReflectionNamespace(member.DeclaringType)
            || IsForbiddenType(valueType);

        static bool IsDeclaredOnDelegateOrObjectReflection(MemberInfo member) =>
            member.DeclaringType == typeof(Delegate) || member.DeclaringType == typeof(MulticastDelegate)
            || member.Name == nameof(GetType);

        static bool IsReflectionNamespace(Type type) =>
            type?.Namespace != null && (type.Namespace == "System.Reflection" || type.Namespace.StartsWith("System.Reflection.", StringComparison.Ordinal));
    }
}
