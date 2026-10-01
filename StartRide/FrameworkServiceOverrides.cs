using System;
using System.Linq;
using System.Net.Http;
using System.Reflection;

namespace StartRide.Core
{

    public static class FrameworkServiceOverrides
    {

        public static Type FindType(string fullName)
        {
            if (string.IsNullOrEmpty(fullName)) return null;
            Type direct = typeof(Launcher.Infrastructure.LauncherPathProvider).Assembly.GetType(fullName);
            if (direct != null) return direct;
            foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type t = null;
                try { t = asm.GetType(fullName); } catch { }
                if (t != null) return t;
            }
            return null;
        }

        public static object CreateWithoutPathProvider(Type type, IServiceProvider serviceProvider)
        {
            if (type == null) return null;

            foreach (ConstructorInfo candidate in type
                         .GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                         .OrderByDescending(c => c.GetParameters().Length))
            {
                ParameterInfo[] ps = candidate.GetParameters();
                if (ps.Any(p => p.ParameterType == typeof(Launcher.Infrastructure.LauncherPathProvider))) continue;

                var args = new object[ps.Length];
                bool ok = true;
                for (int i = 0; i < ps.Length; i++)
                {
                    object value = serviceProvider?.GetService(ps[i].ParameterType);
                    if (value == null && ps[i].ParameterType == typeof(HttpClient))
                    {

                        value = SharedHttpClient;
                    }
                    if (value == null && ps[i].HasDefaultValue) { args[i] = Type.Missing; continue; }
                    if (value == null) { ok = false; break; }
                    args[i] = value;
                }
                if (!ok) continue;

                try { return candidate.Invoke(args); }
                catch (TargetInvocationException ex) { throw ex.InnerException ?? ex; }
            }
            return null;
        }

        private static readonly HttpClient SharedHttpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30)
        };

        public static object CreateWithServiceProvider(Type type, IServiceProvider serviceProvider)
        {
            if (type == null) throw new ArgumentNullException(nameof(type));

            foreach (ConstructorInfo candidate in type
                         .GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                         .OrderBy(c => c.IsPublic ? 0 : 1)
                         .ThenBy(c => c.GetParameters().Length))
            {
                ParameterInfo[] ps = candidate.GetParameters();
                var args = new object[ps.Length];
                bool ok = true;
                for (int i = 0; i < ps.Length; i++)
                {
                    object value = serviceProvider?.GetService(ps[i].ParameterType);
                    if (value == null && ps[i].HasDefaultValue) { args[i] = Type.Missing; continue; }
                    if (value == null) { ok = false; break; }
                    args[i] = value;
                }
                if (!ok) continue;

                try { return candidate.Invoke(args); }
                catch (TargetInvocationException ex) { throw ex.InnerException ?? ex; }
            }
            throw new InvalidOperationException("没有可用的构造函数：" + type.FullName);
        }

        public static object CreateWithExplicitDirectory(Type type, string directory, IServiceProvider serviceProvider)
        {
            if (type == null || string.IsNullOrEmpty(directory)) return null;

            HttpClient http = (serviceProvider?.GetService(typeof(HttpClient)) as HttpClient) ?? new HttpClient();

            object deletionService = serviceProvider?.GetService(Type.GetType(
                "Launcher.Application.Services.IUserFileDeletionService, Launcher.Application", throwOnError: false));

            foreach (ConstructorInfo candidate in type
                         .GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                         .OrderByDescending(c => c.GetParameters().Length))
            {
                ParameterInfo[] ps = candidate.GetParameters();
                if (ps.Length < 2) continue;
                if (ps[0].ParameterType != typeof(HttpClient)) continue;
                if (ps[1].ParameterType != typeof(string)) continue;

                var args = new object[ps.Length];
                args[0] = http;
                args[1] = directory;
                bool ok = true;
                for (int i = 2; i < ps.Length; i++)
                {
                    object value = deletionService != null && ps[i].ParameterType.IsInstanceOfType(deletionService)
                        ? deletionService
                        : serviceProvider?.GetService(ps[i].ParameterType);
                    if (value == null) { ok = false; break; }
                    args[i] = value;
                }
                if (!ok) continue;

                try { return candidate.Invoke(args); }
                catch (TargetInvocationException ex) { throw ex.InnerException ?? ex; }
            }
            return null;
        }
    }
}
