using System;
using System.Linq;
using System.Net.Http;
using System.Reflection;

namespace StartRide.Core
{
    /// <summary>
    /// 无源码框架层里有几个服务**只能**从 internal 构造函数拿到显式目录：
    /// 它们的 public 构造函数只收 <c>LauncherPathProvider</c>，而那个类是 sealed、
    /// 属性也不可重写，于是目录只能由它按 <c>ApplicationId</c>（框架自己的缩写）现拼。
    ///
    /// 这些 internal 构造函数是稳定存在的（DLL 无源码、不会再变），所以在 DI 注册时
    /// 用反射按签名选中它，把我们自己的目录注进去——框架那套派生路径就永远不会被用到。
    /// </summary>
    public static class FrameworkServiceOverrides
    {
        /// <summary>
        /// 按全名在框架程序集里找类型（这些服务类型本身是 internal，编译期引用不到）。
        /// </summary>
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

        /// <summary>
        /// 用「不含 LauncherPathProvider 参数」的那个构造函数造实例。
        ///
        /// 用在这样的类型上：它有两个构造函数，public 的收 <c>LauncherPathProvider</c>（于是在
        /// 构造函数里按 <c>DefaultAccountDataDirectory</c> 把 &lt;根&gt;\&lt;框架名&gt;\accounts\... 这套
        /// 目录建出来），internal 的不收路径。选后者既保留原有行为，又不会建那套目录。
        /// 可选参数用 <see cref="Type.Missing"/> 走默认值；解析不到的参数或构造失败都返回 null，
        /// 由调用方退回框架原注册。
        /// </summary>
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
                        // 容器里通常不直接注册 HttpClient（框架走的是公开构造，不收它），
                        // 这里给一个等价的可复用实例，否则 internal 构造永远选不上。
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

        /// <summary>
        /// 兜底：用参数都能从容器里取到的那个构造函数造实例（优先公开构造、参数少的优先）。
        /// 只在 <see cref="CreateWithoutPathProvider"/> 不可用时才会走到这里。
        /// </summary>
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

        /// <summary>
        /// 在 <paramref name="type"/> 上找一个「第一个参数 HttpClient、第二个参数 string(目录)」
        /// 的构造函数（public/internal 均可）并调用它。找不到返回 null，调用方可退回原注册。
        /// </summary>
        public static object CreateWithExplicitDirectory(Type type, string directory, IServiceProvider serviceProvider)
        {
            if (type == null || string.IsNullOrEmpty(directory)) return null;

            HttpClient http = (serviceProvider?.GetService(typeof(HttpClient)) as HttpClient) ?? new HttpClient();

            // 优先带 IUserFileDeletionService 的重载（行为与框架默认注册最接近）
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
